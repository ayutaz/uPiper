# 多言語対応 技術調査レポート - 概要

## 調査背景

piper-plus `feat/bilingual-phonemizer` ブランチ（後に `feat/multilingual-phonemizer` へ発展）で6言語マルチリンガル対応が完了。
Unity側（uPiper）への反映に必要な技術調査を実施。

## piper-plus 側の変更サマリ

### 対応言語（6言語）

| 言語ID | 言語 | コード | Prosody | 音素化方式 |
|--------|------|--------|---------|-----------|
| 0 | 日本語 | ja | Yes | dot-net-g2p (MeCab辞書) |
| 1 | 英語 | en | No | g2p-en (ARPAbet→IPA) |
| 2 | 中国語 | zh | Yes (tone) | pypinyin (ピンイン→IPA) |
| 3 | スペイン語 | es | No | ルールベースG2P |
| 4 | フランス語 | fr | No | ルールベースG2P |
| 5 | ポルトガル語 | pt | No | ルールベースG2P |

※韓国語(ko)もPhonemizer実装あり（ko含む場合は183音素）。モデル学習は6言語(ja,en,zh,es,fr,pt)で実施。

### 主要な変更点

1. **ONNXモデル入力**: `lid` (language_id) テンソル追加
2. **統一音素ID空間**: 173音素（6言語統合、ko含む場合183音素）
3. **Unicode言語判定**: `UnicodeLanguageDetector` による自動検出
4. **Prosodyマスキング**: 日本語は完全Prosody、中国語はtone情報、その他言語はモデル内でゼロ化
5. **学習済みモデル**: `tsukuyomi-6lang-v2-fixed.onnx` (6言語対応)

### ONNXモデル入出力シグネチャ

```
入力:
  input            (int64, [1, max_phoneme_length])  - 音素ID配列
  input_lengths    (int64, [1])                      - 入力長
  scales           (float32, [3])                    - noise_scale, length_scale, noise_w
  sid              (int64, [1])                      - Speaker ID (多言語時は常に含む)
  lid              (int64, [1])                      - Language ID (多言語モデルのみ)
  prosody_features (int64, [1, max_phoneme_length, 3]) - A1/A2/A3 (オプション)

出力:
  output           (float32, [1, 1, audio_length])   - 音声波形 (22050Hz)
  durations        (float32, [1, max_phoneme_length]) - 音素ごとの推定持続時間
```

## uPiper 側で必要な変更一覧

| # | コンポーネント | 変更内容 | 詳細ドキュメント |
|---|--------------|---------|----------------|
| 1 | InferenceAudioGenerator | `lid`/`sid` テンソル入力対応 | [12_onnx_inference.md](12_onnx_inference.md) |
| 2 | MultilingualPhonemizer | C#移植（言語セグメンテーション+委譲） | [13_multilingual_phonemizer.md](13_multilingual_phonemizer.md) |
| 3 | LanguageDetector | Unicode範囲拡張（6言語対応） | [14_language_detector.md](14_language_detector.md) |
| 4 | PhonemeEncoder | 多言語音素IDマップ+IPA/PUA判定拡張 | [15_phoneme_id_map.md](15_phoneme_id_map.md) |
| 5 | PiperTTS | 初期化・生成フロー改修 | [16_pipertts_flow.md](16_pipertts_flow.md) |
| 6 | PhonemeEncoder | 言語別マッピング戦略 | [17_phoneme_encoder.md](17_phoneme_encoder.md) |
| 7 | 新言語Phonemizer | 各言語のC#実装戦略 | [18_new_lang_phonemizers.md](18_new_lang_phonemizers.md) |
| 8 | プラットフォーム | WebGL/IL2CPP/モバイル影響 | [19_platform_impact.md](19_platform_impact.md) |
| 9 | テスト | テスト戦略・追加テスト一覧 | [20_test_strategy.md](20_test_strategy.md) |
| 10 | API設計 | 公開API拡張・後方互換性 | [21_api_design.md](21_api_design.md) |

## 実装ロードマップ（推奨）

### Phase 1: 基盤（ONNX推論層）
- InferenceAudioGenerator に `lid`/`sid` 入力対応
- `SupportsMultilingual` / `SupportsMultiSpeaker` 判定追加
- PhonemeEncoder の多言語音素IDマップ対応

### Phase 2: 言語検出・セグメンテーション
- UnicodeLanguageDetector の C# 実装（6言語対応）
- 既存 LanguageDetector のリファクタリング
- CJK曖昧性解決（日本語/中国語判定）

### Phase 3: 多言語Phonemizer統合
- MultilingualPhonemizer C# 移植
- 既存 MixedLanguagePhonemizer との統合
- BOS/EOS/パディング処理

### Phase 4: PiperTTS API拡張
- `GenerateAudioAsync(text, language)` オーバーロード追加
- PiperConfig 言語設定拡張
- 後方互換性保証

### Phase 5: 新言語Phonemizer実装
- スペイン語（直接ポート、最も実装容易）
- フランス語（ルールベース直接ポート）
- ポルトガル語（ルールベース直接ポート）
- 中国語（ルックアップテーブル+トーン規則）
- 韓国語（Hangul分解+簡略音韻規則）

### Phase 6: テスト・品質保証
- 多言語テストスイート作成
- CI/CDパイプライン拡張
- クロスプラットフォーム検証

## アーキテクチャ概念図

```
テキスト入力 ("今日はgoodですね")
    |
    v
UnicodeLanguageDetector (言語判定)
    |
    v
MultilingualPhonemizer (セグメント分割)
    |
    +---> [ja] "今日は"     → DotNetG2PPhonemizer → 音素 + Prosody
    +---> [en] "good"       → FliteLTSPhonemizer → 音素
    +---> [ja] "ですね"     → DotNetG2PPhonemizer → 音素 + Prosody
    |
    v
統一音素ID空間にマージ (post_process_ids)
    |
    v
PhonemeEncoder (IPA/PUA判定 → ID配列)
    |
    v
InferenceAudioGenerator
    +---> input: 音素ID配列
    +---> lid: 言語ID配列 (セグメントごと)
    +---> prosody_features: A1/A2/A3 (日本語セグメントのみ有効)
    |
    v
ONNX推論 (VITS + 言語conditioning)
    |
    v
AudioClip (22050Hz, float32)
```
