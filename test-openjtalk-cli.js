#!/usr/bin/env node

/**
 * CLI Test for OpenJTalk WebGL Integration
 * Tests the wrapper without needing Unity build or browser
 */

const fs = require('fs');
const path = require('path');
const { JSDOM } = require('jsdom');
const fetch = require('node-fetch');

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
    log('=== OpenJTalk WebGL CLI Test ===', 'blue');
    
    // Create browser-like environment
    const dom = new JSDOM('<!DOCTYPE html><html><body></body></html>', {
        url: 'http://localhost/',
        runScripts: 'dangerously',
        resources: 'usable',
        pretendToBeVisual: true
    });
    
    const window = dom.window;
    const document = window.document;
    
    // Set up globals that Unity WebGL would have
    global.window = window;
    global.document = document;
    global.WebAssembly = WebAssembly;
    
    // Make fetch available to both window and global
    const fakeFetch = async (url) => {
        log(`Fetch requested: ${url}`, 'yellow');
        
        // Map Unity paths to local files
        const mappings = {
            'StreamingAssets/openjtalk.js': 'Assets/StreamingAssets/openjtalk.js',
            'StreamingAssets/openjtalk.wasm': 'Assets/StreamingAssets/openjtalk.wasm',
            'StreamingAssets/openjtalk-wrapper.js': 'Assets/StreamingAssets/openjtalk-wrapper.js'
        };
        
        const localPath = mappings[url] || url;
        const fullPath = path.join(__dirname, localPath);
        
        if (!fs.existsSync(fullPath)) {
            log(`File not found: ${fullPath}`, 'red');
            throw new Error(`404: ${url}`);
        }
        
        const content = fs.readFileSync(fullPath);
        
        return {
            ok: true,
            status: 200,
            text: async () => content.toString(),
            arrayBuffer: async () => content.buffer.slice(content.byteOffset, content.byteOffset + content.byteLength)
        };
    };
    
    // Set fetch on both window and global
    window.fetch = fakeFetch;
    global.fetch = fakeFetch;
    
    // Set up console capture
    const originalConsole = {
        log: console.log,
        error: console.error,
        warn: console.warn
    };
    
    const consoleOutput = [];
    window.console.log = (...args) => {
        consoleOutput.push(['log', args.join(' ')]);
        originalConsole.log(...args);
    };
    window.console.error = (...args) => {
        consoleOutput.push(['error', args.join(' ')]);
        originalConsole.error(...args);
    };
    window.console.warn = (...args) => {
        consoleOutput.push(['warn', args.join(' ')]);
        originalConsole.warn(...args);
    };
    
    // Test 1: Load wrapper
    log('\n--- Test 1: Loading wrapper ---', 'blue');
    try {
        const wrapperPath = path.join(__dirname, 'Assets/StreamingAssets/openjtalk-wrapper.js');
        const wrapperCode = fs.readFileSync(wrapperPath, 'utf-8');
        
        // Execute wrapper in the simulated environment
        const script = document.createElement('script');
        script.textContent = wrapperCode;
        document.head.appendChild(script);
        
        if (typeof window.OpenJTalkModule === 'function') {
            log('✓ Wrapper loaded successfully', 'green');
        } else {
            log('✗ Wrapper failed to load', 'red');
            return;
        }
    } catch (error) {
        log(`✗ Wrapper load error: ${error.message}`, 'red');
        return;
    }
    
    // Test 2: Check for HEAP8 export issues
    log('\n--- Test 2: Checking HEAP8 export issue ---', 'blue');
    
    // Look for error patterns in console output
    const heapErrors = consoleOutput.filter(([type, msg]) => 
        type === 'error' && msg.includes('HEAP8') && msg.includes('not exported')
    );
    
    if (heapErrors.length > 0) {
        log('✗ HEAP8 export errors detected:', 'red');
        heapErrors.forEach(([, msg]) => log(`  ${msg}`, 'red'));
    } else {
        log('✓ No HEAP8 export errors in console', 'green');
    }
    
    // Test 3: Try to initialize module
    log('\n--- Test 3: Module initialization ---', 'blue');
    try {
        // Set up Unity-like environment variables
        window._malloc = () => 0;
        window._free = () => {};
        window.UTF8ToString = () => '';
        window.stringToUTF8 = () => {};
        window.lengthBytesUTF8 = () => 0;
        
        const modulePromise = window.OpenJTalkModule({
            print: (text) => log(`[Module]: ${text}`, 'yellow'),
            printErr: (text) => log(`[Module Error]: ${text}`, 'red')
        });
        
        // Give it some time but not too long
        const timeout = new Promise((_, reject) => 
            setTimeout(() => reject(new Error('Module initialization timeout')), 5000)
        );
        
        const module = await Promise.race([modulePromise, timeout]);
        
        // Check if module has required exports
        const requiredExports = ['HEAP8', 'HEAPU8', '_malloc', '_free', 'UTF8ToString'];
        const missingExports = requiredExports.filter(name => !module[name]);
        
        if (missingExports.length === 0) {
            log('✓ Module initialized with all required exports', 'green');
        } else {
            log(`✗ Missing exports: ${missingExports.join(', ')}`, 'red');
        }
        
    } catch (error) {
        log(`✗ Module initialization failed: ${error.message}`, 'red');
        
        // Check if it's the HEAP8 error
        if (error.message.includes('HEAP8') && error.message.includes('not exported')) {
            log('\n⚠️  This is the piper-plus HEAP8 export issue!', 'red');
            log('The wrapper needs to be fixed to handle this.', 'yellow');
        }
    }
    
    // Test 4: Analyze the actual openjtalk.js file
    log('\n--- Test 4: Analyzing openjtalk.js ---', 'blue');
    try {
        const openjtalkPath = path.join(__dirname, 'Assets/StreamingAssets/openjtalk.js');
        const openjtalkCode = fs.readFileSync(openjtalkPath, 'utf-8');
        
        // Check for EXPORTED_RUNTIME_METHODS
        if (openjtalkCode.includes('EXPORTED_RUNTIME_METHODS')) {
            const match = openjtalkCode.match(/EXPORTED_RUNTIME_METHODS[^']*'([^']+)'/);
            if (match) {
                log(`Found EXPORTED_RUNTIME_METHODS: ${match[1].substring(0, 100)}...`, 'yellow');
                if (!match[1].includes('HEAP8')) {
                    log('✗ HEAP8 is NOT in EXPORTED_RUNTIME_METHODS!', 'red');
                    log('  This confirms the piper-plus build issue.', 'yellow');
                }
            }
        } else {
            log('⚠️  No EXPORTED_RUNTIME_METHODS found in code', 'yellow');
        }
        
        // Check if updateMemoryViews exists
        if (openjtalkCode.includes('updateMemoryViews')) {
            log('✓ updateMemoryViews function found', 'green');
        }
        
        // Check for unexportedRuntimeSymbol
        if (openjtalkCode.includes('unexportedRuntimeSymbol')) {
            log('✓ unexportedRuntimeSymbol function found (this throws the error)', 'green');
        }
        
    } catch (error) {
        log(`✗ Failed to analyze openjtalk.js: ${error.message}`, 'red');
    }
    
    // Summary
    log('\n=== Test Summary ===', 'blue');
    const errors = consoleOutput.filter(([type]) => type === 'error');
    const warnings = consoleOutput.filter(([type]) => type === 'warn');
    
    log(`Errors: ${errors.length}`, errors.length > 0 ? 'red' : 'green');
    log(`Warnings: ${warnings.length}`, warnings.length > 0 ? 'yellow' : 'green');
    
    if (errors.length > 0) {
        log('\nFirst few errors:', 'red');
        errors.slice(0, 3).forEach(([, msg]) => {
            log(`  ${msg.substring(0, 100)}...`, 'red');
        });
    }
    
    // Cleanup
    dom.window.close();
}

// Check if required files exist
function checkRequiredFiles() {
    const required = [
        'Assets/StreamingAssets/openjtalk.js',
        'Assets/StreamingAssets/openjtalk.wasm',
        'Assets/StreamingAssets/openjtalk-wrapper.js'
    ];
    
    let allExist = true;
    required.forEach(file => {
        const fullPath = path.join(__dirname, file);
        if (!fs.existsSync(fullPath)) {
            log(`Missing required file: ${file}`, 'red');
            allExist = false;
        }
    });
    
    return allExist;
}

// Main
async function main() {
    if (!checkRequiredFiles()) {
        log('\nPlease ensure all required files are in place.', 'red');
        process.exit(1);
    }
    
    try {
        await runTests();
    } catch (error) {
        log(`\nUnexpected error: ${error.message}`, 'red');
        console.error(error);
        process.exit(1);
    }
}

// Run if executed directly
if (require.main === module) {
    main();
}

module.exports = { runTests };