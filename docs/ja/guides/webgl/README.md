# WebGL実装ガイド

## 概要

uPiperのWebGL実装は、ONNX Runtime Webを使用して完全な日本語音声合成機能を提供します。Unity AI Inference EngineのWebGL制限を回避し、piper-plusと同等の精度を実現しています。

## 主な特徴

- ✅ **完全な辞書サポート**: NAIST Japanese Dictionary (102.33MB) を使用
- ✅ **高精度な音素化**: Windows/Android版と同等の精度
- ✅ **GitHub Pages対応**: 100MB制限に対応した自動ファイル分割
- ✅ **ONNX Runtime Web統合**: ブラウザネイティブの高速推論

## アーキテクチャ

```
Unity C# Layer
    ↓
JSLib Bridge (ONNXRuntimeBridge.jslib)
    ↓
JavaScript Runtime (onnx-runtime-wrapper.js)
    ↓
ONNX Runtime Web + OpenJTalk WASM
```

## 必要なファイル

WebGLビルドには以下のファイルが必要です：

### OpenJTalk WASM
- `openjtalk-unity.js` - OpenJTalkのJavaScriptインターフェース
- `openjtalk-unity.wasm` - OpenJTalkのWebAssemblyバイナリ
- `openjtalk-unity.data` - NAIST辞書データ（分割済み）
  - `openjtalk-unity.data.part000` (90MB)
  - `openjtalk-unity.data.part001` (13MB)
  - `openjtalk-unity.data.manifest.json`

### ONNX Runtime Web
- `onnx-runtime-wrapper.js` - ONNX Runtime Webラッパー
- `ONNXRuntimeBridge.jslib` - Unity-JavaScript ブリッジ

### 統合スクリプト
- `github-pages-adapter.js` - GitHub Pages用パス解決とファイル再構築
- `openjtalk-unity-wrapper.js` - OpenJTalk統合ラッパー

## ビルド手順

### 1. 事前準備

```bash
# 大きなファイルの分割（初回のみ）
uv run python split-openjtalk-data.py
```

### 2. Unity設定

Player Settings:
- **Target Platform**: WebGL
- **Compression Format**: Gzip または Disabled（開発時）
- **Decompression Fallback**: 有効（Unity 6000.0.55f1で修正済み）

### 3. ビルド実行

1. Unity Editorで `File > Build Settings` を開く
2. WebGLプラットフォームを選択
3. `Build` または `Build And Run` をクリック

### 4. ビルド後処理

`WebGLPostBuildProcessor` が自動的に以下を実行：
- 必要なファイルをStreamingAssetsにコピー
- index.htmlにスクリプトタグを追加
- 分割ファイルの配置

## ローカルテスト

```bash
# Pythonサーバーで実行
python -m http.server 8080 --directory Build

# ブラウザでアクセス
http://localhost:8080
```

## GitHub Pagesデプロイ

### 1. リポジトリ設定

1. GitHubリポジトリの Settings > Pages
2. Source: Deploy from a branch
3. Branch: gh-pages (または任意のブランチ)
4. Folder: / (root)

### 2. ファイルアップロード

```bash
# gh-pagesブランチにビルド結果をプッシュ
git checkout -b gh-pages
cp -r Build/* .
git add -A
git commit -m "Deploy WebGL build"
git push origin gh-pages
```

### 3. アクセス

```
https://[username].github.io/[repository]/
```

## トラブルシューティング

### エラー: "Failed to load openjtalk-unity.data"

**原因**: ファイルが100MBを超えている

**解決策**: 
```bash
# ファイル分割を確認
ls -lh Assets/StreamingAssets/openjtalk-unity.data.*
```

### エラー: "ONNX Runtime initialization failed"

**原因**: ONNXモデルファイルが見つからない

**解決策**:
```bash
# モデルファイルの確認
ls Assets/StreamingAssets/*.onnx*
```

### エラー: "Cannot read properties of undefined"

**原因**: Unity 6000.0.35f1のWebGLバグ

**解決策**: Unity 6000.0.55f1以降にアップデート

## パフォーマンス最適化

### 1. モデルサイズの最適化

小さいモデルを使用：
- `ja_JP-test-small.onnx` (15MB) - 開発用
- `ja_JP-test-medium.onnx` (63MB) - 本番用

### 2. キャッシング戦略

```javascript
// github-pages-adapter.js でキャッシュ有効化
const CACHE_NAME = 'upiper-v1';
const CACHE_DURATION = 7 * 24 * 60 * 60 * 1000; // 7日間
```

### 3. 遅延読み込み

```javascript
// 必要時にのみONNXモデルを読み込み
async function loadModelOnDemand() {
    if (!window.onnxModel) {
        window.onnxModel = await loadONNXModel();
    }
    return window.onnxModel;
}
```

## 技術詳細

### メモリ管理

WebGLでは限られたメモリで動作するため、以下の対策を実装：

1. **自動メモリ解放**
```csharp
[DllImport("__Internal")]
private static extern void ONNXRuntime_FreeMemory(IntPtr ptr);
```

2. **ストリーミング処理**
- 大きな音声データを分割処理
- 不要なバッファの即座解放

### 非同期処理

JavaScript Promiseを使用した非同期処理：

```csharp
public async Task<float[]> GenerateAudioAsync(int[] phonemeIds)
{
    var tcs = new TaskCompletionSource<float[]>();
    ONNXRuntime_Synthesize(phonemeIds, callback);
    return await tcs.Task;
}
```

## 関連ドキュメント

- [Unity WebGL音声合成問題 - 完全調査記録](unity-webgl-audio-issue-complete-investigation.md)
- [WebGLトラブルシューティングガイド](../../../WEBGL_TROUBLESHOOTING_GUIDE.md)
- [ONNX Runtime Webアーキテクチャ](../../../ONNX_RUNTIME_WEB_ARCHITECTURE.md)
- [WebGLビルドチェックリスト](../../../../WEBGL_BUILD_CHECKLIST.md)
- [Unity WebGL実装計画](../../../../UNITY_WEBGL_OPENJTALK_IMPLEMENTATION_PLAN.md)