#!/usr/bin/env node
/**
 * OpenJTalk WASM CLI Test
 * Run with: node test-openjtalk.mjs
 */

import fs from 'fs';
import { fileURLToPath } from 'url';
import { dirname, join } from 'path';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

// Test cases
const TEST_CASES = [
    // Critical tests - MUST pass
    { input: "こんにちは", expected: "k o N n i ch i w a", critical: true },
    { input: "ありがとうございます", expected: "a r i g a t o: g o z a i m a s u", critical: true },
    { input: "ありがとう", expected: "a r i g a t o:" },
    { input: "おはよう", expected: "o h a y o:" },
    { input: "おはようございます", expected: "o h a y o: g o z a i m a s u" },
    { input: "こんばんは", expected: "k o N b a N w a" },
    { input: "さようなら", expected: "s a y o: n a r a" },
    { input: "すみません", expected: "s u m i m a s e N" },
    
    // Common words
    { input: "テスト", expected: "t e s u t o" },
    { input: "音声", expected: "o N s e:" },
    { input: "合成", expected: "g o: s e:" },
    { input: "日本", expected: "n i h o N" },
    { input: "日本語", expected: "n i h o N g o" },
    { input: "世界", expected: "s e k a i" },
    
    // Time words
    { input: "今日", expected: "ky o:" },
    { input: "明日", expected: "a sh i t a" },
    { input: "昨日", expected: "k i n o:" },
    
    // School related
    { input: "学校", expected: "g a q k o:" },
    { input: "先生", expected: "s e N s e:" },
    { input: "生徒", expected: "s e: t o" },
    { input: "勉強", expected: "b e N ky o:" },
    
    // Pronouns
    { input: "私", expected: "w a t a sh i" },
    { input: "あなた", expected: "a n a t a" },
    { input: "彼", expected: "k a r e" },
    { input: "彼女", expected: "k a n o j o" },
    
    // Particles
    { input: "です", expected: "d e s u" },
    { input: "ます", expected: "m a s u" },
    { input: "ません", expected: "m a s e N" },
    
    // Numbers
    { input: "一", expected: "i ch i" },
    { input: "二", expected: "n i" },
    { input: "三", expected: "s a N" },
    { input: "四", expected: "y o N" },
    { input: "五", expected: "g o" },
];

// Colors for terminal output
const colors = {
    reset: '\x1b[0m',
    bright: '\x1b[1m',
    red: '\x1b[31m',
    green: '\x1b[32m',
    yellow: '\x1b[33m',
    blue: '\x1b[34m',
    magenta: '\x1b[35m',
    cyan: '\x1b[36m',
};

function log(message, color = colors.reset) {
    console.log(`${color}${message}${colors.reset}`);
}

async function loadWASMModule() {
    log('\n📦 Loading OpenJTalk WASM module...', colors.cyan);
    
    const wasmPath = join(__dirname, 'openjtalk-unity.wasm');
    const jsPath = join(__dirname, 'openjtalk-unity.js');
    
    // Check if files exist
    if (!fs.existsSync(wasmPath)) {
        throw new Error(`WASM file not found: ${wasmPath}\nRun build_wasm.bat first!`);
    }
    if (!fs.existsSync(jsPath)) {
        throw new Error(`JS file not found: ${jsPath}\nRun build_wasm.bat first!`);
    }
    
    // Load the module
    const wasmBinary = fs.readFileSync(wasmPath);
    
    // Create module loader
    const OpenJTalkModule = (await import(jsPath)).default;
    
    // Initialize module
    const module = await OpenJTalkModule({
        wasmBinary,
        print: (text) => console.log(`  [WASM] ${text}`),
        printErr: (text) => console.error(`  [WASM Error] ${text}`),
    });
    
    log('✅ Module loaded successfully', colors.green);
    return module;
}

function testPhonemization(module, text) {
    // Allocate memory for input
    const textBytes = module.lengthBytesUTF8(text) + 1;
    const textPtr = module._malloc(textBytes);
    module.stringToUTF8(text, textPtr, textBytes);
    
    // Allocate output buffer
    const outputSize = 1024;
    const outputPtr = module._malloc(outputSize);
    
    // Call synthesis
    const result = module._Open_JTalk_synthesis(textPtr, outputPtr, outputSize);
    
    let phonemes = '';
    if (result > 0) {
        phonemes = module.UTF8ToString(outputPtr);
    }
    
    // Clean up
    module._free(textPtr);
    module._free(outputPtr);
    
    return phonemes;
}

async function runTests() {
    log('\n' + '='.repeat(60), colors.bright);
    log('🎌 OpenJTalk WASM Test Suite', colors.bright + colors.cyan);
    log('='.repeat(60), colors.bright);
    
    let module;
    
    try {
        // Load module
        module = await loadWASMModule();
        
        // Initialize OpenJTalk
        log('\n🔧 Initializing OpenJTalk...', colors.cyan);
        const initResult = module._Open_JTalk_initialize();
        if (initResult !== 0) {
            throw new Error(`Initialization failed with code: ${initResult}`);
        }
        log('✅ Initialized successfully', colors.green);
        
        // Load dictionary
        const loadResult = module._Open_JTalk_load(0);
        if (loadResult !== 0) {
            throw new Error(`Dictionary load failed with code: ${loadResult}`);
        }
        log('✅ Dictionary loaded', colors.green);
        
    } catch (error) {
        log(`\n❌ Setup failed: ${error.message}`, colors.red);
        process.exit(1);
    }
    
    // Run tests
    log('\n🧪 Running tests...', colors.cyan);
    log('-'.repeat(60));
    
    let passCount = 0;
    let failCount = 0;
    const failures = [];
    let hardcodedDetected = false;
    
    for (const testCase of TEST_CASES) {
        try {
            const actual = testPhonemization(module, testCase.input);
            const pass = actual === testCase.expected;
            
            // Check for hardcoded response
            if (testCase.input !== "こんにちは" && actual === "k o N n i ch i w a") {
                hardcodedDetected = true;
                pass = false;
            }
            
            if (pass) {
                passCount++;
                log(`  ✅ "${testCase.input}" → "${actual}"`, colors.green);
            } else {
                failCount++;
                const marker = testCase.critical ? ' [CRITICAL]' : '';
                log(`  ❌ "${testCase.input}"${marker}`, colors.red);
                log(`     Expected: "${testCase.expected}"`, colors.yellow);
                log(`     Got:      "${actual}"`, colors.red);
                failures.push({ ...testCase, actual });
            }
            
        } catch (error) {
            failCount++;
            log(`  ❌ "${testCase.input}" - Error: ${error.message}`, colors.red);
            failures.push({ ...testCase, error: error.message });
        }
    }
    
    // Summary
    log('\n' + '='.repeat(60), colors.bright);
    log('📊 Test Summary', colors.bright + colors.cyan);
    log('='.repeat(60), colors.bright);
    
    const total = passCount + failCount;
    const passRate = total > 0 ? ((passCount / total) * 100).toFixed(1) : 0;
    
    log(`Total:     ${total}`, colors.cyan);
    log(`Passed:    ${passCount}`, colors.green);
    log(`Failed:    ${failCount}`, colors.red);
    log(`Pass Rate: ${passRate}%`, passRate >= 80 ? colors.green : colors.red);
    
    if (hardcodedDetected) {
        log('\n⚠️  WARNING: Hardcoded responses detected!', colors.yellow + colors.bright);
        log('The module is returning "konnichiwa" for all inputs.', colors.yellow);
        log('This indicates the WASM module is not properly implemented.', colors.yellow);
    }
    
    if (failCount > 0) {
        log('\n❌ Failed Tests:', colors.red + colors.bright);
        failures.forEach(f => {
            const marker = f.critical ? ' [CRITICAL]' : '';
            log(`  - "${f.input}"${marker}`, colors.red);
            if (f.error) {
                log(`    Error: ${f.error}`, colors.red);
            } else {
                log(`    Expected: "${f.expected}"`, colors.yellow);
                log(`    Got:      "${f.actual}"`, colors.red);
            }
        });
        
        log('\n❌ Tests FAILED. Implementation needs fixing.', colors.red + colors.bright);
        process.exit(1);
    } else {
        log('\n✅ All tests PASSED! Implementation is working correctly.', colors.green + colors.bright);
        process.exit(0);
    }
}

// Check for different inputs producing different outputs
async function checkNotHardcoded(module) {
    log('\n🔍 Checking for hardcoded responses...', colors.cyan);
    
    const testInputs = ["ありがとう", "テスト", "日本", "おはよう"];
    const outputs = new Set();
    
    for (const input of testInputs) {
        const result = testPhonemization(module, input);
        outputs.add(result);
        
        if (input !== "こんにちは" && result === "k o N n i ch i w a") {
            log(`  ⚠️ Hardcoded response for "${input}"`, colors.yellow);
            return false;
        }
    }
    
    if (outputs.size === 1) {
        log('  ⚠️ All inputs produced the same output!', colors.yellow);
        return false;
    }
    
    log(`  ✅ ${outputs.size} different outputs for ${testInputs.length} inputs`, colors.green);
    return true;
}

// Run the tests
runTests().catch(error => {
    log(`\n❌ Unexpected error: ${error.message}`, colors.red);
    console.error(error);
    process.exit(1);
});