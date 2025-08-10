#!/usr/bin/env node

/**
 * OpenJTalk WASM CLIテスト
 * Unity WebGLビルド前の動作確認
 */

const fs = require('fs');
const path = require('path');

console.log('=== OpenJTalk WASM CLI Test ===\n');

// テストケース
const testCases = [
    { text: 'こんにちは', expected: 'k o N n i ch i w a' },
    { text: 'ありがとう', expected: 'a r i g a t o:' },
    { text: 'テスト', expected: 't e s u t o' },
    { text: '日本語', expected: 'n i h o N g o' },
    { text: 'おはようございます', expected: 'o h a y o: g o z a i m a s u' }
];

// WASMモジュールのロード
async function loadOpenJTalk() {
    try {
        console.log('Loading OpenJTalk WASM module...');
        
        // WASMファイルを読み込み
        const wasmPath = path.join(__dirname, 'Assets/StreamingAssets/openjtalk-unity.wasm');
        const dataPath = path.join(__dirname, 'Assets/StreamingAssets/openjtalk-unity.data');
        
        if (!fs.existsSync(wasmPath)) {
            throw new Error(`WASM file not found: ${wasmPath}`);
        }
        
        if (!fs.existsSync(dataPath)) {
            throw new Error(`Data file not found: ${dataPath}`);
        }
        
        const wasmBinary = fs.readFileSync(wasmPath);
        const dataBinary = fs.readFileSync(dataPath);
        
        console.log(`WASM size: ${(wasmBinary.length / 1024 / 1024).toFixed(2)} MB`);
        console.log(`Data size: ${(dataBinary.length / 1024 / 1024).toFixed(2)} MB`);
        
        // モジュールをロード（CommonJS形式）
        const OpenJTalkModule = require('./Assets/StreamingAssets/openjtalk-unity.js');
        
        // 初期化
        const Module = await OpenJTalkModule({
            wasmBinary: wasmBinary,
            preRun: [],
            postRun: [],
            print: (text) => console.log('[OpenJTalk]', text),
            printErr: (text) => console.error('[OpenJTalk Error]', text),
            locateFile: (filename) => {
                if (filename === 'openjtalk-unity.data') {
                    // データファイルのバイナリを直接返す
                    return dataPath;
                }
                return filename;
            },
            // データファイルの内容を事前に設定
            preloadedData: dataBinary
        });
        
        console.log('OpenJTalk module loaded successfully\n');
        return Module;
        
    } catch (error) {
        console.error('Failed to load OpenJTalk:', error);
        process.exit(1);
    }
}

// 音素変換テスト
function testPhonemize(Module, text) {
    try {
        // テキストをメモリに書き込み
        const textPtr = Module.allocateUTF8(text);
        
        // 音素変換実行
        const resultPtr = Module._openjtalk_phonemize(textPtr);
        
        if (resultPtr === 0) {
            Module._free(textPtr);
            return null;
        }
        
        // 結果を取得
        const result = Module.UTF8ToString(resultPtr);
        
        // メモリ解放
        Module._openjtalk_free_string(resultPtr);
        Module._free(textPtr);
        
        return result;
        
    } catch (error) {
        console.error(`Error phonemizing "${text}":`, error);
        return null;
    }
}

// メイン処理
async function main() {
    const Module = await loadOpenJTalk();
    
    // 初期化確認
    console.log('Testing OpenJTalk initialization...');
    const isInitialized = Module._openjtalk_is_initialized();
    console.log(`Initialized: ${isInitialized ? 'YES' : 'NO'}\n`);
    
    if (!isInitialized) {
        console.error('OpenJTalk is not initialized properly');
        process.exit(1);
    }
    
    // テスト実行
    console.log('=== Phoneme Conversion Tests ===\n');
    
    let passed = 0;
    let failed = 0;
    
    for (const testCase of testCases) {
        const result = testPhonemize(Module, testCase.text);
        
        if (result === null) {
            console.log(`❌ "${testCase.text}" → ERROR`);
            failed++;
            continue;
        }
        
        // 結果の整形
        const phonemes = result.trim().replace(/\s+/g, ' ');
        const expected = testCase.expected;
        
        // 長音記号の正規化（: と - を同一視）
        const normalizedResult = phonemes.replace(/:/g, '-');
        const normalizedExpected = expected.replace(/:/g, '-');
        
        const isCorrect = normalizedResult === normalizedExpected;
        
        if (isCorrect) {
            console.log(`✅ "${testCase.text}" → ${phonemes}`);
            passed++;
        } else {
            console.log(`❌ "${testCase.text}"`);
            console.log(`   Expected: ${expected}`);
            console.log(`   Got:      ${phonemes}`);
            failed++;
        }
    }
    
    // 結果サマリー
    console.log('\n=== Test Results ===');
    console.log(`Passed: ${passed}/${testCases.length}`);
    console.log(`Failed: ${failed}/${testCases.length}`);
    
    // 追加テスト：辞書サイズ確認
    console.log('\n=== Dictionary Info ===');
    const dictSize = Module._openjtalk_get_dictionary_size ? Module._openjtalk_get_dictionary_size() : -1;
    if (dictSize > 0) {
        console.log(`Dictionary entries: ${dictSize}`);
        console.log(`Dictionary type: Full NAIST Japanese Dictionary`);
    } else {
        console.log('Dictionary size not available');
    }
    
    // メモリ使用量
    if (Module.HEAP8) {
        const heapSize = Module.HEAP8.length / 1024 / 1024;
        console.log(`\nMemory usage: ${heapSize.toFixed(2)} MB`);
    }
    
    // 終了
    if (failed > 0) {
        console.error('\n⚠️  Some tests failed. Please check the implementation.');
        process.exit(1);
    } else {
        console.log('\n✨ All tests passed! Ready for Unity WebGL build.');
        process.exit(0);
    }
}

// エラーハンドリング
process.on('unhandledRejection', (error) => {
    console.error('Unhandled error:', error);
    process.exit(1);
});

// 実行
main().catch(error => {
    console.error('Fatal error:', error);
    process.exit(1);
});