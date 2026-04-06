# MS3-1: スウェーデン語 (SV) 対応

**マイルストーン**: [MS3: 新機能追加](../piper-plus-v1.10.0-milestones.md#ms3-新機能追加)
**優先度**: P2
**ステータス**: ブロック中（スウェーデン語対応モデル待ち）
**見積もり**: 1.5人日（実装1日 + テスト・調整0.5日）
**依存チケット**: MS1-1 (PUA マッピングにSV PUA 0xE059-0xE061 追加が含まれる)
**前提条件（DotNetG2P.Swedish側）**: `SwedishPuaMapper` に長母音PUA（0xE059-0xE061）のマッピングを追加する必要がある。現在の `SwedishPuaMapper` には `t͡ɕ` → 0xE023 のマッピングしか含まれておらず、長母音はIPA形式のまま出力される。`ToPuaPhonemes()` が長母音をPUA文字に変換できるよう、DotNetG2P.Swedish リポジトリ側で対応が必要。他言語（Chinese, Korean）もそれぞれの PuaMapper で PUA 変換を完結させているアーキテクチャに合わせる。
**後続チケット**: なし

---

## 1. タスク目的とゴール

uPiperの8番目の言語としてスウェーデン語 (SV) を追加する。DotNetG2P.Swedish (v1.9.0) は dot-net-g2p リポジトリで開発完了済みであり、本チケットは **uPiper側の統合作業のみ** を対象とする。

**ゴール**:
- スウェーデン語テキストを `MultilingualPhonemizer` 経由で音素化できる
- Prosody情報 (A1=ピッチアクセント, A2=ストレス, A3=音節数) が正しく取得される
- 既存7言語 (ja/en/zh/es/fr/pt/ko) の動作に影響を与えない
- デモUI (`InferenceEngineDemo`) でスウェーデン語が選択・テスト可能になる

**DotNetG2P.Swedish の準備状況** (確認済み):
- `SwedishG2PEngine`: 5フェーズG2Pルール、47音素定義、方言対応 (Central/FinlandSwedish)
- Prosody API: `ToIpaWithProsody()` -> `SwedishProsodyResult` (A1/A2/A3)
- PUA API: `ToPuaPhonemes()` / `ToPuaString()` (内部で `SwedishPuaMapper` を使用)
- PUAマッピング: `t͡ɕ` -> 0xE023 (中国語/韓国語と共有) のみ。長母音PUAはモデル側で 0xE059-0xE061
- テスト: 399+テストケース
- Unity対応: asmdef定義、`[Preserve]` アノテーション済み

---

## 2. 実装する内容の詳細

### 2.1 パッケージ参照の追加

**ファイル**: `Packages/manifest.json`

既存パターンに倣い、`com.dotnetg2p.swedish` を追加する。

```json
"com.dotnetg2p.swedish": "https://github.com/ayutaz/dot-net-g2p.git?path=src/DotNetG2P.Swedish#v1.9.0",
```

**挿入位置**: `com.dotnetg2p.spanish` (行17) の直後。アルファベット順を維持する。

現在の参照パターン (`manifest.json` 行10-17):
```
"com.dotnetg2p.chinese": "...#v1.8.2",
"com.dotnetg2p.core": "...#v1.8.2",
"com.dotnetg2p.english": "...#v1.8.2",
"com.dotnetg2p.french": "...#v1.8.2",
"com.dotnetg2p.korean": "...#v1.8.2",
"com.dotnetg2p.mecab": "...#v1.8.2",
"com.dotnetg2p.portuguese": "...#v1.8.2",
"com.dotnetg2p.spanish": "...#v1.8.2",
```

> **注意**: バージョンタグは `#v1.9.0` を使用。他パッケージが v1.8.2 のままだが、DotNetG2P.Swedish は v1.9.0 で初回リリースのため整合性に問題なし。全パッケージを v1.9.0 にバンプするかはMS1で判断する。

### 2.2 asmdef 参照の追加

**ファイル**: `Assets/uPiper/Runtime/uPiper.Runtime.asmdef`

`references` 配列に `"DotNetG2P.Swedish"` を追加する。

現在の参照 (行4-14):
```json
"references": [
    "Unity.InferenceEngine",
    "Unity.TextMeshPro",
    "DotNetG2P",
    "DotNetG2P.MeCab",
    "DotNetG2P.Chinese",
    "DotNetG2P.English",
    "DotNetG2P.Korean",
    "DotNetG2P.Spanish",
    "DotNetG2P.French",
    "DotNetG2P.Portuguese"
],
```

**追加後**:
```json
"references": [
    "Unity.InferenceEngine",
    "Unity.TextMeshPro",
    "DotNetG2P",
    "DotNetG2P.MeCab",
    "DotNetG2P.Chinese",
    "DotNetG2P.English",
    "DotNetG2P.Korean",
    "DotNetG2P.Spanish",
    "DotNetG2P.Swedish",
    "DotNetG2P.French",
    "DotNetG2P.Portuguese"
],
```

> DotNetG2P.Swedish の asmdef 名は `"DotNetG2P.Swedish"` (`dot-net-g2p/src/DotNetG2P.Swedish/DotNetG2P.Swedish.asmdef` で確認済み)。`references` は空で外部依存なし。

### 2.3 LanguageConstants への SV 追加

**ファイル**: `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/LanguageConstants.cs`

以下の定数・配列・辞書に SV を追加する。

**(a) Language ID / Code 定数 (行37-60付近の後に追加)**:
```csharp
/// <summary>Language ID for Swedish (sv). Used when the model includes Swedish support.</summary>
public const int LanguageIdSwedish = 7;

/// <summary>ISO 639-1 code for Swedish.</summary>
public const string CodeSwedish = "sv";
```

**(b) AllLanguages 配列 (行65)**:
```csharp
// 変更前
public static readonly string[] AllLanguages = { "ja", "en", "zh", "es", "fr", "pt", "ko" };
// 変更後
public static readonly string[] AllLanguages = { "ja", "en", "zh", "es", "fr", "pt", "ko", "sv" };
```

**(c) LatinLanguages 配列 (行72)**:
```csharp
// 変更前
public static readonly string[] LatinLanguages = { "en", "es", "fr", "pt" };
// 変更後
public static readonly string[] LatinLanguages = { "en", "es", "fr", "pt", "sv" };
```

**(d) CodeToId / IdToCode 辞書 (行82-101)**:

辞書サイズを `new(7)` -> `new(8)` に変更し、以下のエントリを追加:
```csharp
[CodeSwedish] = LanguageIdSwedish     // CodeToId
[LanguageIdSwedish] = CodeSwedish     // IdToCode
```

**(e) IsLatinLanguage メソッド (行148-153)**:
```csharp
// 変更後
public static bool IsLatinLanguage(string languageCode)
{
    return languageCode == CodeEnglish ||
           languageCode == CodeSpanish ||
           languageCode == CodeFrench ||
           languageCode == CodePortuguese ||
           languageCode == CodeSwedish;
}
```

**(f) GetLanguageCode エラーメッセージ (行136)**:
```csharp
// 更新: "7 (sv)" を追加
$"Supported IDs: 0 (ja), 1 (en), 2 (zh), 3 (es), 4 (fr), 5 (pt), 6 (ko), 7 (sv)"
```

### 2.4 MultilingualPhonemizer への SV ルーティング追加

**ファイル**: `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/MultilingualPhonemizer.cs`

**(a) using ディレクティブの追加 (行1-16)**:
```csharp
using DotNetG2P.Swedish;
```

**(b) フィールドの追加 (行58-76付近)**:
```csharp
private SwedishG2PEngine _svEngine;     // Swedish (DotNetG2P)
private bool _ownsSv;
```

**(c) コンストラクタへのパラメータ追加 (行96-106)**:

既存パターン (他言語のオプショナルパラメータ) に倣い追加:
```csharp
/// <param name="svEngine">Optional pre-built Swedish G2P engine (DotNetG2P.Swedish).</param>
public MultilingualPhonemizer(
    IReadOnlyList<string> languages,
    string defaultLatinLanguage = "en",
    DotNetG2PPhonemizer jaPhonemizer = null,
    IPhonemizerBackend enPhonemizer = null,
    SpanishG2PEngine esEngine = null,
    FrenchG2PEngine frEngine = null,
    PortugueseG2PEngine ptEngine = null,
    ChineseG2PEngine zhEngine = null,
    IPhonemizerBackend koPhonemizer = null,
    KoreanG2PEngine koG2PEngine = null,
    SwedishG2PEngine svEngine = null)    // 新規追加
```

コンストラクタ本体で `_svEngine = svEngine;` を追加。

**(d) InitializeAsync への SV 初期化追加 (行250付近、Korean初期化ブロックの後)**:

ES/FR/PT と同じパターン (辞書ファイル不要、コンストラクタのみで初期化完了):
```csharp
// Initialize Swedish G2P engine if needed
if (ContainsLanguage("sv") && _svEngine == null)
{
    _svEngine = new SwedishG2PEngine();
    _ownsSv = true;
    PiperLogger.LogInfo("[MultilingualPhonemizer] Swedish backend initialized: DotNetG2P.Swedish");
    await Task.CompletedTask;
}
```

初期化済みチェック (行252-254) の条件に `_svEngine == null` を追加。

**(e) PhonemizeWithProsodyAsync への SV 分岐追加 (行468、ko ブロックの後)**:

FR/PT パターン（`ToPuaPhonemes` + `ToIpaWithProsody` の二重呼び出し + `ExtractProsodyArrays`）に準拠:

> **注記**: ES分岐は `result.Phonemes` を直接使用する異なるパターン。
```csharp
else if (lang == "sv" && _svEngine != null)
{
    // Use DotNetG2P.Swedish engine directly with prosody
    segPhonemes = _svEngine.ToPuaPhonemes(segText);
    var result = _svEngine.ToIpaWithProsody(segText);
    (segA1, segA2, segA3) = ExtractProsodyArrays(
        result.Prosody, p => (p.A1, p.A2, p.A3), segPhonemes.Length);
}
```

> `SwedishProsodyResult.Prosody` は `SwedishProsodyInfo[]` 型で、各要素に `A1` (ピッチアクセント: 0/1/2), `A2` (ストレスレベル: 0/1/2), `A3` (語の音節数) を持つ。`SwedishProsodyResult.Phonemes` と `SwedishProsodyResult.Prosody` は同一長が保証されている (コンストラクタでバリデーション済み)。そのため `ExtractProsodyArrays` ヘルパーがそのまま使える。

**(f) Dispose への SV 追加 (行579-602)**:
```csharp
if (_ownsSv) _svEngine?.Dispose();
```

### 2.5 UnicodeLanguageDetector の SV 対応

**ファイル**: `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/UnicodeLanguageDetector.cs`

**課題**: スウェーデン語はラテン文字を使用するため、英語 (en)、スペイン語 (es)、フランス語 (fr)、ポルトガル語 (pt) とUnicode範囲だけでは区別できない。

**スウェーデン語固有文字**: `a`, `a`, `o` (U+00E5, U+00E4, U+00F6) はスウェーデン語で頻出するが、ドイツ語 (a, o, u) やフランス語 (他のアクセント文字) とも重複する可能性がある。ただし、uPiperの対応言語セット内では `a` (U+00E5) はスウェーデン語にほぼ固有である。

**対応方針**: 完全な自動検出は困難であるため、以下の2段階アプローチを採用する。

**(a) 簡易ヒューリスティック検出** (最小限の変更):
- `a` (U+00E5) が含まれるセグメントは SV と判定する
- `_hasSv` フラグをコンストラクタで追加 (行19、`_hasKo` の後)
- `DetectChar` (行165-207) で Latin 判定の前にスウェーデン語固有文字チェックを挿入:

```csharp
// Priority 6a: Swedish-specific characters (a U+00E5, A U+00C5)
if (_hasSv && (ch == '\u00E5' || ch == '\u00C5'))
    return "sv";
```

> `a` (U+00E4) と `o` (U+00F6) は他のラテン文字言語 (特にドイツ語) でも使われるため、判定文字には含めない。`a` は uPiper の対応言語セット内ではスウェーデン語の強い指標となる。

**(b) defaultLatinLanguage による明示指定** (既存機構の活用):
- ユーザーが `defaultLatinLanguage = "sv"` を指定した場合、ラテン文字全般がスウェーデン語として処理される
- これは既存の `_defaultLatinLanguage` 機構でそのまま動作する (追加実装不要)
- デモUI (`InferenceEngineDemo`) でスウェーデン語を選択した場合、`InitializeMultilingualPhonemizerAsync("sv")` が呼ばれ、`defaultLatinLanguage` が "sv" に設定される (既存の行530-533の `IsLatinLanguage` 判定で自動的にこのパスに入る)

### 2.6 Prosody マッピング仕様

| パラメータ | SV の意味 | 値域 | piper-plus 仕様の出典 |
|-----------|----------|------|---------------------|
| A1 (ProsodyA1) | ピッチアクセント番号 | 0=不明, 1=accent 1, 2=accent 2 | `SwedishProsodyInfo.A1` |
| A2 (ProsodyA2) | ストレスレベル | 0=なし, 1=primary | `SwedishProsodyInfo.A2` |
| A3 (ProsodyA3) | 語の音節数 | 1以上の正整数 | `SwedishProsodyInfo.A3` |

> スウェーデン語のピッチアクセントは日本語のアクセントに類似した声調パターンであり、Accent 1 (急性アクセント) と Accent 2 (重アクセント) の2種が存在する。DotNetG2P.Swedish の `StressAssigner` が自動推定する。FinlandSwedish 方言では `SwedishG2PEngine` がピッチアクセントを 0 にリセットする (`SwedishG2PEngine.cs` 行193-198)。
>
> **A2値域の注記**: `SwedishG2PEngine.ToIpaWithProsody()` の実装では `StressedSyllableIndex >= 0 ? 1 : 0` で0または1のみを返す。secondary stress (2) は現時点では生成されない。

### 2.7 デモUI への SV 追加

**ファイル**: `Assets/uPiper/Runtime/Demo/InferenceEngineDemo.cs`

**(a) SupportedLanguages 配列 (行84)**:
```csharp
// 変更前
private static readonly string[] SupportedLanguages = { "ja", "en", "zh", "es", "fr", "pt" };
// 変更後
private static readonly string[] SupportedLanguages = { "ja", "en", "zh", "es", "fr", "pt", "sv" };
```

**(b) LanguageDisplayNames 辞書 (行86-94)**:
```csharp
{ "sv", "Svenska (Swedish)" }
```

**(c) テスト定型文の追加 (InitializeTestPhrases メソッド)**:
```csharp
["sv"] = new List<string>
{
    "Fri inmatning",
    "Hej",
    "God morgon",
    "Tack sa mycket",
    "Hur mar du idag?",
    "Valkommen till rostsyntes"
}
```

### 2.8 PuaTokenMapper の SV PUA エントリ追加 (MS1-1 との関係)

**ファイル**: `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/PuaTokenMapper.cs`

> **MS1-1 で対応済みの場合はスキップ**。MS1-1 チケットに以下のSV PUAエントリ追加が含まれている。MS1-1 が未完了の場合は本チケットで追加する。

追加エントリ (9件、行166 の French ブロック後に挿入):
```csharp
// =================================================================
// Swedish (SV) -- swedish long vowels
// =================================================================
{ "i\u02D0", 0xE059 },    // long close front unrounded (SV i:)
{ "y\u02D0", 0xE05A },    // long close front rounded (SV y:)
{ "e\u02D0", 0xE05B },    // long close-mid front unrounded (SV e:)
{ "\u025B\u02D0", 0xE05C },  // long open-mid front unrounded (SV a:)
{ "\u00F8\u02D0", 0xE05D },  // long close-mid front rounded (SV o:)
{ "\u0251\u02D0", 0xE05E },  // long open back unrounded (SV a:)
{ "o\u02D0", 0xE05F },    // long close-mid back rounded (SV o:)
{ "u\u02D0", 0xE060 },    // long close back rounded (SV u:)
{ "\u0289\u02D0", 0xE061 },  // long close central rounded (SV u:)
```

`LastFixedCodepoint` を `0xE058` -> `0xE061` に更新 (行171)。
`DynamicPuaStart` を `0xE059` -> `0xE062` に更新 (行176)。

---

## 3. エージェントチーム構成

| エージェント | 役割 | 担当ファイル |
|------------|------|------------|
| **実装エージェント** | コア統合実装 | `LanguageConstants.cs`, `MultilingualPhonemizer.cs`, `UnicodeLanguageDetector.cs`, `uPiper.Runtime.asmdef`, `manifest.json` |
| **実装エージェント** | PUA/デモ実装 | `PuaTokenMapper.cs` (MS1-1未完了時のみ), `InferenceEngineDemo.cs` |
| **テストエージェント** | ユニットテスト・統合テスト | `MultilingualPhonemizerTests.cs`, 新規テストファイル |
| **レビューエージェント** | コードレビュー・互換性確認 | 全変更ファイル |

> 単一エージェントで実装可能な規模。並行作業のメリットは小さいため、1名の実装エージェントが全タスクを順次実施する構成を推奨する。

---

## 4. 提供範囲・テスト項目

### 提供範囲 (Scope)

**In-scope**:
- `MultilingualPhonemizer` 経由でのスウェーデン語音素化
- SV Prosody (A1/A2/A3) の正しい伝搬
- `LanguageConstants` への SV 定義追加
- `UnicodeLanguageDetector` の簡易 SV 判定 (`a` ヒューリスティック + `defaultLatinLanguage` 対応)
- デモUI への SV 選択肢追加
- パッケージ参照 (`manifest.json`) と asmdef 参照の追加
- PuaTokenMapper への SV PUA エントリ追加 (MS1-1 未完了時)

**Out-of-scope**:
- DotNetG2P.Swedish 本体の変更 (別リポジトリ管理)
- スウェーデン語対応 ONNX モデルのトレーニング・配布
- FinlandSwedish 方言の明示的 UI 切り替え (将来タスク)
- WebGL 環境でのスウェーデン語テスト (WebGL 対応が安定した後に実施)

### Unit テスト

**ファイル**: `Assets/uPiper/Tests/Editor/MultilingualPhonemizerTests.cs`

| テストケース | 内容 | 検証ポイント |
|------------|------|------------|
| `LanguageConstants_SwedishId_Returns7` | `GetLanguageId("sv")` が 7 を返す | 定数定義の正確性 |
| `LanguageConstants_SwedishCode_ReturnsSv` | `GetLanguageCode(7)` が "sv" を返す | 逆引きの正確性 |
| `LanguageConstants_IsLatinLanguage_SvReturnsTrue` | `IsLatinLanguage("sv")` が `true` | ラテン文字判定 |
| `LanguageConstants_IsSupportedLanguage_SvReturnsTrue` | `IsSupportedLanguage("sv")` が `true` | サポート言語判定 |
| `SegmentText_SwedishWithRingA_DetectedAsSv` | `"Hej, jag heter Asa"` → sv セグメント | `a` ヒューリスティック検出 |
| `SegmentText_SwedishWithoutSpecialChars_FallsBackToDefault` | `"hej"` → `defaultLatinLanguage` | 固有文字なし時のフォールバック |
| `SegmentText_MixedJaSv_SplitsByScript` | `"こんにちは Hej"` → ja + defaultLatin | 日本語+ラテン文字混在 |
| `Constructor_WithSvLanguage_CreatesInstance` | `new MultilingualPhonemizer(new[] { "sv" })` が成功 | SV のみ構成 |
| `Constructor_With8Languages_CreatesInstance` | 全8言語指定でインスタンス生成 | 8言語構成の妥当性 |

**既存テスト更新（`LanguageConstantsTests.cs`）**: 以下の既存テストがSV追加時に失敗するため更新が必要:
- `AllLanguages_Contains7Languages`: Assert値を 7 → 8 に変更
- `AllLanguages_ContainsAllExpectedCodes`: expected配列に `"sv"` を追加
- `LatinLanguages_ContainsEnEsFrPt`: expected配列に `"sv"` を追加
- `IsSupportedLanguage_AllSupported_ReturnTrue`: TestCase に `"sv"` を追加
- `GetLanguageId_ValidCode_ReturnsCorrectId`: TestCase に `("sv", 7)` を追加
- `GetLanguageCode_ValidId_ReturnsCorrectCode`: TestCase に `(7, "sv")` を追加

### E2E テスト

> スウェーデン語対応 ONNX モデルが利用可能になった時点で実施する。

| テストケース | 内容 | 検証ポイント |
|------------|------|------------|
| SV テキスト -> AudioClip 生成 | `"Hej, hur mar du?"` で音声生成 | 全パイプラインの疎通 |
| SV Prosody 値の伝搬 | Prosody対応モデルで A1/A2/A3 がゼロでないことを確認 | Prosody パイプライン |
| JA + SV 混在テキスト | `"こんにちは Hej"` で音声生成 | 多言語セグメント化 + 推論 |
| デモUI での SV 選択 | ドロップダウンから SV を選択し音声生成 | UI 統合 |

---

## 5. 懸念事項・レビュー項目

### 5.1 Unicode 言語検出の限界 (最重要)

スウェーデン語はラテン文字を使用するため、`UnicodeLanguageDetector` による自動検出には本質的な限界がある。

- **`a` (U+00E5) ヒューリスティック**: uPiper の対応言語セット内では有効だが、将来ドイツ語やノルウェー語を追加する場合は再設計が必要
- **`a` / `o` の非使用**: これらはドイツ語 (`a`, `o`, `u`) と重複するため、判定文字に含めていない
- **推奨運用**: ユーザーには `defaultLatinLanguage = "sv"` による明示的言語指定を推奨する。デモUIの言語ドロップダウン選択時に `InitializeMultilingualPhonemizerAsync("sv")` が呼ばれる既存機構 (`InferenceEngineDemo.cs` 行530-533) でこれは自動的に実現される

### 5.2 モデル可用性

- 現行モデル (`multilingual-test-medium`) は 6言語 (ja/en/zh/es/fr/pt) のみ対応
- SV を含むモデルが利用可能になるまで、E2E テストは実施不可
- G2P パイプラインの Unit テスト (音素化 + Prosody) はモデルなしで実行可能

### 5.3 asmdef 参照の整合性

- `DotNetG2P.Swedish.asmdef` は `references: []` で外部依存なし (確認済み)
- `uPiper.Runtime.asmdef` に `"DotNetG2P.Swedish"` を追加するだけで十分
- テスト用 asmdef (`uPiper.Tests.Editor.asmdef`, `uPiper.Tests.Runtime.asmdef`) は `uPiper.Runtime` を参照しているため、追加変更は不要

### 5.4 パッケージバージョンの不整合

- 既存 DotNetG2P パッケージは全て `#v1.8.2` タグを使用
- DotNetG2P.Swedish は `#v1.9.0` で初回リリース
- v1.8.2 に Swedish パッケージは存在しないため、v1.9.0 を指定する必要がある
- 全パッケージの v1.9.0 へのバンプは別チケットとして管理することを推奨 (本チケットでは Swedish のみ v1.9.0)

### 5.5 CI 環境でのパス書き換え

- CI では `sed` で `file:../../dot-net-g2p/src/...` を `file:../dot-net-g2p/src/...` に書き換える運用がある (MEMORY.md 記載)
- git URL 参照 (`https://github.com/...`) の場合は書き換え不要だが、ローカル開発時 (`file:` 参照) に切り替える場合は `DotNetG2P.Swedish` のパスも追加が必要

### 5.6 SwedishG2PEngine の初期化コスト

- `SwedishG2PEngine` のコンストラクタはルールのコンパイルのみで、外部ファイル読み込みなし
- ES/FR/PT と同様の軽量初期化パターン (`new SwedishG2PEngine()` + `await Task.CompletedTask`)
- 例外辞書 (549語) は静的クラス `SwedishExceptionDictionary` に埋め込み済み、初回アクセス時にロード

### 5.7 レビュー重点項目

- [ ] `MultilingualPhonemizer` の SV 分岐が ES/FR/PT パターンと一貫しているか
- [ ] `ExtractProsodyArrays` のジェネリック推論が `SwedishProsodyInfo` に対して正しく動作するか
- [ ] `Dispose` で `_svEngine` が正しく解放されるか
- [ ] `LanguageConstants.AllLanguages` の要素数が辞書サイズと一致しているか
- [ ] `UnicodeLanguageDetector` の `a` ヒューリスティックが既存言語の検出に影響を与えないか

---

## 6. ゼロから作り直すとしたら

### 言語検出の設計再考

現在の `UnicodeLanguageDetector` は Unicode 文字範囲による検出に特化しており、ラテン文字言語の増加に対してスケールしない。もしゼロから設計するなら:

1. **N-gram ベースの言語検出**: 文字 N-gram (trigram) の頻度分布に基づく統計的言語検出を導入する。スウェーデン語の `och`, `att`, `det` などの高頻度パターンで英語と区別可能
2. **ハイブリッド検出**: CJK は Unicode 範囲検出 (高速・確実)、ラテン文字間は N-gram 検出 (精度重視) の2段階構成
3. **言語ヒント API**: テキストに `[sv]` のようなインラインタグを埋め込み、明示的に言語を切り替える機能

ただし、現時点での uPiper の主要ユースケース (日本語中心 + 英語混在) では、`defaultLatinLanguage` による明示指定で十分にカバーできるため、N-gram 検出の優先度は低い。

### MultilingualPhonemizer のリファクタリング

現在の `MultilingualPhonemizer` は各言語の分岐が `if-else` チェーンで直列に並んでおり、言語追加のたびに分岐が増える。将来的には:

1. **Strategy パターン**: `ILanguagePhonemizerStrategy` インターフェースで各言語の音素化ロジックをカプセル化し、`Dictionary<string, ILanguagePhonemizerStrategy>` で言語コードからストラテジーを解決する
2. **自動登録**: アセンブリスキャンまたは手動登録で言語ストラテジーを `MultilingualPhonemizer` に注入する

これにより、言語追加時のコード変更箇所を最小化できる。ただし、現在の if-else チェーンでも8言語程度であれば十分に保守可能であり、過度な抽象化は避ける。

---

## 7. 後続タスクへの連絡事項

1. **モデルチーム**: スウェーデン語対応モデルのトレーニング時、`phoneme_id_map` に SV PUA (0xE059-0xE061) と SV 音素を含めること。言語 ID は `lid = 7` を使用すること。
2. **CLAUDE.md 更新**: 本チケット完了後、以下を更新する:
   - 対応言語テーブルに `sv | DotNetG2P.Swedish (SwedishG2PEngine)` を追加
   - Prosody マッピングテーブルに `sv | pitch_accent (0/1/2) | stress (0/1/2) | syllable_count` を追加
   - `AllLanguages` の記述を "7言語" -> "8言語" に更新
   - データフロー図の `MultilingualPhonemizer` 分岐に `sv: SwedishG2PEngine` を追加
3. **MS1-1 との依存関係**: PuaTokenMapper への SV PUA 追加は MS1-1 に含まれている。MS1-1 が先に完了した場合、本チケットのセクション 2.8 はスキップできる。MS1-1 が未着手の場合は本チケットで PUA 追加も実施し、MS1-1 チケットから該当部分を除外する。
4. **全パッケージバージョンバンプ**: DotNetG2P パッケージ群を v1.8.2 -> v1.9.0 (or later) に統一するバンプは別チケットとして管理する。本チケットでは Swedish のみ v1.9.0 を参照する。
5. **FinlandSwedish 方言 UI**: 将来、方言切り替えが必要になった場合は `SwedishG2POptions(dialect: SwedishDialect.FinlandSwedish)` を `SwedishG2PEngine` コンストラクタに渡す。`PiperConfig` への方言設定追加が必要になる。
