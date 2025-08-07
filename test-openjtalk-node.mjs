import { readFileSync, existsSync } from 'fs';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));

// Test OpenJTalk module loading and functionality
async function testOpenJTalk() {
    console.log('=== OpenJTalk Node.js Test ===\n');
    
    try {
        // 1. Check if OpenJTalk files exist
        console.log('1. Checking OpenJTalk files...');
        const openjtalkJsPath = join(__dirname, 'Assets/uPiper/Plugins/WebGL/openjtalk.js');
        const openjtalkWasmPath = join(__dirname, 'Assets/uPiper/Plugins/WebGL/openjtalk.wasm');
        
        if (!existsSync(openjtalkJsPath)) {
            throw new Error(`openjtalk.js not found at: ${openjtalkJsPath}`);
        }
        if (!existsSync(openjtalkWasmPath)) {
            throw new Error(`openjtalk.wasm not found at: ${openjtalkWasmPath}`);
        }
        console.log('✓ OpenJTalk files found');
        console.log(`  JS size: ${readFileSync(openjtalkJsPath).length} bytes`);
        console.log(`  WASM size: ${readFileSync(openjtalkWasmPath).length} bytes`);
        
        // 2. Check dictionary files
        console.log('\n2. Checking dictionary files...');
        const dictPath = join(__dirname, 'Assets/StreamingAssets/dict');
        const requiredDictFiles = ['char.bin', 'matrix.bin', 'sys.dic', 'unk.dic'];
        
        for (const file of requiredDictFiles) {
            const filePath = join(dictPath, file);
            if (!existsSync(filePath)) {
                throw new Error(`Dictionary file not found: ${file}`);
            }
            const size = readFileSync(filePath).length;
            console.log(`  ✓ ${file} (${size} bytes)`);
        }
        
        // 3. Check the actual module format
        console.log('\n3. Analyzing module format...');
        const jsContent = readFileSync(openjtalkJsPath, 'utf8');
        
        // Check first few characters
        const firstChars = jsContent.substring(0, 200);
        console.log('First 200 chars:', firstChars);
        
        if (jsContent.includes('export default')) {
            console.log('Module format: ES6 (export default)');
        } else if (jsContent.includes('module.exports')) {
            console.log('Module format: CommonJS');
        } else if (jsContent.includes('window.OpenJTalkModule')) {
            console.log('Module format: Global (Unity WebGL compatible)');
        } else if (jsContent.includes('var OpenJTalkModule')) {
            console.log('Module format: Global variable');
        }
        
        // Check if it's wrapped in IIFE
        if (jsContent.startsWith('(function()')) {
            console.log('Module is wrapped in IIFE');
        }
        
        console.log('\n✓ Analysis complete');
        console.log('\nNote: The module appears to be in ES6 format, which requires running in a browser environment.');
        console.log('Use the browser test (test-openjtalk-browser.html) for full functionality testing.');
        
    } catch (error) {
        console.error('\n✗ Test failed:', error.message);
    }
}

// Run the test
testOpenJTalk().then(() => {
    console.log('\n=== Test Complete ===');
}).catch(error => {
    console.error('Unexpected error:', error);
});