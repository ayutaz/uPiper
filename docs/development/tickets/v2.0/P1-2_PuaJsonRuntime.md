# P1-2: pua.json ランタイム読み込み

**マイルストーン**: M2 - Phase 1 Completion (alpha)
**優先度**: P1
**見積もり**: 7h (1 人日)
**依存チケット**: P1-1（PuaTokenMapperインスタンス化が前提）
**後続チケット**: なし（Phase 1完了ゲート）
**ブランチ名**: `feature/v2.0-P1-2-pua-json-runtime`

---

## 1. タスク目的とゴール

`PuaTokenMapper` のハードコード固定マッピング (96エントリ) を `StreamingAssets/uPiper/pua.json` からランタイム読み込みに切り替え、piper-plus との同期を JSON ファイル更新のみで完結できるようにする。

**解決する問題**:

1. **手動同期の二重メンテナンス**: piper-plus で `pua.json` が更新された場合、C# のハードコード (`FixedPuaMapping` の `Dictionary<string, int>` リテラル) と `PuaJsonCrossValidationTests` のスナップショット配列の両方を手動更新する必要がある
2. **更新漏れによる本番不整合**: テストスナップショットの更新忘れは CI で検出されるが、ランタイムのハードコード更新忘れは本番で音素変換の不整合を引き起こす
3. **piper-plus の設計思想との乖離**: piper-plus は `pua.json` を Python/Rust/JS 全実装の "single source of truth" として設計しているが、C# だけがハードコードを保持している

**完了状態 (Definition of Done)**:

- `PuaJsonData` / `PuaJsonEntry` DTO クラスが定義済み
- `LoadFromJson(string json)` がバージョンチェック + エントリパースを実施
- `InitializeFromFile()` が PC/Mobile/Editor で同期読み込みに成功
- `InitializeAsync()` が WebGL を含む全プラットフォームで非同期読み込みに成功
- `StreamingAssets/uPiper/pua.json` にファイル配置済み（piper-plus からコピー）
- `MultilingualPhonemizer.InitializeAsync()` 内で `PuaTokenMapper` 初期化を呼び出し
- `PuaJsonCrossValidationTests` がファイル読み込みベースに改修済み（スナップショット配列廃止）
- pua.json 不在時はハードコード固定マッピングにフォールバック
- 全テスト通過（EditMode + PlayMode）
- `dotnet format --verify-no-changes` 通過

---

## 2. 実装する内容の詳細

### Step 1: pua.json ファイル配置

piper-plus の正規データソース `piper-plus/src/python/g2p/piper_plus_g2p/data/pua.json` を `StreamingAssets/uPiper/pua.json` に手動コピーし、git 管理下に置く。

配置先:
```
StreamingAssets/
  uPiper/
    pua.json          <-- NEW: piper-plus からコピー
    Dictionaries/     <-- 既存: カスタム辞書
    OpenJTalk/        <-- 既存: MeCab 辞書
    Phonemizers/      <-- 既存: CMU dict
```

### Step 2: PuaJsonData / PuaJsonEntry DTO 定義 (P1-2a)

対象ファイル: `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/PuaTokenMapper.cs`（既存ファイル内に内部クラスとして追加）

```csharp
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
    public string codepoint;  // "0xE000" 形式の16進文字列
    public string language;
    public string description;
}
```

pua.json スキーマ (v1):
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
    }
  ]
}
```

| フィールド | 型 | 必須 | 説明 |
|-----------|------|------|------|
| `version` | `int` | Yes | スキーマバージョン。現行は `1` |
| `description` | `string` | No | 人間向け説明文 |
| `entries` | `array` | Yes | PUA マッピングエントリの配列 |
| `entries[].token` | `string` | Yes | 多文字音素トークン (例: `"a:"`, `"ch"`) |
| `entries[].codepoint` | `string` | Yes | PUA コードポイント (`"0xE000"` 形式) |
| `entries[].language` | `string` | Yes | 言語コード (`"ja"`, `"zh"`, `"ko"`, `"es"`, `"fr"`, `"sv"`, `"shared"`) |
| `entries[].description` | `string` | No | 音素の説明 |

現行エントリ数: 96 (ja:28, shared:2, zh:31, ko:8, es:2, fr:3, sv:9)。未割り当てギャップ: `0xE01F`, `0xE053`。

**`JsonUtility` の選定理由**: `CustomDictionary` では `Dictionary<string, object>` 型のため手動パースが必要だったが、`PuaJsonData` は固定スキーマの `[Serializable]` クラスであり `JsonUtility.FromJson<PuaJsonData>(json)` で処理可能。ただし `codepoint` が `"0xE000"` 形式の文字列であるため、数値変換 (`Convert.ToInt32(codepoint, 16)`) は手動で行う。

### Step 3: LoadFromJson パースロジック (P1-2b)

`PuaTokenMapper` インスタンスメソッドとして追加。

**パース手順**:
1. `JsonUtility.FromJson<PuaJsonData>(json)` でデシリアライズ
2. `version` フィールドを `SupportedVersion` (= 1) と比較
3. 各エントリの `codepoint` を `Convert.ToInt32(codepoint, 16)` でパース
4. `_token2Char` / `_char2Token` 辞書をクリア・再構築
5. `_nextDynamic` を最大コードポイント + 1 にセット

**バリデーション項目**:
1. `version` と `entries` フィールドの存在確認
2. `version == SupportedVersion` の検証
3. 各エントリの `token` 非空、`codepoint` パース可能性
4. コードポイント範囲 `0xE000` - `0xF8FF` (PUA-A) の検証
5. 同一 token / 同一 codepoint の重複検出 (Warning ログ)

**エラー時の挙動**: パース失敗・バージョン不一致・データ不正の場合は全て Warning/Error ログを出力し、コンストラクタで初期化済みのハードコードマッピングを維持（`return` で処理を中断、辞書のクリアは行わない）。個別エントリの不正はスキップし、残りのエントリは読み込む。

### Step 4: InitializeFromFile 同期読み込み (P1-2c)

```
InitializeFromFile()
  ├─ Path.Combine(Application.streamingAssetsPath, "uPiper/pua.json")
  ├─ File.Exists() チェック
  │   ├─ false → return false (ハードコードフォールバック維持)
  │   └─ true → File.ReadAllText()
  ├─ LoadFromJson(json)
  │   ├─ バージョンチェック
  │   ├─ エントリパース
  │   └─ _token2Char / _char2Token 再構築
  └─ return true
```

非 WebGL プラットフォームではコンストラクタからの同期呼び出しも可能:

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

### Step 5: InitializeAsync 非同期読み込み (P1-2d)

```
InitializeAsync(ct)
  ├─ WebGLStreamingAssetsLoader.LoadTextAsync("uPiper/pua.json", ct)
  │   ├─ WebGL: UnityWebRequest.Get(url)
  │   └─ PC/Mobile: File.ReadAllTextAsync()
  ├─ LoadFromJson(json)
  └─ return true
  ├─ catch FileNotFoundException → return false (フォールバック)
  └─ catch InvalidOperationException → return false (WebGL 404)
```

**プラットフォーム別読み込み戦略**:

| プラットフォーム | 読み込み方式 | API | フォールバック |
|-----------------|-------------|-----|---------------|
| Windows / Linux / macOS | 同期 (`File.ReadAllText`) | `InitializeFromFile()` | ハードコード |
| iOS / Android | 同期 (`File.ReadAllText`) | `InitializeFromFile()` | ハードコード |
| Unity Editor | 同期 (`File.ReadAllText`) | `InitializeFromFile()` | ハードコード |
| WebGL | 非同期 (`UnityWebRequest`) | `InitializeAsync()` | ハードコード |
| 全プラットフォーム (統一) | 非同期 (`WebGLStreamingAssetsLoader`) | `InitializeAsync()` | ハードコード |

**WebGL 固有の考慮事項**:
- `File.ReadAllText()` は WebGL で使用不可
- `WebGLStreamingAssetsLoader.LoadTextAsync()` が統一的な非同期読み込み API を提供
- 既存パターン: `CustomDictionary.LoadDefaultDictionariesAsync()` が同じ `WebGLStreamingAssetsLoader` を使用しており、同一設計を踏襲

### Step 6: MultilingualPhonemizer.InitializeAsync 統合 (P1-2e)

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

**P1-1 で確立された注入パスとの接続**: P1-1 の後続連絡事項 (Section 7) に記載の通り、`PuaTokenMapper` インスタンスは `PiperTTS` が Composition Root として生成する。P1-2 では、この `PiperTTS` が保持するインスタンスに対して `InitializeAsync` / `InitializeFromFile` を呼び出す。`Token2Char` / `Char2Token` の公開型が `IReadOnlyDictionary` であるため、pua.json 読み込み後の上書きは `private` な `_token2Char` / `_char2Token` に対して行い、内部実装で完結する。

### Step 7: PuaJsonCrossValidationTests 改修 (P1-2g)

現在のスナップショット配列をファイル読み込みに置き換え:

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

### Step 8: 新規テスト追加 (P1-2h)

テストクラス `PuaTokenMapperJsonTests.cs` を `Assets/uPiper/Tests/Editor/Phonemizers/` に新規作成。テスト項目は Section 4.2 を参照。

---

## 3. エージェントチームの役割と人数

**1エージェント構成**（7h = 1人日）

| 役割 | 担当内容 | 工数 |
|------|---------|------|
| 実装エージェント | Step 1-8 の全実装 + テスト | 7h |

**内訳**:

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

**理由**: P1-1 の Agent 2 が M2 で継続するのが効率的（v2.0-milestones.md M2 セクション記載）。PuaTokenMapper のインスタンス化を理解しているエージェントが JSON 読み込み機能を追加する方が、コンテキスト共有コストが低い。

**推奨実施順序**: P1-2a → P1-2b → P1-2f → P1-2c → P1-2d → P1-2e → P1-2g → P1-2h → 全テスト実行

---

## 4. 提供範囲とテスト項目

### 4.1 提供範囲

| カテゴリ | 範囲内 | 範囲外 |
|---------|-------|-------|
| PuaTokenMapper | `LoadFromJson`, `InitializeFromFile`, `InitializeAsync`, DTO クラス追加 | `FixedPuaMapping` の static 降格（P1-2 ではハイブリッド維持） |
| MultilingualPhonemizer | `InitializeAsync` 内での PuaTokenMapper 初期化呼び出し | `ILanguageG2PHandler` 関連の変更（P1-4 の範囲） |
| StreamingAssets | `pua.json` ファイル配置 | piper-plus リポジトリ側の変更 |
| テスト | PuaJsonCrossValidationTests 改修 + PuaTokenMapperJsonTests 新規 | PhonemeEncoder テストの変更（P1-1 で完了済み） |
| CI 検証 | pua.json 存在確認 + LoadFromJson 成功確認 | ビルド時自動コピー機構の構築（将来検討） |

### 4.2 Unit テスト

**PuaTokenMapperJsonTests（新規）**:

| テストメソッド | 検証内容 |
|--------------|---------|
| `LoadFromJson_ValidJson_PopulatesMapping` | 正常な pua.json から全エントリがロードされること |
| `LoadFromJson_InvalidJson_FallsBackToHardcoded` | 不正 JSON でハードコードフォールバックが維持されること |
| `LoadFromJson_UnsupportedVersion_FallsBackToHardcoded` | 未対応バージョンでフォールバックすること |
| `LoadFromJson_MissingEntries_SkipsInvalid` | 不正エントリがスキップされること |
| `LoadFromJson_EmptyCodepoint_SkipsEntry` | codepoint 空のエントリがスキップされること |
| `LoadFromJson_DuplicateToken_LastWins` | 重複 token は後勝ちになること |
| `LoadFromJson_UpdatesDynamicStart` | 動的割り当て開始位置が正しく更新されること |
| `InitializeFromFile_FileExists_ReturnsTrue` | ファイル存在時に true を返すこと |
| `InitializeFromFile_FileMissing_ReturnsFalse` | ファイル不在時に false を返すこと |
| `Register_AfterJsonLoad_DynamicAllocFromCorrectStart` | JSON 読み込み後の動的割り当てが正しい位置から開始されること |

**PuaJsonCrossValidationTests（改修）**:

| 変更種別 | 内容 |
|---------|------|
| スナップショット配列廃止 | `PuaJsonEntries` ハードコード配列を `LoadPuaJsonEntries()` ファイル読み込みに置換 |
| テストロジック維持 | `FixedPuaMapping` との照合テストは同一ロジックのまま、データソースのみ変更 |

### 4.3 E2E テスト

| テスト | 内容 |
|-------|------|
| `InferenceEngineDemo` 手動実行 | pua.json 読み込み後の 6 言語音声生成が正常動作することを確認 |
| CI `unity-tests.yml` 全通過 | EditMode + PlayMode テストの GREEN 確認 |
| pua.json 削除時のフォールバック確認 | pua.json を一時的に除外し、ハードコードフォールバックで音声生成が正常動作することを確認 |

---

## 5. 懸念事項とレビュー項目

### 5.1 技術的懸念

| 懸念 | 深刻度 | 対策 |
|------|--------|------|
| **FixedPuaMapping (static) と pua.json (インスタンス) の不整合** | 中 | P1-1 Section 6.5 で指摘済み。pua.json の内容がハードコードと異なる場合、`IsFixedPua()` が static な `LastFixedCodepoint` を参照するため判定が狂う可能性。v2.0 では pua.json とハードコードを同一内容に維持することで回避。将来的に `FixedPuaMapping` のインスタンス降格を検討 |
| **JsonUtility の制約** | 低 | `JsonUtility.FromJson` はルート配列の直接パースに制約があるが、`PuaJsonData` はオブジェクトルートのため問題なし。`[Serializable]` クラスのネスト配列 (`PuaJsonEntry[]`) は正しくデシリアライズされる |
| **codepoint フォーマットの多様性** | 低 | piper-plus は `"0xE000"` 形式で統一されているが、`"0xe000"` (小文字) や `"E000"` (プレフィックスなし) も `Convert.ToInt32(codepoint, 16)` の前処理で対応する |
| **WebGL での UnityWebRequest 404** | 低 | `WebGLStreamingAssetsLoader` が 404 を `InvalidOperationException` として throw する既存パターンを踏襲。catch してフォールバック |
| **pua.json 読み込みタイミング** | 低 | `MultilingualPhonemizer.InitializeAsync()` 内で呼び出すため、各 DotNetG2P エンジンの初期化より前に完了。エンジンが `ToPuaPhonemes()` を呼ぶ時点では PuaTokenMapper は更新済み |

### 5.2 pua.json と piper-plus の同期に関する懸念

| 懸念 | 対策 |
|------|------|
| piper-plus 側で pua.json が更新されたことに気づかない | CI 自動検証を導入（uPiper の CI で piper-plus の pua.json を取得し差分検出）。v2.0 初期は手動コピー + PR レビューで運用 |
| pua.json の version がインクリメントされた場合 | `SupportedVersion` 定数で未対応バージョンを検出。ハードコードフォールバック + Warning ログ |
| piper-plus と C# の pua.json パースの挙動差異 | `PuaJsonCrossValidationTests` が pua.json ファイルから直接読み込むことで、パースロジックの整合性を保証 |

### 5.3 レビューチェックリスト

- [ ] `PuaJsonData` / `PuaJsonEntry` が `[Serializable]` + `internal` であるか
- [ ] `LoadFromJson` がパース失敗時にハードコードマッピングを維持する（辞書クリアせずに `return`）か
- [ ] `LoadFromJson` がバージョン不一致時に Warning ログを出してフォールバックするか
- [ ] `LoadFromJson` が不正エントリをスキップし、残りを読み込むか
- [ ] `InitializeFromFile` がファイル不在時に `false` を返し、例外を throw しないか
- [ ] `InitializeAsync` が WebGL 404 エラー時に `false` を返し、フォールバックするか
- [ ] `MultilingualPhonemizer.InitializeAsync` 内で PuaTokenMapper 初期化が各言語エンジンより前に呼ばれるか
- [ ] `_nextDynamic` が JSON 読み込み後の最大コードポイント + 1 に更新されるか
- [ ] `StreamingAssets/uPiper/pua.json` が piper-plus の正規ソースと同一内容か
- [ ] `PuaJsonCrossValidationTests` からスナップショット配列が削除され、ファイル読み込みに置換されているか
- [ ] `PuaTokenMapperJsonTests` が正常系/異常系を網羅しているか
- [ ] `dotnet format --verify-no-changes` が通過するか
- [ ] P1-1 で定義された `Token2Char` / `Char2Token` の `IReadOnlyDictionary` 公開型に影響がないか

---

## 6. 一から作り直すとしたら

### 6.1 ハードコード vs ランタイム読み込みのトレードオフ

本チケットの核心は「96エントリのハードコードマッピングをランタイム読み込みに移行する」点にある。両者のトレードオフを整理する。

**ハードコード (v1.x の現状)**:
- メリット: 外部ファイル依存なし。StreamingAssets の読み込み失敗が起こりえない。起動時のファイル I/O ゼロ。コンパイル時にマッピング内容が確定するため、デバッグが容易。
- デメリット: piper-plus との二重メンテナンス。更新漏れの検出がテスト通過後のランタイムまで遅延。C# のみが "single source of truth" から外れたサイロ化。

**ランタイム読み込み (v2.0 の目標)**:
- メリット: piper-plus と同一の `pua.json` を共有することで、ファイルコピーのみで同期完了。テスト・ランタイム・CI が全て同一データソースを参照するため、不整合が構造的に発生しない。
- デメリット: ファイル I/O 依存（WebGL では非同期必須）。pua.json 破損/不在時のフォールバックパスが必要。初期化フローの複雑化（同期/非同期の2パス）。

**ゼロから設計する場合の判断**:

ランタイム読み込みを選択する。理由は以下の通り:

1. **piper-plus エコシステムとの整合性**: pua.json は Python/Rust/JS の3実装で共有される正規データソースであり、C# がこれに参加することで 4 実装全てが同一ソースを参照する。ハードコードの維持は技術的負債の蓄積を意味する。
2. **96エントリの読み込みコスト**: JSON パース + 辞書構築のコストは数ミリ秒レベルであり、TTS 初期化全体（ONNX モデルロード等）と比較して無視可能。
3. **フォールバックによる安全ネット**: ハードコードをフォールバックとして残すことで、ランタイム読み込みの全リスクを吸収できる。「pua.json が読めなくても動く」状態を保証しつつ、「pua.json が読めれば最新のマッピングを使う」という段階的移行が可能。

ただし、ゼロから設計するなら **ハードコードフォールバック自体を廃止し、pua.json を必須ファイルとする** 方がシンプルになる。フォールバックの存在は「pua.json がなくても動いてしまう」状態を許容し、同期忘れを検出する契機を失わせる。pua.json が必須であれば、配置忘れは即座に初期化エラーとして検出される。v2.0 ではハードコードフォールバックを維持するが、v2.x+ で安定した段階で pua.json 必須化を検討すべき理由はここにある。

### 6.2 pua.json スキーマと piper-plus 同期メカニズムの設計考察

**同期メカニズムの3段階**:

v2.0 では「手動コピー + CI 検証」を採用するが、ゼロから設計する場合は以下の3段階を計画的に進める:

| 段階 | 方式 | 実装コスト | 同期精度 |
|------|------|-----------|---------|
| 1. 手動コピー + CI 差分検出 | uPiper CI が piper-plus の pua.json を fetch し、StreamingAssets のコピーと diff | 低 | 高（CI で検出） |
| 2. ビルド時自動コピー | Editor スクリプトが piper-plus ローカルパスから `OnPreprocessBuild` でコピー | 中 | 最高（ビルド時に最新化） |
| 3. NuGet / UPM パッケージ化 | pua.json を含む共有パッケージとして配布 | 高 | 最高（バージョン管理） |

段階 1 が v2.0 の範囲であり、段階 2-3 は将来拡張。段階 1 の具体的な CI 検証:

```yaml
# uPiper CI (.github/workflows/unity-tests.yml) での検証イメージ
- name: Verify pua.json sync
  run: |
    curl -sL https://raw.githubusercontent.com/ayutaz/piper-plus/main/src/python/g2p/piper_plus_g2p/data/pua.json -o /tmp/pua_upstream.json
    diff StreamingAssets/uPiper/pua.json /tmp/pua_upstream.json
```

**バージョニング戦略**:

pua.json の `version` フィールドと piper-plus の `PUA_COMPAT_VERSION` (現在 `1`) を同期させる。C# 側は `SupportedVersion` 定数を持ち、未対応バージョンはフォールバック。モデルの `config.json` に `pua_compat_version` が含まれる場合の互換性チェックは将来拡張として予約。

```
piper-plus 側:
  pua.json version: 1
  PUA_COMPAT_VERSION: 1

uPiper 側:
  SupportedVersion: 1
  → version 1 のみ受け入れ
  → version 2+ は Warning + フォールバック
```

### 6.3 WebGL 非同期読み込みの設計考察

WebGL で `File.ReadAllText` が使えないことは、pua.json 読み込みに限らず `CustomDictionary` / MeCab 辞書でも同じ制約であり、`WebGLStreamingAssetsLoader.LoadTextAsync` が統一的な解決策として確立されている。

**ゼロから設計する場合の選択肢**:

| 案 | 方式 | メリット | デメリット |
|----|------|---------|-----------|
| A. 現行案 | `InitializeFromFile` (同期) + `InitializeAsync` (非同期) の2 API | プラットフォームごとに最適な呼び出しが可能 | 呼び出し側が `#if UNITY_WEBGL` で分岐する必要あり |
| B. 非同期一本化 | `InitializeAsync` のみ提供。PC/Mobile でも非同期 | API がシンプル。呼び出し側の分岐不要 | PC/Mobile での同期コンストラクタ初期化ができない |
| C. ビルド時埋め込み | `Editor` スクリプトで pua.json をバイト配列としてクラスに埋め込み | ランタイムファイル I/O 不要。WebGL 制約を完全回避 | ビルドプロセスへの依存。ランタイムで差し替え不可 |

v2.0 では **案 A** を採用。理由:
- PC/Mobile/Editor では同期読み込みの方がコードフローがシンプル（`async void` 回避）
- WebGL では `InitializeAsync` が必須だが、これは `CustomDictionary` と同じパターンであり追加の学習コストがない
- `MultilingualPhonemizer.InitializeAsync()` が既に非同期メソッドとして存在するため、その中で `PuaTokenMapper.InitializeAsync()` を `await` する自然な統合ポイントがある

ゼロから設計するなら **案 B** が最もシンプルになる。全プラットフォームで `await mapper.InitializeAsync()` の一行で済み、`#if UNITY_WEBGL` 分岐が不要。ただし既存の初期化フローが同期/非同期のハイブリッドで構築されているため、v2.0 での全面的な非同期一本化は影響範囲が大きすぎる。

### 6.4 ハードコードフォールバックの存在意義と段階的廃止

P1-1 Section 6.5 で指摘された「FixedPuaMapping (static) と pua.json (インスタンス) の不整合」は、ハードコードフォールバックが存在する限り構造的に残る。

**段階的廃止計画**:

| バージョン | ハードコードの扱い | pua.json の扱い |
|-----------|-------------------|----------------|
| v2.0 | フォールバック専用として全96エントリ維持 | オプショナル（推奨） |
| v2.1 | 最小サブセット (ja のみ, 28エントリ) に縮小 | 事実上必須（非 ja 言語は pua.json 必須） |
| v2.2+ | 完全削除 | 必須（pua.json 不在は初期化エラー） |

v2.0 でフォールバックを維持する理由は移行リスクの最小化のみであり、将来的な廃止は設計意図として明確にしておく。

---

## 7. 後続タスクへの連絡事項

### Phase 1 完了ゲートへの影響

P1-2 は M2 の Phase 1 完了ゲートに含まれる最後の PuaTokenMapper 関連タスクであり、後続チケットはない。M2 の完了条件:

- `PuaTokenMapper` が pua.json からランタイム読み込み可能（本チケット）
- `MultilingualPhonemizer` が `Dictionary<string, HandlerEntry>` レジストリで動作（P1-3）
- `IPhonemizerBackend` インターフェースが削除されている（P1-5）
- `[Obsolete]` コンストラクタ・プロパティが全て削除されている（P1-6）

P1-2 は他の M2 タスク（P1-3, P1-5, P1-6）と並行実行可能であり、マージ順序の制約はない（P1-2 は `PuaTokenMapper.cs` がメイン変更対象であり、P1-3/P1-5/P1-6 の `MultilingualPhonemizer.cs` 集中変更とは独立）。

### 変更対象ファイル一覧

| ファイル | 変更内容 |
|---------|---------|
| `Runtime/Core/Phonemizers/Multilingual/PuaTokenMapper.cs` | `InitializeFromFile()`, `InitializeAsync()`, `LoadFromJson()`, DTO クラス追加 |
| `Runtime/Core/Phonemizers/Multilingual/MultilingualPhonemizer.cs` | `InitializeAsync()` 内で PuaTokenMapper 初期化呼び出し追加 |
| `Tests/Editor/PuaJsonCrossValidationTests.cs` | スナップショット配列 → ファイル読み込みベースに改修 |
| `Tests/Editor/Phonemizers/PuaTokenMapperJsonTests.cs` | **新規**: LoadFromJson 正常系/異常系テスト |
| `StreamingAssets/uPiper/pua.json` | **新規**: piper-plus からコピー |

### 影響を受けない箇所

- `PhonemeEncoder`: PuaTokenMapper の公開 API (`Register`, `MapSequence`, `MapToken`, `UnmapChar`) は変更なし
- 各 DotNetG2P エンジン: `ToPuaPhonemes()` 内部で `PuaTokenMapper` を使用するが、API は同一
- ONNX 推論パイプライン: 音素 ID 変換はマッピング内容に依存するが、pua.json とハードコードが同一内容である限り動作変更なし

### M3 以降への申し送り

- pua.json ランタイム読み込みが安定したら、ハードコードフォールバックの縮小/廃止を検討（Section 6.4 の段階的廃止計画を参照）
- `FixedPuaMapping` を static から完全インスタンス化する場合は `IsFixedPua()` のロジック変更が必要（P1-1 Section 6.3 案 A に相当）
- CI 自動検証（piper-plus の pua.json との diff 検出）の導入は M2 完了後に検討