# P1-2: pua.json ランタイム読み込み 設計ドキュメント

**作成日**: 2026-04-08
**ステータス**: Draft
**依存**: P1-1 (PuaTokenMapper インスタンスクラス化)

---

## 1. 背景と目的

### 現状の課題

`PuaTokenMapper` の固定マッピング (96エントリ) は C# ソースコードにハードコードされている (`FixedPuaMapping` の `Dictionary<string, int>` リテラル)。piper-plus 側の正規データソースは `piper_plus_g2p/data/pua.json` であり、Python/Rust/JS の各実装はこの JSON から読み込む。

C# 側との整合性は `PuaJsonCrossValidationTests` (スナップショットベース) で検出しているが、以下の問題がある:

1. **手動同期**: piper-plus で pua.json が更新された場合、C# のハードコードと `PuaJsonCrossValidationTests` のスナップショット配列の両方を手動更新する必要がある
2. **更新漏れリスク**: テストのスナップショット更新を忘れると CI が通らないが、ランタイムのハードコード更新を忘れると本番で音素変換が不整合になる
3. **piper-plus の設計思想との乖離**: piper-plus は pua.json を "single source of truth" として設計。C# だけがハードコードを保持している

### 目的

- `StreamingAssets/uPiper/pua.json` からランタイム読み込みを行い、ハードコードへの依存を除去
- pua.json ファイルを更新するだけで piper-plus との仕様同期が完了する仕組みを構築
- WebGL を含む全プラットフォームで動作する非同期読み込みパスを提供

---

## 2. pua.json スキーマ

### 2.1 piper-plus 正規フォーマット (v1)

piper-plus の `src/python/g2p/piper_plus_g2p/data/pua.json` が正規データソース。

```json
{
  "version": 1,
  "description": "PUA mapping table -- shared across Python/Rust/JS",
  "entries": [
    {
      "token": "a:",
      "codepoint": "0xE000",
      "language": "ja",
      "description": "Long vowel a"
    },
    {
      "token": "cl",
      "codepoint": "0xE005",
      "language": "ja",
      "description": "Geminate consonant (sokuon)"
    },
    ...
  ]
}
```

### 2.2 フィールド定義

| フィールド | 型 | 必須 | 説明 |
|-----------|------|------|------|
| `version` | `int` | Yes | スキーマバージョン。現行は `1` |
| `description` | `string` | No | 人間向け説明文 |
| `entries` | `array` | Yes | PUA マッピングエントリの配列 |
| `entries[].token` | `string` | Yes | 多文字音素トークン (例: `"a:"`, `"ch"`, `"t\u0255\u02b0"`) |
| `entries[].codepoint` | `string` | Yes | PUA コードポイント (`"0xE000"` 形式の16進文字列) |
| `entries[].language` | `string` | Yes | 言語コード (`"ja"`, `"zh"`, `"ko"`, `"es"`, `"fr"`, `"sv"`, `"shared"`) |
| `entries[].description` | `string` | No | 音素の説明 |

### 2.3 現行エントリ数

96 エントリ (pua.json v1):

| 言語 | エントリ数 | コードポイント範囲 |
|------|-----------|-------------------|
| ja | 28 | `0xE000` - `0xE01C` |
| shared | 2 | `0xE01D` - `0xE01E` |
| zh | 31 | `0xE020` - `0xE04A` |
| ko | 8 | `0xE04B` - `0xE052` |
| es (shared w/ pt) | 2 | `0xE054` - `0xE055` |
| fr | 3 | `0xE056` - `0xE058` |
| sv | 9 | `0xE059` - `0xE061` |

未割り当てギャップ: `0xE01F`, `0xE053`

---

## 3. ファイル配置

### 3.1 StreamingAssets パス

```
StreamingAssets/
  uPiper/
    pua.json          <-- NEW: piper-plus からコピー
    Dictionaries/     <-- 既存: カスタム辞書
    OpenJTalk/        <-- 既存: MeCab 辞書
    Phonemizers/      <-- 既存: CMU dict
```

相対パス: `uPiper/pua.json`

### 3.2 piper-plus からの取得

pua.json の正規ソースは `piper-plus/src/python/g2p/piper_plus_g2p/data/pua.json`。
uPiper へのコピー方法は以下のいずれか:

1. **手動コピー** (v2.0 初期): piper-plus リポジトリから手動でコピーし、git 管理
2. **CI 自動検証** (推奨): uPiper の CI で piper-plus の pua.json を取得し、StreamingAssets のコピーとの差分を検出
3. **ビルド時自動コピー** (将来検討): Editor スクリプトで piper-plus ローカルパスから自動コピー

v2.0 では方法 1 + 2 を採用。`PuaJsonCrossValidationTests` を pua.json ファイル読み込みベースに改修し、スナップショット配列を廃止する。

---

## 4. ランタイム読み込み API 設計

### 4.1 PuaTokenMapper への統合

P1-1 でインスタンスクラスに変更された `PuaTokenMapper` に、JSON 読み込み機能を追加する。

```csharp
namespace uPiper.Core.Phonemizers.Multilingual
{
    /// <summary>
    /// PUA mapping between multi-character phoneme tokens and single Unicode PUA codepoints.
    /// Supports both hardcoded fallback and pua.json runtime loading.
    /// </summary>
    public class PuaTokenMapper
    {
        // ── 定数 ──────────────────────────────────────────────────
        private const string PuaJsonRelativePath = "uPiper/pua.json";
        private const int SupportedVersion = 1;

        // ── コンストラクタ ────────────────────────────────────────
        /// <summary>
        /// ハードコード固定マッピングで初期化。
        /// pua.json が利用できない環境でのフォールバック用。
        /// </summary>
        public PuaTokenMapper()
        {
            InitializeFromHardcoded();
        }

        // ── 同期初期化 (PC/Mobile) ───────────────────────────────
        /// <summary>
        /// StreamingAssets/uPiper/pua.json から同期読み込みで初期化。
        /// ファイルが存在しない場合はハードコードフォールバックを維持。
        /// </summary>
        /// <returns>true: pua.json から読み込み成功, false: フォールバック</returns>
        public bool InitializeFromFile()
        {
            // ...
        }

        // ── 非同期初期化 (WebGL 対応) ────────────────────────────
        /// <summary>
        /// WebGLStreamingAssetsLoader 経由で非同期読み込み。
        /// WebGL では必須。PC/Mobile でも使用可能。
        /// </summary>
        public async Task<bool> InitializeAsync(
            CancellationToken cancellationToken = default)
        {
            // ...
        }

        // ── JSON パース (共通) ────────────────────────────────────
        /// <summary>
        /// JSON 文字列からマッピングを読み込み、内部辞書を再構築。
        /// バージョンチェック + エントリパースを行う。
        /// </summary>
        /// <param name="json">pua.json の内容</param>
        /// <exception cref="FormatException">パース失敗時</exception>
        /// <exception cref="NotSupportedException">未対応バージョン</exception>
        public void LoadFromJson(string json)
        {
            // ...
        }

        // ── 既存 API (P1-1 でインスタンスメソッド化済み) ─────────
        public char Register(string token) { ... }
        public List<char> MapSequence(IList<string> tokens) { ... }
        public char MapToken(string token) { ... }
        public string UnmapChar(char ch) { ... }
        public bool IsFixedPua(char ch) { ... }
    }
}
```

### 4.2 JSON デシリアライズ

Unity の `JsonUtility` は配列のルート直接パースに制約があるため、`CustomDictionary` と同様に手動パース or ラッパークラスを使用する。

```csharp
// pua.json デシリアライズ用の内部 DTO
[Serializable]
internal class PuaJsonData
{
    public int version;
    public string description;
    public PuaJsonEntry[] entries;
}

[Serializable]
internal class PuaJsonEntry
{
    public string token;
    public string codepoint;  // "0xE000" 形式
    public string language;
    public string description;
}
```

**パース手順**:

1. `JsonUtility.FromJson<PuaJsonData>(json)` でデシリアライズ
2. `version` フィールドを検証 (`SupportedVersion` と比較)
3. 各エントリの `codepoint` を `Convert.ToInt32(codepoint, 16)` でパース
4. `Token2Char` / `Char2Token` 辞書を再構築
5. `_nextDynamic` を最大コードポイント + 1 にセット

**注意**: `JsonUtility.FromJson` は `PuaJsonEntry[]` 配列を `entries` フィールドとして正しくデシリアライズできる (`[Serializable]` クラスのネスト配列は対応)。`CustomDictionary` で手動パースが必要だったのは `Dictionary<string, object>` 型のためであり、固定スキーマの配列は `JsonUtility` で処理可能。

ただし、`codepoint` フィールドが `"0xE000"` 形式の文字列であるため、数値変換は手動で行う。

### 4.3 初期化フロー詳細

#### 同期パス (PC / Mobile / Editor)

```
PuaTokenMapper()
  └─ InitializeFromHardcoded()  // コンストラクタでハードコード初期化

InitializeFromFile()
  ├─ Path.Combine(Application.streamingAssetsPath, "uPiper/pua.json")
  ├─ File.Exists() チェック
  │   ├─ false → return false (ハードコードフォールバック維持)
  │   └─ true → File.ReadAllText()
  ├─ LoadFromJson(json)
  │   ├─ バージョンチェック
  │   ├─ エントリパース
  │   └─ Token2Char / Char2Token 再構築
  └─ return true
```

#### 非同期パス (WebGL / 全プラットフォーム共通)

```
PuaTokenMapper()
  └─ InitializeFromHardcoded()  // コンストラクタでハードコード初期化

InitializeAsync(ct)
  ├─ WebGLStreamingAssetsLoader.LoadTextAsync("uPiper/pua.json", ct)
  │   ├─ WebGL: UnityWebRequest.Get(url)
  │   └─ PC/Mobile: File.ReadAllTextAsync()
  ├─ LoadFromJson(json)
  └─ return true
  ├─ catch FileNotFoundException → return false (フォールバック)
  └─ catch InvalidOperationException → return false (WebGL 404)
```

---

## 5. プラットフォーム別読み込み戦略

### 5.1 戦略マトリクス

| プラットフォーム | 読み込み方式 | API | フォールバック |
|-----------------|-------------|-----|---------------|
| Windows / Linux / macOS | 同期 (`File.ReadAllText`) | `InitializeFromFile()` | ハードコード |
| iOS / Android | 同期 (`File.ReadAllText`) | `InitializeFromFile()` | ハードコード |
| Unity Editor | 同期 (`File.ReadAllText`) | `InitializeFromFile()` | ハードコード |
| WebGL | 非同期 (`UnityWebRequest`) | `InitializeAsync()` | ハードコード |
| 全プラットフォーム (統一) | 非同期 (`WebGLStreamingAssetsLoader`) | `InitializeAsync()` | ハードコード |

### 5.2 WebGL 固有の考慮事項

- `File.ReadAllText()` は WebGL で使用不可
- `WebGLStreamingAssetsLoader.LoadTextAsync()` が統一的な非同期読み込み API を提供
- `MultilingualPhonemizer.InitializeAsync()` 内で `PuaTokenMapper.InitializeAsync()` を呼び出す
- 既存パターン: `CustomDictionary.LoadDefaultDictionariesAsync()` が同じ `WebGLStreamingAssetsLoader` を使用

### 5.3 呼び出し統合ポイント

`MultilingualPhonemizer.InitializeAsync()` に PuaTokenMapper 初期化を組み込む:

```csharp
public async Task InitializeAsync(CancellationToken cancellationToken = default)
{
    if (_isInitialized)
        return;

    // Step 1: pua.json からマッピング読み込み (P1-2)
    var loaded = await _puaTokenMapper.InitializeAsync(cancellationToken);
    if (loaded)
        PiperLogger.LogInfo("[MultilingualPhonemizer] PUA mapping loaded from pua.json");
    else
        PiperLogger.LogInfo("[MultilingualPhonemizer] PUA mapping: using hardcoded fallback");

    // Step 2: 各言語エンジン初期化 (既存)
    // ...
}
```

非 WebGL プラットフォームでは、コンストラクタ内で同期読み込みも可能:

```csharp
public PuaTokenMapper(bool loadFromFile = true)
{
    InitializeFromHardcoded();
    if (loadFromFile)
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        InitializeFromFile();  // 同期読み込み (WebGL 以外)
#endif
    }
}
```

---

## 6. エラーハンドリング

### 6.1 エラー分類と対処

| エラー | 原因 | 対処 | ログレベル |
|--------|------|------|-----------|
| ファイル不在 | StreamingAssets に pua.json がない | ハードコードフォールバック | Warning |
| JSON パース失敗 | ファイル破損・不正フォーマット | ハードコードフォールバック + エラーログ | Error |
| バージョン不一致 | 未対応の `version` 値 | ハードコードフォールバック + 警告 | Warning |
| エントリ不正 | `token` 空 / `codepoint` パース失敗 | 当該エントリスキップ、残りは読み込み | Warning |
| WebGL ネットワークエラー | 404 / タイムアウト | ハードコードフォールバック | Warning |

### 6.2 フォールバック戦略

**原則**: pua.json 読み込みに失敗しても、ハードコード固定マッピングで動作を継続する。pua.json は "上書き更新" であり、読み込み失敗時はコンストラクタで初期化済みのハードコードマッピングがそのまま使われる。

```csharp
public void LoadFromJson(string json)
{
    PuaJsonData data;
    try
    {
        data = JsonUtility.FromJson<PuaJsonData>(json);
    }
    catch (Exception ex)
    {
        PiperLogger.LogError(
            $"[PuaTokenMapper] Failed to parse pua.json: {ex.Message}. " +
            "Using hardcoded fallback.");
        return;  // ハードコードマッピングを維持
    }

    if (data == null || data.entries == null)
    {
        PiperLogger.LogError("[PuaTokenMapper] pua.json is empty or invalid. Using hardcoded fallback.");
        return;
    }

    if (data.version != SupportedVersion)
    {
        PiperLogger.LogWarning(
            $"[PuaTokenMapper] pua.json version {data.version} is not supported " +
            $"(expected {SupportedVersion}). Using hardcoded fallback.");
        return;
    }

    // 読み込み成功: 既存マッピングをクリアして再構築
    Token2Char.Clear();
    Char2Token.Clear();

    var maxCodepoint = 0;
    var loadedCount = 0;

    foreach (var entry in data.entries)
    {
        if (string.IsNullOrEmpty(entry.token) || string.IsNullOrEmpty(entry.codepoint))
        {
            PiperLogger.LogWarning($"[PuaTokenMapper] Skipping invalid entry: token='{entry.token}'");
            continue;
        }

        int codepoint;
        try
        {
            codepoint = Convert.ToInt32(entry.codepoint, 16);
        }
        catch (FormatException)
        {
            PiperLogger.LogWarning(
                $"[PuaTokenMapper] Invalid codepoint format: '{entry.codepoint}' for token '{entry.token}'");
            continue;
        }

        var ch = (char)codepoint;
        Token2Char[entry.token] = ch;
        Char2Token[ch] = entry.token;
        loadedCount++;

        if (codepoint > maxCodepoint)
            maxCodepoint = codepoint;
    }

    _nextDynamic = maxCodepoint + 1;

    PiperLogger.LogInfo(
        $"[PuaTokenMapper] Loaded {loadedCount} entries from pua.json (version {data.version})");
}
```

### 6.3 バリデーション

`LoadFromJson` 内で以下のバリデーションを実施:

1. **JSON 構造**: `version` と `entries` フィールドの存在確認
2. **バージョン互換**: `version == SupportedVersion` の検証
3. **エントリ整合性**: 各エントリの `token` 非空、`codepoint` パース可能性
4. **コードポイント範囲**: `0xE000` - `0xF8FF` (PUA-A) 内であること
5. **重複チェック**: 同一 token / 同一 codepoint の重複検出 (Warning ログ)

---

## 7. P1-1 との依存関係

### 7.1 前提条件

P1-2 は P1-1 (PuaTokenMapper インスタンスクラス化) の完了が前提。

| P1-1 の成果物 | P1-2 での利用 |
|--------------|-------------|
| `PuaTokenMapper` が通常クラスに変更 | `InitializeFromFile()` / `InitializeAsync()` をインスタンスメソッドとして追加 |
| `Token2Char` / `Char2Token` がインスタンスフィールド | `LoadFromJson` 内でインスタンスの辞書をクリア・再構築 |
| `_nextDynamic` がインスタンスフィールド | `LoadFromJson` 後に動的割り当て開始位置を更新 |
| コンストラクタ注入パターン | `MultilingualPhonemizer` が所有する `PuaTokenMapper` インスタンスに対して初期化を呼び出し |
| `ResetForTesting()` 不要 | テストでは新規インスタンスを生成するだけで済む |

### 7.2 実装順序

```
P1-1: PuaTokenMapper インスタンスクラス化
  ↓
P1-2a: PuaJsonData / PuaJsonEntry DTO 定義
P1-2b: LoadFromJson() パースロジック実装
P1-2c: InitializeFromFile() 同期読み込み実装
P1-2d: InitializeAsync() 非同期読み込み実装
P1-2e: MultilingualPhonemizer.InitializeAsync() への統合
P1-2f: StreamingAssets/uPiper/pua.json ファイル配置
P1-2g: PuaJsonCrossValidationTests 改修 (スナップショット廃止 → ファイル読み込みベース)
P1-2h: 新規テスト追加 (LoadFromJson 正常系/異常系)
```

---

## 8. テスト計画

### 8.1 既存テスト改修

#### PuaJsonCrossValidationTests

現在の `PuaJsonEntries` スナップショット配列をファイル読み込みに置き換え:

```csharp
// Before (v1.x): ハードコードスナップショット
private static readonly (string Token, int Codepoint)[] PuaJsonEntries = new[] { ... };

// After (v2.0): StreamingAssets から読み込み
private static (string Token, int Codepoint)[] LoadPuaJsonEntries()
{
    var path = Path.Combine(Application.streamingAssetsPath, "uPiper", "pua.json");
    var json = File.ReadAllText(path);
    var data = JsonUtility.FromJson<PuaJsonData>(json);
    return data.entries
        .Select(e => (e.token, Convert.ToInt32(e.codepoint, 16)))
        .ToArray();
}
```

### 8.2 新規テスト

| テストクラス | テストメソッド | 検証内容 |
|------------|-------------|---------|
| `PuaTokenMapperJsonTests` | `LoadFromJson_ValidJson_PopulatesMapping` | 正常な pua.json から全エントリがロードされること |
| | `LoadFromJson_InvalidJson_FallsBackToHardcoded` | 不正 JSON でハードコードフォールバックが維持されること |
| | `LoadFromJson_UnsupportedVersion_FallsBackToHardcoded` | 未対応バージョンでフォールバックすること |
| | `LoadFromJson_MissingEntries_SkipsInvalid` | 不正エントリがスキップされること |
| | `LoadFromJson_EmptyCodepoint_SkipsEntry` | codepoint 空のエントリがスキップされること |
| | `LoadFromJson_DuplicateToken_LastWins` | 重複 token は後勝ちになること |
| | `LoadFromJson_UpdatesDynamicStart` | 動的割り当て開始位置が正しく更新されること |
| | `InitializeFromFile_FileExists_ReturnsTrue` | ファイル存在時に true を返すこと |
| | `InitializeFromFile_FileMissing_ReturnsFalse` | ファイル不在時に false を返すこと |
| | `Register_AfterJsonLoad_DynamicAllocFromCorrectStart` | JSON 読み込み後の動的割り当てが正しい位置から開始されること |

---

## 9. ハードコード固定マッピングの扱い

### 9.1 v2.0 での位置付け

ハードコード `FixedPuaMapping` は **フォールバック専用** として維持する。

理由:
- pua.json が配置されていない環境 (開発中、テスト環境) での動作保証
- StreamingAssets 読み込み失敗時の安全ネット
- P1-1 でインスタンスフィールド化後も、コンストラクタでの初期化に使用

### 9.2 将来の方針 (v2.x+)

pua.json ランタイム読み込みが安定したら、以下を検討:
- ハードコードを最小限のサブセット (ja のみ等) に縮小
- または完全に除去し、pua.json 必須化
- `PUA_COMPAT_VERSION` チェック機構の導入 (piper-plus の `check_pua_compat()` に相当)

---

## 10. piper-plus との同期メカニズム

### 10.1 同期フロー

```
piper-plus リポジトリ                          uPiper リポジトリ
  src/python/g2p/                              StreamingAssets/uPiper/
    piper_plus_g2p/data/pua.json  ──(手動コピー)──>  pua.json
                                                        │
                                               PuaTokenMapper.InitializeFromFile()
                                               PuaTokenMapper.InitializeAsync()
                                                        │
                                               Token2Char / Char2Token 辞書構築
```

### 10.2 CI 検証

uPiper の CI (`unity-tests.yml`) で以下を自動検証:

1. `StreamingAssets/uPiper/pua.json` が存在すること
2. `PuaTokenMapper` の `LoadFromJson` が成功すること (既存のクロスバリデーションテスト)
3. ロードされたエントリ数がハードコード `FixedPuaMapping` と一致すること

piper-plus 側の CI (`g2p-cross-platform-ci.yml`) でも pua.json のエントリ数を各言語実装と照合している (Python/Rust/C++ 間)。

### 10.3 バージョニング

pua.json の `version` フィールドでスキーマ互換性を管理:
- **version 1**: 現行フォーマット (96 エントリ)
- **version 2+**: 将来のフォーマット変更時にインクリメント
- C# 側で `SupportedVersion` 定数を持ち、未対応バージョンはフォールバック

piper-plus の `PUA_COMPAT_VERSION` (現在 `1`) と同期を取る。モデルの `config.json` に `pua_compat_version` が含まれる場合、読み込み時に互換性チェックを行う (将来拡張)。

---

## 11. 影響範囲

### 11.1 変更対象ファイル

| ファイル | 変更内容 |
|---------|---------|
| `Runtime/Core/Phonemizers/Multilingual/PuaTokenMapper.cs` | `InitializeFromFile()`, `InitializeAsync()`, `LoadFromJson()` 追加、DTO クラス追加 |
| `Runtime/Core/Phonemizers/Multilingual/MultilingualPhonemizer.cs` | `InitializeAsync()` 内で PuaTokenMapper 初期化呼び出し追加 |
| `Tests/Editor/PuaJsonCrossValidationTests.cs` | スナップショット配列 → ファイル読み込みベースに改修 |
| `Tests/Editor/PuaTokenMapperJsonTests.cs` | **新規**: LoadFromJson テスト |
| `StreamingAssets/uPiper/pua.json` | **新規**: piper-plus からコピー |

### 11.2 影響を受けない箇所

- `PhonemeEncoder`: PuaTokenMapper の公開 API (`Register`, `MapSequence`, `MapToken`, `UnmapChar`) は変更なし
- 各 DotNetG2P エンジン: `ToPuaPhonemes()` 内部で `PuaTokenMapper` を使用するが、API は同一
- ONNX 推論パイプライン: 音素 ID 変換はマッピング内容に依存するが、pua.json とハードコードが同一内容である限り動作変更なし

---

## 12. 実装見積もり

| サブタスク | 見積もり |
|-----------|---------|
| P1-2a: DTO 定義 | 0.5h |
| P1-2b: LoadFromJson パースロジック | 1.5h |
| P1-2c: InitializeFromFile 同期読み込み | 0.5h |
| P1-2d: InitializeAsync 非同期読み込み | 1h |
| P1-2e: MultilingualPhonemizer 統合 | 0.5h |
| P1-2f: StreamingAssets ファイル配置 | 0.5h |
| P1-2g: PuaJsonCrossValidationTests 改修 | 1h |
| P1-2h: 新規テスト追加 | 1.5h |
| **合計** | **7h** |
