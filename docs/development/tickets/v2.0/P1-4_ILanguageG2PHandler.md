# P1-4: ILanguageG2PHandler 全面移行

**マイルストーン**: M1 - Foundation Start
**優先度**: P0（クリティカルパス）
**見積もり**: 5.5 人日（Step1: 0.5 + Step2: 2.0 + Step3: 1.5 + Step4: 1.0 + Step5: 0.5）
**依存チケット**: なし（起点タスク）
**後続チケット**: P1-3, P1-5, P1-6
**ブランチ名**: `feature/v2.0-P1-4-language-g2p-handler`

---

## 1. タスク目的とゴール

### なぜこのタスクが必要か

v1.4.0 で `MultilingualPhonemizer.PhonemizeWithProsodyAsync` 内の if-else チェーンは `switch` 文 + `ProcessXxx()` private メソッドに抽出済みだが、以下の問題が残存している:

1. **7言語分のエンジンフィールド + 所有権フラグが個別に存在**（`_jaPhonemizer` + `_ownsJa`, `_enEngine` + `_ownsEn`, ... の計16フィールド）。言語追加時にフィールド2つ + `InitializeAsync` 分岐 + `Dispose` 分岐の追加が必要で、スケーラビリティが低い。
2. **各エンジン型が異なる**（`DotNetG2PPhonemizer`, `EnglishG2PEngine`, `SpanishG2PEngine` 等）ため、統一的なコレクションで管理できない。
3. **レガシー `IPhonemizerBackend` フィールドが2つ残存**（`_enPhonemizer`, `_koPhonemizer`、いずれも `[Obsolete]`）。
4. **`ProcessXxx` メソッドが `MultilingualPhonemizer` の private メソッド**のままで、個別テストが困難。

`ILanguageG2PHandler` インターフェースを定義し、7言語の処理ロジックを独立したハンドラクラスに分離することで、Strategy パターンによる言語プラグイン化を実現する。これは P1-3（Dictionary Registry 化）、P1-5（IPhonemizerBackend 廃止）、P1-6（Obsolete 削除）の全ての起点となるクリティカルパスタスクである。

### 完了の定義

- `ILanguageG2PHandler` インターフェースが `Runtime/Core/Phonemizers/Multilingual/Handlers/` に定義されている
- 7言語のハンドラクラス（`JapaneseG2PHandler` 〜 `KoreanG2PHandler`）が実装・テスト済み
- `G2PHandlerUtils` static クラスに `ExtractProsodyArrays` が移動済み
- `MultilingualPhonemizer.PhonemizeWithProsodyAsync` の switch 文が `_handlers[lang].Process(text)` に置換済み
- `MultilingualPhonemizerOptions` に `Handlers` プロパティが追加済み
- `PiperTTS.Inference.cs` が `JapaneseG2PHandler(phonemizer)` 経由で日本語ハンドラを注入
- 全既存テスト（EditMode + PlayMode）がパス
- `dotnet format --verify-no-changes` パス

---

## 2. 実装する内容の詳細

### Step 1: ILanguageG2PHandler インターフェース + G2PHandlerUtils 定義（0.5 人日）

#### 2.1.1 ILanguageG2PHandler.cs（新規作成）

**ファイル**: `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/Handlers/ILanguageG2PHandler.cs`

```csharp
namespace uPiper.Core.Phonemizers.Multilingual.Handlers
{
    public interface ILanguageG2PHandler : IDisposable
    {
        string LanguageCode { get; }
        bool IsInitialized { get; }
        Task InitializeAsync(CancellationToken cancellationToken = default);
        (string[] Phonemes, int[] A1, int[] A2, int[] A3) Process(string text);
    }
}
```

設計判断:
- `Process` は同期メソッド。現在の7言語の `ProcessXxx` は全て同期処理。非同期パスは `ProcessFallbackAsync`（P1-5 で廃止予定）のみ。
- `InitializeAsync` は必須。ja/en/zh は辞書ロードが必要。WebGL では全言語で非同期初期化の可能性あり。es/fr/pt/ko は no-op（`Task.CompletedTask` 返却）。
- `IDisposable` 継承。ja（MeCab）と en（CMUdict）はネイティブリソース保持。統一的に Dispose を呼べるようにする。

#### 2.1.2 G2PHandlerUtils.cs（新規作成）

**ファイル**: `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/Handlers/G2PHandlerUtils.cs`

```csharp
namespace uPiper.Core.Phonemizers.Multilingual.Handlers
{
    internal static class G2PHandlerUtils
    {
        internal static (int[] a1, int[] a2, int[] a3) ExtractProsodyArrays<T>(
            T[] prosody, Func<T, (int a1, int a2, int a3)> accessor, int phonemeCount)
        {
            // MultilingualPhonemizer.ExtractProsodyArrays (L686-701) のロジックをそのまま移動
        }
    }
}
```

ES/FR/PT の3言語で共通利用。基底クラス (`LanguageG2PHandlerBase`) は YAGNI（共通ロジックがヘルパー1つのみのため）として不採用。

### Step 2: 7つのハンドラクラス実装 + 単体テスト（2.0 人日）

全ハンドラは `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/Handlers/` に配置。`MultilingualPhonemizer` はまだこのステップでは変更しない。

#### 2.2.1 JapaneseG2PHandler.cs

**ファイル**: `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/Handlers/JapaneseG2PHandler.cs`

- `ProcessJapanese` (MultilingualPhonemizer.cs L450-468) のロジックを移植
- コンストラクタ2種: 外部注入 `JapaneseG2PHandler(DotNetG2PPhonemizer)` + 自動生成 `JapaneseG2PHandler()`
- `_ownsEngine` フラグで所有権管理
- `InitializeAsync`: WebGL 分岐 (`#if UNITY_WEBGL && !UNITY_EDITOR`) を維持
- **主要ロジック**: `PhonemizeWithProsody()` 呼び出し + 先頭PAD (`"_"`) 除去

#### 2.2.2 EnglishG2PHandler.cs

**ファイル**: `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/Handlers/EnglishG2PHandler.cs`

- `ProcessEnglish` (L470-486) のロジックを移植
- `InitializeAsync`: CMUdict パス検索ロジック（`Application.streamingAssetsPath` 参照、L168-199）を移植
- コンストラクタ2種: `EnglishG2PHandler(EnglishG2PEngine)` + `EnglishG2PHandler()`
- **主要ロジック**: `ToPuaPhonemes()` + `ToIpaWithProsody()` + Prosody展開ループ

#### 2.2.3 SpanishG2PHandler.cs / FrenchG2PHandler.cs / PortugueseG2PHandler.cs

**ファイル**: 各 `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/Handlers/XxxG2PHandler.cs`

- パターンA（共通構造）: `ToPuaPhonemes()` + `ToIpaWithProsody()` + `G2PHandlerUtils.ExtractProsodyArrays()`
- ES は `ToIpaWithProsody().Phonemes` を使用（ProcessSpanish L488-495）
- FR は `ToPuaPhonemes()` の結果を音素として使用（ProcessFrench L497-504）
- PT は FR と同一パターン（ProcessPortuguese L506-513）
- 各 `InitializeAsync` は `new XxxG2PEngine()` + `Task.CompletedTask`

#### 2.2.4 ChineseG2PHandler.cs（最も複雑）

**ファイル**: `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/Handlers/ChineseG2PHandler.cs`

- `ProcessChinese` (L515-593、78行) のロジックをそのまま移植
- `TonePuaChars` static readonly フィールドを `MultilingualPhonemizer` から本クラスに移動
- `InitializeAsync`: pinyin辞書パス検索ロジック（L229-260）を移植。`charPath` / `phrasePath` の `File.Exists` チェック含む
- **主要ロジック**: 音節分配、トーンPUAマーカー挿入（`TonePuaChars[toneVal]`）、残余音素処理、配列リサイズ
- **注意**: 78行の複雑ロジック。移植時にテスト網羅を確認すること

#### 2.2.5 KoreanG2PHandler.cs

**ファイル**: `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/Handlers/KoreanG2PHandler.cs`

- `ProcessKorean` (L595-645、50行) のロジックを移植
- PUA/Prosody 長不一致時のフォールバック + `PiperLogger.LogWarning` を維持
- `InitializeAsync`: `new KoreanG2PEngine()` + `Task.CompletedTask`

### Step 3: MultilingualPhonemizer 書き換え（1.5 人日）

**ファイル**: `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/MultilingualPhonemizer.cs`

1. `MultilingualPhonemizerOptions` に `Handlers` プロパティ追加（`Dictionary<string, ILanguageG2PHandler>`）
2. コンストラクタで `options.Handlers` を `_handlers` Dictionary に格納
3. `InitializeAsync` を書き換え:
   - 未登録言語に対して `CreateDefaultHandler(lang)` ファクトリからハンドラ生成
   - 全ハンドラの `InitializeAsync` をループ呼び出し
4. `PhonemizeWithProsodyAsync` の switch 文を `_handlers.TryGetValue(lang, out var handler)` + `handler.Process(segText)` に置換
5. `Dispose` を `foreach (var handler in _handlers.Values) handler.Dispose()` に書き換え
6. 以下を削除:
   - `_jaPhonemizer`, `_enEngine`, `_esEngine`, `_frEngine`, `_ptEngine`, `_zhEngine`, `_koG2PEngine` フィールド
   - `_ownsJa`, `_ownsEn`, `_ownsEs`, `_ownsFr`, `_ownsPt`, `_ownsZh`, `_ownsKo` フラグ
   - `ProcessJapanese`, `ProcessEnglish`, `ProcessSpanish`, `ProcessFrench`, `ProcessPortuguese`, `ProcessChinese`, `ProcessKorean` private メソッド
   - `ExtractProsodyArrays` private static メソッド（`G2PHandlerUtils` に移動済み）
   - `TonePuaChars` private static フィールド（`ChineseG2PHandler` に移動済み）
   - `ContainsLanguage` private メソッド（`_handlers.ContainsKey` に置換）
7. 残すもの:
   - `EosLikeTokens` static フィールド（EOS処理はセグメント結合時の責務）
   - `PadToLength` ヘルパー（Prosody配列整合はセグメント結合後の責務）
   - `_enPhonemizer`, `_koPhonemizer` フィールド（P1-5 で削除）
   - `ProcessFallbackAsync`, `GetBackendForLanguage`（P1-5 で削除）
   - `[Obsolete]` コンストラクタ（P1-6 で削除）

**MultilingualPhonemizerOptions 変更**:

**ファイル**: `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/MultilingualPhonemizerOptions.cs`

- `Handlers` プロパティ追加: `public Dictionary<string, ILanguageG2PHandler> Handlers { get; set; }`
- 既存の個別エンジンプロパティ（`JaPhonemizer`, `EnEngine` 等）は本タスクでは削除しない（P1-5/P1-6 で削除）

**PiperTTS.Inference.cs 変更**:

**ファイル**: `Assets/uPiper/Runtime/Core/PiperTTS.Inference.cs` (L81-88)

```csharp
// Before (v1.4.0)
var phonemizerOptions = new MultilingualPhonemizerOptions
{
    Languages = supportedLanguages,
    DefaultLatinLanguage = _config.DefaultLanguage ?? "en",
    JaPhonemizer = _phonemizer as DotNetG2PPhonemizer
};

// After (v2.0 P1-4)
var handlers = new Dictionary<string, ILanguageG2PHandler>();
if (_phonemizer is DotNetG2PPhonemizer jaPhonemizer)
    handlers["ja"] = new JapaneseG2PHandler(jaPhonemizer);

var phonemizerOptions = new MultilingualPhonemizerOptions
{
    Languages = supportedLanguages,
    DefaultLatinLanguage = _config.DefaultLanguage ?? "en",
    Handlers = handlers,
};
```

### Step 4: 既存テスト移行（1.0 人日）

4テストファイル + StubG2PHandler 新設。

1. **StubG2PHandler 作成**: `ILanguageG2PHandler` 実装のテストスタブ（`StubPhonemizerBackend` の置き換え）
2. **MultilingualPhonemizerTests.cs**: `[Obsolete]` コンストラクタ → Options + Handlers 経由に変更
3. **MultilingualPhonemizerDeepTests.cs**: `CreateInitialized` ヘルパーを Handlers 経由に変更
4. **MultilingualPhonemizerEosTests.cs**: `StubPhonemizerBackend` → `StubG2PHandler` に置換
5. **MultilingualPhonemizerPhase5Tests.cs**: 個別エンジン注入 → Handlers 経由に変更
6. 全 `#pragma warning disable CS0618` の除去（Options 版に移行するため不要に）

### Step 5: レガシー API クリーンアップ（0.5 人日）

**注意**: 本ステップの大部分は P1-5/P1-6 のスコープと重複する。P1-4 では以下のみ実施:

- `MultilingualPhonemizerOptions` の個別エンジンプロパティに `[Obsolete("Use Handlers dictionary. Removed in P1-5/P1-6.")]` を付与
- `MultilingualPhonemizer` の `[Obsolete]` コンストラクタ内部で `Handlers` Dictionary への変換ブリッジを追加（後方互換の過渡期対応）

---

## 3. エージェントチームの役割と人数

### 推奨構成: エージェント 2名

| エージェント | 担当 | 並行フェーズ |
|------------|------|------------|
| **Agent A（メイン）** | Step 1-3: インターフェース定義、7ハンドラ実装、MultilingualPhonemizer 書き換え | Step 1-2 を並行で進行可（インターフェース定義完了後にハンドラ実装開始） |
| **Agent B（テスト）** | Step 4: 既存テスト移行、StubG2PHandler 作成、新規ハンドラ単体テスト | Agent A の Step 2 完了後に開始。Step 3 完了後にテスト実行 |

### 並行実行の計画

```
Time ──────────────────────────────────────────────────>

Agent A: [Step1: IF定義 0.5d] → [Step2: 7ハンドラ 2.0d] → [Step3: MP書換 1.5d] → [Step5: Obsolete 0.5d]
Agent B:                         [テスト設計 -----] → [Step4: テスト移行 1.0d    ]

合計クリティカルパス: 4.5 人日 (Step1 + Step2 + Step3 + Step5)
Agent B は Step2 完了後に合流し、Step3 と並行でテスト実装。
```

### 単独エージェントの場合

1名で順次実施する場合は Step 1 -> 2 -> 3 -> 4 -> 5 の順。5.5 人日。

---

## 4. 提供範囲とテスト項目

### 4.1 提供範囲

#### スコープ内

- `ILanguageG2PHandler` インターフェース定義
- `G2PHandlerUtils` static クラス（`ExtractProsodyArrays` 移動）
- 7言語ハンドラクラスの実装（`JapaneseG2PHandler` 〜 `KoreanG2PHandler`）
- `MultilingualPhonemizer` の switch 文 -> Dictionary lookup 置換
- `MultilingualPhonemizerOptions` への `Handlers` プロパティ追加
- `PiperTTS.Inference.cs` の Options 生成変更
- 既存テストの Options/Handlers 経由への移行
- 各ハンドラの単体テスト追加
- `StubG2PHandler` テストスタブ作成
- `TonePuaChars` の `ChineseG2PHandler` への移動

#### スコープ外（後続タスクで対応）

- `IPhonemizerBackend` インターフェース削除 -> **P1-5**
- `ProcessFallbackAsync` / `GetBackendForLanguage` 削除 -> **P1-5**
- `MultilingualPhonemizerOptions` の `EnPhonemizer` / `KoPhonemizer` 削除 -> **P1-5**
- `[Obsolete]` 14引数コンストラクタの削除 -> **P1-6**
- `MultilingualPhonemizerOptions` の個別エンジンプロパティ（`JaPhonemizer` 等）の削除 -> **P1-6**
- `_ownsXxx` フラグの `HandlerEntry` struct 化（`IsOwned` プロパティ） -> **P1-3**
- `PuaTokenMapper` インスタンスクラス化 -> **P1-1**（独立タスク、M1 で並行実施）

### 4.2 Unitテスト

テストファイル配置: `Assets/uPiper/Tests/Editor/Phonemizers/Handlers/`

#### 4.2.1 全ハンドラ共通テスト（7ファイル x 7メソッド = 49テスト）

| テストクラス | テストメソッド | 検証内容 |
|------------|--------------|---------|
| `JapaneseG2PHandlerTests` | `LanguageCode_ReturnsJa` | `LanguageCode == "ja"` |
| 〃 | `IsInitialized_BeforeInit_ReturnsFalse` | デフォルトコンストラクタ後 `IsInitialized == false` |
| 〃 | `InitializeAsync_SetsIsInitializedTrue` | 初期化後 `IsInitialized == true` |
| 〃 | `Process_ValidText_ReturnsAlignedArrays` | `Phonemes.Length == A1.Length == A2.Length == A3.Length` |
| 〃 | `Process_EmptyText_ReturnsEmptyArrays` | 空文字列で長さ0の配列が返る |
| 〃 | `Dispose_CalledTwice_DoesNotThrow` | 二重 Dispose で例外なし |
| 〃 | `Dispose_ExternalEngine_DoesNotDisposeEngine` | 外部注入コンストラクタ使用時、Dispose でエンジンが生存 |

上記パターンを `EnglishG2PHandlerTests`, `SpanishG2PHandlerTests`, `FrenchG2PHandlerTests`, `PortugueseG2PHandlerTests`, `ChineseG2PHandlerTests`, `KoreanG2PHandlerTests` にも適用。

#### 4.2.2 言語固有テスト

| テストクラス | テストメソッド | 検証内容 |
|------------|--------------|---------|
| `JapaneseG2PHandlerTests` | `Process_LeadingPadStripped` | 先頭 `"_"` が除去されている |
| `EnglishG2PHandlerTests` | `Process_ProsodyA1AllZero` | 全ての A1 値が 0 |
| `ChineseG2PHandlerTests` | `Process_TonePuaInserted` | トーンPUA文字（`\ue046`-`\ue04a`）が音素列に含まれる |
| `ChineseG2PHandlerTests` | `Process_SyllableDistribution_CorrectCount` | 音節分配が正しい（PUA音素数 + トーンマーカー数 = 総音素数） |
| `KoreanG2PHandlerTests` | `Process_ProsodyLengthMismatch_LogsWarning` | PUA/Prosody 長不一致時に警告ログが出力される |
| `SpanishG2PHandlerTests` | `Process_ProsodyExtracted` | Prosody A2 に stress 値（0 or 2）が含まれる |
| `FrenchG2PHandlerTests` | `Process_UsesToPuaPhonemes` | `ToPuaPhonemes()` の結果が音素として返される |
| `PortugueseG2PHandlerTests` | `Process_SamePatternAsFrench` | FR と同一パターン（`ToPuaPhonemes` 使用）の確認 |

#### 4.2.3 G2PHandlerUtils テスト

| テストクラス | テストメソッド | 検証内容 |
|------------|--------------|---------|
| `G2PHandlerUtilsTests` | `ExtractProsodyArrays_CorrectExtraction` | 正常ケース |
| 〃 | `ExtractProsodyArrays_ProsodyShorterThanPhonemes_PadsWithZero` | Prosody 配列が音素数より短い場合 |
| 〃 | `ExtractProsodyArrays_EmptyProsody_ReturnsZeroArrays` | 空 Prosody |

### 4.3 E2Eテスト

#### 4.3.1 既存統合テストの振る舞い不変確認

以下の既存テストファイルが **修正後に全てパス** することを確認:

| テストファイル | テスト数 | 確認事項 |
|--------------|--------|---------|
| `Assets/uPiper/Tests/Editor/MultilingualPhonemizerTests.cs` | 10 | Options + Handlers 経由に書き換え後、同一結果 |
| `Assets/uPiper/Tests/Editor/Phonemizers/MultilingualPhonemizerDeepTests.cs` | 20 | 3言語以上混在テストの振る舞い不変 |
| `Assets/uPiper/Tests/Editor/Phonemizers/MultilingualPhonemizerEosTests.cs` | 10 | StubG2PHandler 使用後、EOS処理の振る舞い不変 |
| `Assets/uPiper/Tests/Editor/Phonemizers/MultilingualPhonemizerPhase5Tests.cs` | 18 | Handlers 経由注入後、各言語テストの振る舞い不変 |
| `Assets/uPiper/Tests/Editor/Phonemizers/MultilingualAutoPromotionTests.cs` | - | auto-promotion テストが影響を受けないこと |

#### 4.3.2 追加統合テスト

| テストクラス | テストメソッド | 検証内容 |
|------------|--------------|---------|
| `MultilingualPhonemizerHandlerIntegrationTests` | `PhonemizeWithProsodyAsync_JaEnMixed_UsesHandlers` | 日英混在テキストが Handler 経由で正しく音素化 |
| 〃 | `PhonemizeWithProsodyAsync_UnregisteredLang_SkipsSegment` | 未登録言語のセグメントがスキップされログ出力 |
| 〃 | `InitializeAsync_CreatesDefaultHandlers_ForUnregisteredLanguages` | Handlers 未指定の言語に対してデフォルトハンドラが生成される |
| 〃 | `Dispose_DisposesAllHandlers` | 全ハンドラの Dispose が呼ばれる |

---

## 5. 懸念事項とレビュー項目

### 5.1 技術的懸念

| 懸念 | リスク | 緩和策 |
|------|--------|--------|
| **ChineseG2PHandler の78行ロジック移植** | 音節分配・トーンPUA挿入の回帰バグ | `ProcessChinese` の既存テスト（Phase5Tests の中国語テスト）を移行前に全パス確認。移植後に同一入出力を検証 |
| **PiperTTS.Inference.cs の DotNetG2PPhonemizer キャスト** | `_phonemizer as DotNetG2PPhonemizer` が null の場合の挙動 | `JapaneseG2PHandler` のデフォルトコンストラクタによる自動生成フォールバックがある。null 時は `handlers` に "ja" が登録されず、`InitializeAsync` でデフォルトハンドラが生成される |
| **P1-1 とのマージコンフリクト** | P1-1（PuaTokenMapper）と P1-4 は `MultilingualPhonemizer.cs` を同時変更する可能性 | P1-1 は `PhonemeEncoder` 側がメイン。P1-4 は `MultilingualPhonemizer` がメイン。マージ時にコンフリクト解決が必要だが影響は限定的 |
| **WebGL の `#if` 分岐** | `JapaneseG2PHandler.InitializeAsync` 内の `#if UNITY_WEBGL && !UNITY_EDITOR` | 既存の `MultilingualPhonemizer.InitializeAsync` と同一ロジック。プリプロセッサ分岐をそのまま移植 |
| **テストスタブの大量書き換え** | 4テストファイルの `[Obsolete]` コンストラクタ使用箇所（約35箇所） | 機械的な書き換え。Options + Handlers パターンへの統一で可読性向上 |

### 5.2 レビューチェックリスト

- [ ] `ILanguageG2PHandler` のメソッドシグネチャが設計ドキュメント通りか
- [ ] 全7ハンドラの `Process` メソッドが対応する `ProcessXxx` と同一ロジックか（差分なし）
- [ ] `ChineseG2PHandler.Process` の音節分配ロジックが `ProcessChinese` と完全一致するか（78行の忠実な移植）
- [ ] `TonePuaChars` が `ChineseG2PHandler` に移動し、`MultilingualPhonemizer` から削除されているか
- [ ] `ExtractProsodyArrays` が `G2PHandlerUtils` に移動し、ES/FR/PT ハンドラから参照されているか
- [ ] `MultilingualPhonemizer` の switch 文が完全に `_handlers` Dictionary lookup に置換されているか
- [ ] `ProcessFallbackAsync` / `GetBackendForLanguage` / `_enPhonemizer` / `_koPhonemizer` が **残存している** こと（P1-5 のスコープ）
- [ ] `[Obsolete]` 14引数コンストラクタが **残存している** こと（P1-6 のスコープ）
- [ ] `PiperTTS.Inference.cs` が `JapaneseG2PHandler(phonemizer)` 経由で注入しているか
- [ ] 各ハンドラの `_ownsEngine` フラグが正しく機能するか（外部注入時 = `false`、自動生成時 = `true`）
- [ ] `StubG2PHandler` が `ILanguageG2PHandler` を正しく実装しているか
- [ ] Assembly Definition への変更が不要であることの確認（`uPiper.Runtime.asmdef` 範囲内）
- [ ] `dotnet format --verify-no-changes` パス

---

## 6. 一から作り直すとしたら

### 6.1 現設計が抱える構造的妥協

本チケットの設計は「v1.4.0 の switch + ProcessXxx パターンからの段階的移行」を前提としている。この前提自体が設計上の妥協をいくつか含む:

1. **レガシー互換層の残存**: P1-5/P1-6 で削除予定の `IPhonemizerBackend`、`ProcessFallbackAsync`、`[Obsolete]` コンストラクタが P1-4 完了後も残存する。これにより、P1-4 単体では `MultilingualPhonemizer` のコードが「新旧混在」の状態になり、リファクタリングの効果が半減する。v2.0 リリース時にはクリーンになるが、開発期間中はテストの `#pragma warning disable` が残り続ける。

2. **ハンドラ内部の所有権管理が冗長**: 各ハンドラが `_ownsEngine` フラグを持ち、Dispose 時に条件分岐する。これは P1-3 の `HandlerEntry` struct 導入で解消される予定だが、P1-4 単体では所有権管理が「ハンドラ内部のフラグ」と「MultilingualPhonemizer の `_handlers` Dictionary」の2層に分散する。

3. **InitializeAsync の責務分散**: ハンドラごとに `InitializeAsync` を持つ設計だが、実際に非同期初期化が必要なのは日本語（WebGL の MeCab 辞書）と英語（CMUdict ファイル存在チェック）のみ。残り5言語は `Task.CompletedTask` を返す no-op である。「全ハンドラが一律に `InitializeAsync` を持つ」設計は一貫性があるが、実態との乖離が大きい。

### 6.2 ゼロベース設計: 所有権を外に出す

ゼロから設計するなら、ハンドラは「純粋な処理ロジック」のみを担い、ライフサイクル管理は外部（Composition Root）が行う:

```csharp
// ハンドラは処理に専念。IDisposable を継承しない。
public interface ILanguageG2PHandler
{
    string LanguageCode { get; }
    G2PResult Process(string text);
}

// ライフサイクル管理は Composition Root が担う
public sealed class LanguageHandlerRegistry : IDisposable
{
    private readonly Dictionary<string, OwnedHandler> _handlers;

    public void Register(string lang, ILanguageG2PHandler handler, bool ownsLifecycle) { ... }
    public ILanguageG2PHandler Get(string lang) => _handlers[lang].Handler;
    public void Dispose() { /* ownsLifecycle == true のハンドラのみ Dispose */ }
}
```

**利点**: ハンドラのテストが完全に分離される。テスト時にモックを渡す際、所有権フラグの設定が不要。ハンドラ自体がステートレスに近づく。

**不採用の理由**: 現実的には、各ハンドラが内部にエンジンインスタンスを保持する必要があり（`DotNetG2PPhonemizer`, `EnglishG2PEngine` 等）、完全なステートレス化は困難。また、`LanguageHandlerRegistry` を導入すると P1-3 の Dictionary Registry 化と責務が重複する。段階的移行を重視する現設計では、ハンドラ内部に所有権管理を留めた方がマージコンフリクトが少ない。

### 6.3 Attribute-based Auto-Discovery の検討

もしゼロからなら、Open/Closed Principle を徹底し、言語追加時に既存コードの変更を不要にする設計も考えられる:

```csharp
[LanguageHandler("ja")]
public class JapaneseG2PHandler : ILanguageG2PHandler { ... }

// 起動時にリフレクションで自動発見
var handlers = Assembly.GetExecutingAssembly()
    .GetTypes()
    .Where(t => t.GetCustomAttribute<LanguageHandlerAttribute>() != null)
    .ToDictionary(
        t => t.GetCustomAttribute<LanguageHandlerAttribute>().LanguageCode,
        t => (ILanguageG2PHandler)Activator.CreateInstance(t));
```

| 観点 | Auto-Discovery | 現設計（Dictionary + Factory） | 判断 |
|------|---------------|-------------------------------|------|
| 言語追加の容易さ | クラス追加のみ | ファクトリに1行追加 | 7言語固定のため差は微小 |
| パフォーマンス | リフレクションコスト | Dictionary lookup | IL2CPP ではリフレクション制約あり |
| Unity IL2CPP 互換 | `link.xml` で strip 対策が必須 | 問題なし | **決定打**: Unity 制約が重い |
| コンストラクタ DI | `Activator.CreateInstance` ではコンストラクタ引数を渡せない | Options で自由に注入可能 | DI の柔軟性で劣る |

**不採用理由**: IL2CPP の managed code stripping が Auto-Discovery の最大の障壁。`link.xml` で全ハンドラクラスを preserve 指定する必要があり、「クラス追加だけで完了」という利点が消える。さらに、`Activator.CreateInstance` ではコンストラクタ引数（外部注入された `DotNetG2PPhonemizer` 等）を渡せないため、ファクトリメソッドとの併用が必要になり、複雑度が増す。

### 6.4 piper-plus との設計思想の違い

piper-plus は Rust で実装されており、言語ハンドラは trait object（`Box<dyn LanguageProcessor>`）として管理される。Rust の所有権システムが「誰がリソースを解放するか」を型レベルで強制するため、`_ownsEngine` のような実行時フラグは不要である。

C# + Unity 環境では:
- **`IDisposable` の呼び出しは規約ベース** であり、呼び忘れは実行時にリソースリークとして現れる（コンパイルエラーにならない）
- **Unity の `Object.Destroy` と `IDisposable.Dispose` が二重のライフサイクル管理** を要求し、ネイティブリソース（MeCab 辞書等）の解放タイミングが複雑になる
- **`GC.SuppressFinalize` の呼び忘れ** がファイナライザスレッドからの二重解放を引き起こすリスクがある

この差異を踏まえると、C# 側で所有権管理を完全にゼロコストにすることは言語仕様上困難であり、`_ownsEngine` フラグによる明示的管理は実用的な妥協である。ただし、P1-3 で `HandlerEntry` struct に所有権を集約することで、フラグの散在は解消される予定。

### 6.5 ProcessXxx 移植の正直なリスク

7つの `ProcessXxx` メソッドをハンドラクラスに「忠実に移植」する方針だが、これには見えにくいリスクがある:

1. **中国語ハンドラの78行ロジック**: `ProcessChinese` の音節分配 + トーンPUA挿入ロジックは、移植時に「変数名のリネーム」や「this 参照の追加」を行う過程でオフバイワンエラーが混入しやすい。特に `phonemesPerSyllable + (syl < remainder ? 1 : 0)` の分配ロジックは、テスト入力の網羅性が不十分だと回帰バグを見逃す。

2. **スペイン語の微妙な非対称性**: `ProcessSpanish` は `ToIpaWithProsody().Phonemes` を音素として使用するが、`ProcessFrench` / `ProcessPortuguese` は `ToPuaPhonemes()` を使用する。この3言語が「同一パターン」として G2PHandlerUtils を共有する設計だが、音素取得元の違いはハンドラ内部に隠蔽される。レビュー時にこの非対称性を見落とすと、スペイン語だけ IPA 音素が返る（PUA 変換されない）バグが残存するリスクがある。

3. **テストの「同一入出力」検証の限界**: 移植前後で「同じ入力に対して同じ出力」を検証するが、`MultilingualPhonemizer` 内での EOS トリム処理（L383-396）はハンドラの外側で行われるため、ハンドラ単体テストではカバーされない。統合テスト（MultilingualPhonemizerTests）でのみ検証可能な振る舞いが存在する。

### 6.6 見送った代替案

| 代替案 | 検討内容 | 見送り理由 |
|--------|---------|-----------|
| **基底抽象クラス `LanguageG2PHandlerBase`** | `InitializeAsync` の no-op デフォルト実装、`Dispose` パターンの共通化 | 共通ロジックが `ExtractProsodyArrays` 1つ + no-op `InitializeAsync` のみ。7クラス中5クラスが no-op を override するだけの thin wrapper になる。YAGNI |
| **`Process` を async にする** | 将来的に WebGL で全言語の辞書が非同期読み込みになる可能性 | 現在の7言語は全て同期。P1-5 で `ProcessFallbackAsync` を廃止する方向性と矛盾。async の ValueTask 化を検討するなら Phase 2 以降 |
| **ハンドラのジェネリック化 (`ILanguageG2PHandler<TEngine>`)** | エンジン型をジェネリックパラメータとして注入 | JA の先頭PAD除去、ZH のトーンPUA挿入、KO のProsody長不一致フォールバック等、言語固有ロジックが大きく異なる。ジェネリックで吸収できる共通性が乏しい |
| **Enum-based dispatch (`LanguageCode` enum)** | string の typo を防ぐ型安全性 | piper-plus のモデル設定が `"ja"`, `"en"` 等の string ベース。enum 変換層が増える上、新言語追加時に enum の拡張が必要になり OCP に反する |
| **P1-3 と P1-4 の同一 PR 実施** | Registry 化とハンドラ分離を一括実施 | PR が 30+ ファイル変更となりレビュー負荷が過大。P1-4 の7ハンドラ実装だけで十分に大きい（5.5人日）。P1-3 は P1-4 完了後の薄い追加レイヤー |
| **`G2PResult` record struct の導入** | `(string[], int[], int[], int[])` タプルを型付き構造体に置換 | P2-2（Prosody フラット配列化）で `int[] ProsodyFlat` に統合予定。現時点で中間型を導入すると二重の破壊的変更になる |

### 6.7 Phase 1 全体のゼロベース設計考察

P1-1（PuaTokenMapper インスタンス化）と P1-4（ILanguageG2PHandler）を含む Phase 1 全体（P1-1 ~ P1-6）を、個別タスクではなく一つの統合リファクタリングとして設計する場合を考察する。（P1-1 セクション 6.6、P1-2 セクション 6.5、P1-3 セクション 6.6、P1-5 セクション 6.5、P1-6 セクション 6.6 と相互参照）

#### DI 戦略の統一

P1-1 は PuaTokenMapper を Composition Root（`PiperTTS`）からコンストラクタ注入する設計。P1-4 は `MultilingualPhonemizerOptions.Handlers` Dictionary 経由でハンドラを注入する設計。この2つの DI パターンは異なっている:

| 項目 | P1-1（PuaTokenMapper） | P1-4（ILanguageG2PHandler） |
|------|----------------------|---------------------------|
| 注入方式 | コンストラクタパラメータ | Options オブジェクトの Dictionary プロパティ |
| 注入元 | `PiperTTS`（Composition Root） | `PiperTTS.Inference.cs`（同じ Composition Root） |
| null 時の挙動 | `ArgumentNullException` | `InitializeAsync` でデフォルト生成 |
| テスト時 | `new PhonemeEncoder(config, new PuaTokenMapper())` | `Options.Handlers = new Dictionary<...> { ["ja"] = stub }` |

**統合するなら**: `PiperTTS` が `PuaTokenMapper` と `Dictionary<string, ILanguageG2PHandler>` の両方を生成し、`MultilingualPhonemizer` と `PhonemeEncoder` に注入する一元的な Composition Root パターンにできる。しかし現実的には:
- `PuaTokenMapper` は `PhonemeEncoder` が消費し、`MultilingualPhonemizer` は直接参照しない（DotNetG2P エンジンが内部で PUA 変換済み出力を返すため）
- ハンドラ Dictionary は `MultilingualPhonemizer` のみが消費し、`PhonemeEncoder` は無関係

つまり、2つの依存は消費者が異なるため、DI パターンを無理に統一する必要はない。Options 経由の注入は「複数の依存を名前付きで渡す」ケースに適しており、コンストラクタ注入は「単一の依存を渡す」ケースに適している。現設計のパターン混在は合理的である。P1-3 セクション 6.6 でも同じ結論に至っている。

#### テスタビリティ最優先の設計

テスト容易性を最優先にするなら、インターフェース境界は以下に引くのが最適:

1. **`ILanguageG2PHandler`**（本チケット）: 言語別処理のモック化。最も効果が高い。
2. **`IPuaTokenMapper`**: PhonemeEncoder テストで PUA マッピングをモック可能にする。ただし P1-1 セクション 6.3 案 B で YAGNI と判断済み。PhonemeEncoder テストは実データの PuaTokenMapper で十分動作するため、モック化の実需がない。
3. **`ILanguageDetector`**: 言語検出のモック化。Phase 4（P4-1）で N-gram 検出器との差し替えが必要になった時点で導入が妥当。
4. **`IPhonemeEncoder`**: エンコーダのモック化。TTSSynthesisOrchestrator のテストで有用だが、現時点ではオーバーエンジニアリング。

現設計では (1) のみを導入する。(2)-(4) は各フェーズの必要性が顕在化した時点で追加する YAGNI 方針。この判断は、Unity プロジェクトでは「インターフェース1つ追加 = asmdef の依存解決 + IDE のインデックス更新 + リファクタリングコスト」が非ゼロであるという現実的な制約に基づく。P1-5 セクション 6.5 で整理した通り、Phase 1 のインターフェース戦略は「必要なものだけ導入し（`ILanguageG2PHandler`）、不要なものは廃止する（`IPhonemizerBackend`）」最小主義で一貫している。

#### テストヘルパーの横断設計

P1-4 で導入する `StubG2PHandler` は、P1-3 / P1-5 / P1-6 の3チケットにわたって共用される。一から設計するなら、`StubG2PHandler` の配置先を最初から共通テストヘルパーディレクトリにすべき（P1-3 セクション 6.6 参照）:

```
Tests/Editor/Helpers/
  StubG2PHandler.cs          // P1-4 で導入
  PhonemizerTestHelper.cs    // CreatePhonemizer ファクトリ（P1-6 で活用）
```

さらに、P1-6 セクション 6.4 で指摘されている `CreatePhonemizer(params string[] languages)` テストファクトリは、P1-4 の時点で導入していれば P1-6 の46箇所の書き換えコストを大幅に削減できた。Phase 1 全体の書き換え量（98箇所以上、P1-1 セクション 6.6 参照）を考慮すると、テストヘルパーの早期導入は ROI が高い。

#### P1-1 ~ P1-6 を統合した場合のアーキテクチャ

6つのタスクを一括実施するなら、以下の構造が最終形になる:

```
PiperTTS (Composition Root)
  ├─ new PuaTokenMapper(puaJsonData)           // P1-1 + P1-2 統合
  ├─ new PhonemeEncoder(config, tokenMapper)    // P1-1
  ├─ new MultilingualPhonemizer(options)        // P1-4 + P1-3 + P1-5 + P1-6
  │     ├─ Dictionary<string, HandlerEntry>     // P1-3
  │     │   ├─ JapaneseG2PHandler               // P1-4
  │     │   ├─ EnglishG2PHandler                // P1-4
  │     │   └─ ...（7言語）                      // P1-4
  │     └─ (IPhonemizerBackend なし)            // P1-5
  └─ TTSSynthesisOrchestrator(encoder, ...)
```

この最終形から逆算すると、最適なタスク分割は P1-6 セクション 6.6 で分析した5タスク案:

| タスク | 内容 | 見積もり |
|-------|------|---------|
| P1-A | PuaTokenMapper インスタンス化（P1-1） | 1.5 人日 |
| P1-B | pua.json ランタイム読み込み（P1-2） | 1 人日 |
| P1-C | ILanguageG2PHandler 全面移行（P1-4） | 5.5 人日 |
| P1-D | Dictionary Registry 化（P1-3） | 2 人日 |
| P1-E | レガシー API 全廃（P1-5 + P1-6 統合） | 1.5 人日 |

しかし P1-4 と P1-3 の統合（7.5 人日、P1-3 セクション 6.6 参照）は PR サイズが大きすぎるため、6タスク分割を維持。P1-5 + P1-6 の統合は実行可能だが、安全性を重視して分離を維持する（P1-5 セクション 6.5 参照）。

#### piper-plus 互換性の Phase 1 横断整理

Phase 1 の各タスクと piper-plus の設計思想との関係を横断的に整理する（P1-2 セクション 6.5 参照）:

| チケット | piper-plus との関係 | C# 固有の要件 |
|---------|-------------------|-------------|
| P1-1 | piper-plus は module-level dict で問題なし | Unity テストランナーの AppDomain 共有問題の解消 |
| P1-2 | **pua.json 共有で single source of truth に参加** | WebGL の非同期ファイル I/O 対応 |
| P1-3 | piper-plus に直接対応するパターンなし | `_ownsEngine` フラグの集約（Rust は所有権システムで型安全に管理） |
| P1-4 | piper-plus の `trait LanguageProcessor` に概念対応 | `IDisposable` による手動ライフサイクル管理 |
| P1-5 | piper-plus は最初から同期 G2P | C# の `async`/`await` 機構の不要コスト除去 |
| P1-6 | piper-plus に Deprecation サイクルの概念なし | SemVer major bump での API 整理 |

P1-4 の `ILanguageG2PHandler` と piper-plus の `trait LanguageProcessor` は概念的に対応するが、実装の詳細は大きく異なる（セクション 6.4 参照）。特に所有権管理は、Rust の型レベル保証 vs C# の実行時フラグという根本的な差異がある。この差異は P1-3 の `HandlerEntry.IsOwned` フラグで pragmatic に対処する（P1-3 セクション 6.4 参照）。

#### 現設計の正直な弱点

1. **中間状態の長期残存**: P1-4 完了から P1-5/P1-6 完了まで、`MultilingualPhonemizer` は「ハンドラ Dictionary + レガシー IPhonemizerBackend + Obsolete コンストラクタ」の三重構造を持つ。この中間状態でバグが見つかった場合、原因の切り分けが困難になる。P1-5 セクション 6.5 で分析した通り、P1-5 + P1-6 を統合すれば中間状態の滞留期間を短縮できるが、現計画では安全性を優先して分離を維持。
2. **CreateDefaultHandler ファクトリの switch 文**: ハンドラ分離によって `PhonemizeWithProsodyAsync` 内の switch は消えるが、`CreateDefaultHandler` 内に新たな switch 文が生まれる。OCP の観点では switch 文の移動に過ぎない。ただし、ファクトリの switch は「初期化時に1回」なので、ランタイムパスの分岐削減という効果は得られる。
3. **7ハンドラの重複コード**: `InitializeAsync` の no-op パターン（5言語）、`_ownsEngine` フラグによる Dispose パターン（7言語共通）がボイラープレートとして7ファイルに分散する。基底クラスを入れない判断（YAGNI、セクション 6.6 参照）の代償。P1-3 で `HandlerEntry.IsOwned` に所有権を移行した後は `_ownsEngine` フラグが7ハンドラから消滅し、Dispose のボイラープレートは解消される。
4. **テスト書き換えの構造的コスト**: Phase 1 全体で98箇所以上のテスト書き換えが発生（P1-1: 17, P1-4: 35, P1-6: 46）。テストヘルパーの早期導入（上記参照）により軽減可能だったが、現計画では各チケットで個別に書き換える方針。

#### Phase 1 完了後の理想 vs 現実

**理想**: 6タスクが一括実施され、`MultilingualPhonemizer` は一度のリファクタリングで最終形に到達。`PuaTokenMapper` は pua.json から初期化される完全なインスタンスクラス。`IPhonemizerBackend` と Obsolete コンストラクタは最初から存在しない。テストは `StubG2PHandler` + `CreatePhonemizer` ファクトリで DRY に記述。

**現実の妥協**: 6タスクの段階的移行により、M1 → M2 の間に複数の中間状態が発生。特に P1-4 完了後の `MultilingualPhonemizer` は新旧混在の三重構造を持つ。各チケットの「一から作り直すとしたら」セクションで指摘された理想的設計（P1-1 + P1-2 統合、P1-4 + P1-3 統合、P1-5 + P1-6 統合）は、PR サイズとレビュー負荷の制約により見送られた。ただし、これらの妥協点は全て Phase 1 完了時に解消される設計であり、M2 完了後の codebase は理想に近い状態に到達する。

---

## 7. 後続タスクへの連絡事項

### P1-3（Dictionary Registry 化）への連絡

- P1-4 完了時点で `MultilingualPhonemizer` は既に `Dictionary<string, ILanguageG2PHandler> _handlers` を保持している
- P1-3 の主な残作業は `HandlerEntry` struct（`ILanguageG2PHandler` + `IsOwned` bool）の導入と、`MultilingualPhonemizerOptions` の個別エンジンプロパティ削除
- **重要**: P1-4 では各ハンドラ内部の `_ownsEngine` フラグで所有権を管理している。P1-3 で `HandlerEntry.IsOwned` に移行する場合、ハンドラの `Dispose` ロジック内の `_ownsEngine` チェックを `HandlerEntry` 側に移す必要がある
- P1-4 の `CreateDefaultHandler` ファクトリメソッドは P1-3 でそのまま再利用可能

### P1-5（G2P 全同期化 / IPhonemizerBackend 廃止）への連絡

- P1-4 完了後、`MultilingualPhonemizer` には以下のレガシーコードが意図的に残存している:
  - `_enPhonemizer` フィールド（`IPhonemizerBackend`）
  - `_koPhonemizer` フィールド（`IPhonemizerBackend`）
  - `ProcessFallbackAsync` メソッド
  - `GetBackendForLanguage` メソッド
  - switch 文の `default` ケースでの `ProcessFallbackAsync` 呼び出し
- P1-5 ではこれらを全て削除し、`default` ケースを「警告ログ + `continue`」に置換する
- `MultilingualPhonemizerEosTests.cs` の `StubPhonemizerBackend` は P1-4 で `StubG2PHandler` に移行済みだが、一部テストが `IPhonemizerBackend` ベースのスタブをまだ使用している可能性がある。P1-5 でこれらを完全に掃除すること
- `PhonemeResult` / `PhonemeOptions` は `Backend/` ディレクトリに残存する（他の用途で使用）。P1-5 では `IPhonemizerBackend.cs` と `PhonemizerBackendOptions.cs` のみ削除

### P1-6（Obsolete コンストラクタ削除）への連絡

- P1-4 完了後、`MultilingualPhonemizer` の14引数 `[Obsolete]` コンストラクタ（L114-142）は残存している
- P1-6 ではこのコンストラクタを削除し、`MultilingualPhonemizerOptions` 経由のコンストラクタのみ残す
- テスト内の `#pragma warning disable CS0618` の残存箇所は P1-4 の Step 4 で大半が除去済みだが、`ProcessFallbackAsync` 経由の `_enPhonemizer` / `_koPhonemizer` 参照が残っている可能性がある。P1-5 完了後に P1-6 を実施するのが安全
- `InferenceEngineDemo.cs`（`Runtime/Demo/`）も Options 版への書き換えが必要
- `MultilingualPhonemizerOptions` の個別エンジンプロパティ（`JaPhonemizer`, `EnEngine`, `EsEngine`, `FrEngine`, `PtEngine`, `ZhEngine`, `KoG2PEngine`）は P1-6 で削除。P1-4 で付与した `[Obsolete]` アノテーションを確認すること