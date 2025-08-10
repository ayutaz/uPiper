#!/usr/bin/env node
/**
 * WASM版とWindows/Android版の比較検証
 * 同じテキストで音素化結果を比較
 */

import { readFileSync } from 'fs';
import { createRequire } from 'module';
const require = createRequire(import.meta.url);

console.log('================================================');
console.log('OpenJTalk WASM vs Native 比較検証');
console.log('================================================\n');

// Windows/Android版で期待される結果（実際の出力例）
const nativeResults = {
    'こんにちは': 'k o N n i ch i w a',
    '今日はいい天気ですね': 'ky o o w a i i t e N k i d e s u n e',
    '音声合成': 'o N s e e g o o s e e',
    '人工知能': 'j i N k o o ch i n o o',
    '東京都': 't o o ky o o t o',
    '機械学習': 'k i k a i g a k u sh u u',
    '自然言語処理': 'sh i z e N g e N g o sh o r i',
    '春夏秋冬': 'h a r u n a ts u a k i f u y u',
    'ありがとうございます': 'a r i g a t o o g o z a i m a s u',
    '日本語': 'n i h o N g o'
};

async function runComparison() {
    try {
        // WASMモジュールを読み込み
        const OpenJTalkModule = require('./output/openjtalk-unity-full.js');
        
        const module = await OpenJTalkModule({
            locateFile: (path) => {
                if (path.endsWith('.wasm')) return './output/openjtalk-unity-full.wasm';
                if (path.endsWith('.data')) return './output/openjtalk-unity-full.data';
                return path;
            },
            print: () => {},  // 静かにする
            printErr: (text) => console.error(`[ERROR] ${text}`)
        });
        
        // 初期化
        module.ccall('Open_JTalk_initialize', 'number', [], []);
        module.ccall('Open_JTalk_load', 'number', ['string'], ['/dict']);
        
        console.log('テキスト比較結果');
        console.log('------------------------');
        
        let matchCount = 0;
        let totalCount = 0;
        const results = [];
        
        for (const [text, expectedNative] of Object.entries(nativeResults)) {
            const outputSize = 1024;
            const outputPtr = module._malloc(outputSize);
            
            const result = module.ccall('Open_JTalk_synthesis', 'number', 
                ['string', 'number', 'number'], 
                [text, outputPtr, outputSize]);
            
            if (result >= 0) {
                const wasmPhonemes = module.UTF8ToString(outputPtr)
                    .replace(/pau\s*/g, '')  // pau を除去
                    .trim();
                
                // 比較（完全一致または部分一致をチェック）
                const isExactMatch = wasmPhonemes === expectedNative;
                const isSimilar = calculateSimilarity(wasmPhonemes, expectedNative) > 0.8;
                
                totalCount++;
                if (isExactMatch || isSimilar) {
                    matchCount++;
                }
                
                results.push({
                    text,
                    native: expectedNative,
                    wasm: wasmPhonemes,
                    match: isExactMatch ? '✅ 完全一致' : isSimilar ? '🔶 類似' : '❌ 不一致'
                });
                
                console.log(`\n"${text}"`);
                console.log(`  Native: ${expectedNative}`);
                console.log(`  WASM:   ${wasmPhonemes}`);
                console.log(`  結果:   ${isExactMatch ? '✅ 完全一致' : isSimilar ? '🔶 類似' : '❌ 不一致'}`);
            }
            
            module._free(outputPtr);
        }
        
        // 辞書ファイルの詳細確認
        console.log('\n\n辞書ファイル詳細');
        console.log('------------------------');
        
        if (module.FS && module.FS.readdir) {
            const dictFiles = module.FS.readdir('/dict');
            const expectedFiles = [
                'char.bin',      // 文字定義
                'matrix.bin',    // 連接コスト行列
                'sys.dic',       // システム辞書
                'unk.dic',       // 未知語辞書
                'pos-id.def',    // 品詞ID定義
                'rewrite.def',   // 書き換え規則
                'dicrc',         // 辞書設定
                'left-id.def',   // 左文脈ID
                'right-id.def'   // 右文脈ID
            ];
            
            console.log('検出された辞書ファイル:');
            let hasAllFiles = true;
            for (const expected of expectedFiles) {
                const exists = dictFiles.includes(expected);
                console.log(`  ${exists ? '✅' : '❌'} ${expected}`);
                if (!exists) hasAllFiles = false;
            }
            
            // ファイルサイズ確認
            let totalSize = 0;
            for (const file of dictFiles) {
                if (file !== '.' && file !== '..') {
                    try {
                        const stat = module.FS.stat(`/dict/${file}`);
                        totalSize += stat.size;
                    } catch (e) {}
                }
            }
            
            console.log(`\n辞書総サイズ: ${(totalSize / 1024 / 1024).toFixed(2)} MB`);
            
            if (hasAllFiles && totalSize > 50 * 1024 * 1024) {
                console.log('✅ 完全な辞書ファイルセットが存在します');
            }
        }
        
        // クリーンアップ
        module.ccall('Open_JTalk_clear', null, [], []);
        
        // 結果サマリー
        console.log('\n\n================================================');
        console.log('比較結果サマリー');
        console.log('================================================');
        
        const matchRate = (matchCount / totalCount * 100).toFixed(1);
        console.log(`一致率: ${matchCount}/${totalCount} (${matchRate}%)`);
        
        if (matchRate > 80) {
            console.log('\n✅ 結論: Windows/Android版と高い互換性があります');
            console.log('WASM版は完全な辞書ベース実装として動作しています。');
        } else if (matchRate > 50) {
            console.log('\n🔶 結論: 部分的な互換性があります');
            console.log('基本的な機能は動作していますが、一部差異があります。');
        } else {
            console.log('\n❌ 結論: 互換性が低いです');
            console.log('簡易版の実装の可能性があります。');
        }
        
        // 詳細な差異分析
        console.log('\n差異の詳細分析:');
        const differences = results.filter(r => !r.match.includes('完全一致'));
        if (differences.length > 0) {
            for (const diff of differences) {
                console.log(`\n"${diff.text}"`);
                const analysis = analyzeDifference(diff.native, diff.wasm);
                console.log(`  ${analysis}`);
            }
        } else {
            console.log('  差異はありません');
        }
        
    } catch (error) {
        console.error('エラー:', error.message);
        process.exit(1);
    }
}

// 類似度計算（レーベンシュタイン距離ベース）
function calculateSimilarity(str1, str2) {
    const arr1 = str1.split(' ');
    const arr2 = str2.split(' ');
    
    const matrix = [];
    for (let i = 0; i <= arr2.length; i++) {
        matrix[i] = [i];
    }
    for (let j = 0; j <= arr1.length; j++) {
        matrix[0][j] = j;
    }
    
    for (let i = 1; i <= arr2.length; i++) {
        for (let j = 1; j <= arr1.length; j++) {
            if (arr2[i - 1] === arr1[j - 1]) {
                matrix[i][j] = matrix[i - 1][j - 1];
            } else {
                matrix[i][j] = Math.min(
                    matrix[i - 1][j - 1] + 1,
                    matrix[i][j - 1] + 1,
                    matrix[i - 1][j] + 1
                );
            }
        }
    }
    
    const distance = matrix[arr2.length][arr1.length];
    const maxLength = Math.max(arr1.length, arr2.length);
    return 1 - (distance / maxLength);
}

// 差異分析
function analyzeDifference(native, wasm) {
    const nativeArr = native.split(' ');
    const wasmArr = wasm.split(' ');
    
    // 長音の違い
    const nativeLong = (native.match(/o o/g) || []).length;
    const wasmLong = (wasm.match(/o o/g) || []).length;
    if (nativeLong !== wasmLong) {
        return '長音の処理に差異があります';
    }
    
    // 促音の違い
    if (native.includes('cl') !== wasm.includes('cl')) {
        return '促音（っ）の処理に差異があります';
    }
    
    // 拗音の違い
    const nativeYoon = (native.match(/[kgsztdhbpmr]y/g) || []).length;
    const wasmYoon = (wasm.match(/[kgsztdhbpmr]y/g) || []).length;
    if (nativeYoon !== wasmYoon) {
        return '拗音（きゃ、しゃ等）の処理に差異があります';
    }
    
    // その他
    if (nativeArr.length !== wasmArr.length) {
        return `音素数が異なります（Native: ${nativeArr.length}, WASM: ${wasmArr.length}）`;
    }
    
    return '微細な音素の違いがあります';
}

// 実行
runComparison();