# WebGL対応 現在の実装状況

最終更新: 2025-08-07

## 概要

uPiperのWebGL対応実装が完了しました。Unity 6000.0.55f1へのアップグレードを含む、包括的なWebGLサポートが実装されています。

## 実装済み機能

### 1. Unity 6アップグレード
- **Unity 6000.0.35f1 → 6000.0.55f1**: cacheControlバグ（UUM-99050）の修正のため
- **すべてのワークフロー更新**: GitHub Actions、ドキュメント、設定ファイル

### 2. WebGLビルド設定
- **CI/CD統合**: GitHub ActionsによるWebGLビルド自動化
- **GitHub Pages対応**: 自動デプロイ設定
- **画面サイズ**: 960x600（デフォルト）
- **圧縮形式**: Gzip（GitHub Pages対応）

### 3. 音素化実装

#### 日本語音素化（OpenJTalk）
- **WebAssemblyモジュール統合**: openjtalk.js/openjtalk.wasm
- **ES6モジュール対応**: import.meta.url変換
- **フォールバック実装**: HEAP8エクスポートエラー対策
  - OpenJTalkの初期化に失敗した場合、簡易かな→音素変換を使用
  - WebGLJapanesePhonemizer: 軽量な代替実装

#### 多言語音素化（eSpeak-ng）
- **WebAssemblyモジュール統合**: espeak-ng.js/espeak-ng.wasm
- **英語音素化**: piper音素セット対応
- **将来の拡張**: 中国語、韓国語サポート準備

### 4. 統合音素化システム
- **UnifiedWebGLPhonemizer**: 言語自動検出
- **混合言語対応**: 日本語/英語混在テキスト処理
- **非同期処理**: UIのブロッキング回避

### 5. メモリ管理とキャッシュ
- **IndexedDB統合**: WebGLCacheManager
- **音素キャッシュ**: 重複処理の回避
- **メモリ制限対応**: Unity WebGLの1GBヒープ制限考慮

## 既知の問題と回避策

### 1. OpenJTalk HEAP8エクスポートエラー
- **問題**: EmscriptenビルドでHEAP8等がエクスポートされない
- **回避策**: フォールバック音素化実装を自動使用
- **影響**: 漢字の音素化精度が低下（かな文字は正常）

### 2. AudioContext警告
- **問題**: "The AudioContext was not allowed to start"
- **原因**: ブラウザのオートプレイポリシー
- **対策**: ユーザー操作後に音声再生開始

### 3. WebGL無効列挙エラー
- **問題**: "WebGL: INVALID_ENUM: getInternalformatParameter"
- **原因**: Unity WebGLレンダリング設定
- **影響**: 機能には影響なし（警告のみ）

## ビルドとテスト

### ローカルビルド
```bash
# Unityエディタから
Menu: uPiper/Build/Build WebGL

# ビルド出力
Build/Web/
```

### ローカルテスト
```bash
cd Build/Web
python -m http.server 8000
# http://localhost:8000 でアクセス
```

### CI/CDビルド
- プルリクエスト作成時に自動ビルド
- mainブランチマージ時にGitHub Pagesへ自動デプロイ

## パフォーマンス特性

### 初期化時間
- OpenJTalk: 約2-3秒（辞書ロード含む）
- eSpeak-ng: 約1-2秒
- フォールバック使用時: 即座

### メモリ使用量
- 基本使用量: 約200-300MB
- ONNXモデルロード後: +50-100MB（モデルサイズ依存）
- ピーク時: 最大500-600MB

### ビルドサイズ
- 現在: 約100MB（圧縮後）
- 内訳:
  - Unity Framework: 約30MB
  - ONNX Models: 約40MB
  - WebAssembly Modules: 約20MB
  - その他アセット: 約10MB

## 今後の改善案

### 短期的改善
1. **OpenJTalkビルド修正**: HEAP8エクスポート問題の根本解決
2. **ビルドサイズ最適化**: 不要アセットの削除、圧縮設定改善
3. **エラーハンドリング改善**: より詳細なエラーメッセージ

### 長期的改善
1. **Service Worker対応**: オフライン動作サポート
2. **WebGPU対応**: GPU推論による高速化
3. **SharedArrayBuffer対応**: マルチスレッド処理

## 参考リンク

- [Unity 6 WebGL ドキュメント](https://docs.unity3d.com/Manual/webgl-gettingstarted.html)
- [Emscripten EXPORTED_RUNTIME_METHODS](https://emscripten.org/docs/porting/connecting_cpp_and_javascript/Interacting-with-code.html)
- [GitHub Pages デプロイ](https://docs.github.com/en/pages)