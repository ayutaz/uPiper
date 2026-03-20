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
| 1 | InferenceAudioGenerator | `lid`/`sid` テンソル入力対応 | (実装済み、コード参照) |
| 2 | MultilingualPhonemizer | C#移植（言語セグメンテーション+委譲） | (実装済み、コード参照) |
| 3 | LanguageDetector | Unicode範囲拡張（6言語対応） | (実装済み、コード参照) |
| 4 | PhonemeEncoder | 多言語音素IDマップ+IPA/PUA判定拡張 | [17_phoneme_encoder.md](17_phoneme_encoder.md) |
| 5 | PiperTTS | 初期化・生成フロー改修 | [21_api_design.md](21_api_design.md) |
| 6 | PhonemeEncoder | 言語別マッピング戦略 | [17_phoneme_encoder.md](17_phoneme_encoder.md) |
| 7 | 新言語Phonemizer | 各言語のC#実装戦略 **（Phase 5完了: es/fr/pt/zh/ko 5言語実装済み）** | [18_new_lang_phonemizers.md](18_new_lang_phonemizers.md) |
| 8 | プラットフォーム | WebGL/IL2CPP/モバイル影響 | [19_platform_impact.md](19_platform_impact.md) |
| 9 | テスト | テスト戦略・追加テスト一覧 | [20_test_strategy.md](20_test_strategy.md) |
| 10 | API設計 | 公開API拡張・後方互換性 | [21_api_design.md](21_api_design.md) |

## 実装ロードマップ（推奨）

### Phase 1: 基盤（ONNX推論層） — **完了**
- InferenceAudioGenerator に `lid`/`sid` 入力対応
- `SupportsMultilingual` / `SupportsMultiSpeaker` 判定追加
- PhonemeEncoder の多言語音素IDマップ対応

### Phase 2: 言語検出・セグメンテーション — **完了**
- UnicodeLanguageDetector の C# 実装（6言語対応）
- 既存 LanguageDetector のリファクタリング
- CJK曖昧性解決（日本語/中国語判定）

### Phase 3: 多言語Phonemizer統合 — **完了**
- MultilingualPhonemizer C# 移植
- 既存 MixedLanguagePhonemizer との統合
- BOS/EOS/パディング処理

### Phase 4: PiperTTS API拡張 — **完了**
- `GenerateAudioAsync(text, language)` オーバーロード追加
- PiperConfig 言語設定拡張
- 後方互換性保証

### Phase 5: 新言語Phonemizer実装 — **完了**
- スペイン語: `SpanishPhonemizerBackend` (ルールベースG2P, ~1233行)
- フランス語: `FrenchPhonemizerBackend` (ルールベースG2P, ~1350行)
- ポルトガル語: `PortuguesePhonemizerBackend` (BR-PT, ~1190行)
- 中国語: `ChinesePhonemizerBackend` + `PinyinData` (~1716行)
- 韓国語: `KoreanPhonemizerBackend` (Hangul分解+音韻規則, ~964行)
- `PuaTokenMapper`: 87エントリ固定PUAマッピング（JA/ZH/KO/ES/PT/FR全言語対応）
- `LanguageConstants`: 言語ID/コード定数（7言語: ja, en, zh, es, fr, pt, ko）
- `MultilingualPhonemizer`: 7言語バックエンド委譲対応に更新
- `UnicodeLanguageDetector`: Latin Extended (U+0100-024F)、CJK句読点判定を拡張
- テスト: 207テスト追加（Spanish 30, French 36, Portuguese 39, Chinese 30, Korean 43, Phase5統合 29）

### Phase 6: 多言語モデル互換性修正 — **完了**
- PhonemeEncoder: `NeedsInterspersePadding()` メソッド追加（`IsESpeakModel()` を置き換え）
  - `phoneme_type: "multilingual"` モデルに intersperse PAD を正しく適用
  - `[BOS, PAD, ph1, PAD, ph2, PAD, ..., EOS]` 形式のID配列生成
- PhonemeEncoder: `MapPhoneme()` 多言語パススルー対応
  - 多言語モデルの音素はIPA/PUA変換をバイパスし、phoneme_id_map で直接検索
- PhonemeEncoder: N variant 保持（`puaToPhonemeMapMultilingual`）
  - 多言語モデルでは N_m / N_n / N_ng / N_uvular を個別IDとして保持
- PhonemeEncoder: skip-after-pad ロジック（PAD ID の二重挿入防止）
- MultilingualPhonemizer: EosLikeTokens に PUA トークン追加（`\ue016`, `\ue017`, `\ue018`）
- MultilingualPhonemizer: 日本語セグメントの先頭PAD除去（"sil" 変換由来）
- PiperTTS: 多言語モデル自動昇格（auto-promotion）ロジック
  - `LanguageIdMap.Count > 1` のモデルで自動的に MultilingualPhonemizer を初期化
  - `GenerateAudioAsync()` が `SupportsLanguageId` 検出時に多言語パスへ自動ルーティング

### Phase 7: テスト・品質保証 — TODO
- 多言語テストスイート作成
- CI/CDパイプライン拡張
- クロスプラットフォーム検証

## アーキテクチャ概念図

```
テキスト入力 ("今日はgoodですね")
    |
    v
PiperTTS.GenerateAudioAsync()
    |  SupportsLanguageId検出 → 自動的に多言語パスへルーティング (auto-promotion)
    |  LanguageIdMap.Count > 1 → MultilingualPhonemizer 自動初期化
    v
UnicodeLanguageDetector (言語判定: 7言語対応)
    |  Hangul → ko, Kana → ja, CJK漢字 → ja/zh, Latin → en/es/fr/pt
    |  Latin Extended (U+0100-024F) 対応、CJK句読点コンテキスト判定
    v
MultilingualPhonemizer (セグメント分割 → 言語別バックエンド委譲)
    |  日本語セグメント: 先頭PAD除去 ("sil"変換由来の"_"を除去)
    |  中間セグメント: EOS-likeトークン除去 (PUA形式含む)
    |
    +---> [ja] "今日は"     → DotNetG2PPhonemizer      → 音素 + Prosody
    +---> [en] "good"       → FliteLTSPhonemizerBackend → 音素
    +---> [es] "hola"       → SpanishPhonemizerBackend  → 音素 (ルールベースG2P)
    +---> [fr] "bonjour"    → FrenchPhonemizerBackend   → 音素 (ルールベースG2P)
    +---> [pt] "obrigado"   → PortuguesePhonemizerBackend → 音素 (BR-PT G2P)
    +---> [zh] "你好"       → ChinesePhonemizerBackend  → 音素 + Tone (PinyinData)
    +---> [ko] "안녕하세요" → KoreanPhonemizerBackend   → 音素 (Hangul分解+音韻規則)
    +---> [ja] "ですね"     → DotNetG2PPhonemizer      → 音素 + Prosody
    |
    v
PuaTokenMapper (87エントリ固定PUAマッピング: 多文字音素→単一PUA文字)
    |
    v
統一音素ID空間にマージ (post_process_ids)
    |
    v
PhonemeEncoder (モデルタイプ別処理)
    ├─ PUAモデル: PUA文字→phoneme_id_map で検索
    ├─ IPAモデル: PUA→元音素→IPA変換→phoneme_id_map で検索
    └─ 多言語モデル (phoneme_type: "multilingual"):
       パススルー→phoneme_id_map で直接検索
       intersperse PAD: [BOS, PAD, ph1, PAD, ph2, PAD, ..., EOS]
       N variant 保持 (N_m/N_n/N_ng/N_uvular → 個別ID)
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
