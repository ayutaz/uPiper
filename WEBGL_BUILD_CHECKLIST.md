# Unity WebGL ビルドチェックリスト

## ビルド前の確認事項

### ✅ 実装完了項目

1. **ONNX Runtime Web 統合**
   - [x] onnx-runtime-wrapper.js（StreamingAssets配置済み）
   - [x] ONNXRuntimeBridge.jslib（WebGLプラグイン設定済み）
   - [x] ONNXRuntimeWebGL.cs（条件付きコンパイル実装済み）

2. **OpenJTalk WASM 統合**
   - [x] openjtalk-unity.wasm（完全辞書版 102.33MB）
   - [x] openjtalk-unity.js（初期化スクリプト）
   - [x] openjtalk-unity-wrapper.js（Unity統合ラッパー）
   - [x] openjtalk-unity.data（辞書データ）

3. **Unity 側の実装**
   - [x] InferenceAudioGenerator.cs（WebGL自動切り替え）
   - [x] WebGLOpenJTalkUnityPhonemizer.cs（音素変換）
   - [x] OpenJTalkToPiperMapping.cs（PUA文字対応）
   - [x] WebGLPostBuildProcessor.cs（自動ファイルコピー）

4. **必要なファイル**
   - [x] ja_JP-test-medium.onnx（StreamingAssets配置済み）
   - [x] ja_JP-test-medium.onnx.json（StreamingAssets配置済み）

## ビルド手順

### 1. Unity ビルド設定

1. **File > Build Settings** を開く
2. **Platform: WebGL** を選択
3. **Player Settings** で以下を確認：
   - Color Space: Linear（推奨）
   - WebGL Template: OpenJTalkTest（カスタムテンプレート）
   - Compression Format: Gzip または Disabled（開発時）
   - Memory Size: 512 MB 以上

### 2. ビルド実行

```
1. Build Settings で "Build" をクリック
2. 出力フォルダを選択（例: Build/Web）
3. ビルド完了を待つ（約5-10分）
```

### 3. ビルド後の確認

ビルドフォルダ構造：
```
Build/Web/
├── Build/
│   ├── Web.data
│   ├── Web.framework.js
│   ├── Web.loader.js
│   └── Web.wasm
├── StreamingAssets/
│   ├── openjtalk-unity.wasm
│   ├── openjtalk-unity.js
│   ├── openjtalk-unity.data
│   ├── openjtalk-unity-wrapper.js
│   ├── onnx-runtime-wrapper.js
│   ├── ja_JP-test-medium.onnx
│   └── ja_JP-test-medium.onnx.json
└── index.html
```

### 4. ローカルサーバーでのテスト

```bash
# Python 3 の場合
cd Build/Web
python -m http.server 8000

# Node.js の場合
npx http-server -p 8000
```

ブラウザで `http://localhost:8000` を開く

## テスト項目

### 基本動作確認

1. **初期化確認**
   - [ ] OpenJTalk WASM がロードされる
   - [ ] ONNX Runtime Web がロードされる
   - [ ] 日本語モデルがロードされる

2. **音素変換テスト**
   - [ ] 「こんにちは」→ 正しい音素列
   - [ ] 音素ID が Windows と一致

3. **音声合成テスト**
   - [ ] 「こんにちは」が正しく発音される
   - [ ] 「ありがとうございます」が正しく発音される
   - [ ] 長文の音声合成が可能

### パフォーマンス確認

- [ ] 初回ロード時間: 10秒以内
- [ ] 音声合成時間: 1秒以内（短文）
- [ ] メモリ使用量: 512MB以内

## トラブルシューティング

### よくある問題

1. **CORS エラー**
   - ローカルファイルではなくHTTPサーバー経由でアクセス
   - サーバーのCORS設定を確認

2. **ファイルが見つからない**
   - StreamingAssets フォルダの配置を確認
   - WebGLPostBuildProcessor が実行されているか確認

3. **メモリ不足**
   - Player Settings で Memory Size を増やす
   - ブラウザのメモリ制限を確認

4. **音声が出ない/おかしい**
   - ブラウザコンソールでエラーを確認
   - 音素ID が正しく生成されているか確認
   - ONNX Runtime の推論結果を確認

## デバッグ方法

### ブラウザコンソールで実行

```javascript
// OpenJTalk 状態確認
window.OpenJTalkUnityAPI._debug.isInitialized()

// 音素変換テスト
window.OpenJTalkUnityAPI.phonemize('こんにちは')

// ONNX Runtime 状態確認
window.UnityONNXRuntime.isInitialized()

// 推論テスト
window.UnityONNX.synthesize([0, 23, 30, 8, 4, 28, 9, 4, 32, 9, 4, 18, 1, 0])
```

## 最終確認

- [ ] すべての必要ファイルが StreamingAssets に存在
- [ ] WebGL ビルドが正常に完了
- [ ] ローカルサーバーで動作確認
- [ ] 「こんにちは」が正しく発音される
- [ ] Windows/Android と同等の音質

## 注意事項

1. **ファイルサイズ**
   - 総ビルドサイズ: 約 150-200MB
   - 初回ロードに時間がかかる場合がある

2. **ブラウザ互換性**
   - Chrome 90+ 推奨
   - Firefox 88+ 対応
   - Safari 14.1+ 対応（制限あり）
   - Edge 90+ 対応

3. **モバイル対応**
   - iOS Safari: 制限付き動作
   - Android Chrome: 動作可能（パフォーマンス注意）