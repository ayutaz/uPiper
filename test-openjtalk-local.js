const fs = require('fs');
const path = require('path');

// Test OpenJTalk module loading and functionality
async function testOpenJTalk() {
    console.log('=== OpenJTalk Local Test ===\n');
    
    try {
        // 1. Check if OpenJTalk files exist
        console.log('1. Checking OpenJTalk files...');
        const openjtalkJsPath = path.join(__dirname, 'Assets/uPiper/Plugins/WebGL/openjtalk.js');
        const openjtalkWasmPath = path.join(__dirname, 'Assets/uPiper/Plugins/WebGL/openjtalk.wasm');
        
        if (!fs.existsSync(openjtalkJsPath)) {
            throw new Error(`openjtalk.js not found at: ${openjtalkJsPath}`);
        }
        if (!fs.existsSync(openjtalkWasmPath)) {
            throw new Error(`openjtalk.wasm not found at: ${openjtalkWasmPath}`);
        }
        console.log('✓ OpenJTalk files found');
        
        // 2. Check dictionary files
        console.log('\n2. Checking dictionary files...');
        const dictPath = path.join(__dirname, 'Assets/StreamingAssets/dict');
        const requiredDictFiles = ['char.bin', 'matrix.bin', 'sys.dic', 'unk.dic'];
        
        for (const file of requiredDictFiles) {
            const filePath = path.join(dictPath, file);
            if (!fs.existsSync(filePath)) {
                throw new Error(`Dictionary file not found: ${file}`);
            }
        }
        console.log('✓ All dictionary files found');
        
        // 3. Load and test OpenJTalk module
        console.log('\n3. Loading OpenJTalk module...');
        
        // Read the openjtalk.js file
        const jsContent = fs.readFileSync(openjtalkJsPath, 'utf8');
        
        // Check module format
        if (jsContent.includes('export default')) {
            console.log('Module format: ES6');
        } else if (jsContent.includes('module.exports')) {
            console.log('Module format: CommonJS');
        } else if (jsContent.includes('window.OpenJTalkModule')) {
            console.log('Module format: Global (Unity WebGL compatible)');
        } else {
            console.log('Module format: Unknown');
        }
        
        // Create a minimal test environment
        global.window = global;
        global.document = { currentScript: { src: '' } };
        global.WebAssembly = require('util').types.isWebAssemblyCompiledModule ? WebAssembly : null;
        
        // Try to load the module
        console.log('\n4. Attempting to load module...');
        
        // Execute the module code
        eval(jsContent);
        
        if (typeof OpenJTalkModule !== 'undefined') {
            console.log('✓ OpenJTalkModule function found');
            
            // Try to initialize
            console.log('\n5. Initializing OpenJTalk...');
            const module = await OpenJTalkModule({
                locateFile: (path) => {
                    if (path.endsWith('.wasm')) {
                        return openjtalkWasmPath;
                    }
                    return path;
                },
                print: (text) => console.log('[OpenJTalk]', text),
                printErr: (text) => console.error('[OpenJTalk Error]', text),
                onRuntimeInitialized: () => {
                    console.log('✓ OpenJTalk runtime initialized');
                }
            });
            
            // Check for exported functions
            console.log('\n6. Checking exported functions...');
            const expectedFunctions = [
                '_openjtalk_initialize',
                '_openjtalk_synthesis_labels',
                '_openjtalk_free_string',
                '_openjtalk_clear',
                '_test_function',
                '_get_version'
            ];
            
            for (const func of expectedFunctions) {
                if (module[func]) {
                    console.log(`✓ ${func} found`);
                } else {
                    console.log(`✗ ${func} NOT found`);
                }
            }
            
            // Test basic functionality
            if (module._test_function) {
                console.log('\n7. Testing basic functionality...');
                const result = module._test_function(5, 3);
                console.log(`Test function result: ${result} (expected: 8)`);
            }
            
            if (module._get_version) {
                const versionPtr = module._get_version();
                const version = module.UTF8ToString(versionPtr);
                console.log(`OpenJTalk version: ${version}`);
            }
            
            console.log('\n✓ OpenJTalk module loaded successfully!');
            
        } else {
            console.error('✗ OpenJTalkModule not found in global scope');
        }
        
    } catch (error) {
        console.error('\n✗ Test failed:', error.message);
        console.error(error.stack);
    }
}

// Run the test
testOpenJTalk().then(() => {
    console.log('\n=== Test Complete ===');
}).catch(error => {
    console.error('Unexpected error:', error);
});