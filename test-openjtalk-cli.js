#!/usr/bin/env node

/**
 * CLI Test for OpenJTalk WebGL Integration
 * Simulates Unity WebGL environment
 */

const fs = require('fs');
const path = require('path');
const { JSDOM } = require('jsdom');

// Colors for terminal output
const colors = {
    reset: '\x1b[0m',
    red: '\x1b[31m',
    green: '\x1b[32m',
    yellow: '\x1b[33m',
    blue: '\x1b[34m'
};

function log(message, color = 'reset') {
    console.log(`${colors[color]}${message}${colors.reset}`);
}

async function runTests() {
    log('=== OpenJTalk Unity WebGL CLI Test ===', 'blue');
    
    // Create Unity WebGL-like environment
    const dom = new JSDOM('<!DOCTYPE html><html><body></body></html>', {
        url: 'http://localhost/',
        runScripts: 'dangerously',
        resources: 'usable',
        pretendToBeVisual: true
    });
    
    const window = dom.window;
    const document = window.document;
    
    // Simulate Unity WebGL globals
    log('\n--- Setting up Unity WebGL environment ---', 'blue');
    
    // Unity's Module object (separate from OpenJTalk)
    window.Module = {
        print: (text) => console.log('[Unity]', text),
        printErr: (text) => console.error('[Unity]', text),
        HEAP8: new Int8Array(1024),
        HEAPU8: new Uint8Array(1024),
        _malloc: (size) => {
            console.log(`[Unity _malloc] ${size} bytes`);
            return 100; // Dummy pointer
        },
        _free: (ptr) => {
            console.log(`[Unity _free] ptr ${ptr}`);
        }
    };
    
    // Unity's global functions
    window._malloc = window.Module._malloc;
    window._free = window.Module._free;
    window.UTF8ToString = (ptr) => `[Unity string from ${ptr}]`;
    window.stringToUTF8 = (str, ptr, maxBytes) => {
        console.log(`[Unity stringToUTF8] "${str}" to ${ptr}`);
        return str.length;
    };
    window.lengthBytesUTF8 = (str) => str.length + 1;
    
    // WebAssembly support
    global.WebAssembly = WebAssembly;
    window.WebAssembly = WebAssembly;
    
    // Fetch mock for loading modules
    const mockFetch = async (url) => {
        log(`[Fetch] ${url}`, 'yellow');
        
        const mappings = {
            'StreamingAssets/openjtalk.js': path.join(__dirname, 'Assets/StreamingAssets/openjtalk.js'),
            'StreamingAssets/openjtalk.wasm': path.join(__dirname, 'Assets/StreamingAssets/openjtalk.wasm'),
            'StreamingAssets/openjtalk-wrapper.js': path.join(__dirname, 'Assets/StreamingAssets/openjtalk-wrapper.js'),
            'StreamingAssets/openjtalk-unity-wrapper.js': path.join(__dirname, 'Assets/StreamingAssets/openjtalk-unity-wrapper.js'),
            'StreamingAssets/openjtalk-final.js': path.join(__dirname, 'Assets/StreamingAssets/openjtalk-final.js')
        };
        
        const filePath = mappings[url] || url;
        
        if (!fs.existsSync(filePath)) {
            throw new Error(`File not found: ${filePath}`);
        }
        
        const content = fs.readFileSync(filePath);
        
        return {
            ok: true,
            status: 200,
            text: async () => content.toString(),
            arrayBuffer: async () => content.buffer.slice(content.byteOffset, content.byteOffset + content.byteLength)
        };
    };
    
    window.fetch = mockFetch;
    global.fetch = mockFetch;
    
    log('✓ Unity WebGL environment setup complete', 'green');
    
    // Test 1: Load wrapper without conflicts
    log('\n--- Test 1: Loading wrapper without Unity conflicts ---', 'blue');
    
    try {
        // Check which wrapper to use
        const wrapperPath = path.join(__dirname, 'Assets/StreamingAssets/openjtalk-final.js');
        
        if (!fs.existsSync(wrapperPath)) {
            log('✗ Wrapper file not found', 'red');
            return;
        }
        
        const wrapperCode = fs.readFileSync(wrapperPath, 'utf-8');
        
        // Execute wrapper
        const script = document.createElement('script');
        script.textContent = wrapperCode;
        document.head.appendChild(script);
        
        // Check if OpenJTalkModule is available
        if (typeof window.OpenJTalkModule === 'function') {
            log('✓ OpenJTalkModule loaded successfully', 'green');
        } else {
            log('✗ OpenJTalkModule not found', 'red');
            return;
        }
        
        // Verify Unity globals are not overwritten
        if (window.Module && window.Module.print) {
            log('✓ Unity Module preserved', 'green');
        } else {
            log('✗ Unity Module was overwritten!', 'red');
        }
        
    } catch (error) {
        log(`✗ Wrapper load error: ${error.message}`, 'red');
        return;
    }
    
    // Test 2: Initialize OpenJTalk module
    log('\n--- Test 2: Module initialization ---', 'blue');
    
    let openJTalkModule = null;
    
    try {
        const config = {
            print: (text) => console.log('[OpenJTalk]', text),
            printErr: (text) => console.error('[OpenJTalk]', text),
            locateFile: (path) => {
                if (path.endsWith('.wasm')) {
                    return 'StreamingAssets/openjtalk.wasm';
                }
                return path;
            },
            // Don't wait for runtime initialization in test
            noInitialRun: true
        };
        
        // Initialize with timeout
        const initPromise = window.OpenJTalkModule(config);
        const timeoutPromise = new Promise((_, reject) => 
            setTimeout(() => reject(new Error('Initialization timeout')), 5000)
        );
        
        openJTalkModule = await Promise.race([initPromise, timeoutPromise]);
        
        if (openJTalkModule) {
            log('✓ OpenJTalk module initialized', 'green');
        } else {
            log('✗ Module initialization returned null', 'red');
            return;
        }
        
    } catch (error) {
        log(`✗ Module initialization failed: ${error.message}`, 'red');
        
        // Check if it's the HEAP8 error
        if (error.message.includes('HEAP8') && error.message.includes('not exported')) {
            log('⚠ HEAP8 export issue detected!', 'red');
            log('This is the piper-plus build configuration issue', 'yellow');
        }
        return;
    }
    
    // Test 3: Check module exports
    log('\n--- Test 3: Checking module exports ---', 'blue');
    
    if (openJTalkModule) {
        const requiredExports = ['HEAP8', 'HEAPU8', '_malloc', '_free', 'UTF8ToString', 'stringToUTF8'];
        const exportStatus = {};
        
        requiredExports.forEach(name => {
            const exists = !!openJTalkModule[name];
            exportStatus[name] = exists;
            
            if (exists) {
                log(`✓ ${name} exported`, 'green');
            } else {
                log(`✗ ${name} missing`, 'red');
            }
        });
        
        // Check if Unity's functions are still intact
        if (window.Module._malloc === window._malloc) {
            log('✓ Unity memory functions intact', 'green');
        } else {
            log('⚠ Unity memory functions may have been overridden', 'yellow');
        }
    }
    
    // Test 4: Memory isolation
    log('\n--- Test 4: Memory isolation test ---', 'blue');
    
    if (openJTalkModule && openJTalkModule.HEAP8) {
        // Check if OpenJTalk HEAP is different from Unity HEAP
        if (openJTalkModule.HEAP8 !== window.Module.HEAP8) {
            log('✓ OpenJTalk HEAP is isolated from Unity HEAP', 'green');
        } else {
            log('✗ OpenJTalk and Unity share the same HEAP (conflict!)', 'red');
        }
        
        // Check HEAP sizes
        log(`Unity HEAP8 size: ${window.Module.HEAP8.length}`, 'yellow');
        log(`OpenJTalk HEAP8 size: ${openJTalkModule.HEAP8.length}`, 'yellow');
    }
    
    // Summary
    log('\n=== Test Summary ===', 'blue');
    
    const errors = [];
    const warnings = [];
    
    // Collect issues
    if (!window.OpenJTalkModule) {
        errors.push('OpenJTalkModule not loaded');
    }
    if (!openJTalkModule) {
        errors.push('Module initialization failed');
    }
    if (openJTalkModule && !openJTalkModule.HEAP8) {
        errors.push('HEAP8 not exported');
    }
    
    if (errors.length === 0) {
        log('✓ All tests passed!', 'green');
        log('Unity WebGL integration is working correctly', 'green');
    } else {
        log(`✗ ${errors.length} error(s) found:`, 'red');
        errors.forEach(err => log(`  - ${err}`, 'red'));
    }
    
    if (warnings.length > 0) {
        log(`⚠ ${warnings.length} warning(s):`, 'yellow');
        warnings.forEach(warn => log(`  - ${warn}`, 'yellow'));
    }
    
    // Cleanup
    dom.window.close();
}

// Check required files
function checkRequiredFiles() {
    const required = [
        'Assets/StreamingAssets/openjtalk.js',
        'Assets/StreamingAssets/openjtalk.wasm'
    ];
    
    const optional = [
        'Assets/StreamingAssets/openjtalk-wrapper.js',
        'Assets/StreamingAssets/openjtalk-unity-wrapper.js',
        'Assets/StreamingAssets/openjtalk-final.js'
    ];
    
    log('Checking required files...', 'blue');
    
    let allRequired = true;
    required.forEach(file => {
        const fullPath = path.join(__dirname, file);
        if (fs.existsSync(fullPath)) {
            log(`✓ ${file}`, 'green');
        } else {
            log(`✗ Missing: ${file}`, 'red');
            allRequired = false;
        }
    });
    
    log('\nChecking wrapper files...', 'blue');
    let hasWrapper = false;
    optional.forEach(file => {
        const fullPath = path.join(__dirname, file);
        if (fs.existsSync(fullPath)) {
            log(`✓ ${file}`, 'green');
            hasWrapper = true;
        } else {
            log(`- ${file} (not found)`, 'yellow');
        }
    });
    
    if (!hasWrapper) {
        log('✗ No wrapper file found!', 'red');
        allRequired = false;
    }
    
    return allRequired;
}

// Main
async function main() {
    log('OpenJTalk Unity WebGL Integration Test\n', 'blue');
    
    if (!checkRequiredFiles()) {
        log('\n✗ Required files missing. Cannot run tests.', 'red');
        process.exit(1);
    }
    
    try {
        await runTests();
    } catch (error) {
        log(`\n✗ Unexpected error: ${error.message}`, 'red');
        console.error(error);
        process.exit(1);
    }
}

// Run if executed directly
if (require.main === module) {
    main();
}

module.exports = { runTests };