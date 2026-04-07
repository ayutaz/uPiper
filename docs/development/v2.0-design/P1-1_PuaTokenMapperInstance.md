# P1-1: PuaTokenMapper インスタンスクラス化 設計ドキュメント

**作成日**: 2026-04-08
**対象チケット**: v2.0-plan.md P1-1
**ステータス**: 設計レビュー待ち

---

## 1. 現状分析

### 1.1 ファイル位置

`Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/PuaTokenMapper.cs`

### 1.2 クラス宣言

```csharp
public static class PuaTokenMapper
```

`[assembly: InternalsVisibleTo("uPiper.Tests.Editor")]` でテストアセンブリに internal メンバーを公開。

### 1.3 static フィールド一覧

| フィールド | アクセス修飾子 | 型 | 分類 | 説明 |
|-----------|-------------|-----|------|------|
| `FixedPuaMapping` | `public static readonly` | `IReadOnlyDictionary<string, int>` | 固定 | 96エントリの固定マッピングテーブル（0xE000-0xE061） |
| `LastFixedCodepoint` | `private const` | `int` | 固定 | 最終固定コードポイント（0xE061） |
| `DynamicPuaStart` | `private const` | `int` | 固定 | 動的割り当て開始位置（0xE062） |
| `Token2Char` | `public static readonly` | `ConcurrentDictionary<string, char>` | **動的** | トークン→PUA文字の双方向辞書（固定+動的） |
| `Char2Token` | `public static readonly` | `ConcurrentDictionary<char, string>` | **動的** | PUA文字→トークンの双方向辞書（固定+動的） |
| `_nextDynamic` | `private static` | `int` | **動的** | 次の動的割り当てコードポイント |
| `_dynamicLock` | `private static readonly` | `object` | **動的** | 動的割り当てのロックオブジェクト |

### 1.4 static メソッド一覧

| メソッド | アクセス修飾子 | シグネチャ | 分類 |
|---------|-------------|-----------|------|
| `static PuaTokenMapper()` | - | static コンストラクタ | 初期化 |
| `Register` | `public static` | `char Register(string token)` | 動的登録 |
| `MapSequence` | `public static` | `List<char> MapSequence(IList<string> tokens)` | バッチ変換 |
| `MapToken` | `public static` | `char MapToken(string token)` | 単一変換 |
| `UnmapChar` | `public static` | `string UnmapChar(char ch)` | 逆変換 |
| `IsFixedPua` | `public static` | `bool IsFixedPua(char ch)` | 範囲判定 |
| `ResetForTesting` | `internal static` | `void ResetForTesting()` | テスト用リセット |
| `ResetOnDomainReload` | `private static` | `void ResetOnDomainReload()` | エディタ用リセット |

### 1.5 固定マッピングと動的割り当ての境界

- **固定マッピング（96エントリ）**: `FixedPuaMapping` に定義。0xE000-0xE061 の範囲（2つのギャップ: 0xE01F, 0xE053）。piper-plus の `pua.json` と完全一致が必須。モデルに焼き込まれておりコードポイント変更不可。
- **動的割り当て**: 0xE062 以降。固定マッピングに存在しない複数文字トークンが `Register()` で登録された際に逐次割り当て。上限 0xF8FF（BMP PUA 末尾）。
- **単一文字トークン**: PUA 変換不要。自分自身にマッピング。

### 1.6 問題点

1. **テスト間状態リーク**: `Token2Char` / `Char2Token` / `_nextDynamic` がプロセスグローバル。あるテストで動的登録されたトークンが別テストに影響する。
2. **ResetForTesting の脆弱性**: テスト `[SetUp]` / `[TearDown]` で `ResetForTesting()` を呼び忘れると状態が汚染される。現状テストでは呼ばれていない（テスト自体が静的フィールドに依存した設計）。
3. **ResetOnDomainReload の重複**: `ResetForTesting` とほぼ同一ロジックが `#if UNITY_EDITOR` ブロックに重複。
4. **DI 不可能**: static class のため、`PhonemeEncoder` や `MultilingualPhonemizer` がハードコード依存。モックやスタブへの差し替えが不可。
5. **並行テスト非対応**: 複数テストが同時に動的割り当てを行うと `_nextDynamic` が予測不能に進行し、テスト結果が非決定的になる。

---

## 2. 設計方針

### 2.1 基本方針

| 対象 | 方針 |
|------|------|
| `FixedPuaMapping` (96エントリ) | `static readonly` のまま保持。全インスタンスで共有（不変データ） |
| `LastFixedCodepoint`, `DynamicPuaStart` | `const` のまま保持 |
| `Token2Char`, `Char2Token` | **インスタンスフィールド**に移動 |
| `_nextDynamic`, `_dynamicLock` | **インスタンスフィールド**に移動 |
| `Register`, `MapSequence`, `MapToken`, `UnmapChar` | **インスタンスメソッド**に変更 |
| `IsFixedPua` | **static メソッド**のまま保持（純粋関数、インスタンス状態不要） |
| `ResetForTesting` | **廃止**（インスタンスを new するだけで済む） |
| `ResetOnDomainReload` | **廃止**（ドメインリロード時はインスタンス再生成） |

### 2.2 スレッドセーフティ

- `Token2Char` / `Char2Token` は引き続き `ConcurrentDictionary` を使用。
- `_nextDynamic` は `lock (_dynamicLock)` でシリアライズ。
- 1つの `PuaTokenMapper` インスタンスを複数スレッドから同時使用しても安全。

---

## 3. 新クラス設計

### 3.1 クラスシグネチャ

```csharp
namespace uPiper.Core.Phonemizers.Multilingual
{
    /// <summary>
    /// Maps multi-character phoneme tokens to single Unicode PUA codepoints.
    /// Thread-safe. Each instance maintains its own dynamic allocation state;
    /// fixed mappings (96 entries) are shared across all instances.
    /// </summary>
    public sealed class PuaTokenMapper
    {
        // ── Shared immutable data ───────────────────────────────────
        public static readonly IReadOnlyDictionary<string, int> FixedPuaMapping = ...;
        private const int LastFixedCodepoint = 0xE061;
        private const int DynamicPuaStart = 0xE062;

        // ── Instance state ──────────────────────────────────────────
        private readonly ConcurrentDictionary<string, char> _token2Char;
        private readonly ConcurrentDictionary<char, string> _char2Token;
        private int _nextDynamic;
        private readonly object _dynamicLock = new();

        // ── Constructor ─────────────────────────────────────────────
        public PuaTokenMapper() { ... }

        // ── Public instance API ─────────────────────────────────────
        public char Register(string token) { ... }
        public List<char> MapSequence(IList<string> tokens) { ... }
        public char MapToken(string token) { ... }
        public string UnmapChar(char ch) { ... }

        // ── Public read-only properties ─────────────────────────────
        public IReadOnlyDictionary<string, char> Token2Char => _token2Char;
        public IReadOnlyDictionary<char, string> Char2Token => _char2Token;

        // ── Static utility (pure function) ──────────────────────────
        public static bool IsFixedPua(char ch) { ... }
    }
}
```

### 3.2 コンストラクタ

```csharp
public PuaTokenMapper()
{
    _token2Char = new ConcurrentDictionary<string, char>();
    _char2Token = new ConcurrentDictionary<char, string>();
    _nextDynamic = DynamicPuaStart;

    // 固定マッピングを初期化
    foreach (var kvp in FixedPuaMapping)
    {
        var ch = (char)kvp.Value;
        _token2Char[kvp.Key] = ch;
        _char2Token[ch] = kvp.Key;
    }
}
```

### 3.3 プロパティ変更

v1.x の `Token2Char` / `Char2Token` は `public static readonly ConcurrentDictionary<string, char>` で、呼び出し側が直接辞書を参照していた。

v2.0 では:
- インスタンスフィールド `_token2Char` / `_char2Token` は `private`。
- 読み取り専用プロパティ `Token2Char` / `Char2Token` を `IReadOnlyDictionary` として公開。
- 外部から直接 `ContainsKey` / `TryGetValue` / indexer 参照はそのまま動作する（`IReadOnlyDictionary` が同メソッドを公開）。
- `ConcurrentDictionary` への直接書き込みアクセスは `Register()` 経由のみに制限。

### 3.4 廃止メンバー

| メンバー | 理由 |
|---------|------|
| `static PuaTokenMapper()` | static コンストラクタ不要。インスタンスコンストラクタで初期化 |
| `ResetForTesting()` | `new PuaTokenMapper()` で代替 |
| `ResetOnDomainReload()` | `RuntimeInitializeOnLoadMethod` で新インスタンスを再生成（後述） |
| `[assembly: InternalsVisibleTo]` | `ResetForTesting` 廃止により不要（他に internal メンバーがなければ） |

---

## 4. 参照箇所の更新一覧

### 4.1 ランタイムコード（2ファイル）

| ファイル | 行 | 現状の参照 | 更新内容 |
|---------|-----|-----------|---------|
| `PhonemeEncoder.cs` | 312 | `Phonemizers.Multilingual.PuaTokenMapper.Token2Char.TryGetValue(...)` | コンストラクタ注入された `PuaTokenMapper` インスタンスの `.Token2Char.TryGetValue(...)` |
| `PhonemeEncoder.cs` | 330 | `Phonemizers.Multilingual.PuaTokenMapper.Char2Token.TryGetValue(...)` | 同上 `.Char2Token.TryGetValue(...)` |
| `PhonemeEncoder.cs` | 351 | `Phonemizers.Multilingual.PuaTokenMapper.Token2Char.TryGetValue(...)` | 同上 `.Token2Char.TryGetValue(...)` |

**PhonemeEncoder のコンストラクタ変更**:

```csharp
// v1.x
public PhonemeEncoder(PiperVoiceConfig config)

// v2.0
public PhonemeEncoder(PiperVoiceConfig config, PuaTokenMapper tokenMapper)
```

`PhonemeEncoder` 内部で `_tokenMapper` フィールドを保持し、`MapPhoneme()` メソッド内の参照を置き換える。

### 4.2 テストコード（3ファイル）

| ファイル | 参照数 | 主な変更 |
|---------|--------|---------|
| `PuaTokenMapperTests.cs` | 67箇所 | `PuaTokenMapper.XXX` → `_mapper.XXX`（`[SetUp]` で `_mapper = new PuaTokenMapper()` ） |
| `PuaJsonCrossValidationTests.cs` | 6箇所 | `PuaTokenMapper.FixedPuaMapping` はそのまま（static readonly） |
| `ChinesePhonemizerTests.cs` | 13箇所 | `PuaTokenMapper.FixedPuaMapping` はそのまま（static readonly） |

### 4.3 参照箇所の分類

**変更不要（`FixedPuaMapping` 参照のみ）**:
- `PuaJsonCrossValidationTests.cs` -- 全6箇所が `PuaTokenMapper.FixedPuaMapping` 参照。static readonly のため変更不要。
- `ChinesePhonemizerTests.cs` -- 全13箇所が `PuaTokenMapper.FixedPuaMapping` 参照。同上。
- `PuaTokenMapperTests.cs` 内の `FixedPuaMapping` 参照テスト群 -- 変更不要。

**変更必要（インスタンスメソッド/プロパティ参照）**:
- `PhonemeEncoder.cs` -- 3箇所。コンストラクタ注入。
- `PuaTokenMapperTests.cs` 内の `Token2Char`, `Char2Token`, `MapToken`, `MapSequence`, `UnmapChar`, `Register`, `IsFixedPua` テスト -- インスタンス経由に変更。

### 4.4 ドキュメント参照（変更不要だが記載更新が望ましい）

| ファイル | 内容 |
|---------|------|
| `docs/ARCHITECTURE_ja.md` | PuaTokenMapper の説明 |
| `docs/ARCHITECTURE_en.md` | 同上（英語版） |
| `CLAUDE.md` | PuaTokenMapper の役割説明 |
| `docs/development/v2.0-plan.md` | P1-1 チケット記述 |
| `docs/development/architecture-improvement-roadmap.md` | ロードマップ記述 |
| `docs/development/piper-plus-v1.10.0-completed.md` | 完了チケット記述 |
| `CHANGELOG.md` | リリースノート |
| `docs/development/tickets/MS3-1_swedish-language-support.md` | PuaTokenMapper 言及 |
| `docs/development/tickets/v1.4.0-P5-3_integration-tests.md` | PuaTokenMapper 言及 |

---

## 5. DI / ファクトリパターン設計

### 5.1 インスタンス生成戦略

```
PiperTTS (エントリポイント)
  └─ PuaTokenMapper mapper = new PuaTokenMapper();
      ├─→ MultilingualPhonemizer(options, mapper)   // コンストラクタ注入
      └─→ PhonemeEncoder(config, mapper)            // コンストラクタ注入
```

`PiperTTS` が唯一のインスタンス生成者（Composition Root）。`MultilingualPhonemizer` と `PhonemeEncoder` は同一インスタンスを共有する。

### 5.2 PhonemeEncoder へのコンストラクタ注入

```csharp
public class PhonemeEncoder
{
    private readonly PuaTokenMapper _tokenMapper;

    public PhonemeEncoder(PiperVoiceConfig config, PuaTokenMapper tokenMapper)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _tokenMapper = tokenMapper ?? throw new ArgumentNullException(nameof(tokenMapper));
        // ...
    }

    private string MapPhoneme(string phoneme)
    {
        // v1.x: Phonemizers.Multilingual.PuaTokenMapper.Token2Char.TryGetValue(...)
        // v2.0:
        if (_tokenMapper.Token2Char.TryGetValue(phoneme, out var puaChar))
        { ... }
    }
}
```

### 5.3 TTSSynthesisOrchestrator / PiperTTS からの伝播

`TTSSynthesisOrchestrator` が `PhonemeEncoder` を生成する箇所で `PuaTokenMapper` を渡す。

```csharp
// TTSSynthesisOrchestrator のコンストラクタまたはファクトリ
var tokenMapper = new PuaTokenMapper();
var encoder = new PhonemeEncoder(voiceConfig, tokenMapper);
var phonemizer = new MultilingualPhonemizer(options); // options 経由または直接注入
```

> **注意**: `MultilingualPhonemizer` は現状 `PuaTokenMapper` を直接参照していない（DotNetG2P の各エンジンが内部で `ToPuaPhonemes()` を呼ぶ）。ただし P1-2（pua.json ランタイム読み込み）で `PuaTokenMapper.InitializeAsync()` を追加する際に注入が必要になるため、v2.0 計画に含める。

### 5.4 Unity エディタ ドメインリロード対応

`ResetOnDomainReload` の代替として、インスタンスを保持する側（`PiperTTS` 等）が `RuntimeInitializeOnLoadMethod` でインスタンスを再生成する。

```csharp
// PiperTTS.cs (概念)
#if UNITY_EDITOR
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
private static void ResetOnDomainReload()
{
    // インスタンスフィールドは MonoBehaviour の場合自動リセット。
    // static キャッシュがある場合のみここでクリア。
}
#endif
```

`PuaTokenMapper` 自体にはドメインリロードハンドラが不要になる。

---

## 6. テスト改善

### 6.1 ResetForTesting 廃止

**Before (v1.x)**:
```csharp
[TestFixture]
public class PuaTokenMapperTests
{
    // 暗黙的に共有されるプロセスグローバル状態
    // ResetForTesting() は呼ばれていない（テスト間リーク発生中）

    [Test]
    public void MapToken_UnknownMultiCharToken_RegistersDynamically()
    {
        var token = "_test_dynamic_maptoken_unique_001"; // ユニーク名で回避
        var result = PuaTokenMapper.MapToken(token);
        // ... _nextDynamic がインクリメントされる → 他テストに影響
    }
}
```

**After (v2.0)**:
```csharp
[TestFixture]
public class PuaTokenMapperTests
{
    private PuaTokenMapper _mapper;

    [SetUp]
    public void SetUp()
    {
        _mapper = new PuaTokenMapper(); // テストごとにクリーンインスタンス
    }

    [Test]
    public void MapToken_UnknownMultiCharToken_RegistersDynamically()
    {
        var token = "test_dynamic";
        var result = _mapper.MapToken(token);
        Assert.GreaterOrEqual((int)result, 0xE062);
        // 他テストに影響しない
    }
}
```

### 6.2 テスト分類と変更方針

| テストカテゴリ | テスト数 | 変更方針 |
|--------------|---------|---------|
| FixedPuaMapping 検証 | 12テスト | 変更不要（static readonly 参照） |
| Token2Char / Char2Token 整合性 | 3テスト | `_mapper.Token2Char` / `_mapper.Char2Token` に変更 |
| MapToken | 4テスト | `_mapper.MapToken()` に変更 |
| MapSequence | 4テスト | `_mapper.MapSequence()` に変更 |
| UnmapChar | 3テスト | `_mapper.UnmapChar()` に変更 |
| IsFixedPua | 3テスト | 変更不要（static メソッドのまま） |
| スレッドセーフティ | 1テスト | `_mapper.Register()` / `_mapper.Token2Char` に変更 |
| PUA値検証 | 4テスト | 変更不要（FixedPuaMapping 参照） |

### 6.3 スレッドセーフティテストの改善

```csharp
[Test]
public void Register_ConcurrentCalls_NoDuplicates()
{
    // 各テストが独自インスタンスを持つため、
    // 並行テスト実行でも干渉しない
    var mapper = new PuaTokenMapper();

    const int threadCount = 8;
    const int tokensPerThread = 50;
    // ... mapper.Register() を使用
}
```

### 6.4 PuaJsonCrossValidationTests / ChinesePhonemizerTests

これらは全て `PuaTokenMapper.FixedPuaMapping` を参照しており、static readonly のまま保持されるため**変更不要**。

---

## 7. 破壊的変更の影響

### 7.1 Public API 変更一覧

| v1.x | v2.0 | 種別 |
|------|------|------|
| `public static class PuaTokenMapper` | `public sealed class PuaTokenMapper` | クラス定義変更 |
| `public static char Register(string)` | `public char Register(string)` | static → instance |
| `public static List<char> MapSequence(IList<string>)` | `public List<char> MapSequence(IList<string>)` | static → instance |
| `public static char MapToken(string)` | `public char MapToken(string)` | static → instance |
| `public static string UnmapChar(char)` | `public string UnmapChar(char)` | static → instance |
| `public static readonly ConcurrentDictionary<string, char> Token2Char` | `public IReadOnlyDictionary<string, char> Token2Char { get; }` | フィールド → プロパティ、型変更 |
| `public static readonly ConcurrentDictionary<char, string> Char2Token` | `public IReadOnlyDictionary<char, string> Char2Token { get; }` | フィールド → プロパティ、型変更 |
| `public static bool IsFixedPua(char)` | `public static bool IsFixedPua(char)` | **変更なし** |
| `public static readonly IReadOnlyDictionary<string, int> FixedPuaMapping` | `public static readonly IReadOnlyDictionary<string, int> FixedPuaMapping` | **変更なし** |
| `internal static void ResetForTesting()` | (削除) | 廃止 |

### 7.2 外部利用者への影響

`PuaTokenMapper` を直接使用する外部コードは以下の変更が必要:

```csharp
// v1.x
var ch = PuaTokenMapper.Register("ch");
var mapped = PuaTokenMapper.MapSequence(tokens);
var token = PuaTokenMapper.UnmapChar(ch);
bool isPua = PuaTokenMapper.Token2Char.ContainsKey("ch");

// v2.0
var mapper = new PuaTokenMapper();
var ch = mapper.Register("ch");
var mapped = mapper.MapSequence(tokens);
var token = mapper.UnmapChar(ch);
bool isPua = mapper.Token2Char.ContainsKey("ch");  // IReadOnlyDictionary.ContainsKey

// 変更不要
var fixedValue = PuaTokenMapper.FixedPuaMapping["ch"];  // static readonly のまま
bool isFixed = PuaTokenMapper.IsFixedPua('\uE00E');     // static メソッドのまま
```

### 7.3 Token2Char / Char2Token の型変更の影響

`ConcurrentDictionary<K,V>` → `IReadOnlyDictionary<K,V>` への変更で、以下のメソッドが使えなくなる:

| 使えなくなるメソッド | 代替 |
|-------------------|------|
| `Token2Char[key] = value` (書き込み) | `mapper.Register(token)` |
| `Token2Char.TryAdd(...)` | `mapper.Register(token)` |
| `Char2Token[key] = value` (書き込み) | `mapper.Register(token)` |

現状の外部コードは全て読み取りのみ（`TryGetValue` / `ContainsKey` / indexer 読み取り）であり、`IReadOnlyDictionary` でこれらは全てサポートされている。書き込みは `Register()` メソッド経由に一本化される。

### 7.4 PhonemeEncoder コンストラクタの破壊的変更

```csharp
// v1.x
var encoder = new PhonemeEncoder(voiceConfig);

// v2.0
var encoder = new PhonemeEncoder(voiceConfig, tokenMapper);
```

`PhonemeEncoder` を直接インスタンス化している箇所（`TTSSynthesisOrchestrator` 等）の更新が必要。

---

## 8. 実装手順

### Step 1: PuaTokenMapper クラス変更
1. `public static class` → `public sealed class` に変更
2. `Token2Char` / `Char2Token` を `private readonly` インスタンスフィールドに移動
3. `_nextDynamic` / `_dynamicLock` をインスタンスフィールドに移動
4. static コンストラクタ → インスタンスコンストラクタに変更
5. `public static` メソッド → `public` インスタンスメソッドに変更（`IsFixedPua` は static のまま）
6. `Token2Char` / `Char2Token` を `IReadOnlyDictionary` プロパティとして公開
7. `ResetForTesting()` / `ResetOnDomainReload()` を削除
8. `[assembly: InternalsVisibleTo]` を削除（他に internal が不要な場合）

### Step 2: PhonemeEncoder 更新
1. コンストラクタに `PuaTokenMapper tokenMapper` パラメータ追加
2. `_tokenMapper` フィールド追加
3. `MapPhoneme()` 内の3箇所の static 参照をインスタンス参照に変更

### Step 3: PhonemeEncoder 生成元の更新
1. `TTSSynthesisOrchestrator` で `PuaTokenMapper` インスタンスを生成・注入
2. 必要に応じて `PiperTTS` レベルでインスタンスを管理

### Step 4: テスト更新
1. `PuaTokenMapperTests` に `[SetUp]` で `_mapper = new PuaTokenMapper()` を追加
2. インスタンスメソッド/プロパティ参照に書き換え
3. 動的登録テストからユニーク接頭辞を除去（不要になる）
4. `PuaJsonCrossValidationTests` / `ChinesePhonemizerTests` は変更不要を確認

### Step 5: ドキュメント更新
1. `CLAUDE.md` の PuaTokenMapper 説明を更新
2. `ARCHITECTURE_ja.md` / `ARCHITECTURE_en.md` を更新
3. `CHANGELOG.md` に破壊的変更を記載

---

## 9. P1-2 との関係

P1-2（pua.json ランタイム読み込み）は P1-1 完了後に実施する。P1-1 でインスタンスクラス化した後:

```csharp
// P1-2 で追加
public async Task InitializeFromJsonAsync(string json) { ... }
public void InitializeFromJson(string json) { ... }
```

コンストラクタではハードコード固定マッピングで初期化し、`InitializeFromJsonAsync` で pua.json の内容でオーバーライドする設計。P1-1 の時点で `InitializeFromJsonAsync` は実装しない。

---

## 10. リスク・注意事項

1. **DotNetG2P エンジンとの関係**: 各言語の DotNetG2P エンジン（dot-net-g2p リポジトリ）は内部で `ToPuaPhonemes()` を持ち、PUA 変換を自前で行う。`PuaTokenMapper` のインスタンス化はこれらに影響しない（別リポジトリであり、uPiper 側の責務ではない）。
2. **FixedPuaMapping の不変性**: `FixedPuaMapping` は `static readonly` のまま保持するため、モデル互換性に影響なし。
3. **パフォーマンス**: インスタンス生成時に96エントリの辞書初期化が発生するが、TTS 初期化時に1回のみであり無視できるコスト。
4. **ConcurrentDictionary の IReadOnlyDictionary 公開**: `ConcurrentDictionary` は `IReadOnlyDictionary` を実装しているため、キャスト不要でそのままプロパティから返せる。
