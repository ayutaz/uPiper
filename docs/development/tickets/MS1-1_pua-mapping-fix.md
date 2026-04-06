# MS1-1: PUA マッピング差分修正

**マイルストーン**: [MS1: 互換性修正](../piper-plus-v1.10.0-milestones.md#ms1-互換性修正)
**優先度**: P0
**ステータス**: 完了
**見積もり**: 2-3時間（実装1h + テスト1h + レビュー0.5h）
**依存チケット**: なし
**後続チケット**: MS3-1 (スウェーデン語対応はこのPUAマッピングに依存)

---

## 1. タスク目的とゴール

### 目的

uPiper の `PuaTokenMapper` (88エントリ) と piper-plus v1.10.0 の `pua.json` (96エントリ) の間に **10エントリの差分** がある。この差分を解消し、モデルの完全互換性を確保する。

PUAマッピングはONNXモデルの `phoneme_id_map` に焼き込まれており、不一致があると推論時に音素IDが正しく解決されず、音声品質の劣化やエンコードエラーを引き起こす。

### 差分の内訳

| 差分タイプ | コードポイント | トークン | 説明 |
|-----------|---------------|---------|------|
| uPiperのみ（削除対象） | 0xE053 | `ɔɪ` (U+0254 U+026A) | 英語二重母音。piper-plus では予約(gap)扱い |
| piper-plusのみ（追加対象） | 0xE059 | `iː` (i + U+02D0) | SV 長母音 |
| piper-plusのみ（追加対象） | 0xE05A | `yː` (y + U+02D0) | SV 長母音 |
| piper-plusのみ（追加対象） | 0xE05B | `eː` (e + U+02D0) | SV 長母音 |
| piper-plusのみ（追加対象） | 0xE05C | `ɛː` (U+025B + U+02D0) | SV 長母音 |
| piper-plusのみ（追加対象） | 0xE05D | `øː` (U+00F8 + U+02D0) | SV 長母音 |
| piper-plusのみ（追加対象） | 0xE05E | `ɑː` (U+0251 + U+02D0) | SV 長母音 |
| piper-plusのみ（追加対象） | 0xE05F | `oː` (o + U+02D0) | SV 長母音 |
| piper-plusのみ（追加対象） | 0xE060 | `uː` (u + U+02D0) | SV 長母音 |
| piper-plusのみ（追加対象） | 0xE061 | `ʉː` (U+0289 + U+02D0) | SV 長母音 |

### 完了条件

1. `PuaTokenMapper.FixedPuaMapping` のエントリ数が **96** (piper-plus の `pua.json` と一致)
2. `0xE053` エントリが削除され、予約(gap)として扱われている
3. SV長母音 9エントリ (0xE059-0xE061) が追加されている
4. 動的割り当て開始点が `0xE062` に更新されている
5. `PhonemeEncoder.cs` に `0xE053` への直接参照がないことを確認済み（変更不要）
6. 既存テスト全パス + 新規テスト追加
7. `IsFixedPua()` のチェック範囲が `0xE061` に拡張されている

---

## 2. 実装する内容の詳細

### 2.1 PuaTokenMapper.cs の修正

**ファイル**: `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/PuaTokenMapper.cs`

#### 2.1.1 0xE053 エントリの削除 (行151)

**現状** (行150-151):
```csharp
// English diphthong (dynamically allocated during multilingual training)
{ "\u0254\u026A", 0xE053 },    // ɔɪ  (as in "boy")
```

**変更**: この2行を削除する。piper-plus の `pua.json` および `token_mapper.py` (行125) では `0xE053` は予約(gap)であり、`OpenJTalkToPiperMapping.cs` (行157) でも `// 0xE053 reserved` とコメントされている。

英語の `ɔɪ` は IPA 文字2つ (`ɔ` = U+0254, `ɪ` = U+026A) から成り、piper-plus ではそれぞれが個別のIDで処理される（PUAマッピング不要）。

#### 2.1.2 スウェーデン語(SV)セクションの追加 (行155の後、FR セクションの後)

フランス語の `ɔ̃` エントリ (0xE058) の直後に以下を追加:

```csharp
// =================================================================
// Swedish (SV) -- swedish_phonemize.cpp
// =================================================================
// --- Long vowels (Complementary Quantity) ---
{ "i\u02D0", 0xE059 },          // iː  close front unrounded long
{ "y\u02D0", 0xE05A },          // yː  close front rounded long
{ "e\u02D0", 0xE05B },          // eː  close-mid front unrounded long
{ "\u025B\u02D0", 0xE05C },     // ɛː  open-mid front unrounded long
{ "\u00F8\u02D0", 0xE05D },     // øː  close-mid front rounded long
{ "\u0251\u02D0", 0xE05E },     // ɑː  open back unrounded long
{ "o\u02D0", 0xE05F },          // oː  close-mid back rounded long
{ "u\u02D0", 0xE060 },          // uː  close back rounded long
{ "\u0289\u02D0", 0xE061 },     // ʉː  close central rounded long
```

**参照元**: piper-plus `pua.json` (行92-100) および `OpenJTalkToPiperMapping.cs` (行173-186)

**注意**: piper-plus の Python 実装 `token_mapper.py` は SV エントリを含んでいない（87エントリ、`_PUA_START = 0xE059`）。`pua.json` および `OpenJTalkToPiperMapping.cs` (C#) を正とする。

#### 2.1.3 定数の更新

**`LastFixedCodepoint`** (行171):
```csharp
// 変更前
private const int LastFixedCodepoint = 0xE058;
// 変更後
private const int LastFixedCodepoint = 0xE061;
```

**`DynamicPuaStart`** (行176):
```csharp
// 変更前
private const int DynamicPuaStart = 0xE059;
// 変更後
private const int DynamicPuaStart = 0xE062;
```

これにより `_nextDynamic` (行195) の初期値も自動的に `0xE062` になる。

#### 2.1.4 IsFixedPua の XML doc comment 更新

`PuaTokenMapper.cs` 行299 の XML doc comment `(0xE000 .. 0xE058)` を `(0xE000 .. 0xE061)` に更新する。

#### 2.1.5 エントリ数の変化

- 削除: 1エントリ (0xE053 `ɔɪ`)
- 追加: 9エントリ (0xE059-0xE061 SV長母音)
- 結果: 88 - 1 + 9 = **96エントリ**
- piper-plus `pua.json`: 96エントリ (0xE000-0xE061 の98スロットから予約gap 0xE01F, 0xE053 を除く)

### 2.2 PhonemeEncoder.cs の修正

**ファイル**: `Assets/uPiper/Runtime/Core/AudioGeneration/PhonemeEncoder.cs`

#### 2.2.1 multiCharPhonemeMap から 0xE053 を削除

**現状**: `multiCharPhonemeMap` (行112-148) には `ɔɪ` エントリが **含まれていない**。確認のみで変更不要。

#### 2.2.2 puaToPhonemeMap から 0xE053 を確認

**現状**: `puaToPhonemeMap` (行179-214) および `puaToPhonemeMapMultilingual` (行218-251) にも `0xE053` エントリは **含まれていない**。確認のみで変更不要。

> **結論**: `PhonemeEncoder.cs` には 0xE053 に関する直接的なコード変更は不要。`PuaTokenMapper` を通じた間接参照のみであり、`PuaTokenMapper` 側の修正で自動的に対応される。

### 2.3 PuaTokenMapperTests.cs の修正

**ファイル**: `Assets/uPiper/Tests/Editor/Phonemizers/PuaTokenMapperTests.cs`

#### 2.3.1 エントリ数テストの更新 (行113-117)

```csharp
// 変更前
[Test]
public void FixedPuaMapping_TotalCount_Is88()
{
    Assert.AreEqual(88, PuaTokenMapper.FixedPuaMapping.Count,
        "FixedPuaMapping should contain exactly 88 entries");
}

// 変更後
[Test]
public void FixedPuaMapping_TotalCount_Is96()
{
    Assert.AreEqual(96, PuaTokenMapper.FixedPuaMapping.Count,
        "FixedPuaMapping should contain exactly 96 entries (matching piper-plus pua.json)");
}
```

#### 2.3.2 SVエントリの検証テスト追加

```csharp
[Test]
public void FixedPuaMapping_ContainsAllSwedishEntries()
{
    var svTokens = new[]
    {
        "i\u02D0", "y\u02D0", "e\u02D0",
        "\u025B\u02D0", "\u00F8\u02D0", "\u0251\u02D0",
        "o\u02D0", "u\u02D0", "\u0289\u02D0"
    };

    foreach (var token in svTokens)
    {
        Assert.IsTrue(
            PuaTokenMapper.FixedPuaMapping.ContainsKey(token),
            $"FixedPuaMapping should contain Swedish token '{token}'");
    }
}

[Test]
public void VerifySwedishPuaValues()
{
    Assert.AreEqual(0xE059, PuaTokenMapper.FixedPuaMapping["i\u02D0"], "iː -> 0xE059");
    Assert.AreEqual(0xE05A, PuaTokenMapper.FixedPuaMapping["y\u02D0"], "yː -> 0xE05A");
    Assert.AreEqual(0xE05B, PuaTokenMapper.FixedPuaMapping["e\u02D0"], "eː -> 0xE05B");
    Assert.AreEqual(0xE05C, PuaTokenMapper.FixedPuaMapping["\u025B\u02D0"], "ɛː -> 0xE05C");
    Assert.AreEqual(0xE05D, PuaTokenMapper.FixedPuaMapping["\u00F8\u02D0"], "øː -> 0xE05D");
    Assert.AreEqual(0xE05E, PuaTokenMapper.FixedPuaMapping["\u0251\u02D0"], "ɑː -> 0xE05E");
    Assert.AreEqual(0xE05F, PuaTokenMapper.FixedPuaMapping["o\u02D0"], "oː -> 0xE05F");
    Assert.AreEqual(0xE060, PuaTokenMapper.FixedPuaMapping["u\u02D0"], "uː -> 0xE060");
    Assert.AreEqual(0xE061, PuaTokenMapper.FixedPuaMapping["\u0289\u02D0"], "ʉː -> 0xE061");
}
```

#### 2.3.3 0xE053 削除の回帰テスト追加

```csharp
[Test]
public void FixedPuaMapping_DoesNotContain0xE053()
{
    // 0xE053 is a reserved gap in piper-plus (not assigned to any token)
    var hasE053 = PuaTokenMapper.FixedPuaMapping.Values.Any(v => v == 0xE053);
    Assert.IsFalse(hasE053,
        "0xE053 should be a reserved gap (not assigned to any token)");
}
```

#### 2.3.4 IsFixedPua 境界値テストの更新 (行333-347)

```csharp
// 変更前
Assert.IsTrue(PuaTokenMapper.IsFixedPua('\uE058'),
    "0xE058 (last fixed PUA) should be in range");
Assert.IsFalse(PuaTokenMapper.IsFixedPua('\uE059'),
    "0xE059 (first dynamic PUA) should NOT be in range");

// 変更後
Assert.IsTrue(PuaTokenMapper.IsFixedPua('\uE061'),
    "0xE061 (last fixed PUA) should be in range");
Assert.IsFalse(PuaTokenMapper.IsFixedPua('\uE062'),
    "0xE062 (first dynamic PUA) should NOT be in range");
```

また、IsFixedPua のパラメトリックテスト (行313-331) も更新:

```csharp
// InRange テスト: 0xE058 → 0xE061 に拡張
[TestCase('\uE000', true)]
[TestCase('\uE02A', true)]
[TestCase('\uE058', true)]
[TestCase('\uE059', true)]  // 追加: SV PUA 開始境界
[TestCase('\uE061', true)]  // 追加: SV最終エントリ

// OutOfRange テスト: 0xE059 → 0xE062 に更新
[TestCase('\uDFFF', false)]
[TestCase('\uE062', false)]  // 変更: 0xE059 → 0xE062
[TestCase('\uE100', false)]
```

#### 2.3.5 動的割り当て開始点テストの更新 (行200-212)

`MapToken_UnknownMultiCharToken_RegistersDynamically` テスト内の動的PUA範囲チェック:

```csharp
// 変更前
Assert.GreaterOrEqual((int)result, 0xE059,
    "Dynamically registered token should be in the dynamic PUA range");

// 変更後
Assert.GreaterOrEqual((int)result, 0xE062,
    "Dynamically registered token should be in the dynamic PUA range");
```

### 2.4 変更対象ファイルまとめ

| ファイル | 変更内容 | 行番号 |
|---------|---------|--------|
| `PuaTokenMapper.cs` | 0xE053削除, SV追加, 定数更新 | 150-151, 166後, 171, 176 |
| `PuaTokenMapperTests.cs` | テスト更新・追加 | 113-117, 200-206, 313-347 + 新規テスト |
| `PhonemeEncoder.cs` | 変更不要（確認のみ） | — |

---

## 3. エージェントチーム構成

| 役割 | 人数 | 担当範囲 |
|------|------|---------|
| 実装エージェント | x1 | `PuaTokenMapper.cs` の修正（エントリ追加・削除・定数更新） |
| テストエージェント | x1 | `PuaTokenMapperTests.cs` のテスト修正・追加。修正後に全EditModeテスト実行 |
| レビューエージェント | x1 | piper-plus `pua.json` とのエントリ完全一致を照合。Unicode エスケープの正確性を検証 |

> 実装規模が小さいため、実装とテストを1エージェントが兼任しても問題ない。最小構成は **実装兼テスト x1 + レビュー x1** の2名。

---

## 4. 提供範囲・テスト項目

### 提供範囲 (Scope)

- `PuaTokenMapper.FixedPuaMapping` のエントリ修正（96エントリに統一）
- `LastFixedCodepoint`, `DynamicPuaStart` 定数の更新
- `IsFixedPua()` の範囲が自動的に拡張される（`LastFixedCodepoint` 参照のため）
- `PuaTokenMapperTests.cs` のテスト更新・新規テスト追加

### スコープ外

- `PhonemeEncoder.cs` の `multiCharPhonemeMap` / `puaToPhonemeMap` への SV エントリ追加（MS3-1 で対応）
- `MultilingualPhonemizer` への SV ルーティング追加（MS3-1 で対応）
- DotNetG2P.Swedish パッケージの統合（MS3-1 で対応）

### Unit テスト

| テストケース | 検証内容 | 既存/新規 |
|------------|---------|----------|
| `FixedPuaMapping_TotalCount_Is96` | エントリ数が96であること | 既存修正 |
| `FixedPuaMapping_ContainsAllSwedishEntries` | SV 9トークンの存在確認 | 新規 |
| `VerifySwedishPuaValues` | SV 各エントリのコードポイント正引き確認 | 新規 |
| `FixedPuaMapping_DoesNotContain0xE053` | 0xE053が予約gapであること | 新規 |
| `FixedPuaMapping_NoDuplicateCodepoints` | 全コードポイントがユニークであること | 既存（変更なしでパスすること） |
| `Token2Char_ContainsAllFixedMappings` | 全固定マッピングがToken2Charに存在 | 既存（変更なしでパスすること） |
| `Char2Token_ContainsAllFixedMappings` | 逆引きが正しいこと | 既存（変更なしでパスすること） |
| `Token2Char_And_Char2Token_AreConsistent` | 双方向一貫性 | 既存（変更なしでパスすること） |
| `IsFixedPua_BoundaryValues` | 0xE061=true, 0xE062=false | 既存修正 |
| `IsFixedPua_InRange_ReturnsTrue` | 0xE061がtrue | 既存修正 |
| `IsFixedPua_OutOfRange_ReturnsFalse` | 0xE062がfalse | 既存修正 |
| `MapToken_UnknownMultiCharToken_RegistersDynamically` | 動的割り当てが0xE062以降 | 既存修正 |
| `Register_ConcurrentCalls_NoDuplicates` | スレッドセーフティ | 既存（変更なしでパスすること） |

### E2E テスト

| テストケース | 検証内容 |
|------------|---------|
| 既存の多言語推論テスト全パス | SV PUA追加が既存言語 (ja/en/zh/es/fr/pt/ko) の推論に影響しないこと |
| `PuaTokenMapper.MapToken("i\u02D0")` → `'\uE059'` | SV長母音の正引きがRuntime上で動作すること |
| `PuaTokenMapper.UnmapChar('\uE059')` → `"i\u02D0"` | SV長母音の逆引きがRuntime上で動作すること |
| `PuaTokenMapper.MapSequence(svPhonemes)` | SV音素シーケンスの一括変換が正しいこと |

---

## 5. 懸念事項・レビュー項目

### 5.1 0xE053 削除の影響範囲

- **確認済み**: `PhonemeEncoder.cs` の `multiCharPhonemeMap`, `puaToPhonemeMap`, `puaToPhonemeMapMultilingual` のいずれにも `0xE053` エントリは存在しない
- **確認済み**: `Packages/` 配下の DotNetG2P.English には `0xE053` への参照がない（Grepで確認済み）
- **リスク**: もし外部コードやモデルが `0xE053` を `ɔɪ` として使用していた場合、動的割り当てで異なるコードポイントにマッピングされる可能性がある。ただし、piper-plus の英語実装では `ɔɪ` は `ɔ` (U+0254) と `ɪ` (U+026A) の2文字として個別に処理されており、PUA化は行われていない

### 5.2 Unicode エスケープの正確性

レビュー時に以下の Unicode コードポイントを厳密に確認すること:

| トークン | 文字列リテラル | 確認ポイント |
|---------|--------------|-------------|
| `iː` | `"i\u02D0"` | U+02D0 = MODIFIER LETTER TRIANGULAR COLON (長音記号) |
| `yː` | `"y\u02D0"` | 同上 |
| `eː` | `"e\u02D0"` | 同上 |
| `ɛː` | `"\u025B\u02D0"` | U+025B = LATIN SMALL LETTER OPEN E |
| `øː` | `"\u00F8\u02D0"` | U+00F8 = LATIN SMALL LETTER O WITH STROKE |
| `ɑː` | `"\u0251\u02D0"` | U+0251 = LATIN SMALL LETTER ALPHA |
| `oː` | `"o\u02D0"` | ASCII 'o' (U+006F) |
| `uː` | `"u\u02D0"` | ASCII 'u' (U+0075) |
| `ʉː` | `"\u0289\u02D0"` | U+0289 = LATIN SMALL LETTER U BAR |

piper-plus `pua.json` および `OpenJTalkToPiperMapping.cs` (行177-186) のリテラルと1文字ずつ照合すること。

### 5.3 静的コンストラクタの挙動

`PuaTokenMapper` は静的クラスであり、静的コンストラクタ (行204-213) で `FixedPuaMapping` を `Token2Char` / `Char2Token` に展開する。エントリ追加後も初期化順序に問題がないことを確認する。

### 5.4 ConcurrentDictionary のパフォーマンス

96エントリは小規模であり、`ConcurrentDictionary` の初期化コストに問題はない。

### 5.5 CLAUDE.md の記述更新

**CLAUDE.md では PuaTokenMapper を「87固定エントリ」と記載しているが、実際は88エントリ。本チケット完了後に CLAUDE.md の該当箇所を「96固定エントリ」に更新すること。**

### 5.6 既存テストの動的割り当てアドレスへの依存

`PuaTokenMapperTests` のスレッドセーフティテスト (行351-399) は動的トークン `_concurrent_{threadIndex}_{i}` を登録する。テスト実行順序によっては `_nextDynamic` の値が変動するが、テストは「ユニーク性」のみを検証しており、具体的なコードポイント値には依存していないため問題ない。

---

## 6. ゼロから作り直すとしたら

### 6.1 pua.json を単一ソースにする

現在の `PuaTokenMapper.cs` はコード内にハードコードされた `Dictionary<string, int>` でマッピングを定義している。piper-plus は `pua.json` をSingle Source of Truthとし、Python/Rust/JS/C# の全実装がこの JSON を参照する設計に移行済み。

ゼロから作り直すなら、`pua.json` を `StreamingAssets/` または `Resources/` に配置し、`PuaTokenMapper` は初期化時に JSON をパースする方式が望ましい。これにより:
- piper-plus との差分発生を根本的に防止できる
- エントリ追加時にコード変更が不要になる
- `pua.json` のバージョン (`PUA_COMPAT_VERSION`) による互換性チェックが可能になる

ただし、Unity の `StreamingAssets` はWebGLで非同期読み込みが必要なため、静的コンストラクタでの初期化ができなくなるトレードオフがある。

### 6.2 PhonemeEncoder の重複マッピングを排除する

現在 `PhonemeEncoder.cs` は `multiCharPhonemeMap` (行112-148)、`puaToPhonemeMap` (行179-214)、`puaToPhonemeMapMultilingual` (行218-251) の3つの内部辞書を持ち、`PuaTokenMapper.FixedPuaMapping` と部分的に重複している。理想的には `PuaTokenMapper` を唯一のPUAマッピングソースとし、`PhonemeEncoder` は IPA変換ロジックのみを担当すべきである。

### 6.3 予約gapの明示的管理

0xE01F と 0xE053 が予約gapだが、これがコード上で明示されていない。`pua.json` 方式であればgapも明示的に記録できる。

---

## 7. 後続タスクへの連絡事項

### MS3-1 (スウェーデン語対応) への影響

1. **PUAマッピングは本チケットで完了**: MS3-1 では `PuaTokenMapper.cs` への SV PUA 追加は不要（本チケットで対応済み）。MS3-1 のタスク3「PuaTokenMapper に SV PUA を追加」はスキップしてよい。

2. **PhonemeEncoder への SV 対応は MS3-1 の責務**: 本チケットでは `PhonemeEncoder.cs` の `multiCharPhonemeMap` / `puaToPhonemeMap` / `puaToPhonemeMapMultilingual` に SV エントリを追加しない。SV テキストを実際に音素化・エンコードするには、MS3-1 でこれらの辞書に SV 用エントリを追加する必要がある。

3. **`LastFixedCodepoint` の値**: 本チケット完了後は `0xE061`。MS3-1 で新たな言語が追加される場合でも、piper-plus `pua.json` に新規エントリが追加されない限り、この値は変更不要。

4. **動的割り当て開始点**: `0xE062`。新しい言語の固有PUAが `pua.json` に追加された場合はこの値を再度更新する必要がある。

### テストとの整合性

本チケットで `FixedPuaMapping_TotalCount` テストを `Is96` に更新する。後続チケットでエントリ数が変わる場合はこのテストも更新すること。
