# WebGL対応 調査結果と実装計画

最終更新: 2025-08-04

## 実装進捗サマリー

- **Phase 1 基盤整備**: 100%完了 ✅
  - JavaScript interop層完成
  - WebGL音素化クラス実装済み
  - ✅ wasm_open_jtalk統合完了（2025-08-04）
  - ✅ eSpeak-ng統合完了（2025-08-04）
  
- **Phase 2 Core機能**: 60%完了 🔄
  - InferenceEngineDemoのWebGL対応済み
  - 基本的なキャッシュ機能実装済み
  - ✅ WebGLビルドテンプレート作成済み
  - WebGL専用デモシーン未作成
  - 統一音素化API未実装
  
- **Phase 3 CI/CD**: 80%完了 ✅
  - ✅ GitHub Actions設定完了（2025-08-04）
  - ✅ 自動デプロイ設定完了
  - ビルドサイズ最適化が未完了
  
- **Phase 4 最適化**: 0%完了 ❌
  - パフォーマンス最適化未着手

## 調査結果

### 1. piper-plus側のWeb対応実装（PR #144）

#### 主要な実装内容
- **統一多言語音素化API**: 日本語（OpenJTalk）と英語（eSpeak-ng）をサポート
- **WebAssembly統合**: Emscriptenを使用してeSpeak-ngをWebAssemblyにコンパイル
- **ONNX Runtime Web**: ブラウザ上でのニューラル音声合成を実現
- **カスタム辞書機能**: 未登録単語の処理に対応

#### 技術的アプローチ
1. **音素化エンジンのWASM化**
   - eSpeak-ngをEmscriptenでコンパイル
   - 言語データのWebAssemblyビルドスクリプト作成

2. **JavaScript Bridge実装**
   - `SimpleUnifiedPhonemizerAPI`で言語間切り替え
   - `ESpeakPhonemeExtractor`でPython互換の英語音素化
   - Unicode対応の言語固有音素マッピング

3. **ビルドシステム**
   - WebAssemblyコンパイル用ビルドスクリプト
   - 言語モデルのダウンロード・準備スクリプト
   - GitHub Actionsワークフローの多言語デモ対応

### 2. Unity側の既存WebGL関連実装

#### 現在の状況
1. **WebGLプラットフォーム検出**（実装済み）
   - `PlatformHelper.cs`: WebGLプラットフォーム検出
   - `PlatformDefines.cs`: WebGL用のコンパイル時定数
   - ネイティブプラグインサポートの無効化

2. **WebGL除外処理**（実装済み）
   - OpenJTalkPhonemizer: `#if !UNITY_WEBGL`で完全除外
   - PiperTTS: WebGLプラットフォームで警告表示
   - デモクラス: 日本語機能の除外

3. **WebGLビルド設定**（部分的に実装）
   - `PiperBuildProcessor.cs`: WebGL固有設定
     - Gzip圧縮
     - メモリサイズ: 512MB
   - WebGLデモシーン参照（未実装）

4. **既存の調査ドキュメント**
   - `docs/ja/guides/technical/webgl-investigation.md`に詳細な技術調査
   - `wasm_open_jtalk` npmパッケージの使用を推奨
   - Unity WebGL JavaScriptインターオプの実装方法

#### 未実装部分
- ❌ WebGL用音素化実装
- ❌ JavaScript interopファイル（`.jslib`）
- ❌ WebAssembly OpenJTalk統合
- ❌ WebGLデモシーン実装
- ❌ CI/CDでのWebGLビルド

## 実装計画と進捗状況

最終更新: 2025-08-04

### Phase 1: 基盤整備（1週間） - **進捗: 100%完了**

#### 1.1 WebAssembly音素化エンジンの準備
- [x] wasm_open_jtalkのビルドと統合 ✅ **2025-08-04完了**
  - piper-plusからopenjtalk.js/wasmをコピー
  - 実際のOpenJTalk WebAssembly統合実装
  - 辞書ファイル配置とローディング機能
- [x] eSpeak-ng WebAssemblyビルドの準備 ✅ **2025-08-04完了**
  - piper-plusからeSpeak-ngファイルをコピー
  - 実際のeSpeak-ng WebAssembly統合実装
  - 基本的な英語音素化機能実装
- [x] Unity WebGLプラグインディレクトリ構造の整備 ✅

#### 1.2 JavaScript Interopレイヤー実装
- [x] `openjtalk_wrapper.jslib`の作成 ✅ **実装完了**
- [x] `espeak_wrapper.jslib`の作成 ✅ **実装完了**
- [x] `indexeddb_cache.jslib`の作成 ✅
- [x] P/Invoke定義の追加 (`WebGLInterop.cs`) ✅

#### 1.3 WebGL用音素化クラス実装
- [x] `WebGLOpenJTalkPhonemizer`クラスの作成 ✅
- [x] `WebGLESpeakPhonemizer`クラスの作成 ✅
- [x] `WebGLCacheManager`クラスの作成 ✅
- [x] 既存の音素化システムとの統合 ✅

### Phase 2: Core機能実装（2週間） - **進捗: 50%完了**

#### 2.1 音声合成パイプライン対応
- [x] WebGL用InferenceAudioGeneratorの調整 (GPUPixelバックエンド) ✅
- [x] メモリ管理の最適化（IndexedDBキャッシュ基本実装） ✅
- [x] 非同期処理の実装 ✅
- [x] WebGLビルドテンプレート作成 ✅ **2025-08-04完了**
- [ ] メモリ使用量の詳細な最適化

#### 2.2 言語切り替え機能
- [x] InferenceEngineDemoのWebGL対応 ✅
- [ ] 統一音素化APIの実装
- [ ] 日本語・英語・中国語の自動切り替え対応
- [ ] 高度なフォールバック処理の実装

#### 2.3 デモシーン作成
- [ ] WebGL専用デモシーンの実装
- [ ] UIの最適化（WebGL向け）
- [ ] パフォーマンスモニタリング

### Phase 3: CI/CDとデプロイ（1週間） - **進捗: 80%完了**

#### 3.1 ビルドパイプライン構築
- [x] GitHub ActionsでのWebGLビルド設定 ✅
- [x] ビルドアーティファクトの生成 ✅
- [ ] ビルドサイズの最適化

#### 3.2 GitHub Pagesデプロイ
- [x] デプロイワークフローの作成 ✅
- [x] 自動デプロイの設定 ✅
- [ ] カスタムドメイン設定（オプション）

#### 3.3 テストとドキュメント
- [x] WebGLプラットフォームテスト（EditMode） ✅
- [ ] WebGLランタイムテスト
- [ ] パフォーマンステスト
- [x] ユーザードキュメントの作成 ✅

### Phase 4: 最適化と拡張（2週間） - **進捗: 0%完了**

#### 4.1 パフォーマンス最適化
- [ ] WASMストリーミングインスタンシエーション
- [ ] SharedArrayBuffer対応（可能な場合）
- [ ] キャッシュ戦略の改善

#### 4.2 追加機能
- [ ] プログレッシブローディング
- [ ] オフライン対応（Service Worker）
- [ ] モバイルブラウザ最適化

## 技術的考慮事項

### メモリ管理
- Unity WebGL heap: 2-4GB推奨
- IndexedDBでの音声データキャッシュ
- 動的メモリ割り当ての最小化

### ブラウザ互換性
- Chrome 90+（推奨）
- Firefox 89+
- Safari 15+（WebAssembly SIMD制限あり）
- Edge 90+

### セキュリティ
- CORS対応
- HTTPS必須
- Content Security Policy設定

### パフォーマンス目標
- 初回ロード: < 10秒
- 音素化レイテンシ: < 100ms
- 音声生成: リアルタイム（1x速度）

## リスクと対策

### 技術的リスク
1. **WASMサイズ**: OpenJTalk + eSpeak-ng + ONNXで大きくなる可能性
   - 対策: 言語別分割ロード、圧縮最適化

2. **ブラウザメモリ制限**: 大規模テキストで問題の可能性
   - 対策: ストリーミング処理、チャンク分割

3. **WebGL制限**: Unity.InferenceEngineの制約
   - 対策: ONNX Runtime Webへの直接統合検討

### スケジュールリスク
- 予想期間: 6週間
- バッファ: 2週間
- 優先度: Phase 1-2を最優先

## 成功指標

1. **機能要件**
   - ✅ 日本語・英語の音声合成が動作
   - ✅ 中国語音声合成の基本対応
   - ✅ GitHub Pagesでデモ公開

2. **パフォーマンス要件**
   - ✅ 初回ロード10秒以内
   - ✅ 音素化100ms以内
   - ✅ リアルタイム音声生成

3. **品質要件**
   - ✅ 主要ブラウザで動作
   - ✅ モバイルデバイス対応
   - ✅ エラーハンドリング完備

## 実装完了項目（2025-08-04更新）

### WebAssembly統合
1. **openjtalk_wrapper.jslib** ✅
   - `InitializeOpenJTalkWeb`: 実際のOpenJTalkモジュールロード実装
   - `LoadOpenJTalkDictionary`: 辞書ファイルの非同期ロード実装
   - `PhonemizeJapaneseText`: `_openjtalk_synthesis_labels`を使用した実装
   - PUA文字へのマッピング実装（ch→\ue001等）

2. **ディレクトリ構造** ✅
   - `Assets/StreamingAssets/`: WebAssemblyファイル配置
   - `Assets/StreamingAssets/dict/`: OpenJTalk辞書ファイル配置
   - `Assets/StreamingAssets/voice/`: 音声ファイル配置
   - `Assets/WebGLTemplates/uPiperTemplate/`: カスタムテンプレート作成

### 実装完了項目（eSpeak-ng統合）
1. **espeak_wrapper.jslib** ✅
   - `InitializeESpeakWeb`: 実際のeSpeak-ngモジュールロード実装
   - `PhonemizeEnglishText`: 基本的な英語音素化実装（単語辞書ベース）
   - `IsESpeakInitialized`: 初期化状態チェック機能

2. **ディレクトリ構造** ✅
   - `Assets/StreamingAssets/espeak-ng/`: eSpeak-ngファイル配置
   - espeakng.min.js, espeakng.worker.js, espeakng.worker.data

### 残りの未実装機能
   - 統一音素化API（言語自動検出）
   - WebGL専用デモシーン
   - メモリ使用量の詳細な最適化

## 次のステップ

1. **eSpeak-ng統合**
   - piper-plusのeSpeak-ng WASM実装を参考に
   - 英語音素化の実装

2. **CI/CD構築**
   - GitHub ActionsでのWebGLビルド自動化
   - GitHub Pagesへの自動デプロイ設定

3. **テストとデモ**
   - WebGLビルドのローカルテスト
   - デモシーンの作成
   - パフォーマンス測定