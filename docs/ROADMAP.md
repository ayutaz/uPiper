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

### 🚧 Phase 3: Multi-language Support (進行中)

**現在の進捗**: 基盤実装完了、商用ライセンス方針確立

#### Phase 3.1: 基盤構築 (完了)
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

#### Phase 3.2: 商用ライブラリ統合 (計画中)
- 🔲 OpenPhonemizer統合（BSD-3-Clause Clear）
  - スペイン語、ポルトガル語、ドイツ語、フランス語、イタリア語
- 🔲 Flite統合（MIT-CMU）- 英語強化
- 🔲 pypinyin完全移植（MIT）- 中国語
- 🔲 IndicXlit統合（MIT）- ヒンディー語

#### Phase 3.3: 追加言語実装 (計画中)
- 🔲 ベンガル語（bnbphoneticparser - MIT）
- 🔲 ロシア語（調査中）
- 🔲 韓国語改善（g2pK - ライセンス確認中）

### 📅 Phase 4: iOS Support (計画中)
- Xcodeプロジェクト設定
- Objective-C++ブリッジ
- App Store要件対応

### 📅 Phase 5: WebGL Support (計画中)
- Emscriptenビルド
- WebAssembly最適化
- ブラウザ互換性対応

### 📅 Phase 6: Advanced Features (計画中)
- 音声品質向上（高品質モデル）
- 感情表現対応
- 話者切り替え機能
- リアルタイムストリーミング

### 📅 Phase 7: Editor Tools (計画中)
- カスタムインスペクター
- 音声プレビュー機能
- デバッグツール
- VoiceBankマネージャー

### 📅 Phase 8: Optimization & QA (計画中)
- パフォーマンス最適化
- メモリ使用量削減
- 自動テスト拡充
- ドキュメント完成

## Timeline

| Phase | 期間 | 状態 | 進捗 |
|-------|------|------|------|
| Phase 1 | 2023 Q4 | ✅ 完了 | 100% |
| Phase 2 | 2024 Q1 | ✅ 完了 | 100% |
| Phase 3 | 2024 Q1-Q2 | 🚧 進行中 | 30% |
| Phase 4 | 2024 Q2 | 📅 計画中 | 0% |
| Phase 5 | 2024 Q3 | 📅 計画中 | 0% |
| Phase 6 | 2024 Q3-Q4 | 📅 計画中 | 0% |
| Phase 7 | 2024 Q4 | 📅 計画中 | 0% |
| Phase 8 | 2025 Q1 | 📅 計画中 | 0% |

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

### 2024-01-28
- Phase 3の方針を大幅変更
  - GPL v3ライセンス問題によりeSpeak-NG統合を中止
  - 商用利用可能なライブラリのみを使用する方針に転換
- 基本的な多言語実装（5言語）完了
- ライセンス管理システム構築
- コンパイルエラーをすべて解決

### 2024-01-23
- Phase 2（Android対応）完全完了
- Phase 1（Core実装）完全完了

## 次のマイルストーン

1. **OpenPhonemizer統合** - 5言語以上を一度にサポート
2. **Flite統合** - 英語音素化の品質向上
3. **pypinyin完全移植** - 中国語の本格対応

## コントリビューション

プロジェクトへの貢献を歓迎します。詳細は[CONTRIBUTING.md](CONTRIBUTING.md)を参照してください。

## ライセンス

uPiperは商用利用可能なライセンスで提供されます。サードパーティライブラリのライセンスについては[THIRD-PARTY-NOTICES.txt](../Assets/uPiper/Licenses/THIRD-PARTY-NOTICES.txt)を参照してください。