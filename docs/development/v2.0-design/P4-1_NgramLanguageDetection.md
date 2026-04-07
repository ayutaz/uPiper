# P4-1: N-gram 言語検出 設計ドキュメント

**作成日**: 2026-04-08
**対象チケット**: v2.0-plan.md P4-1
**ステータス**: 設計レビュー待ち
**依存**: P1-3 (Dictionary Registry 化), P1-4 (ILanguageG2PHandler)

---

## 1. 現状分析

### 1.1 UnicodeLanguageDetector の構成

**ファイル**: `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/UnicodeLanguageDetector.cs`（287行）

Unicode 文字範囲チェックによる言語検出。文字ごとに言語を判定し、連続する同一言語の文字をセグメントとして出力する。

#### 検出優先順位

| 優先度 | 判定対象 | 検出結果 | 精度 |
|--------|---------|---------|------|
| 1 | かな (Hiragana/Katakana) | "ja" | 確実 |
| 2 | ハングル (Hangul) | "ko" | 確実 |
| 3 | CJK 漢字 | "ja"/"zh" (Kana コンテキストで曖昧性解消) | 高 |
| 4 | 全角ラテン文字 | `defaultLatinLanguage` | **フォールバック** |
| 5 | CJK 句読点 | "ja"/"zh" (Kana コンテキスト) | 高 |
| 6 | ラテン文字 (Basic + Extended) | `defaultLatinLanguage` | **フォールバック** |
| 7 | ニュートラル (空白/数字/句読点) | null (前セグメントに吸収) | N/A |

#### 主要メソッド

- **`DetectChar(char ch, bool contextHasKana)`**: 1文字の言語判定。`[AggressiveInlining]` 付き static ヘルパー群を使用。
- **`SegmentText(string text)`**: テキスト全体を走査し、`(language, text)` タプルのリストを返す。ニュートラル文字は前セグメントに吸収。テキスト先頭のニュートラル文字は最初の言語セグメントに含まれる。
- **`HasKana(string text)`**: CJK 曖昧性解消のための事前スキャン。

### 1.2 ラテン文字検出の限界

現在の `IsLatin()` は以下の Unicode 範囲をカバーする:

```
A-Z (U+0041-005A), a-z (U+0061-007A)
Latin-1 Supplement: U+00C0-00D6, U+00D8-00F6, U+00F8-00FF
Latin Extended-A: U+0100-017F
Latin Extended-B: U+0180-024F
```

全てのラテン文字が `defaultLatinLanguage`（デフォルト: "en"）に一律マッピングされるため、以下の区別が不可能:

| 入力テキスト | 正解 | 現在の出力 | 問題 |
|------------|------|-----------|------|
| "Hello world" | en | en (default) | 正しいが偶然 |
| "Bonjour le monde" | fr | en | 誤判定 |
| "Hola mundo" | es | en | 誤判定 |
| "Bom dia mundo" | pt | en | 誤判定 |

一部のアクセント付き文字は言語ヒントになり得るが（例: ñ はスペイン語、ç はフランス語/ポルトガル語）、現在は全て `defaultLatinLanguage` に帰属するため活用されていない。

### 1.3 piper-plus 側の現状

piper-plus の各言語実装（Python, C++, JS, C#, Rust, Go）は全て **Unicode 範囲ベースの検出のみ** を採用しており、N-gram / trigram ベースの言語検出は実装されていない。

ただし、piper-plus v1.10.0 以降で **スウェーデン語検出** のためにワードレベル後処理（`refineLatinSegmentsForSwedish`）が導入されている:

- **C++** (`language_detector.cpp`): `SWEDISH_FUNCTION_WORDS`（45語）+ `isSwedishChar`（a/o/a ダイアクリティクス）による後処理
- **C#** (`UnicodeLanguageDetector.cs`): 同一ロジックの C# 移植
- **uPiper**: 未移植（スウェーデン語はモデル未対応のため）

このスウェーデン語検出の設計（ワードレベル特徴語チェック + 特殊文字検出 → セグメント再分類）は、本 P4-1 の trigram 検出と同じ目的（ラテン文字間言語区別）を異なる手法で実現しており、統合設計の参考になる。

### 1.4 MultilingualPhonemizer での使用パターン

**ファイル**: `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/MultilingualPhonemizer.cs`（708行）

```csharp
// コンストラクタ内
_detector = new UnicodeLanguageDetector(options.Languages, _defaultLatinLanguage);

// PhonemizeWithProsodyAsync 内
var segments = _detector.SegmentText(text);
```

`MultilingualPhonemizer` は `UnicodeLanguageDetector` を直接インスタンス化して保持。`SegmentText()` の戻り値を言語別 switch 文でルーティングする。検出器の差し替え機構は存在しない。

### 1.5 LanguageConstants の定義

**ファイル**: `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/LanguageConstants.cs`

```csharp
public static readonly string[] LatinLanguages = { "en", "es", "fr", "pt" };
```

uPiper が認識するラテン文字言語は **en, es, fr, pt** の4言語。スウェーデン語（sv）はモデル未対応のため `LatinLanguages` に含まれていない。

---

## 2. ハイブリッド設計方針

### 2.1 基本原則

| 文字種別 | 検出手法 | 理由 |
|---------|---------|------|
| CJK/かな/ハングル | Unicode 範囲 (既存) | 文字範囲で一意に決まる。高速。 |
| ラテン文字 | Trigram 頻度分析 | Unicode 範囲では区別不可。統計的手法が必要。 |

CJK 検出は変更しない。ラテン文字セグメントに対してのみ trigram 検出を適用し、`defaultLatinLanguage` フォールバックの精度を向上させる。

### 2.2 処理フロー

```
入力テキスト
    |
    v
[1] UnicodeLanguageDetector.SegmentText()  -- 既存ロジック（変更なし）
    | (language, text) タプルリスト
    v
[2] LatinSegmentRefiner.Refine()  -- 新規：ラテンセグメント再分類
    | ラテン文字セグメントのみ処理対象
    |   (a) セグメントが短すぎる (< minChars) → フォールバック維持
    |   (b) TrigramLanguageDetector.Detect(segmentText)
    |       - trigram プロファイル照合
    |       - 信頼度スコア算出
    |   (c) スコア > 閾値 → 言語再分類
    |   (d) スコア <= 閾値 → defaultLatinLanguage 維持
    v
[3] 再分類済みセグメントリスト
    |
    v
MultilingualPhonemizer.PhonemizeWithProsodyAsync()  -- 既存ルーティング
```

### 2.3 piper-plus スウェーデン語検出との統合

piper-plus の `refineLatinSegmentsForSwedish` は「特徴語 + 特殊文字 → セグメント再分類」パターン。本設計の `LatinSegmentRefiner` は同じ後処理パターンを trigram ベースに一般化する。将来 uPiper にスウェーデン語サポートが追加された場合、`TrigramLanguageDetector` に sv のプロファイルを追加するだけで対応可能。

---

## 3. TrigramLanguageDetector クラス設計

### 3.1 クラス図

```
ILanguageDetector (新規インターフェース)
  |-- UnicodeLanguageDetector (既存、ILanguageDetector を実装)
  |-- HybridLanguageDetector (新規、Unicode + Trigram を統合)

TrigramLanguageDetector (新規、内部利用)
  |-- TrigramProfile (言語別 trigram 頻度テーブル)

LatinSegmentRefiner (新規、後処理)
```

### 3.2 ILanguageDetector インターフェース

```csharp
namespace uPiper.Core.Phonemizers.Multilingual
{
    /// <summary>
    /// Language detection interface for multilingual TTS.
    /// Extracted from UnicodeLanguageDetector to enable DI and strategy swapping.
    /// </summary>
    public interface ILanguageDetector
    {
        /// <summary>Default language for Latin-script characters.</summary>
        string DefaultLatinLanguage { get; }

        /// <summary>Supported language codes.</summary>
        IReadOnlyList<string> Languages { get; }

        /// <summary>
        /// Segments text into language-specific chunks.
        /// </summary>
        /// <param name="text">Input text (may contain mixed languages).</param>
        /// <returns>List of (languageCode, segmentText) tuples.</returns>
        List<(string language, string text)> SegmentText(string text);
    }
}
```

### 3.3 TrigramLanguageDetector

```csharp
namespace uPiper.Core.Phonemizers.Multilingual
{
    /// <summary>
    /// Trigram frequency-based language detector for Latin-script languages.
    /// Computes normalized cosine distance between input text and pre-computed
    /// language profiles.
    /// </summary>
    internal sealed class TrigramLanguageDetector
    {
        /// <summary>Minimum character count for reliable detection.</summary>
        public const int MinCharsForDetection = 15;

        /// <summary>Default confidence threshold (0.0-1.0).</summary>
        public const float DefaultConfidenceThreshold = 0.65f;

        private readonly Dictionary<string, TrigramProfile> _profiles;
        private readonly float _confidenceThreshold;

        /// <summary>
        /// Creates a detector with the given language profiles.
        /// </summary>
        /// <param name="profiles">Language code -> profile mapping.</param>
        /// <param name="confidenceThreshold">
        /// Minimum score to accept detection result (default: 0.65).
        /// </param>
        public TrigramLanguageDetector(
            Dictionary<string, TrigramProfile> profiles,
            float confidenceThreshold = DefaultConfidenceThreshold)
        {
            _profiles = profiles;
            _confidenceThreshold = confidenceThreshold;
        }

        /// <summary>
        /// Detects the language of Latin-script text.
        /// </summary>
        /// <param name="text">Latin-script text segment.</param>
        /// <returns>
        /// (languageCode, confidence) tuple. languageCode is null
        /// if no profile exceeds the confidence threshold.
        /// </returns>
        public (string languageCode, float confidence) Detect(string text);

        /// <summary>
        /// Extracts trigrams from normalized text.
        /// </summary>
        internal static Dictionary<string, int> ExtractTrigrams(string text);
    }
}
```

### 3.4 TrigramProfile

```csharp
namespace uPiper.Core.Phonemizers.Multilingual
{
    /// <summary>
    /// Pre-computed trigram frequency profile for a single language.
    /// Loaded from StreamingAssets/uPiper/LanguageProfiles/ at runtime.
    /// </summary>
    internal sealed class TrigramProfile
    {
        /// <summary>Language code (e.g., "en", "fr").</summary>
        public string LanguageCode { get; }

        /// <summary>
        /// Top-N trigrams with normalized frequencies.
        /// Key: lowercase trigram (3 chars), Value: frequency rank (0 = most frequent).
        /// </summary>
        public IReadOnlyDictionary<string, int> RankedTrigrams { get; }

        /// <summary>Total number of trigrams in the profile.</summary>
        public int Count { get; }

        /// <summary>
        /// Computes the similarity score (0.0-1.0) between this profile
        /// and the given trigram frequency map.
        /// Uses out-of-place rank distance metric.
        /// </summary>
        public float ComputeSimilarity(Dictionary<string, int> inputTrigrams);
    }
}
```

### 3.5 HybridLanguageDetector

```csharp
namespace uPiper.Core.Phonemizers.Multilingual
{
    /// <summary>
    /// Hybrid language detector: Unicode ranges for CJK, trigram for Latin.
    /// Wraps UnicodeLanguageDetector and TrigramLanguageDetector.
    /// </summary>
    public sealed class HybridLanguageDetector : ILanguageDetector
    {
        private readonly UnicodeLanguageDetector _unicodeDetector;
        private readonly TrigramLanguageDetector _trigramDetector;
        private readonly LatinSegmentRefiner _refiner;

        public string DefaultLatinLanguage => _unicodeDetector.DefaultLatinLanguage;
        public IReadOnlyList<string> Languages => _unicodeDetector.Languages;

        public HybridLanguageDetector(
            IReadOnlyList<string> languages,
            string defaultLatinLanguage = "en",
            TrigramLanguageDetector trigramDetector = null)
        {
            _unicodeDetector = new UnicodeLanguageDetector(languages, defaultLatinLanguage);
            _trigramDetector = trigramDetector;
            _refiner = trigramDetector != null
                ? new LatinSegmentRefiner(trigramDetector, defaultLatinLanguage)
                : null;
        }

        public List<(string language, string text)> SegmentText(string text)
        {
            // Step 1: Unicode-based segmentation (unchanged)
            var segments = _unicodeDetector.SegmentText(text);

            // Step 2: Refine Latin segments with trigram detection
            if (_refiner != null)
            {
                segments = _refiner.Refine(segments);
            }

            return segments;
        }
    }
}
```

### 3.6 LatinSegmentRefiner

```csharp
namespace uPiper.Core.Phonemizers.Multilingual
{
    /// <summary>
    /// Post-processor that re-classifies Latin-script segments using trigram detection.
    /// Follows the same pattern as piper-plus refineLatinSegmentsForSwedish,
    /// but generalized to all Latin languages.
    /// </summary>
    internal sealed class LatinSegmentRefiner
    {
        private readonly TrigramLanguageDetector _detector;
        private readonly string _defaultLatinLanguage;

        public LatinSegmentRefiner(
            TrigramLanguageDetector detector,
            string defaultLatinLanguage)
        {
            _detector = detector;
            _defaultLatinLanguage = defaultLatinLanguage;
        }

        /// <summary>
        /// Re-classifies Latin segments in the given segment list.
        /// Non-Latin segments (ja, zh, ko) pass through unchanged.
        /// </summary>
        public List<(string language, string text)> Refine(
            List<(string language, string text)> segments)
        {
            var result = new List<(string, string)>(segments.Count);

            foreach (var (lang, segText) in segments)
            {
                // Only process segments assigned to defaultLatinLanguage
                if (lang != _defaultLatinLanguage)
                {
                    result.Add((lang, segText));
                    continue;
                }

                // Short segments: keep default (trigram unreliable)
                if (segText.Length < TrigramLanguageDetector.MinCharsForDetection)
                {
                    result.Add((lang, segText));
                    continue;
                }

                // Detect via trigram
                var (detected, confidence) = _detector.Detect(segText);
                result.Add(detected != null ? (detected, segText) : (lang, segText));
            }

            return result;
        }
    }
}
```

---

## 4. Trigram プロファイル設計

### 4.1 Trigram の定義

Trigram とは連続する3文字のシーケンスである。テキスト正規化後に抽出する。

**正規化ルール**:
1. 小文字化（`ToLowerInvariant()`）
2. アクセント記号除去（NFD 分解後に Combining Mark カテゴリを除去）
3. 非アルファベット文字をスペースに置換
4. 連続スペースを1つに圧縮
5. 先頭・末尾にスペースを追加（語境界 trigram の生成）

**例**: "Hello World" -> " hello world " -> trigram: " he", "hel", "ell", "llo", "lo ", "o w", " wo", "wor", "orl", "rld", "ld "

### 4.2 各言語の特徴的 Trigram

以下は各言語の上位 trigram（コーパス研究に基づく代表例）。プロファイル生成時に実コーパスから抽出する。

#### 英語 (en)

| 順位 | Trigram | 説明 |
|------|---------|------|
| 1 | " th" | the, that, this, they |
| 2 | "the" | 定冠詞 |
| 3 | "he " | he, the の末尾 |
| 4 | "ing" | 現在分詞 |
| 5 | "and" | 接続詞 |
| 6 | "ion" | -tion/-sion 接尾辞 |
| 7 | " an" | and, an, any |
| 8 | "tio" | -tion |
| 9 | "ent" | -ment, -ent |
| 10 | " in" | in, into |

固有の排他的 trigram: " th", "the", "ing", "ght", "ous", "ble"

#### スペイン語 (es)

| 順位 | Trigram | 説明 |
|------|---------|------|
| 1 | " de" | 前置詞 de |
| 2 | "de " | de + スペース |
| 3 | " la" | 冠詞 la |
| 4 | "la " | la + スペース |
| 5 | "que" | 接続詞 que |
| 6 | " el" | 冠詞 el |
| 7 | "cion" | -cion 接尾辞 |
| 8 | "ent" | -mente, -ento |
| 9 | " en" | 前置詞 en |
| 10 | "os " | 複数形語尾 |

固有の排他的 trigram: " el", "ue ", "cion", " qu"

#### フランス語 (fr)

| 順位 | Trigram | 説明 |
|------|---------|------|
| 1 | " de" | 前置詞 de |
| 2 | "les" | 冠詞 les |
| 3 | " le" | 冠詞 le |
| 4 | "ent" | 動詞活用語尾 |
| 5 | " la" | 冠詞 la |
| 6 | "de " | de + スペース |
| 7 | " qu" | que, qui |
| 8 | "ion" | -tion/-sion |
| 9 | "des" | 冠詞 des |
| 10 | " pa" | par, pas |

固有の排他的 trigram: "les", "des", " qu", "eux", "ais"

#### ポルトガル語 (pt)

| 順位 | Trigram | 説明 |
|------|---------|------|
| 1 | " de" | 前置詞 de |
| 2 | "de " | de + スペース |
| 3 | " qu" | que |
| 4 | "que" | 接続詞 |
| 5 | "ent" | -mente, -ento |
| 6 | " do" | do (de+o) |
| 7 | " da" | da (de+a) |
| 8 | "os " | 複数形語尾 |
| 9 | " co" | com, como |
| 10 | "aca" | -acao 接尾辞 |

固有の排他的 trigram: " do", " da", "nh ", "lh ", "ao "

### 4.3 言語間の類似性と弁別力

ロマンス言語（es, fr, pt）は共通のラテン語起源により trigram の重複が大きい。以下に重複状況を示す。

| Trigram | en | es | fr | pt | 弁別力 |
|---------|----|----|----|----|--------|
| " de" | - | 高 | 高 | 高 | 低（3言語共通） |
| " th" | 高 | - | - | - | **高**（en 固有） |
| "the" | 高 | - | - | - | **高**（en 固有） |
| "ing" | 高 | - | - | - | **高**（en 固有） |
| " el" | - | 高 | - | - | **高**（es 固有） |
| "les" | - | - | 高 | - | **高**（fr 固有） |
| " do" | - | - | - | 高 | **高**（pt 固有） |
| " da" | - | - | - | 高 | **高**（pt 固有） |
| "que" | - | 高 | 高 | 高 | 低（3言語共通） |
| "ent" | 中 | 中 | 高 | 中 | 低（全言語共通） |

英語は他の3言語と明確に区別可能。es/fr/pt 間の区別が最大の課題となる。

### 4.4 プロファイルサイズの選択

| プロファイルサイズ (N) | 精度 (推定) | メモリ (4言語) | ロード時間 |
|----------------------|------------|---------------|-----------|
| 100 trigrams/言語 | ~85% | ~3.2 KB | < 1ms |
| 200 trigrams/言語 | ~90% | ~6.4 KB | < 1ms |
| 300 trigrams/言語 | ~93% | ~9.6 KB | < 1ms |
| 500 trigrams/言語 | ~95% | ~16 KB | < 2ms |

**推奨**: **300 trigrams/言語** (4言語合計 1,200 エントリ)。精度と軽量性のバランスが良い。TTS 用途では完璧な精度より低レイテンシが重要。

### 4.5 データフォーマット

```json
{
  "version": "1.0",
  "languages": {
    "en": {
      "trigrams": [" th", "the", "he ", "ing", "and", "..."],
      "trigramCount": 300
    },
    "es": {
      "trigrams": [" de", "de ", " la", "la ", "que", "..."],
      "trigramCount": 300
    },
    "fr": {
      "trigrams": [" de", "les", " le", "ent", " la", "..."],
      "trigramCount": 300
    },
    "pt": {
      "trigrams": [" de", "de ", " qu", "que", "ent", "..."],
      "trigramCount": 300
    }
  }
}
```

配列内の順序が rank。インデックス 0 が最頻出 trigram。

**ファイルパス**: `StreamingAssets/uPiper/LanguageProfiles/trigram_profiles.json`

---

## 5. スコアリングアルゴリズム

### 5.1 Out-of-Place 距離法

各言語プロファイルとの類似度を計算するため、Cavnar & Trenkle (1994) の out-of-place 距離メトリックを採用する。

**アルゴリズム**:
1. 入力テキストの trigram を抽出し、頻度順にランク付け
2. 各言語プロファイルについて:
   - 入力 trigram の各ランクとプロファイル内ランクの差の絶対値を合計
   - プロファイルに存在しない trigram にはペナルティ値（= プロファイルサイズ N）を付与
3. 距離を正規化してスコア（0.0-1.0）に変換: `score = 1.0 - (distance / maxDistance)`

### 5.2 擬似コード

```csharp
public float ComputeSimilarity(Dictionary<string, int> inputTrigrams)
{
    // inputTrigrams: key=trigram, value=rank (0=most frequent)
    int totalDistance = 0;
    int maxDistance = inputTrigrams.Count * Count; // worst case

    foreach (var (trigram, inputRank) in inputTrigrams)
    {
        if (RankedTrigrams.TryGetValue(trigram, out int profileRank))
        {
            totalDistance += Math.Abs(inputRank - profileRank);
        }
        else
        {
            totalDistance += Count; // penalty for missing trigram
        }
    }

    return maxDistance > 0 ? 1.0f - ((float)totalDistance / maxDistance) : 0f;
}
```

### 5.3 信頼度閾値

| 閾値 | 動作 |
|-----|------|
| >= 0.65 | 検出結果を採用。最高スコアの言語に再分類 |
| < 0.65 | 検出不確実。`defaultLatinLanguage` を維持 |

追加条件: 最高スコアと2位スコアの差が 0.05 未満の場合もフォールバック（曖昧な判定を避ける）。

```csharp
var sorted = scores.OrderByDescending(s => s.confidence).ToList();
if (sorted[0].confidence < _confidenceThreshold)
    return (null, sorted[0].confidence); // 閾値未満

if (sorted.Count >= 2 &&
    sorted[0].confidence - sorted[1].confidence < 0.05f)
    return (null, sorted[0].confidence); // 2言語が拮抗

return (sorted[0].languageCode, sorted[0].confidence);
```

---

## 6. UnicodeLanguageDetector との統合

### 6.1 インターフェース抽出

`UnicodeLanguageDetector` から `ILanguageDetector` を抽出し、`MultilingualPhonemizer` の依存を抽象化する。

**変更前** (v1.4.0):
```csharp
// MultilingualPhonemizer.cs
private readonly UnicodeLanguageDetector _detector;
```

**変更後** (v2.0):
```csharp
// MultilingualPhonemizer.cs
private readonly ILanguageDetector _detector;
```

### 6.2 後方互換性

`UnicodeLanguageDetector` は `ILanguageDetector` を実装することで既存の動作を維持する。trigram プロファイルが利用不可の場合（ファイル未配置、WebGL ロード失敗等）、`HybridLanguageDetector` は自動的に Unicode のみモードにフォールバックする。

### 6.3 MultilingualPhonemizerOptions への統合

```csharp
public class MultilingualPhonemizerOptions
{
    // 既存プロパティ...

    /// <summary>
    /// Custom language detector. When null, HybridLanguageDetector is created
    /// with default trigram profiles (if available) or falls back to
    /// UnicodeLanguageDetector.
    /// </summary>
    public ILanguageDetector LanguageDetector { get; set; }

    /// <summary>
    /// Whether to enable trigram-based Latin language detection.
    /// Default: true. Set to false to use Unicode-only detection.
    /// </summary>
    public bool EnableTrigramDetection { get; set; } = true;
}
```

### 6.4 初期化フロー

```csharp
// MultilingualPhonemizer.InitializeAsync() 内
if (options.LanguageDetector != null)
{
    _detector = options.LanguageDetector;
}
else if (options.EnableTrigramDetection && HasMultipleLatinLanguages())
{
    // trigram プロファイルを読み込み
    var profiles = await TrigramProfileLoader.LoadAsync(cancellationToken);
    if (profiles != null)
    {
        var trigramDetector = new TrigramLanguageDetector(profiles);
        _detector = new HybridLanguageDetector(
            _languages, _defaultLatinLanguage, trigramDetector);
    }
    else
    {
        // プロファイル読み込み失敗 → Unicode のみにフォールバック
        _detector = new UnicodeLanguageDetector(_languages, _defaultLatinLanguage);
    }
}
else
{
    _detector = new UnicodeLanguageDetector(_languages, _defaultLatinLanguage);
}
```

**trigram 検出が不要なケース**:
- ラテン文字言語が1つのみ（例: `["ja", "en", "zh"]`）
- `EnableTrigramDetection = false`
- プロファイルファイルが存在しない

---

## 7. 精度 vs 速度のトレードオフ

### 7.1 処理時間の見積もり

| 処理ステップ | 推定時間 | 備考 |
|------------|---------|------|
| Unicode SegmentText (既存) | ~0.01ms (100文字) | 文字単位 O(n) |
| テキスト正規化 | ~0.01ms | ToLower + アクセント除去 |
| Trigram 抽出 | ~0.02ms (100文字) | O(n) スライディングウィンドウ |
| 4言語プロファイル照合 | ~0.05ms | 4 x 300 エントリの辞書検索 |
| **合計 (trigram 追加分)** | **~0.08ms** | Unicode のみ比 +0.07ms |

TTS 推論自体が数十〜数百 ms かかるため、0.1ms 未満の追加は無視できる。

### 7.2 メモリ使用量

| 項目 | サイズ |
|------|-------|
| Trigram プロファイル JSON (4言語) | ~12 KB (ファイル) |
| ランタイム Dictionary (4言語 x 300エントリ) | ~48 KB |
| 入力テキストの trigram 抽出バッファ | ~2 KB (一時的) |
| **合計** | **~62 KB** |

Unity のメモリ環境（通常数百 MB-数 GB）に対して無視できるサイズ。

### 7.3 WebGL 制約

- StreamingAssets の非同期読み込み必須 (`UnityWebRequest`)
- `TrigramProfileLoader.LoadAsync()` を `MultilingualPhonemizer.InitializeAsync()` 内で呼び出す
- ファイルサイズが小さい（~12 KB）ため、ネットワーク遅延は最小限

---

## 8. トレーニングデータ（Trigram プロファイル生成）

### 8.1 コーパス選択

| 言語 | 推奨コーパス | サイズ | ライセンス |
|------|------------|------|-----------|
| en | UDHR + Wikipedia subset | 100K文 | Public Domain / CC-BY-SA |
| es | UDHR + Wikipedia subset | 100K文 | Public Domain / CC-BY-SA |
| fr | UDHR + Wikipedia subset | 100K文 | Public Domain / CC-BY-SA |
| pt | UDHR + Wikipedia subset | 100K文 | Public Domain / CC-BY-SA |

**UDHR** (Universal Declaration of Human Rights) は全言語で同一内容のため、trigram の言語特性を抽出するのに適している。ただしテキスト量が少ないため、Wikipedia サブセットで補完する。

### 8.2 プロファイル生成スクリプト

```python
#!/usr/bin/env python3
"""Generate trigram profiles for Latin-script language detection."""

import json
import sys
import unicodedata
from collections import Counter
from pathlib import Path


def normalize_text(text: str) -> str:
    """Normalize text for trigram extraction."""
    # NFD decomposition + strip combining marks (accent removal)
    nfd = unicodedata.normalize("NFD", text.lower())
    stripped = "".join(c for c in nfd if unicodedata.category(c) != "Mn")
    # Replace non-alpha with space, collapse
    result = []
    for c in stripped:
        result.append(c if c.isalpha() or c == " " else " ")
    text = "".join(result)
    while "  " in text:
        text = text.replace("  ", " ")
    return f" {text.strip()} "


def extract_trigrams(text: str) -> Counter:
    """Extract trigram frequency counts."""
    counter = Counter()
    for i in range(len(text) - 2):
        counter[text[i : i + 3]] += 1
    return counter


def build_profile(corpus_path: str, top_n: int = 300) -> list[str]:
    """Build ranked trigram profile from corpus file."""
    text = Path(corpus_path).read_text(encoding="utf-8")
    normalized = normalize_text(text)
    trigrams = extract_trigrams(normalized)
    return [trigram for trigram, _ in trigrams.most_common(top_n)]


def main():
    languages = {"en": "corpus_en.txt", "es": "corpus_es.txt",
                 "fr": "corpus_fr.txt", "pt": "corpus_pt.txt"}
    top_n = 300
    profiles = {}
    for lang, path in languages.items():
        trigrams = build_profile(path, top_n)
        profiles[lang] = {"trigrams": trigrams, "trigramCount": len(trigrams)}
        print(f"{lang}: {len(trigrams)} trigrams extracted")

    output = {"version": "1.0", "languages": profiles}
    Path("trigram_profiles.json").write_text(
        json.dumps(output, ensure_ascii=False, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
```

### 8.3 プロファイルの検証

生成したプロファイルの品質を以下の基準で検証する:

1. **自言語テスト**: 各言語のテストテキスト（100文）で自言語が最高スコアになることを確認
2. **交差検証**: 他言語のテストテキストで誤検出率を測定
3. **重複率チェック**: 言語間の上位100 trigram の重複率が50%未満であることを確認

---

## 9. テスト計画

### 9.1 単体テスト: TrigramLanguageDetector

```csharp
[TestFixture]
public class TrigramLanguageDetectorTests
{
    // ── Trigram 抽出 ──────────────────────────────────────────────

    [Test]
    public void ExtractTrigrams_SimpleText_ReturnsCorrectTrigrams()
    {
        var trigrams = TrigramLanguageDetector.ExtractTrigrams("hello");
        Assert.IsTrue(trigrams.ContainsKey(" he"));
        Assert.IsTrue(trigrams.ContainsKey("hel"));
        Assert.IsTrue(trigrams.ContainsKey("ell"));
        Assert.IsTrue(trigrams.ContainsKey("llo"));
        Assert.IsTrue(trigrams.ContainsKey("lo "));
    }

    [Test]
    public void ExtractTrigrams_EmptyText_ReturnsEmpty()
    {
        var trigrams = TrigramLanguageDetector.ExtractTrigrams("");
        Assert.AreEqual(0, trigrams.Count);
    }

    [Test]
    public void ExtractTrigrams_AccentedText_NormalizesAccents()
    {
        // "cafe" (with accent) should produce same trigrams as "cafe"
        var trigrams = TrigramLanguageDetector.ExtractTrigrams("caf\u00E9");
        Assert.IsTrue(trigrams.ContainsKey("caf"));
        Assert.IsTrue(trigrams.ContainsKey("afe"));
    }

    // ── 言語検出 ──────────────────────────────────────────────────

    [Test]
    public void Detect_EnglishText_ReturnsEn()
    {
        var detector = CreateDetectorWithProfiles();
        var (lang, confidence) = detector.Detect(
            "The quick brown fox jumps over the lazy dog");
        Assert.AreEqual("en", lang);
        Assert.Greater(confidence, 0.65f);
    }

    [Test]
    public void Detect_SpanishText_ReturnsEs()
    {
        var detector = CreateDetectorWithProfiles();
        var (lang, confidence) = detector.Detect(
            "El zorro marron rapido salta sobre el perro perezoso");
        Assert.AreEqual("es", lang);
        Assert.Greater(confidence, 0.65f);
    }

    [Test]
    public void Detect_FrenchText_ReturnsFr()
    {
        var detector = CreateDetectorWithProfiles();
        var (lang, confidence) = detector.Detect(
            "Le renard brun rapide saute par dessus le chien paresseux");
        Assert.AreEqual("fr", lang);
        Assert.Greater(confidence, 0.65f);
    }

    [Test]
    public void Detect_PortugueseText_ReturnsPt()
    {
        var detector = CreateDetectorWithProfiles();
        var (lang, confidence) = detector.Detect(
            "A raposa marrom rapida salta sobre o cachorro preguicoso");
        Assert.AreEqual("pt", lang);
        Assert.Greater(confidence, 0.65f);
    }

    [Test]
    public void Detect_ShortText_ReturnsFallback()
    {
        var detector = CreateDetectorWithProfiles();
        var (lang, _) = detector.Detect("Hi");
        // Text below MinCharsForDetection, expect null (fallback)
        Assert.IsNull(lang);
    }

    [Test]
    public void Detect_AmbiguousText_ReturnsFallback()
    {
        var detector = CreateDetectorWithProfiles();
        // "de" is common in es/fr/pt -> ambiguous
        var (lang, _) = detector.Detect("de la de la de la");
        // Score margin too small, expect null (fallback)
        // Actual behavior depends on profile data
    }
}
```

### 9.2 統合テスト: HybridLanguageDetector

```csharp
[TestFixture]
public class HybridLanguageDetectorTests
{
    [Test]
    public void SegmentText_JapaneseAndFrench_CorrectSegments()
    {
        // "こんにちは Bonjour le monde"
        var detector = CreateHybridDetector(new[] { "ja", "en", "fr" });
        var result = detector.SegmentText(
            "こんにちは Bonjour le monde et bienvenue");

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("ja", result[0].language);
        Assert.AreEqual("fr", result[1].language); // Not "en"
    }

    [Test]
    public void SegmentText_ChineseAndSpanish_CorrectSegments()
    {
        var detector = CreateHybridDetector(new[] { "zh", "en", "es" });
        var result = detector.SegmentText(
            "你好 Hola mundo como estas");

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("zh", result[0].language);
        Assert.AreEqual("es", result[1].language); // Not "en"
    }

    [Test]
    public void SegmentText_LatinOnly_SingleLanguageList_SkipsTrigram()
    {
        // Only one Latin language -> trigram detection unnecessary
        var detector = CreateHybridDetector(new[] { "ja", "en" });
        var result = detector.SegmentText("Bonjour le monde");

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("en", result[0].language); // Default fallback
    }

    [Test]
    public void SegmentText_NoProfiles_FallsBackToUnicode()
    {
        // Trigram detector is null -> pure Unicode detection
        var detector = new HybridLanguageDetector(
            new[] { "ja", "en", "fr" },
            trigramDetector: null);
        var result = detector.SegmentText("Bonjour le monde");

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("en", result[0].language); // Default fallback
    }
}
```

### 9.3 言語別テストケース

| 言語 | テストケース | 文字数 | 期待精度 |
|------|------------|--------|---------|
| en | "The weather is beautiful today and we should go outside" | 55 | > 95% |
| en | "Hello" | 5 | フォールバック |
| es | "Buenos dias como estas hoy espero que bien" | 43 | > 90% |
| es | "Hola" | 4 | フォールバック |
| fr | "Bonjour comment allez vous aujourd hui" | 39 | > 90% |
| fr | "Merci" | 5 | フォールバック |
| pt | "Bom dia como voce esta hoje" | 28 | > 85% |
| pt | "Ola" | 3 | フォールバック |

### 9.4 混合テキストテスト

| テスト | 入力 | 期待セグメント |
|-------|------|--------------|
| ja + en | "こんにちは The weather is great" | [ja: "こんにちは ", en: "The weather is great"] |
| ja + fr | "今日は Bonjour comment allez vous" | [ja: "今日は ", fr: "Bonjour comment allez vous"] |
| zh + es | "你好世界 Buenos dias como estas" | [zh: "你好世界 ", es: "Buenos dias como estas"] |
| ja + en + ja | "おはよう Good morning ですね" | [ja, en, ja] (既存テスト互換) |

### 9.5 パフォーマンステスト

```csharp
[Test]
[Performance]
public void SegmentText_Performance_HybridVsUnicode()
{
    // 100文字のラテンテキスト x 1000回
    var text = "The quick brown fox jumps over the lazy dog repeatedly...";
    var hybrid = CreateHybridDetector(new[] { "en", "es", "fr", "pt" });
    var unicode = new UnicodeLanguageDetector(
        new[] { "en", "es", "fr", "pt" });

    // Hybrid should complete within 2x of Unicode-only
    Measure.Method(() => hybrid.SegmentText(text))
        .WarmupCount(10)
        .MeasurementCount(100)
        .Run();
}
```

---

## 10. 既知の限界

### 10.1 短いテキスト

Trigram 検出は統計的手法のため、短いテキスト（15文字未満）では信頼性が低い。`MinCharsForDetection` により閾値未満のテキストは `defaultLatinLanguage` にフォールバックする。

**影響範囲**: 単語レベルや短いフレーズ（"Bonjour", "Gracias", "Obrigado"）は検出できない。これらは混合テキストの一部として出現する場合に問題となる。

**緩和策**: 将来的に辞書ベースの単語レベル検出（piper-plus のスウェーデン語検出と同様の手法）を `LatinSegmentRefiner` に追加可能。

### 10.2 固有名詞・借用語

固有名詞（"Tokyo", "Paris", "Madrid"）や言語間借用語（"restaurant", "hotel", "internet"）は言語固有の trigram パターンを持たず、誤検出の原因となる。

**緩和策**: 文脈全体の trigram 分布で判定するため、単語単位の借用語は影響が限定的。ただし短いテキストが借用語のみで構成される場合は誤検出の可能性がある。

### 10.3 ロマンス言語間の混同

es/fr/pt は言語的類似性が高く、短いテキストや汎用的な表現（数字混じり、一般名詞のみ等）では混同しやすい。

**見積もり精度**:

| テキスト長 | en vs 他 | es vs fr | es vs pt | fr vs pt |
|-----------|---------|---------|---------|---------|
| 50+ 文字 | > 98% | > 90% | > 88% | > 88% |
| 20-50 文字 | > 95% | > 80% | > 75% | > 75% |
| 15-20 文字 | > 90% | > 70% | > 65% | > 65% |
| < 15 文字 | フォールバック | フォールバック | フォールバック | フォールバック |

### 10.4 コードスイッチング（文中言語切り替え）

ラテン文字セグメント内でのコードスイッチング（"I went to the bibliotheque"）は検出不可。セグメント全体が1つの言語として判定される。

**緩和策**: v2.0 のスコープ外。将来的にはスライディングウィンドウ方式でサブセグメント分割を検討。

### 10.5 プロファイルの陳腐化

コーパスベースの静的プロファイルのため、新語やスラングの影響を受けない。TTS の入力テキストは一般的に標準的な書き言葉であるため、実用上の問題は小さい。

---

## 11. ファイル配置

### 11.1 新規ファイル

| ファイルパス | 種別 | 説明 |
|------------|------|------|
| `Runtime/Core/Phonemizers/Multilingual/ILanguageDetector.cs` | interface | 言語検出抽象 |
| `Runtime/Core/Phonemizers/Multilingual/TrigramLanguageDetector.cs` | class | Trigram ベース検出器 |
| `Runtime/Core/Phonemizers/Multilingual/TrigramProfile.cs` | class | 言語別 trigram プロファイル |
| `Runtime/Core/Phonemizers/Multilingual/TrigramProfileLoader.cs` | class | プロファイル JSON 読み込み |
| `Runtime/Core/Phonemizers/Multilingual/HybridLanguageDetector.cs` | class | Unicode + Trigram 統合検出器 |
| `Runtime/Core/Phonemizers/Multilingual/LatinSegmentRefiner.cs` | class | ラテンセグメント再分類 |
| `StreamingAssets/uPiper/LanguageProfiles/trigram_profiles.json` | data | Trigram プロファイルデータ |
| `Tests/Editor/TrigramLanguageDetectorTests.cs` | test | Trigram 検出テスト |
| `Tests/Editor/HybridLanguageDetectorTests.cs` | test | ハイブリッド統合テスト |

### 11.2 変更ファイル

| ファイルパス | 変更内容 |
|------------|---------|
| `Runtime/Core/Phonemizers/Multilingual/UnicodeLanguageDetector.cs` | `ILanguageDetector` 実装追加 |
| `Runtime/Core/Phonemizers/Multilingual/MultilingualPhonemizer.cs` | `_detector` 型を `ILanguageDetector` に変更 |
| `Runtime/Core/Phonemizers/Multilingual/MultilingualPhonemizerOptions.cs` | `LanguageDetector`, `EnableTrigramDetection` プロパティ追加 |

---

## 12. 実装スケジュール

| ステップ | 内容 | 見積もり |
|---------|------|---------|
| 1 | `ILanguageDetector` インターフェース抽出 | 0.5日 |
| 2 | `UnicodeLanguageDetector` に `ILanguageDetector` 実装 | 0.5日 |
| 3 | `MultilingualPhonemizer` の依存型変更 | 0.5日 |
| 4 | `TrigramProfile` + `TrigramLanguageDetector` 実装 | 1日 |
| 5 | Trigram プロファイル生成スクリプト + データ生成 | 1日 |
| 6 | `TrigramProfileLoader` 実装 (WebGL 対応含む) | 0.5日 |
| 7 | `LatinSegmentRefiner` + `HybridLanguageDetector` 実装 | 1日 |
| 8 | 単体テスト + 統合テスト | 1日 |
| 9 | パフォーマンス検証 + 閾値チューニング | 0.5日 |
| **合計** | | **6.5日** |

### 前提条件

- P1-3 (Dictionary Registry 化) 完了: ハンドラ登録パターンが確定していること
- P1-4 (ILanguageG2PHandler) 完了: 言語プラグインの差し替え機構が整備されていること

---

## 13. 将来の拡展

### 13.1 スウェーデン語 (sv) 対応

モデルがスウェーデン語に対応した場合:
1. `trigram_profiles.json` に sv プロファイルを追加
2. `LanguageConstants.LatinLanguages` に "sv" を追加
3. `TrigramLanguageDetector` は自動的に sv を検出対象に含める
4. piper-plus の `SwedishFunctionWords` ベース検出は不要（trigram で代替可能）

### 13.2 辞書ベース補完

短いテキストの精度向上のため、高頻度の言語固有単語辞書を `LatinSegmentRefiner` に追加:

```csharp
// 例: 各言語 20-30 語の特徴語リスト
private static readonly Dictionary<string, HashSet<string>> LanguageKeywords = new()
{
    ["en"] = new() { "the", "and", "is", "are", "was", "were", "have", "has" },
    ["es"] = new() { "el", "la", "los", "las", "una", "uno", "del", "por" },
    ["fr"] = new() { "le", "la", "les", "des", "une", "est", "dans", "avec" },
    ["pt"] = new() { "o", "a", "os", "as", "um", "uma", "do", "da", "nos" },
};
```

### 13.3 コンテキスト累積

同一セッション内で複数のテキストが同じ言語で入力される場合、前回の検出結果を次回の `defaultLatinLanguage` ヒントとして使用する適応的検出。
