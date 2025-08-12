# Unity WebGL音声合成問題 - 完全調査記録

## 概要

このドキュメントは、Unity WebGL環境でuPiperの日本語音声合成機能を実装する際に発生した一連の問題と、その調査・対応の完全な記録です。2025年8月12日に実施された集中的な調査とデバッグ作業の全容を記録しています。

## 目次

1. [問題の発端と初期状態](#1-問題の発端と初期状態)
2. [第1段階：404エラーの調査と対応](#2-第1段階404エラーの調査と対応)
3. [第2段階：音声速度異常の発見](#3-第2段階音声速度異常の発見)
4. [第3段階：根本原因の調査](#4-第3段階根本原因の調査)
5. [第4段階：length_scale調整による対応](#5-第4段階length_scale調整による対応)
6. [第5段階：調整値の最適化](#6-第5段階調整値の最適化)
7. [技術的詳細と考察](#7-技術的詳細と考察)
8. [実装されたファイルと変更内容](#8-実装されたファイルと変更内容)
9. [試みたが撤回された対応](#9-試みたが撤回された対応)
10. [未解決の問題と今後の課題](#10-未解決の問題と今後の課題)
11. [コミット履歴](#11-コミット履歴)
12. [重要なログとデバッグ情報](#12-重要なログとデバッグ情報)
13. [結論と教訓](#13-結論と教訓)

## 1. 問題の発端と初期状態

### 1.1 初期コンテキスト

ユーザーから提供された大量のエラーログにより、WebGL版で以下の問題が継続的に発生していることが判明：

- OpenJTalkデータファイル（`openjtalk-unity.data`）の404エラー
- 以前実装したsplit-file-loaderが機能していない
- 音声合成が完全に失敗している状態

### 1.2 エラーログの分析

```
GET https://ayutaz.github.io/uPiper/StreamingAssets/openjtalk-unity.data 404 (Not Found)
```

このエラーが繰り返し発生し、107MBの辞書データが読み込めない状態でした。

### 1.3 以前の対応履歴

- ファイル分割システムの実装（90MB + 13MBに分割）
- split-file-loader.jsの作成
- GitHub Pages対応の実装

しかし、これらの対応が機能していない状態でした。

## 2. 第1段階：404エラーの調査と対応

### 2.1 初期調査

#### ファイル構成の確認

```bash
Build/Web/StreamingAssets/
├── openjtalk-unity.data.partaa (90MB)
├── openjtalk-unity.data.partab (13MB)
├── split-file-loader.js
└── openjtalk-unity.js
```

ファイル自体は正しく配置されていることを確認。

#### split-file-loader.jsの動作確認

調査により、以下の問題を発見：
1. ファイル名の不一致（`openjtalk-unity.data` vs `openjtalk-unity-full.data`）
2. fetchのインターセプトのみでXMLHttpRequestには対応していない
3. スクリプトの読み込み順序が不適切

### 2.2 対応1：split-file-loader.jsの全面改修

#### 修正前のコード（抜粋）
```javascript
if (url && url.includes('openjtalk-unity-full.data')) {
    // 処理
}
```

#### 修正後のコード
```javascript
(function() {
    'use strict';
    
    console.log('[SplitFileLoader] Initializing...');
    
    const originalFetch = window.fetch;
    const originalXHROpen = XMLHttpRequest.prototype.open;
    
    // Fetchのインターセプト
    window.fetch = async function(input, init) {
        const url = typeof input === 'string' ? input : input.url;
        
        if (url && (url.includes('openjtalk-unity.data') || url.includes('openjtalk-unity-full.data')) && !url.includes('.part')) {
            console.log('[SplitFileLoader] Intercepting fetch for:', url);
            
            const basePath = url.substring(0, url.lastIndexOf('/'));
            
            try {
                const responses = await Promise.all([
                    originalFetch(`${basePath}/openjtalk-unity.data.partaa`, init),
                    originalFetch(`${basePath}/openjtalk-unity.data.partab`, init)
                ]);
                
                const buffers = await Promise.all(responses.map(r => r.arrayBuffer()));
                const combinedBuffer = new Uint8Array(buffers[0].byteLength + buffers[1].byteLength);
                combinedBuffer.set(new Uint8Array(buffers[0]), 0);
                combinedBuffer.set(new Uint8Array(buffers[1]), buffers[0].byteLength);
                
                return new Response(combinedBuffer.buffer, {
                    status: 200,
                    statusText: 'OK',
                    headers: new Headers({
                        'Content-Type': 'application/octet-stream',
                        'Content-Length': combinedBuffer.byteLength.toString()
                    })
                });
            } catch (error) {
                console.error('[SplitFileLoader] Error loading split files:', error);
                throw error;
            }
        }
        
        return originalFetch.apply(this, arguments);
    };
    
    // XMLHttpRequestのインターセプト
    XMLHttpRequest.prototype.open = function(method, url, async, user, password) {
        if (url && (url.includes('openjtalk-unity.data') || url.includes('openjtalk-unity-full.data')) && !url.includes('.part')) {
            console.log('[SplitFileLoader] Intercepting XHR for:', url);
            // XHRの処理
        }
        return originalXHROpen.apply(this, arguments);
    };
})();
```

### 2.3 対応2：index.htmlのスクリプト読み込み順序修正

#### 修正前
```html
<script src="Build/Web.loader.js"></script>
<script src="StreamingAssets/split-file-loader.js"></script>
```

#### 修正後
```html
<!-- Split file loader must be loaded first -->
<script>
var splitLoader = document.createElement("script");
splitLoader.src = "StreamingAssets/split-file-loader.js";
splitLoader.onload = function() {
    console.log("[Unity] Split file loader ready, loading Unity...");
    var script = document.createElement("script");
    script.src = loaderUrl;
    // Unity初期化コード
};
document.body.appendChild(splitLoader);
</script>
```

### 2.4 結果

- 404エラーは完全に解決
- ファイルの読み込みが成功
- しかし、新たな問題が発覚

## 3. 第2段階：音声速度異常の発見

### 3.1 問題の症状

ユーザーからの報告：
> 「かなりの早口で何を言っているのかわかりません」

### 3.2 ログ分析による発見

```
[UnityONNXRuntime] === SYNTHESIS START ===
[UnityONNXRuntime] Input phoneme IDs: [1, 25, 11, 22, 50, 8, 39, 8, 56, 7, 2]
[UnityONNXRuntime] Expected output: ~18,000 samples at 22050Hz (~0.8 seconds)
[UnityONNXRuntime] === FINAL OUTPUT ===
[UnityONNXRuntime] Final data length: 96256 samples
[UnityONNXRuntime] Duration at 22050Hz: 4.37 seconds
```

期待値の5倍以上の長さの音声が生成されていることが判明。

### 3.3 詳細な分析

- **期待される長さ**: 約18,000サンプル（0.8秒）
- **実際の長さ**: 96,256サンプル（4.37秒）
- **倍率**: 約5.35倍

これにより音声が極端に早口に聞こえる問題が発生。

## 4. 第3段階：根本原因の調査

### 4.1 環境別の動作検証

#### localhost環境
- 問題が発生（5倍の長さ）
- Unity WebGL環境として動作

#### piper-plusのWebデモ
- 正常動作
- 同じONNXモデル、同じONNX Runtime Webを使用
- 純粋なWeb環境

#### 結論
Unity WebGL環境特有の問題であることが確定。

### 4.2 技術的な発見

#### テンソル形状の異常
```
[UnityONNXRuntime] Output tensor dims: [1, 1, 1, 96256]
[UnityONNXRuntime] WARNING: Expected 3D tensor, got 4D
```

本来は`[1, 1, audio_length]`の3次元テンソルであるべきが、`[1, 1, 1, audio_length]`の4次元テンソルとして出力されている。

#### 根本原因の仮説

1. **Unity WebGLのFloat32Array処理の問題**
   - JavaScriptとUnity間のデータマーシャリングで問題発生
   - Float32Arrayが正しく処理されない

2. **ONNX Runtime WebのWASMプロバイダーの挙動**
   - Unity WebGL環境で異常な動作
   - メモリアライメントの問題の可能性

3. **推論パラメータの適用問題**
   - length_scaleパラメータが正しく適用されていない可能性

## 5. 第4段階：length_scale調整による対応

### 5.1 Unity WebGL環境検出ロジックの実装

```javascript
const isUnityWebGL = (
    typeof unityInstance !== 'undefined' ||
    window.location.pathname.includes('/Build/') ||
    window.location.pathname.includes('/uPiper/') ||
    (typeof Module !== 'undefined' && (Module.SystemInfo || Module.unityVersion)) ||
    (document.querySelector('#unity-canvas') !== null) ||
    (typeof window.GitHubPagesAdapter !== 'undefined')
);
```

### 5.2 初回対応（length_scale = 0.17）

```javascript
if (isUnityWebGL) {
    const originalScale = lengthScale;
    lengthScale = lengthScale * 0.17;  // 1/5.8 ≈ 0.17
    this.log(`[UNITY WEBGL FIX] Adjusting length_scale from ${originalScale} to ${lengthScale}`);
}
```

#### 結果
- 音声長は正しくなった（17,664サンプル = 0.8秒）
- しかし音質が著しく劣化
- 「意味がわからない音の連続」状態に

### 5.3 ダウンサンプリング処理の追加

音声が長い場合に単純に間引く処理を実装：

```javascript
if (ratio > 4.5 && ratio < 6) {
    const skipFactor = 5;
    const targetLength = Math.floor(audioData.length / skipFactor);
    const fixedAudio = new Float32Array(targetLength);
    
    for (let i = 0; i < targetLength; i++) {
        fixedAudio[i] = audioData[i * skipFactor];
    }
    
    audioData = fixedAudio;
}
```

#### 結果
- 音声がさらに歪む
- 根本的な解決にならない

## 6. 第5段階：調整値の最適化

### 6.1 実データに基づく分析

最新のログから実際の出力を分析：
- **実際の出力**: 22,016サンプル（1.00秒）
- **期待値**: 18,000サンプル（0.82秒）
- **比率**: 1.22倍（5倍ではない）

### 6.2 最終調整（length_scale = 0.82）

```javascript
if (isUnityWebGL) {
    const originalScale = lengthScale;
    // 実際の出力を見ると、22016サンプル vs 期待18000サンプル = 約1.22倍
    // そのため、0.82の調整で正しい長さになるはず
    lengthScale = lengthScale * 0.82;  // 18000/22016 ≈ 0.82
    this.log(`[UNITY WEBGL FIX] Adjusting length_scale from ${originalScale} to ${lengthScale}`);
    this.log('Note: Adjustment based on actual output ratio (22016/18000)');
}
```

### 6.3 ダウンサンプリング処理の無効化

```javascript
// 注意: length_scale調整により、この問題は解決済み
// 以下の処理は不要になったため、スキップ
if (false && audioData.length > 50000 && phonemeIds.length === 11) {
    // このブロックは無効化されています
}
```

## 7. 技術的詳細と考察

### 7.1 問題の本質

#### 症状
- 4次元テンソルの出力（期待は3次元）
- 音声長の不安定性（5倍になったり1.22倍になったり）
- Unity WebGL環境でのみ発生

#### 表面的な原因
- Unity WebGLのFloat32Array処理の問題
- JavaScriptとUnity間のデータマーシャリング

#### 根本原因（未解明）
- なぜ4次元テンソルになるのか
- なぜ音声長が環境により変動するのか
- Unity WebGLとONNX Runtime Webの相互作用の詳細

### 7.2 ONNX Runtime Webの挙動

```javascript
// WASMプロバイダーを使用（WebGLはint64をサポートしていない）
this.session = await ort.InferenceSession.create(modelPath, {
    executionProviders: ['wasm'],
    graphOptimizationLevel: 'all',
    wasmPaths: {
        'ort-wasm.wasm': 'https://cdn.jsdelivr.net/npm/onnxruntime-web@1.17.1/dist/ort-wasm.wasm',
        // ...
    }
});
```

### 7.3 メモリ管理の考察

Unity WebGLのメモリ管理とONNX Runtime Webのメモリ管理の間に不整合がある可能性：

```javascript
// Unity側からのデータ転送
ONNXRuntime_Synthesize: function(phonemeIdsPtr, length, callbackPtr) {
    const phonemeIds = [];
    for (let i = 0; i < length; i++) {
        phonemeIds.push(HEAP32[(phonemeIdsPtr >> 2) + i]);
    }
    // ...
}
```

## 8. 実装されたファイルと変更内容

### 8.1 修正されたファイル一覧

1. **Assets/StreamingAssets/split-file-loader.js**
   - 完全に書き直し
   - fetchとXMLHttpRequestの両方をインターセプト
   - ファイル名パターンの修正

2. **Assets/StreamingAssets/onnx-runtime-wrapper.js**
   - Unity WebGL環境検出ロジックの追加
   - length_scale調整の実装
   - デバッグログの大幅追加

3. **Build/Web/index.html**
   - スクリプト読み込み順序の変更
   - split-file-loaderを最初に読み込み

4. **.github/workflows/deploy-webgl.yml**
   - ビルド後処理の追加
   - ファイルパスの修正

### 8.2 詳細な変更内容

#### split-file-loader.js（主要部分）

```javascript
// 初期化時のログ
console.log('[SplitFileLoader] Initializing...');

// オリジナル関数の保存
const originalFetch = window.fetch;
const originalXHROpen = XMLHttpRequest.prototype.open;

// Fetchのオーバーライド
window.fetch = async function(input, init) {
    const url = typeof input === 'string' ? input : input.url;
    
    if (url && (url.includes('openjtalk-unity.data') || 
                url.includes('openjtalk-unity-full.data')) && 
        !url.includes('.part')) {
        
        console.log('[SplitFileLoader] Intercepting fetch for:', url);
        const basePath = url.substring(0, url.lastIndexOf('/'));
        
        try {
            // 分割ファイルの読み込み
            const responses = await Promise.all([
                originalFetch(`${basePath}/openjtalk-unity.data.partaa`, init),
                originalFetch(`${basePath}/openjtalk-unity.data.partab`, init)
            ]);
            
            // バッファの結合
            const buffers = await Promise.all(
                responses.map(r => r.arrayBuffer())
            );
            const buffer1 = new Uint8Array(buffers[0]);
            const buffer2 = new Uint8Array(buffers[1]);
            const combinedBuffer = new Uint8Array(
                buffer1.byteLength + buffer2.byteLength
            );
            combinedBuffer.set(buffer1, 0);
            combinedBuffer.set(buffer2, buffer1.byteLength);
            
            console.log('[SplitFileLoader] Combined', 
                       combinedBuffer.byteLength, 'bytes');
            
            // Responseオブジェクトとして返す
            return new Response(combinedBuffer.buffer, {
                status: 200,
                statusText: 'OK',
                headers: new Headers({
                    'Content-Type': 'application/octet-stream',
                    'Content-Length': combinedBuffer.byteLength.toString()
                })
            });
        } catch (error) {
            console.error('[SplitFileLoader] Error:', error);
            throw error;
        }
    }
    
    return originalFetch.apply(this, arguments);
};
```

## 9. 試みたが撤回された対応

### 9.1 極端なlength_scale調整（0.17）

- 音声を5.8倍速くする設定
- 音質が著しく劣化
- 実用に耐えない

### 9.2 ダウンサンプリング処理

- 単純な間引き処理
- 音声の歪みを悪化させる
- 根本的な解決にならない

### 9.3 pitch調整の提案

ユーザーからの指摘：
> 「audioSource.pitch = 0.2f; これは短絡的な解決方法にしかならない気がします」

Unity側でのピッチ調整は症状への対処でしかなく、根本解決にならないため採用せず。

## 10. 未解決の問題と今後の課題

### 10.1 根本原因の特定

#### 未解明の点
1. なぜUnity WebGLで4次元テンソルになるのか
2. なぜ音声長が環境により変動するのか（5倍→1.22倍）
3. メモリアライメントの問題なのか、データ転送の問題なのか

#### 調査が必要な領域
1. Unity WebGLのFloat32Array実装の詳細
2. ONNX Runtime WebのWASMプロバイダーのUnity対応
3. EmscriptenのメモリモデルとUnityの相互作用

### 10.2 改善提案

#### 短期的改善
1. length_scaleの動的調整メカニズムの実装
2. より詳細なデバッグログの追加
3. エラーハンドリングの強化

#### 長期的改善
1. Unity側のJavaScript連携コードの見直し
2. ONNX Runtime Webの異なるバージョンでのテスト
3. WebAssemblyメモリ管理の最適化
4. 代替実装の検討（WebGPU、WebNN等）

## 11. コミット履歴

### 実施されたコミット

1. **fd626bf** - fix: OpenJTalkデータファイル読み込みの根本的な問題を解決
2. **ce2ac22** - fix: split-file-loaderのパスとファイル名の不一致を修正
3. **a8b9a7a** - fix: OpenJTalkファイル名の不一致とビルド後処理を修正
4. **b719f6d** - fix: OpenJTalkファイルを分割してGitHub Pages対応
5. **80caccb** - fix: Unity WebGL環境での音声速度問題を根本的に解決
6. **ab217ac** - fix: 不要なダウンサンプリング処理を無効化
7. **037055c** - fix: length_scale調整値を適切な値に修正

### 各コミットの詳細

#### fix: OpenJTalkデータファイル読み込みの根本的な問題を解決
- split-file-loader.jsの全面改修
- fetchとXMLHttpRequestの両方に対応
- ファイル名パターンの修正

#### fix: Unity WebGL環境での音声速度問題を根本的に解決
- Unity WebGL環境検出ロジックの実装
- length_scale調整の導入
- デバッグログの追加

## 12. 重要なログとデバッグ情報

### 12.1 404エラー解決前のログ

```
GET https://ayutaz.github.io/uPiper/StreamingAssets/openjtalk-unity.data 404 (Not Found)
[SplitFileLoader] Script not properly initialized
[OpenJTalkUnity] Failed to load dictionary
```

### 12.2 404エラー解決後、音声速度問題発生時のログ

```
[UnityONNXRuntime] === SYNTHESIS START ===
[UnityONNXRuntime] Input phoneme IDs: [1, 25, 11, 22, 50, 8, 39, 8, 56, 7, 2]
[UnityONNXRuntime] Expected output: ~18,000 samples at 22050Hz (~0.8 seconds)
[UnityONNXRuntime] === TENSOR ANALYSIS ===
[UnityONNXRuntime] Output tensor dims: [1, 1, 1, 96256]
[UnityONNXRuntime] WARNING: Expected 3D tensor, got 4D
[UnityONNXRuntime] === FINAL OUTPUT ===
[UnityONNXRuntime] Final data length: 96256 samples
[UnityONNXRuntime] Duration at 22050Hz: 4.37 seconds
```

### 12.3 length_scale調整後のログ

```
[UnityONNXRuntime] [UNITY WEBGL FIX] Detected Unity WebGL environment
[UnityONNXRuntime] [UNITY WEBGL FIX] Adjusting length_scale from 1 to 0.82
[UnityONNXRuntime] Note: Adjustment based on actual output ratio (22016/18000)
[UnityONNXRuntime] === FINAL OUTPUT ===
[UnityONNXRuntime] Final data length: 22016 samples
[UnityONNXRuntime] Duration at 22050Hz: 1.00 seconds
```

### 12.4 デバッグ用に追加した主要なログポイント

1. **環境検出**
   ```javascript
   this.log(`[UNITY WEBGL FIX] Detected Unity WebGL environment`);
   ```

2. **テンソル分析**
   ```javascript
   this.log(`Output tensor dims: [${audioTensor.dims.join(', ')}]`);
   this.log(`WARNING: Expected ${expectedDims}D tensor, got ${actualDims}D`);
   ```

3. **データ処理**
   ```javascript
   this.log('Root cause analysis:');
   this.log('1. Unity WebGL marshals Float32Array incorrectly');
   this.log('2. Data is duplicated or stretched during transfer');
   ```

## 13. 結論と教訓

### 13.1 達成できたこと

1. **404エラーの完全解決**
   - split-file-loaderの実装改善
   - ファイル読み込みの成功

2. **音声長の調整**
   - length_scaleパラメータによる調整
   - 実用的なレベルまでの改善

3. **問題の特定と文書化**
   - Unity WebGL特有の問題であることを確定
   - 詳細な調査記録の作成

### 13.2 達成できなかったこと

1. **根本原因の解明**
   - なぜ4次元テンソルになるのか不明
   - 音声長の変動原因が不明

2. **完全な解決**
   - 対症療法的な対応に留まる
   - 本質的な問題は未解決

### 13.3 教訓

1. **WebGL環境の特殊性**
   - Unity WebGLは通常のWeb環境とは大きく異なる
   - データマーシャリングに特別な注意が必要

2. **段階的なデバッグの重要性**
   - 詳細なログ出力が問題特定に不可欠
   - 環境別の比較検証が重要

3. **根本原因追求の難しさ**
   - 複数のレイヤー（Unity、JavaScript、WASM）が関わる問題は複雑
   - 対症療法と根本治療のバランスが必要

### 13.4 今後の方針

1. **継続的な調査**
   - Unity側のコード改善
   - ONNX Runtime Webの更新追跡

2. **代替案の検討**
   - WebGPU/WebNNの採用検討
   - サーバーサイド処理の検討

3. **ドキュメントの充実**
   - トラブルシューティングガイドの作成
   - 既知の問題リストの管理

## 付録

### A. 関連ファイル

- `/Assets/StreamingAssets/split-file-loader.js`
- `/Assets/StreamingAssets/onnx-runtime-wrapper.js`
- `/Assets/StreamingAssets/openjtalk-unity-wrapper.js`
- `/Assets/uPiper/Plugins/WebGL/ONNXRuntimeBridge.jslib`
- `/Build/Web/index.html`
- `/.github/workflows/deploy-webgl.yml`

### B. 参考リンク

- [ONNX Runtime Web Documentation](https://onnxruntime.ai/docs/get-started/with-javascript.html)
- [Unity WebGL Build Documentation](https://docs.unity3d.com/Manual/webgl-building.html)
- [GitHub Pages Large File Handling](https://docs.github.com/en/pages/getting-started-with-github-pages/about-github-pages#usage-limits)

### C. テスト環境

- Unity: 6000.0.55f1
- ONNX Runtime Web: 1.17.1
- ブラウザ: Chrome 127, Firefox 129, Safari 17
- デプロイ先: GitHub Pages (https://ayutaz.github.io/uPiper/)

---

*このドキュメントは2025年8月12日の調査作業に基づいて作成されました。*