#!/usr/bin/env node

/**
 * ONNX Runtime WebGL テストスクリプト
 * Unity WebGLビルドなしで問題を検証
 * 
 * 使用方法:
 * 1. npm install onnxruntime-web
 * 2. node test-onnx.js
 */

const fs = require('fs');
const path = require('path');

// グローバルオブジェクトの設定（ブラウザ環境をシミュレート）
global.performance = {
    now: () => Date.now()
};

console.log('========================================');
console.log('ONNX Runtime WebGL テストスクリプト');
console.log('========================================\n');

async function loadONNXRuntime() {
    console.log('[Setup] ONNX Runtime Webをロード中...');
    
    try {
        // onnxruntime-webをrequire
        const ort = require('onnxruntime-web');
        global.ort = ort;
        console.log('✓ ONNX Runtime Web loaded\n');
        return ort;
    } catch (error) {
        console.error('❌ ONNX Runtime Webのロードに失敗しました');
        console.error('npm install onnxruntime-web を実行してください');
        process.exit(1);
    }
}

async function testDirectONNX() {
    const ort = await loadONNXRuntime();
    
    console.log('[Test 1] 直接ONNX Runtimeを使用（piper-plusスタイル）');
    
    try {
        // モデルとコンフィグをロード
        const modelPath = path.join(__dirname, 'ja_JP-test-medium.onnx');
        const configPath = path.join(__dirname, 'ja_JP-test-medium.onnx.json');
        
        console.log('モデルをロード中:', modelPath);
        
        // モデル設定を読み込み
        const config = JSON.parse(fs.readFileSync(configPath, 'utf-8'));
        
        // ONNXセッションを作成
        const session = await ort.InferenceSession.create(modelPath, {
            executionProviders: ['wasm'],  // Node.jsではwasmを使用
            graphOptimizationLevel: 'all'
        });
        
        console.log('✓ モデルロード完了\n');
        
        // テストケース
        const tests = [
            { name: '単一音素 "あ"', ids: [1, 7, 2], expected: 3000 },
            { name: '"こんにちは"', ids: [1, 25, 11, 22, 50, 8, 39, 8, 56, 7, 2], expected: 18000 }
        ];
        
        for (const test of tests) {
            console.log(`Testing: ${test.name}`);
            console.log(`Input IDs: [${test.ids.join(', ')}]`);
            
            // テンソルを作成（piper-plusと同じ方法）
            const inputTensor = new ort.Tensor('int64',
                new BigInt64Array(test.ids.map(id => BigInt(id))),
                [1, test.ids.length]
            );
            
            const lengthTensor = new ort.Tensor('int64',
                new BigInt64Array([BigInt(test.ids.length)]),
                [1]
            );
            
            const scalesTensor = new ort.Tensor('float32',
                new Float32Array([
                    config.inference?.noise_scale || 0.667,
                    config.inference?.length_scale || 1.0,
                    config.inference?.noise_w || 0.8
                ]),
                [3]
            );
            
            // 推論実行
            const feeds = {
                'input': inputTensor,
                'input_lengths': lengthTensor,
                'scales': scalesTensor
            };
            
            const startTime = Date.now();
            const results = await session.run(feeds);
            const inferenceTime = Date.now() - startTime;
            
            const audioTensor = results['output'] || results[Object.keys(results)[0]];
            
            // 結果分析
            console.log(`Tensor shape: [${audioTensor.dims.join(', ')}]`);
            console.log(`Data length: ${audioTensor.data.length} samples`);
            console.log(`Inference time: ${inferenceTime}ms`);
            
            const ratio = audioTensor.data.length / test.expected;
            const status = ratio > 3 ? '❌ 異常' : ratio > 1.5 ? '⚠️ やや長い' : '✓ 正常';
            console.log(`${status}: ${ratio.toFixed(1)}x expected`);
            console.log('');
        }
        
    } catch (error) {
        console.error('❌ エラー:', error.message);
    }
}

async function testWithWrapper() {
    console.log('[Test 2] onnx-runtime-wrapper.jsを使用');
    console.log('（このテストはブラウザ環境が必要です）\n');
    console.log('ブラウザでテストするには:');
    console.log('1. python -m http.server 8080');
    console.log('2. http://localhost:8080/test-cli.html を開く\n');
}

async function main() {
    await testDirectONNX();
    await testWithWrapper();
    
    console.log('========================================');
    console.log('テスト完了');
    console.log('========================================');
}

main().catch(console.error);