#!/usr/bin/env node
/**
 * OpenJTalk WASM 辞書実装検証テスト
 * Windows/Android版と同等の完全辞書実装かを検証
 */

import { readFileSync, statSync } from 'fs';
import { createRequire } from 'module';
const require = createRequire(import.meta.url);

console.log('===========================================');
console.log('OpenJTalk WASM 完全辞書実装検証テスト');
console.log('===========================================\n');

// ファイルサイズチェック
console.log('1. ファイルサイズ検証');
console.log('------------------------');

const files = [
    { path: './output/openjtalk-unity-full.js', expectedMin: 50000, name: 'JavaScript' },
    { path: './output/openjtalk-unity-full.wasm', expectedMin: 200000, name: 'WebAssembly' },
    { path: './output/openjtalk-unity-full.data', expectedMin: 100000000, name: 'Dictionary Data' }  // 100MB以上
];

let sizeCheckPassed = true;
for (const file of files) {
    try {
        const stats = statSync(file.path);
        const sizeMB = (stats.size / 1024 / 1024).toFixed(2);
        const passed = stats.size >= file.expectedMin;
        
        console.log(`${passed ? '✅' : '❌'} ${file.name}: ${sizeMB} MB (${stats.size} bytes)`);
        
        if (!passed) {
            console.log(`   ⚠️ 警告: ${file.name}のサイズが小さすぎます（期待: >${(file.expectedMin/1024/1024).toFixed(2)} MB）`);
            sizeCheckPassed = false;
        }
    } catch (e) {
        console.log(`❌ ${file.name}: ファイルが見つかりません`);
        sizeCheckPassed = false;
    }
}

if (sizeCheckPassed) {
    console.log('\n✅ ファイルサイズ検証: PASS - 完全版の辞書データが含まれています');
} else {
    console.log('\n❌ ファイルサイズ検証: FAIL - 簡易版の可能性があります');
}

// モジュール読み込みとテスト
console.log('\n2. 機能テスト');
console.log('------------------------');

try {
    const OpenJTalkModule = require('./output/openjtalk-unity-full.js');
    
    const module = await OpenJTalkModule({
        locateFile: (path) => {
            if (path.endsWith('.wasm')) return './output/openjtalk-unity-full.wasm';
            if (path.endsWith('.data')) return './output/openjtalk-unity-full.data';
            return path;
        },
        print: (text) => console.log(`[WASM] ${text}`),
        printErr: (text) => console.error(`[WASM ERROR] ${text}`)
    });
    
    // 初期化
    const initResult = module.ccall('Open_JTalk_initialize', 'number', [], []);
    console.log(`初期化: ${initResult === 0 ? '✅ 成功' : '❌ 失敗'}`);
    
    // 辞書読み込み
    const loadResult = module.ccall('Open_JTalk_load', 'number', ['string'], ['/dict']);
    console.log(`辞書読み込み: ${loadResult === 0 ? '✅ 成功' : '❌ 失敗'}`);
    
    // バージョン確認
    const version = module.ccall('get_version', 'string', [], []);
    console.log(`バージョン: ${version}`);
    
    // テストケース（辞書が必要な複雑な例）
    console.log('\n3. 辞書依存テスト');
    console.log('------------------------');
    
    const testCases = [
        // 簡単なテスト（簡易版でも動作）
        { text: 'こんにちは', type: 'basic' },
        { text: 'ありがとう', type: 'basic' },
        
        // 辞書が必要な複雑なテスト
        { text: '今日は晴天なり', type: 'complex' },
        { text: '人工知能', type: 'complex' },
        { text: '機械学習', type: 'complex' },
        { text: '自然言語処理', type: 'complex' },
        { text: '東京都千代田区', type: 'complex' },
        { text: '令和六年八月十日', type: 'complex' },
        { text: '私は日本語を話します', type: 'complex' },
        { text: '美しい花が咲いている', type: 'complex' },
        
        // 数字・記号混在
        { text: '2024年8月10日', type: 'mixed' },
        { text: '価格は1,234円です', type: 'mixed' },
        
        // 長文
        { text: '本日は晴天なり、絶好の行楽日和です', type: 'long' },
        { text: '人工知能技術の発展により、音声合成の品質が向上しています', type: 'long' }
    ];
    
    let complexTestsPassed = 0;
    let complexTestsTotal = 0;
    
    for (const test of testCases) {
        const outputSize = 1024;
        const outputPtr = module._malloc(outputSize);
        
        const result = module.ccall('Open_JTalk_synthesis', 'number', 
            ['string', 'number', 'number'], 
            [test.text, outputPtr, outputSize]);
        
        if (result >= 0) {
            const phonemes = module.UTF8ToString(outputPtr);
            const phonemeCount = phonemes.split(' ').filter(p => p && p !== 'pau').length;
            
            // 複雑なテキストは音素数が多いはず
            const isComplex = test.type === 'complex' || test.type === 'long';
            const expectedMinPhonemes = isComplex ? test.text.length * 0.8 : 3;
            const passed = phonemeCount >= expectedMinPhonemes;
            
            console.log(`${passed ? '✅' : '⚠️'} "${test.text}"`);
            console.log(`   → ${phonemes}`);
            console.log(`   音素数: ${phonemeCount} (期待: ≥${expectedMinPhonemes})`);
            
            if (isComplex) {
                complexTestsTotal++;
                if (passed) complexTestsPassed++;
            }
        } else {
            console.log(`❌ "${test.text}" - 変換失敗`);
            if (test.type === 'complex') complexTestsTotal++;
        }
        
        module._free(outputPtr);
    }
    
    // クリーンアップ
    module.ccall('Open_JTalk_clear', null, [], []);
    
    // 結果サマリー
    console.log('\n===========================================');
    console.log('検証結果サマリー');
    console.log('===========================================');
    
    const dictSizeMB = statSync('./output/openjtalk-unity-full.data').size / 1024 / 1024;
    const isFullVersion = dictSizeMB > 50 && complexTestsPassed / complexTestsTotal > 0.8;
    
    console.log(`辞書データサイズ: ${dictSizeMB.toFixed(2)} MB`);
    console.log(`複雑テスト成功率: ${complexTestsPassed}/${complexTestsTotal} (${(complexTestsPassed/complexTestsTotal*100).toFixed(1)}%)`);
    
    if (isFullVersion) {
        console.log('\n🎉 結論: 完全な辞書ベース実装です！');
        console.log('Windows/Android版と同等の機能を持っています。');
    } else {
        console.log('\n⚠️ 結論: 簡易版の可能性があります');
        console.log('辞書サイズまたはテスト結果が期待値を下回っています。');
    }
    
} catch (error) {
    console.error('テスト実行エラー:', error.message);
    process.exit(1);
}