# uPiper Development Roadmap

## Project Vision

uPiperは、Unityで動作する高品質な多言語対応Text-to-Speech (TTS)プラグインを目指しています。Piper TTSの技術をベースに、商用利用可能なライセンスで提供します。

## Development Phases

### ✅ Phase 1: Core Implementation (完了)

#### 最終実績
- **Phase 1基本実装**: 22/22人日（100%）✅ 完了
- **Phase 1.12 IL2CPPサポート**: 2/2人日（100%）✅ 完了
- **Phase 1.13 GPU推論とサンプル**: 3/3人日（100%）✅ 完了
- **Phase 1全体**: 27/27人日（100%）✅ 2025年1月23日完全完了

#### 主な成果
- Core API: 完全なインターフェースと実装
- 音素化システム: OpenJTalk統合による高精度日本語処理
- 音声合成: Unity.InferenceEngineによるONNX推論
- プラットフォーム対応: Windows/Linux/macOS完全サポート
- IL2CPP対応: Mono/IL2CPP両対応で最適なパフォーマンス
- GPU推論: 自動検出とフォールバック機構

### ✅ Phase 2: Android Support (完了)

#### 実績
- **完了**: 15/15人日（100%）✅
- **完了日**: 2025年1月23日

#### 主な成果
- 全Android ABI対応（arm64-v8a, armeabi-v7a, x86, x86_64）
- OpenJTalkネイティブライブラリのAndroidビルド成功
- Unity Android統合完了
- CI/CD統合（GitHub Actions）
- 実機での日本語TTS動作確認

### ✅ Phase 3: Multi-language Support (完了)

**完了日**: 2025年1月28日  
**実績**: 日本語・英語の音素化実装完了

#### Phase 3実装内容
- ✅ GPLライセンス問題の調査と方針決定
  - eSpeak-NG（GPL v3）の使用を回避
  - MIT/Apache 2.0/BSD系ライセンスのみ採用
- ✅ PhonemizerBackendBase実装
- ✅ ライセンス管理システム構築
- ✅ 基本的な多言語実装（5言語）
  - 日本語（OpenJTalk）✅
  - 英語（CMU辞書）✅
  - スペイン語（基本G2P）✅
  - 中国語（簡易ピンイン）✅
  - 韓国語（基本ハングル）✅

#### 主な成果
- ✅ UnifiedPhonemizer: 統一音素化インターフェース
- ✅ MixedLanguagePhonemizer: 日英混在テキスト対応
- ✅ SimpleLTSPhonemizer: 純粋なC#による英語音素化
- ✅ CircuitBreakerパターン: エラー耐性の向上
- ✅ リフレクションベースの動的ロード: 拡張性の確保

#### 精度
- 日本語（OpenJTalk）: 95%以上
- 英語（SimpleLTS）: 60-70%（基本単語）
- 混在テキスト: 85-90%

### 📅 Phase 4: 追加言語サポート (計画中)
- OpenPhonemizer統合（BSD-3-Clause Clear）
  - スペイン語、ポルトガル語、ドイツ語、フランス語、イタリア語
- eSpeak-NGオプショナルプラグイン（GPL v3）- 英語精度向上
- pypinyin完全移植（MIT）- 中国語
- IndicXlit統合（MIT）- ヒンディー語

### 📅 Phase 5: iOS Support (計画中)
- Xcodeプロジェクト設定
- Objective-C++ブリッジ
- App Store要件対応

### 📅 Phase 6: WebGL Support (計画中)
- Emscriptenビルド
- WebAssembly最適化
- ブラウザ互換性対応

### 📅 Phase 7: Advanced Features (計画中)
- 音声品質向上（高品質モデル）
- 感情表現対応
- 話者切り替え機能
- リアルタイムストリーミング

### 📅 Phase 8: Editor Tools (計画中)
- カスタムインスペクター
- 音声プレビュー機能
- デバッグツール
- VoiceBankマネージャー

### 📅 Phase 9: Optimization & QA (計画中)
- パフォーマンス最適化
- メモリ使用量削減
- 自動テスト拡充
- ドキュメント完成

## Timeline

| Phase | 期間 | 状態 | 進捗 |
|-------|------|------|------|
| Phase 1 | 2024 Q4 | ✅ 完了 | 100% |
| Phase 2 | 2025 Q1 | ✅ 完了 | 100% |
| Phase 3 | 2025 Q1 | ✅ 完了 | 100% |
| Phase 4 | 2025 Q2 | 📅 計画中 | 0% |
| Phase 5 | 2025 Q2 | 📅 計画中 | 0% |
| Phase 6 | 2025 Q3 | 📅 計画中 | 0% |
| Phase 7 | 2025 Q3-Q4 | 📅 計画中 | 0% |
| Phase 8 | 2025 Q4 | 📅 計画中 | 0% |
| Phase 9 | 2026 Q1 | 📅 計画中 | 0% |

## 技術スタック

### コア技術
- **Unity**: 6000.0.35f1
- **Unity AI Inference Engine**: 2.2.x
- **ONNX Runtime**: モデル推論
- **Piper TTS**: 音声合成エンジン

### 音素化ライブラリ（商用利用可能）
- **OpenJTalk**: 日本語（修正BSD）✅ 実装済み
- **CMU Dictionary**: 英語（BSD系）✅ 基本実装
- **OpenPhonemizer**: 多言語（BSD-3-Clause Clear）🔲 計画中
- **Flite**: 英語（MIT-CMU）🔲 計画中
- **pypinyin**: 中国語（MIT）🔲 完全実装予定
- **IndicXlit**: インド系言語（MIT）🔲 計画中

## 最近の更新

### 2025-01-28
- Phase 3の方針を大幅変更
  - GPL v3ライセンス問題によりeSpeak-NG統合を中止
  - 商用利用可能なライブラリのみを使用する方針に転換
- 基本的な多言語実装（5言語）完了
- ライセンス管理システム構築
- コンパイルエラーをすべて解決

- Phase 3（多言語音素化）完全完了
  - 日本語・英語の2言語に特化した実装
  - CircuitBreakerパターンによるエラー耐性
  - 全コンパイルエラー解決
- プロジェクトサイズ削減（約450-500MB）
  - 不要なパッケージの削除
  - 2D機能、URP、物理エンジン等を除外

### 2025-01-23
- Phase 2（Android対応）完全完了
- Phase 1（Core実装）完全完了

## 次のマイルストーン

1. **eSpeak-NGオプショナルプラグイン** - 英語精度を85-90%に向上（GPL v3、別パッケージ）
2. **OpenPhonemizer統合** - 5言語以上を一度にサポート（BSD-3-Clause Clear）
3. **追加言語実装** - 中国語、韓国語、スペイン語等の対応

## コントリビューション

プロジェクトへの貢献を歓迎します。詳細は[CONTRIBUTING.md](CONTRIBUTING.md)を参照してください。

## ライセンス

uPiperは商用利用可能なライセンスで提供されます。サードパーティライブラリのライセンスについては[THIRD-PARTY-NOTICES.txt](../Assets/uPiper/Licenses/THIRD-PARTY-NOTICES.txt)を参照してください。