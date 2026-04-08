# P1-1: PuaTokenMapper インスタンスクラス化

**マイルストーン**: M1 - Foundation Start
**優先度**: P1
**見積もり**: 1.5 人日
**依存チケット**: なし（独立チェーン）
**後続チケット**: P1-2（pua.json ランタイム読み込み）
**ブランチ名**: `feature/v2.0-P1-1-puatokenmapper-instance`

---

## 1. タスク目的とゴール

`PuaTokenMapper` を `static class` から `sealed class`（通常のインスタンスクラス）に変更する。

**解決する問題**:

1. **テスト間状態リーク**: `Token2Char` / `Char2Token` / `_nextDynamic` がプロセスグローバルな static フィールドであり、あるテストの動的登録が別テストに漏れる。現状テストではユニーク接頭辞（`_test_dynamic_maptoken_unique_001` 等）で回避しているが、本質的解決ではない。
2. **ResetForTesting の脆弱性**: テスト `[SetUp]` / `[TearDown]` で `ResetForTesting()` を呼び忘れると状態が汚染される。現状のテストでは一度も呼ばれておらず、静的フィールドに依存した設計。
3. **DI 不可能**: static class であるため、`PhonemeEncoder` や `MultilingualPhonemizer` がハードコード依存。モックやスタブへの差し替えが不可。
4. **ResetOnDomainReload の重複**: `ResetForTesting` と `ResetOnDomainReload` がほぼ同一ロジック。
5. **並行テスト非対応**: 複数テストが同時に動的割り当てを行うと `_nextDynamic` が予測不能に進行。

**完了状態（Definition of Done）**:

- `PuaTokenMapper` が `new PuaTokenMapper()` でインスタンス生成可能
- `PhonemeEncoder` がコンストラクタ注入で `PuaTokenMapper` を受け取る
- `ResetForTesting()` / `ResetOnDomainReload()` が削除済み
- テストが `[SetUp]` で `_mapper = new PuaTokenMapper()` を生成するパターンに統一
- `FixedPuaMapping` と `IsFixedPua` は `static` のまま保持（不変データ・純粋関数）
- 全テスト通過（EditMode + PlayMode）
- `dotnet format --verify-no-changes` 通過

---

## 2. 実装する内容の詳細

### Step 1: PuaTokenMapper クラス変更（主作業）

対象ファイル: `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/PuaTokenMapper.cs`

1. `public static class PuaTokenMapper` を `public sealed class PuaTokenMapper` に変更
2. インスタンスフィールドに移動するメンバー:
   - `Token2Char`: `public static readonly ConcurrentDictionary<string, char>` → `private readonly ConcurrentDictionary<string, char> _token2Char`
   - `Char2Token`: `public static readonly ConcurrentDictionary<char, string>` → `private readonly ConcurrentDictionary<char, string> _char2Token`
   - `_nextDynamic`: `private static int` → `private int _nextDynamic`
   - `_dynamicLock`: `private static readonly object` → `private readonly object _dynamicLock = new()`
3. static のまま保持するメンバー:
   - `FixedPuaMapping`（`public static readonly IReadOnlyDictionary<string, int>`）
   - `LastFixedCodepoint`（`private const int`）
   - `DynamicPuaStart`（`private const int`）
   - `IsFixedPua(char ch)`（pure function）
4. static コンストラクタ → インスタンスコンストラクタに変更:
   ```csharp
   public PuaTokenMapper()
   {
       _token2Char = new ConcurrentDictionary<string, char>();
       _char2Token = new ConcurrentDictionary<char, string>();
       _nextDynamic = DynamicPuaStart;
       foreach (var kvp in FixedPuaMapping)
       {
           var ch = (char)kvp.Value;
           _token2Char[kvp.Key] = ch;
           _char2Token[ch] = kvp.Key;
       }
   }
   ```
5. `public static` メソッドを `public` インスタンスメソッドに変更: `Register`, `MapSequence`, `MapToken`, `UnmapChar`
6. プロパティ公開（読み取り専用）:
   ```csharp
   public IReadOnlyDictionary<string, char> Token2Char => _token2Char;
   public IReadOnlyDictionary<char, string> Char2Token => _char2Token;
   ```
7. 削除するメンバー:
   - `ResetForTesting()`（`new PuaTokenMapper()` で代替）
   - `ResetOnDomainReload()`（インスタンス保持側で再生成）
   - `[assembly: InternalsVisibleTo("uPiper.Tests.Editor")]`（`ResetForTesting` 廃止により不要）

### Step 2: PhonemeEncoder へのコンストラクタ注入

対象ファイル: `Assets/uPiper/Runtime/Core/AudioGeneration/PhonemeEncoder.cs`

1. コンストラクタにパラメータ追加:
   ```csharp
   // v1.x: public PhonemeEncoder(PiperVoiceConfig config)
   // v2.0:
   public PhonemeEncoder(PiperVoiceConfig config, PuaTokenMapper tokenMapper)
   ```
2. フィールド追加: `private readonly PuaTokenMapper _tokenMapper;`
3. `MapPhoneme()` メソッド内の3箇所の static 参照を更新:
   - L312: `Phonemizers.Multilingual.PuaTokenMapper.Token2Char.TryGetValue(...)` → `_tokenMapper.Token2Char.TryGetValue(...)`
   - L330: `Phonemizers.Multilingual.PuaTokenMapper.Char2Token.TryGetValue(...)` → `_tokenMapper.Char2Token.TryGetValue(...)`
   - L351: `Phonemizers.Multilingual.PuaTokenMapper.Token2Char.TryGetValue(...)` → `_tokenMapper.Token2Char.TryGetValue(...)`

### Step 3: PhonemeEncoder 生成元の更新

`PhonemeEncoder` を `new` している全箇所で `PuaTokenMapper` インスタンスを渡す。

**ランタイムコード（3箇所）**:

| ファイル | 行 | 変更内容 |
|---------|-----|---------|
| `Runtime/Core/PiperTTS.cs` | L379 | `new PhonemeEncoder(voice)` → `new PhonemeEncoder(voice, _tokenMapper)` |
| `Runtime/Core/PiperTTS.Inference.cs` | L55 | `new PhonemeEncoder(voiceConfig)` → `new PhonemeEncoder(voiceConfig, tokenMapper)` |
| `Runtime/Demo/InferenceEngineDemo.cs` | L700 | `new PhonemeEncoder(config)` → `new PhonemeEncoder(config, new PuaTokenMapper())` |

`PiperTTS` レベルで `PuaTokenMapper` インスタンスを生成し、`PhonemeEncoder` と（将来的に）`MultilingualPhonemizer` に共有する Composition Root パターンを採用。

**テストコード（13箇所）**: 各テストの `[SetUp]` で `new PuaTokenMapper()` を生成し、`new PhonemeEncoder(config, mapper)` に変更。

| テストファイル | `new PhonemeEncoder` 箇所数 |
|--------------|--------------------------|
| `PhonemeEncoderTests.cs` | 3 |
| `PhonemeEncoderMultilingualTests.cs` | 2 |
| `PhonemeEncoderMultilingualModelTests.cs` | 4 |
| `PhonemeEncoderESpeakTests.cs` | 1 |
| `PhonemeEncoderIPATests.cs` | 1 |
| `TTSSynthesisOrchestratorTests.cs` | 1 |
| `MultilingualPipelineTests.cs` | 1 |
| `MultilingualModelPipelineTests.cs` | 2 |
| `EnglishPhonemeMappingTest.cs` | 1 |
| `ProsodyInferenceIntegrationTests.cs` | 1 |

### Step 4: PuaTokenMapperTests 更新

対象ファイル: `Assets/uPiper/Tests/Editor/Phonemizers/PuaTokenMapperTests.cs`

1. `[SetUp]` 追加: `private PuaTokenMapper _mapper;` フィールド + `_mapper = new PuaTokenMapper()` 初期化
2. インスタンスメソッド/プロパティ参照への書き換え（約30箇所）:
   - `PuaTokenMapper.MapToken(...)` → `_mapper.MapToken(...)`
   - `PuaTokenMapper.MapSequence(...)` → `_mapper.MapSequence(...)`
   - `PuaTokenMapper.UnmapChar(...)` → `_mapper.UnmapChar(...)`
   - `PuaTokenMapper.Register(...)` → `_mapper.Register(...)`
   - `PuaTokenMapper.Token2Char` → `_mapper.Token2Char`
   - `PuaTokenMapper.Char2Token` → `_mapper.Char2Token`
3. 変更不要の参照:
   - `PuaTokenMapper.FixedPuaMapping` -- static readonly のまま（全12テスト）
   - `PuaTokenMapper.IsFixedPua(...)` -- static メソッドのまま（3テスト）
4. 動的登録テストからユニーク接頭辞を除去:
   - `"_test_dynamic_maptoken_unique_001"` → `"test_dynamic"` 等（テストごとにクリーンインスタンスのため不要）

### Step 5: 変更不要の確認

以下のテストファイルは `FixedPuaMapping` 参照のみのため変更不要:

- `PuaJsonCrossValidationTests.cs`（6箇所 -- 全て `PuaTokenMapper.FixedPuaMapping`）
- `ChinesePhonemizerTests.cs`（13箇所 -- 全て `PuaTokenMapper.FixedPuaMapping`）

### Step 6: ドキュメント更新

- `CLAUDE.md`: PuaTokenMapper の説明を「インスタンスクラス」に更新
- `CHANGELOG.md`: 破壊的変更として記載

---

## 3. エージェントチームの役割と人数

**1エージェント構成**（1.5人日）

| 役割 | 担当内容 | 工数 |
|------|---------|------|
| 実装エージェント | Step 1-4 の全実装 + Step 5 の確認 + Step 6 のドキュメント更新 | 1.5 人日 |

**理由**: 変更ファイルが5つ（ランタイム2 + テスト3）と少なく、全変更が機械的な static → instance 置換が中心。複数エージェントに分割するとマージコストが工数を上回る。

**推奨実施順序**: Step 1 → Step 2 → Step 3 → Step 4 → 全テスト実行 → Step 5 確認 → Step 6

---

## 4. 提供範囲とテスト項目

### 4.1 提供範囲

| カテゴリ | 範囲内 | 範囲外 |
|---------|-------|-------|
| PuaTokenMapper | インスタンスクラス化、プロパティ型変更 | `InitializeFromJsonAsync` の追加（P1-2） |
| PhonemeEncoder | コンストラクタ DI 追加、3箇所の参照更新 | 他のリファクタリング |
| PiperTTS | `PuaTokenMapper` インスタンス生成・注入パス整備 | MultilingualPhonemizer への注入（P1-2 で対応） |
| テスト | PuaTokenMapperTests のインスタンス化対応 | PhonemeEncoder テストの大規模リファクタ |
| ドキュメント | CLAUDE.md / CHANGELOG.md の最小更新 | ARCHITECTURE ドキュメントの全面書き換え |

### 4.2 Unit テスト

**PuaTokenMapperTests（既存テストの移行 + 追加）**:

| テストメソッド | 変更種別 | 内容 |
|--------------|---------|------|
| `FixedPuaMapping_ContainsAllJapaneseEntries` | 変更なし | static readonly 参照 |
| `FixedPuaMapping_ContainsAllChineseEntries` | 変更なし | static readonly 参照 |
| `FixedPuaMapping_ContainsAllKoreanEntries` | 変更なし | static readonly 参照 |
| `FixedPuaMapping_ContainsSpanishPortugueseEntries` | 変更なし | static readonly 参照 |
| `FixedPuaMapping_ContainsFrenchEntries` | 変更なし | static readonly 参照 |
| `FixedPuaMapping_ContainsSharedEntries` | 変更なし | static readonly 参照 |
| `FixedPuaMapping_ContainsAllSwedishEntries` | 変更なし | static readonly 参照 |
| `FixedPuaMapping_DoesNotContain0xE053` | 変更なし | static readonly 参照 |
| `FixedPuaMapping_DoesNotContain0xE01F` | 変更なし | static readonly 参照 |
| `FixedPuaMapping_TotalCount_Is96` | 変更なし | static readonly 参照 |
| `FixedPuaMapping_NoDuplicateCodepoints` | 変更なし | static readonly 参照 |
| `Token2Char_ContainsAllFixedMappings` | **インスタンス化** | `PuaTokenMapper.Token2Char` → `_mapper.Token2Char` |
| `Char2Token_ContainsAllFixedMappings` | **インスタンス化** | `PuaTokenMapper.Char2Token` → `_mapper.Char2Token` |
| `Token2Char_And_Char2Token_AreConsistent` | **インスタンス化** | 双方向整合性を `_mapper` 経由で検証 |
| `MapToken_FixedToken_ReturnsCorrectChar` | **インスタンス化** | `PuaTokenMapper.MapToken` → `_mapper.MapToken` |
| `MapToken_SingleCharToken_ReturnsSelf` | **インスタンス化** | 同上 |
| `MapToken_UnknownMultiCharToken_RegistersDynamically` | **インスタンス化 + 簡素化** | ユニーク接頭辞除去 |
| `MapToken_SameTokenTwice_ReturnsSameChar` | **インスタンス化 + 簡素化** | ユニーク接頭辞除去 |
| `MapSequence_EmptyList_ReturnsEmpty` | **インスタンス化** | `PuaTokenMapper.MapSequence` → `_mapper.MapSequence` |
| `MapSequence_SingleTokens_ReturnsSelf` | **インスタンス化** | 同上 |
| `MapSequence_MultiCharTokens_ReturnsPuaChars` | **インスタンス化** | 同上 |
| `MapSequence_MixedTokens_CorrectMapping` | **インスタンス化** | 同上 |
| `UnmapChar_FixedPuaChar_ReturnsToken` | **インスタンス化** | `PuaTokenMapper.UnmapChar` → `_mapper.UnmapChar` |
| `UnmapChar_NonPuaChar_ReturnsNull` | **インスタンス化** | 同上 |
| `UnmapChar_DynamicallyRegistered_ReturnsToken` | **インスタンス化 + 簡素化** | ユニーク接頭辞除去 |
| `IsFixedPua_InRange_ReturnsTrue` | 変更なし | static メソッド |
| `IsFixedPua_OutOfRange_ReturnsFalse` | 変更なし | static メソッド |
| `IsFixedPua_BoundaryValues` | 変更なし | static メソッド |
| `Register_ConcurrentCalls_NoDuplicates` | **インスタンス化** | `PuaTokenMapper.Register` → `_mapper.Register` |
| `VerifyJapanesePuaValues` | 変更なし | static readonly 参照 |
| `VerifyChinesePuaValues` | 変更なし | static readonly 参照 |
| `VerifyKoreanPuaValues` | 変更なし | static readonly 参照 |
| `VerifySwedishPuaValues` | 変更なし | static readonly 参照 |

**新規テスト（追加推奨）**:

| テストメソッド | 内容 |
|--------------|------|
| `NewInstance_HasCleanDynamicState` | 新インスタンスの `_nextDynamic` が `0xE062` から始まることを検証 |
| `TwoInstances_DynamicAllocationsAreIndependent` | 2つのインスタンスで同じトークンを Register しても互いに干渉しないことを検証 |
| `Token2Char_IsReadOnly_CannotWriteDirectly` | `Token2Char` プロパティが `IReadOnlyDictionary` であることをコンパイル時型安全で検証（実行時は不要だがドキュメント的テスト） |

### 4.3 E2E テスト

| テスト | 内容 |
|-------|------|
| `InferenceEngineDemo` 手動実行 | 6言語ドロップダウンで音声生成が正常に動作することを確認 |
| CI `unity-tests.yml` 全通過 | EditMode + PlayMode テストの GREEN 確認 |
| `PuaJsonCrossValidationTests` 変更なし確認 | pua.json とのクロスバリデーションが引き続き通過 |

---

## 5. 懸念事項とレビュー項目

### 5.1 技術的懸念

| 懸念 | 深刻度 | 対策 |
|------|--------|------|
| **FixedPuaMapping の static 初期化順序** | 低 | `static readonly` フィールドイニシャライザは CLR が保証。インスタンスコンストラクタ内の `foreach (var kvp in FixedPuaMapping)` は安全 |
| **ConcurrentDictionary の IReadOnlyDictionary キャスト** | 低 | `ConcurrentDictionary<K,V>` は `IReadOnlyDictionary<K,V>` を直接実装しているため、プロパティからそのまま返却可能。追加のアロケーションなし |
| **PhonemeEncoder コンストラクタ破壊的変更の影響範囲** | 中 | `new PhonemeEncoder(config)` の全呼び出し箇所（ランタイム3 + テスト17）を漏れなく更新する必要がある。Grep で `new PhonemeEncoder` を検索し全箇所をリスト化済み |
| **P1-4 との MultilingualPhonemizer 競合** | 中 | P1-1 は `PhonemeEncoder` 側がメイン変更対象、P1-4 は `MultilingualPhonemizer` がメイン。直接の競合は限定的だが、同時にマージする場合はコンフリクト解決が必要 |
| **DotNetG2P エンジンへの影響** | なし | 各言語エンジンは内部で `ToPuaPhonemes()` を呼び、`PuaTokenMapper` を直接参照していない。別リポジトリであり uPiper 側の責務外 |
| **パフォーマンス: インスタンス生成コスト** | 低 | コンストラクタで96エントリの辞書初期化が発生するが、TTS 初期化時に1回のみ。ベンチマーク不要レベル |

### 5.2 レビューチェックリスト

- [ ] `PuaTokenMapper` の `static class` 宣言が `sealed class` に変更されているか
- [ ] `FixedPuaMapping` と `IsFixedPua` が `static` のまま保持されているか
- [ ] `Token2Char` / `Char2Token` の公開型が `IReadOnlyDictionary` になっているか
- [ ] `ConcurrentDictionary` への書き込みが `Register()` メソッド経由のみに制限されているか
- [ ] `ResetForTesting()` / `ResetOnDomainReload()` が完全に削除されているか
- [ ] `[assembly: InternalsVisibleTo("uPiper.Tests.Editor")]` が削除されているか（他に internal メンバーがない場合）
- [ ] `PhonemeEncoder` コンストラクタに `PuaTokenMapper tokenMapper` が追加されているか
- [ ] `PhonemeEncoder._tokenMapper` に null ガード（`?? throw new ArgumentNullException`）があるか
- [ ] `MapPhoneme()` 内の3箇所が全て `_tokenMapper` 経由に更新されているか
- [ ] `PiperTTS` が Composition Root として `PuaTokenMapper` インスタンスを生成し、下流に注入しているか
- [ ] `PuaTokenMapperTests` の全インスタンスメソッドテストが `_mapper` 経由に更新されているか
- [ ] 動的登録テストからユニーク接頭辞が除去されているか
- [ ] `PuaJsonCrossValidationTests` / `ChinesePhonemizerTests` が変更されていないことを確認
- [ ] `dotnet format --verify-no-changes` が通過するか
- [ ] スレッドセーフティが維持されているか（`ConcurrentDictionary` + `lock (_dynamicLock)`）

---

## 6. 一から作り直すとしたら

### PUA マッピングという設計自体の妥当性

PUA（Private Use Area）マッピングは、複数文字の音素トークン（`ch`, `ts`, `N_m` 等）を単一の Unicode コードポイントに圧縮する仕組みである。この設計は piper-plus の Python/C++ 実装から継承されたもので、以下の前提に基づく:

- VITS モデルの `phoneme_id_map` が単一文字キーを前提とする
- 複数文字トークンをそのまま渡すと、文字単位で分解されて誤った ID にマッピングされる

**問うべき点**: もし piper-plus の制約がなく、uPiper 独自にモデル入力パイプラインを設計できるなら、PUA マッピングは不要である。トークン文字列を直接 ID にマッピングする `Dictionary<string, int>` で十分であり、中間の PUA 変換レイヤーは余分な複雑性を持ち込んでいる。

しかし現実として:
1. piper-plus のモデルとの互換性が必須（モデル再学習は範囲外）
2. DotNetG2P の各エンジンが `ToPuaPhonemes()` で PUA 変換済みの出力を返す設計
3. pua.json というデータファイルで piper-plus 側と整合性を保つ仕組みが確立済み

以上から、PUA マッピング自体を廃止する選択肢は現実的ではない。ただし、将来的に uPiper 独自モデルを学習する場合は、`phoneme_id_map` を文字列キーベースで再設計し、PUA レイヤーを省略する方が保守性が高い。

### インスタンス化のアプローチ

本チケットでは「固定マッピングは static、動的状態はインスタンス」のハイブリッド設計を採用する。一から設計するなら、以下の代替案も検討に値する:

**案 A: 完全インスタンス化（FixedPuaMapping もインスタンス）**
- メリット: テスト時に固定マッピングも差し替え可能。異なるモデル（PUA テーブルが異なる）への対応が容易。
- デメリット: 全インスタンスで96エントリの辞書が重複。メモリ効率低下。実際に固定マッピングを差し替えるユースケースが P1-2 まで存在しない。

**案 B: インターフェース抽出（`IPuaTokenMapper`）**
- メリット: テスト時にモックが容易。PhonemeEncoder のテストで PUA マッピングを完全制御可能。
- デメリット: 現時点でモック需要が薄い（PhonemeEncoder テストは実データで十分動作）。インターフェース追加はオーバーエンジニアリング。

**採用判断**: 案 A は P1-2（pua.json ランタイム読み込み）で固定マッピングをファイルから上書きする際に自然に対応可能。案 B は YAGNI と判断。現設計のハイブリッドが最も費用対効果が高い。

### PhonemeEncoder への注入方法

コンストラクタ注入以外に以下も考えられる:

**案 C: メソッド注入（`MapPhoneme` の引数に PuaTokenMapper を渡す）**
- メリット: PhonemeEncoder のコンストラクタ変更が不要。
- デメリット: 3箇所の内部メソッド呼び出しで毎回引数を渡す煩雑さ。呼び出し元の責務が増える。

**案 D: プロパティ注入（`PhonemeEncoder.TokenMapper { set; }`）**
- メリット: コンストラクタ互換性を維持できる。
- デメリット: 初期化忘れのリスク。null 状態の PhonemeEncoder が存在しうる。

**採用判断**: コンストラクタ注入が最もシンプルかつ安全。v2.0 は破壊的変更リリースであり、コンストラクタ変更のコストは許容範囲内。

---

## 7. 後続タスクへの連絡事項

### P1-2（pua.json ランタイム読み込み）への引き継ぎ

1. **PuaTokenMapper のインスタンスは `PiperTTS` が Composition Root として生成する**。P1-2 で `InitializeFromJsonAsync(string json)` / `InitializeFromJson(string json)` を追加する際は、このインスタンスに対してメソッドを呼び出す設計。
2. **コンストラクタではハードコード固定マッピング（96エントリ）で初期化される**。`InitializeFromJsonAsync` は pua.json の内容で固定マッピングを上書き（追加エントリの登録も含む）する。ファイルが存在しない場合はハードコードにフォールバック。
3. **`Token2Char` / `Char2Token` の公開型は `IReadOnlyDictionary`**。P1-2 で pua.json 読み込み後の上書きは `private` な `_token2Char` / `_char2Token` に対して行うため、内部実装で完結する。
4. **`[assembly: InternalsVisibleTo]` は本チケットで削除する**。P1-2 で internal メンバーが必要になった場合は再追加を検討。

### P1-4（ILanguageG2PHandler 全面移行）との並行作業に関する注意

- P1-1 と P1-4 は M1 で並行実行される。P1-1 は `PhonemeEncoder.cs` がメイン変更対象、P1-4 は `MultilingualPhonemizer.cs` がメイン変更対象のため直接的な競合は限定的。
- ただし、P1-4 が `MultilingualPhonemizer` に `PuaTokenMapper` 注入パスを追加する場合（P1-2 の先行準備として）、P1-1 で生成したインスタンスの受け渡し方法について事前に合意が必要。
- マージ順序: P1-1 を先にマージし、P1-4 はリベース後にマージするのが安全。P1-1 の方が変更範囲が小さく、コンフリクト解決が容易なため。

### PhonemeEncoder テストへの影響

全 PhonemeEncoder テスト（10ファイル17箇所）で `new PhonemeEncoder(config)` → `new PhonemeEncoder(config, mapper)` への変更が必要。テスト側は `[SetUp]` で `_mapper = new PuaTokenMapper()` を生成するパターンを統一すること。これにより、PhonemeEncoder テストでも PUA マッピング状態のテスト間リークが解消される。
