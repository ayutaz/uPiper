# WebGL対応 調査結果と実装計画

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

## 実装計画

### Phase 1: 基盤整備（1週間）

#### 1.1 WebAssembly音素化エンジンの準備
- [ ] wasm_open_jtalkのビルドと統合
- [ ] eSpeak-ng WebAssemblyビルドの準備
- [ ] Unity WebGLプラグインディレクトリ構造の整備

#### 1.2 JavaScript Interopレイヤー実装
- [ ] `openjtalk_wrapper.jslib`の作成
- [ ] `espeak_wrapper.jslib`の作成
- [ ] P/Invoke定義の追加

#### 1.3 WebGL用音素化クラス実装
- [ ] `WebGLOpenJTalkPhonemizer`クラスの作成
- [ ] `WebGLESpeakPhonemizer`クラスの作成
- [ ] 既存の音素化システムとの統合

### Phase 2: Core機能実装（2週間）

#### 2.1 音声合成パイプライン対応
- [ ] WebGL用InferenceAudioGeneratorの調整
- [ ] メモリ管理の最適化（IndexedDBキャッシュ）
- [ ] 非同期処理の実装

#### 2.2 言語切り替え機能
- [ ] 統一音素化APIの実装
- [ ] 日本語・英語・中国語の切り替え対応
- [ ] フォールバック処理の実装

#### 2.3 デモシーン作成
- [ ] WebGLDemoSceneの実装
- [ ] UIの最適化（WebGL向け）
- [ ] パフォーマンスモニタリング

### Phase 3: CI/CDとデプロイ（1週間）

#### 3.1 ビルドパイプライン構築
- [ ] GitHub ActionsでのWebGLビルド設定
- [ ] ビルドアーティファクトの生成
- [ ] ビルドサイズの最適化

#### 3.2 GitHub Pagesデプロイ
- [ ] デプロイワークフローの作成
- [ ] 自動デプロイの設定
- [ ] カスタムドメイン設定（オプション）

#### 3.3 テストとドキュメント
- [ ] WebGLプラットフォームテスト
- [ ] パフォーマンステスト
- [ ] ユーザードキュメントの作成

### Phase 4: 最適化と拡張（2週間）

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

## 次のステップ

1. wasm_open_jtalkのビルド環境構築
2. 基本的な.jslibファイルの作成とテスト
3. WebGLOpenJTalkPhonemizerクラスの実装開始