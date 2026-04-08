# P1-5: G2P全同期化（IPhonemizerBackend廃止）

**マイルストーン**: M2 - Phase 1 完了 (alpha)
**優先度**: P0
**見積もり**: 1 人日
**依存チケット**: P1-4
**後続チケット**: P1-6
**ブランチ名**: `feature/v2.0-P1-5-g2p-synchronization`
**設計ドキュメント**: [P1-5_G2PSynchronization.md](../../v2.0-design/P1-5_G2PSynchronization.md)

---

## 1. タスク目的とゴール

### なぜこのタスクが必要か

`IPhonemizerBackend` インターフェースと `ProcessFallbackAsync` メソッドは、v1.4.0 時点でプロダクションコードから実質的に呼び出されていない。全言語の G2P 処理は DotNetG2P エンジンの同期呼び出しに移行済みであり、`IPhonemizerBackend` はテストスタブ注入専用のレガシーパスとしてのみ残存している。

P1-4（ILanguageG2PHandler 全面移行）完了後、switch 文は `_handlers[lang].Process(text)` に置換され、`ProcessFallbackAsync` への呼び出しパスは死コードとなる。P1-5 はこのレガシーコードを物理削除し、`MultilingualPhonemizer` 内の非同期フォールバックパスを完全に排除する「掃除」タスクである。

未対応言語はハンドラ未登録として警告ログのみに簡素化し、`IPhonemizerBackend` の非同期インターフェースに依存するコードパスをゼロにする。

### 完了の定義

- `IPhonemizerBackend.cs` + meta が削除されている
- `PhonemizerBackendOptions` クラスが削除されている
- `MultilingualPhonemizer` 内の `ProcessFallbackAsync`, `GetBackendForLanguage` が削除されている
- `MultilingualPhonemizerOptions.EnPhonemizer` / `KoPhonemizer` が削除されている
- `MultilingualPhonemizer` の Obsolete コンストラクタ内の `IPhonemizerBackend` パラメータが削除されている（P1-6 と同時の場合はコンストラクタごと削除）
- 未対応言語に対して `PiperLogger.LogWarning` が出力される
- 未対応言語のセグメントがスキップされ、他セグメントが正常に連結される
- 全既存テスト（EditMode + PlayMode）がパスする
- `dotnet format --verify-no-changes` がパスする
- `Backend/README.md` が更新されている

---

## 2. 実装する内容の詳細

### Step 1: `IPhonemizerBackend.cs` + meta 削除

**削除対象ファイル**:

| ファイル | 理由 |
|---------|------|
| `Assets/uPiper/Runtime/Core/Phonemizers/Backend/IPhonemizerBackend.cs` | インターフェース本体 + `PhonemizerBackendOptions` クラス |
| `Assets/uPiper/Runtime/Core/Phonemizers/Backend/IPhonemizerBackend.cs.meta` | Unity メタファイル |

**保持するファイル**:

| ファイル | 理由 |
|---------|------|
| `PhonemeOptions.cs` | `PhonemeResult` / `PhonemeOptions` は他コンポーネントで広く使用（IPhonemizer, DotNetG2PPhonemizer, PiperTTS, PhonemeCache 等） |
| `README.md` | ディレクトリ説明（内容は Step 6 で更新） |

### Step 2: `MultilingualPhonemizerOptions` から `IPhonemizerBackend` 関連を削除

**ファイル**: `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/MultilingualPhonemizerOptions.cs`

削除対象:
- `EnPhonemizer` プロパティ + `[Obsolete]` 属性 + XML doc（L31-33）
- `KoPhonemizer` プロパティ + `[Obsolete]` 属性 + XML doc（L47-49）
- `using uPiper.Core.Phonemizers.Backend`（L9）

### Step 3: `MultilingualPhonemizer` からレガシーコードを削除

**ファイル**: `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/MultilingualPhonemizer.cs`

削除対象:

| 対象 | 行番号 | 種別 |
|------|--------|------|
| `_enPhonemizer` フィールド | L60 | private フィールド |
| `_koPhonemizer` フィールド | L65 | private フィールド |
| `ProcessFallbackAsync` メソッド | L647-664 | private async メソッド |
| `GetBackendForLanguage` メソッド | L668-678 | private メソッド |
| Options 経由の `_enPhonemizer`/`_koPhonemizer` 代入 | L100-103 | コンストラクタ内 |
| `#pragma warning disable/restore CS0618` | L100, L103 | pragma |
| Obsolete コンストラクタ内の `_enPhonemizer`/`_koPhonemizer` 代入 | L135, L140 | コンストラクタ内 |
| Obsolete コンストラクタの `enPhonemizer`/`koPhonemizer` パラメータ | L119, L124 | パラメータ |
| `InitializeAsync` 内の `_enPhonemizer` null チェック | L168 | 条件式 |
| 全バックエンド未初期化チェック内の `_enPhonemizer`/`_koPhonemizer` | L271-273 | 条件式 |
| Dispose 内の `_enPhonemizer?.Dispose()` | L435 | dispose 呼び出し |
| Dispose 内の `_koPhonemizer?.Dispose()` | L444 | dispose 呼び出し |
| `using uPiper.Core.Phonemizers.Backend` | L14 | using 文 |

注意: 行番号は v1.4.0 ベース。P1-4 完了後にずれている可能性があるため、内容ベースで特定すること。

### Step 4: switch default ケースの置換（警告ログ実装）

P1-4 完了後のハンドラベース実装における未登録言語の処理:

```csharp
// P1-4 完了後の _handlers.TryGetValue パターン
if (_handlers.TryGetValue(lang, out var handler))
{
    (segPhonemes, segA1, segA2, segA3) = handler.Process(segText);
}
else
{
    PiperLogger.LogWarning(
        $"[MultilingualPhonemizer] Unsupported language '{lang}', skipping segment: \"{segText}\"");
    continue;
}
```

P1-4 が switch 文を残している場合（中間状態）:

```csharp
// Before (非同期フォールバック)
default:
    var fallbackResult = await ProcessFallbackAsync(lang, segText, cancellationToken);
    if (fallbackResult.phonemes == null)
        continue;
    (segPhonemes, segA1, segA2, segA3) = fallbackResult;
    break;

// After (警告ログのみ)
default:
    PiperLogger.LogWarning(
        $"[MultilingualPhonemizer] Unsupported language '{lang}', skipping segment: \"{segText}\"");
    continue;
```

**未対応言語の処理方針**:
- 例外は投げない。セグメントを無音（スキップ）として扱う
- 既存動作との一貫性を維持（`ProcessFallbackAsync` でも `backend == null` 時は `continue` していた）
- スキップされたセグメントの音素は出力に含まれず、他セグメントの音素は正常に連結される
- ログレベルは `Warning`（`PiperLogger.LogWarning`）。1セグメントにつき1回出力

### Step 5: テスト書き換え + 新規テスト追加

詳細はセクション 4.2 参照。

### Step 6: ドキュメント更新

- `Assets/uPiper/Runtime/Core/Phonemizers/Backend/README.md`: `IPhonemizerBackend` の記述を削除。`PhonemeResult` / `PhonemeOptions` の説明のみ残す
- `CLAUDE.md`: `IPhonemizerBackend` の記述を削除（「テストスタブのみ使用」のコメント等）

---

## 3. エージェントチームの役割と人数

### 推奨構成: エージェント 1名

P1-5 は P1-4 の成果物に対する「削除 + 掃除」が主体であり、新規実装は警告ログの1箇所のみ。作業量（1人日）から単独エージェントが最適。

| フェーズ | 作業内容 | 見積もり |
|---------|---------|---------|
| ファイル削除 + コード削除 | Step 1-3: IPhonemizerBackend.cs 削除、MultilingualPhonemizer/Options のレガシーコード削除 | 2h |
| 警告ログ実装 | Step 4: 未対応言語の処理パス実装 | 0.5h |
| テスト書き換え | Step 5: EosTests のスタブ移行 + DeepTests の書き換え + 新規テスト | 3h |
| ドキュメント + 検証 | Step 6: README/CLAUDE.md 更新、dotnet format、全テスト実行 | 1h |

### M2 での実施順序

マイルストーン計画に従い、Agent 1 が `P1-3 -> P1-5 -> P1-6` を順次担当する（`MultilingualPhonemizer.cs` の連続変更によるコンフリクト回避）。P1-3 のマージ完了後に P1-5 ブランチを作成し、リベースして実施する。

---

## 4. 提供範囲とテスト項目

### 4.1 提供範囲

#### スコープ内

- `IPhonemizerBackend.cs` + meta ファイルの物理削除
- `PhonemizerBackendOptions` クラスの物理削除（`IPhonemizerBackend.cs` 内に定義）
- `MultilingualPhonemizer` からの `ProcessFallbackAsync`, `GetBackendForLanguage`, `_enPhonemizer`, `_koPhonemizer` 削除
- `MultilingualPhonemizerOptions` からの `EnPhonemizer`, `KoPhonemizer` プロパティ削除
- Obsolete コンストラクタ内の `IPhonemizerBackend` パラメータ削除（コンストラクタ自体は P1-6 スコープ）
- 未対応言語の警告ログ実装（`PiperLogger.LogWarning`）
- テスト内 `StubPhonemizerBackend` の `ILanguageG2PHandler` スタブへの完全移行
- `Backend/README.md`, `CLAUDE.md` の更新

#### スコープ外（後続タスクまたは別スコープ）

- `[Obsolete]` 14引数コンストラクタの削除 -> **P1-6**
- `MultilingualPhonemizerOptions` の個別エンジンプロパティ削除 -> **P1-6**
- `PhonemeResult` / `PhonemeOptions` の名前空間移動（`Backend` -> `Phonemizers`） -> **スコープ外**（将来検討）
- `_ownsXxx` フラグの `HandlerEntry` struct 化 -> **P1-3**

### 4.2 Unitテスト

#### 4.2.1 書き換えが必要なテスト

##### MultilingualPhonemizerEosTests.cs

**ファイル**: `Assets/uPiper/Tests/Editor/Phonemizers/MultilingualPhonemizerEosTests.cs`

**影響**: 大。テスト内に `StubPhonemizerBackend` クラス（`IPhonemizerBackend` 実装）が定義され、12箇所で使用。

**書き換え方針**: `StubPhonemizerBackend` を P1-4 で導入済みの `ILanguageG2PHandler` 実装スタブ（`StubG2PHandler` または `StubEnglishHandler`）に完全置換。テストスタブは同期メソッド `Process(string text)` のみ実装。

**影響を受けるテストメソッド**:

| テストメソッド | StubPhonemizerBackend 使用 |
|--------------|--------------------------|
| `EosLikeTokens_ContainsPuaQuestionMarkers` | enStub (EN intermediate) |
| `IntermediateSegment_PuaEos_Stripped` | enStub (EN intermediate) |
| `FinalSegment_PuaEos_Preserved` | enStub (EN only) |
| `IntermediateSegment_StandardEos_Stripped` | enStub (EN intermediate) |
| `NonJapaneseSegment_NoPadStripping` | enStub (EN only) |
| `AllEosTokens_StrippedFromIntermediateSegment` | enStub (EN intermediate) |
| `IntermediateSegment_NoEos_PassesThrough` | enStub (EN intermediate) |
| `ProsodyArrays_AlignedAfterEosStrip` | enStub (EN intermediate) |

##### MultilingualPhonemizerDeepTests.cs

**ファイル**: `Assets/uPiper/Tests/Editor/Phonemizers/MultilingualPhonemizerDeepTests.cs`

**影響**: 小。`GetBackendForLanguage_UnknownLang_FallsBackToEnglish` テスト（L493-510）のみ。

**書き換え方針**: テスト内容を「未登録言語のセグメントがスキップされ、警告ログが出力されること」の検証に変更。`GetBackendForLanguage` メソッド自体が削除されるため、テスト名も `UnregisteredLanguage_LogsWarningAndSkips` に変更する。

#### 4.2.2 影響なしのテスト

| テストファイル | 理由 |
|-------------|------|
| `PhonemeResultTest.cs` | `PhonemeResult` のみ使用（`IPhonemizerBackend` 非参照） |
| `MultilingualPhonemizerTests.cs` | 実エンジン使用（`IPhonemizerBackend` 非参照） |
| `DotNetG2PPhonemizerTests.cs` 等 | 個別言語テスト（`IPhonemizerBackend` 非参照） |

#### 4.2.3 新規テスト追加

| テストクラス | テストメソッド | 検証内容 |
|------------|--------------|---------|
| `MultilingualPhonemizerUnsupportedLangTests` | `UnsupportedLanguage_LogsWarningAndSkips` | 未登録言語コードのセグメントがスキップされ、`PiperLogger.LogWarning` で警告ログが出力されること |
| 〃 | `UnsupportedLanguage_OtherSegmentsPreserved` | 未対応セグメントがスキップされても、他言語セグメントの音素が正常に含まれること（例: 日本語 + 未対応言語の混在テキスト） |

### 4.3 E2Eテスト

#### 既存統合テストの振る舞い不変確認

以下の既存テストファイルが修正後に全てパスすることを確認:

| テストファイル | 確認事項 |
|--------------|---------|
| `MultilingualPhonemizerTests.cs` | 実エンジン使用テストの振る舞い不変 |
| `MultilingualPhonemizerDeepTests.cs` | `GetBackendForLanguage` テスト書き換え後、他テストの振る舞い不変 |
| `MultilingualPhonemizerEosTests.cs` | スタブ移行後、EOS処理の振る舞い不変 |
| `MultilingualPhonemizerPhase5Tests.cs` | 各言語テストの振る舞い不変 |

---

## 5. 懸念事項とレビュー項目

### 5.1 技術的懸念

| 懸念 | リスク | 緩和策 |
|------|--------|--------|
| **EOS テストのカバレッジ低下** | `StubPhonemizerBackend` 削除でテストの忠実度が下がる可能性 | P1-4 で導入済みの `ILanguageG2PHandler` スタブ（`StubG2PHandler`）で同等のテストケースを確実に移植。入出力の同一性を検証 |
| **未対応言語でのサイレント無音** | ユーザーが未対応言語を入力しても音声が生成されず、原因が分かりにくい | `PiperLogger.LogWarning` で明示的にログ出力。ドキュメント（README, CLAUDE.md）で対応言語を明示 |
| **Obsolete コンストラクタ内の `IPhonemizerBackend` パラメータ削除の影響** | P1-6 の Obsolete コンストラクタ削除と作業が重複する可能性 | P1-5 ではパラメータの削除のみ行い、コンストラクタ自体の削除は P1-6 に委譲。P1-5 を先にマージし、P1-6 はリベース後に実施 |
| **P1-4 の完了状態への依存** | P1-4 で `StubPhonemizerBackend` が完全に `StubG2PHandler` に移行されていない場合、P1-5 での追加書き換えが増える | P1-4 の後続タスク連絡事項（セクション7）に「一部テストが `IPhonemizerBackend` ベースのスタブをまだ使用している可能性」と明記済み。P1-5 着手前に grep で残存箇所を確認 |
| **using 参照の削除漏れ** | `using uPiper.Core.Phonemizers.Backend` が `IPhonemizerBackend` 削除後もコンパイルエラーにならない場合がある（`PhonemeResult` 参照で using が必要なファイル）| `IPhonemizerBackend` のみを参照していたファイルから using を削除。`PhonemeResult` を参照するファイルは using を保持。設計ドキュメントの using 参照一覧表（セクション2.5）で判定 |
| **WebGLでの長文入力のフレームドロップ** | WebGLでの長文入力(500文字超)の場合、同期G2P処理がフレームドロップの可能性あり | v2.0ではドキュメント記載のみで対応。v2.1でTask.Yield()による処理分割を検討 |

### 5.2 IPhonemizerBackend がプロダクション未使用である根拠

P1-5 の前提となる「`IPhonemizerBackend` はプロダクションで実質未使用」という判断の根拠:

1. **v1.4.0 での移行完了**: 全7言語の G2P 処理は DotNetG2P エンジンの同期呼び出しに移行済み。`MultilingualPhonemizer.PhonemizeWithProsodyAsync` の switch 文で各言語の `Process` メソッドが直接呼ばれ、`ProcessFallbackAsync` に到達するのは switch の `default` ケース（未対応言語）のみ
2. **`_enPhonemizer` / `_koPhonemizer` は常に null**: `PiperTTS.Inference.cs`（Composition Root）が `MultilingualPhonemizerOptions` を構築する際、`EnPhonemizer` / `KoPhonemizer` プロパティを設定していない。結果として `ProcessFallbackAsync` 内の `GetBackendForLanguage` は常に null を返し、警告ログに到達する
3. **テストスタブ専用**: `IPhonemizerBackend` の唯一の実装は `MultilingualPhonemizerEosTests.cs` 内の `StubPhonemizerBackend` クラス（テスト用）

### 5.3 レビューチェックリスト

- [ ] `IPhonemizerBackend.cs` + meta が物理削除されているか
- [ ] `PhonemizerBackendOptions` クラスが削除されているか
- [ ] `MultilingualPhonemizer` から `ProcessFallbackAsync`, `GetBackendForLanguage`, `_enPhonemizer`, `_koPhonemizer` が全て削除されているか
- [ ] `MultilingualPhonemizerOptions` から `EnPhonemizer`, `KoPhonemizer` が削除されているか
- [ ] `using uPiper.Core.Phonemizers.Backend` が `IPhonemizerBackend` のみを参照していたファイルから削除されているか
- [ ] `PhonemeResult` を参照するファイルの using は **保持** されているか
- [ ] `Backend/` ディレクトリが `PhonemeOptions.cs` のために存続しているか（ディレクトリごと削除していないか）
- [ ] 未対応言語に対して `PiperLogger.LogWarning` が出力されるか
- [ ] テスト内の `StubPhonemizerBackend` が完全に除去されているか
- [ ] `#pragma warning disable CS0618`（`IPhonemizerBackend` 関連）が除去されているか
- [ ] 全テスト（EditMode + PlayMode）がパスするか
- [ ] `dotnet format --verify-no-changes` がパスするか

---

## 6. 一から作り直すとしたら

### 6.1 非同期 G2P の必要性自体の再検討

本チケットは「`IPhonemizerBackend` の非同期インターフェースを廃止する」という方針だが、一から設計するなら「G2P に非同期が必要か」をそもそも問い直す必要がある。

**現状の非同期の用途**:
- `IPhonemizerBackend.PhonemizeAsync()`: 外部プロセス（espeak 等）への非同期呼び出しを想定した設計。しかし v1.4.0 で全言語が DotNetG2P（インプロセス同期呼び出し）に移行し、非同期の実需が消滅
- `MultilingualPhonemizer.InitializeAsync()`: MeCab 辞書の読み込み（WebGL では非同期が必須）。これは G2P の「処理」ではなく「初期化」の非同期であり、`IPhonemizerBackend` とは無関係

**ゼロベースの判断**: G2P の「処理」は同期で十分。7言語全てがインプロセスの辞書ルックアップ + ルールベース変換であり、I/O バウンドな処理は存在しない。非同期化のオーバーヘッド（`Task` アロケーション、`async` state machine 生成）は不要なコスト。

唯一の例外は「将来的にクラウド API ベースの G2P（例: Google Cloud TTS の音素化 API）を統合する場合」だが、これは uPiper のオフライン TTS というコンセプトと矛盾する。クラウド G2P が必要になった場合は、`ILanguageG2PHandler` に `ProcessAsync` を追加する（または別インターフェースを定義する）方が、全ハンドラに async を強制するより健全。

### 6.2 PhonemeResult / PhonemeOptions の配置問題

P1-5 では `IPhonemizerBackend.cs` を削除しつつ `PhonemeResult` / `PhonemeOptions` を `Backend/` ディレクトリに残す。これにより `Backend/` というディレクトリ名と実態（Backend インターフェースなし、共有型のみ）が乖離する。

**ゼロベース設計**: `PhonemeResult` / `PhonemeOptions` を `uPiper.Core.Phonemizers` 名前空間に移動し、`Backend/` ディレクトリを廃止する。ただし、この名前空間移動は P1-5 のスコープでは過大な変更（using 参照の更新が8ファイル以上）であり、Phase 2 以降の検討事項とする。

現設計の妥協点: `Backend/README.md` を更新し、ディレクトリの存在理由（`PhonemeResult` / `PhonemeOptions` の共有型定義）を明記することで、「なぜ Backend というディレクトリに Backend がないのか」への回答を残す。

### 6.3 テストスタブの設計: IPhonemizerBackend vs ILanguageG2PHandler

`IPhonemizerBackend` のテストスタブ（`StubPhonemizerBackend`）は非同期メソッド（`PhonemizeAsync`）を実装しており、テスト内で `Task.FromResult` を返す定型コードが多い。P1-4 で導入された `ILanguageG2PHandler` のスタブ（`StubG2PHandler`）は同期メソッド（`Process`）のみであり、テストコードが簡潔になる。

**一から作るなら**: テストスタブはそもそも `IPhonemizerBackend` のような「外部プロセス呼び出しを抽象化するインターフェース」ではなく、「G2P 処理の純粋なインプット/アウトプットを差し替える」だけの薄い層であるべきだった。`ILanguageG2PHandler` の同期 `Process` メソッドがこの理想形に近い。

P1-5 でのスタブ移行は「非同期の不要なオーバーヘッドを除去する」という効果に加え、テストの可読性向上にも寄与する。

### 6.4 段階的削除 vs 一括削除

P1-5 は P1-4 の「残骸掃除」、P1-6 は「Obsolete コンストラクタ削除」と、2つのチケットに分かれている。一から計画するなら P1-5 と P1-6 を統合し「レガシー API 全廃」として1チケットにする選択肢もある。

**分割の理由（現設計の判断）**:
- P1-5（`IPhonemizerBackend` 廃止）と P1-6（Obsolete コンストラクタ削除）は影響範囲が異なる。P1-5 は `MultilingualPhonemizer` 内部のリファクタリング、P1-6 は外部 API（コンストラクタシグネチャ）の破壊的変更
- PR のレビュー粒度として、「内部の掃除」と「外部 API の破壊」を分離した方がレビュー負荷が低い
- M2 マイルストーンの推奨マージ順序が `P1-3 -> P1-5 -> P1-6` であり、P1-5 マージ後の安定確認を挟んでから P1-6 に進める安全策

**一括実施のリスク**: `IPhonemizerBackend` 削除と Obsolete コンストラクタ削除を同時に行うと、コンパイルエラーの修正箇所が交差し、デバッグが困難になる可能性がある。特にテスト内の `#pragma warning disable CS0618` と `StubPhonemizerBackend` の書き換えが同時に発生すると、テスト失敗時の原因切り分けが難しい。

### 6.5 Phase 1 全体のゼロベース設計考察

P1-5 を Phase 1 全体（P1-1 ~ P1-6）の一部として統合的に評価する。（P1-1 セクション 6.6、P1-4 セクション 6.7、P1-6 セクション 6.5 と相互参照）

#### P1-5/P1-6 統合の再検討

P1-5（`IPhonemizerBackend` 廃止）と P1-6（Obsolete コンストラクタ削除）は両方とも P1-4 完了後の「レガシー掃除」であり、統合の可能性がセクション 6.4 および P1-6 セクション 6.5 で言及されている。Phase 1 全体を一から設計する場合、この統合を改めて検討する。

**統合する場合の作業量**: P1-5（1人日）+ P1-6（1人日）= 2人日。変更ファイルの重複を考慮すると、統合時は約 1.5 人日に圧縮可能（`MultilingualPhonemizerOptions` の変更が1回で済む、`#pragma warning disable` の除去が一括化等）。PR サイズは 12 ファイル程度で、P1-4（30+ ファイル）と比較してレビュー可能な範囲。

**統合しない根拠の再評価**: セクション 6.4 で「P1-5 は未対応言語の処理設計を含む」と述べたが、実際にはその設計判断は「警告ログ + continue」の1行に集約される。これは独立したレビューポイントとするほどの設計の重さを持たない。P1-6 の「機械的な削除 + テスト書き換え」もレビュー負荷は低い。

**結論**: 一から計画するなら P1-5 + P1-6 を「P1-5: レガシー API 全廃」として統合するのが効率的。ただし、現計画では M2 の推奨マージ順序（P1-3 -> P1-5 -> P1-6）を維持し、P1-5 完了後の安定確認を挟む安全策を優先する。この判断は「効率性 < 安全性」のトレードオフであり、チームの安全志向に合致する。

#### P1-4 との連続性: switch 文のライフサイクル

P1-4 が `PhonemizeWithProsodyAsync` 内の switch 文を `_handlers[lang].Process(text)` に置換した後、P1-5 では switch の `default` ケース（`ProcessFallbackAsync` 呼び出し）を「警告ログ + continue」に置換する。しかし、P1-4 完了時点で switch 文自体が `_handlers.TryGetValue` パターンに置き換わっている場合、`default` ケースは `else` ブロックに対応する。

Phase 1 全体を通した switch 文のライフサイクル:

| 時点 | `PhonemizeWithProsodyAsync` 内の分岐 |
|------|--------------------------------------|
| v1.4.0 | `switch (lang)` + 7言語 case + `default: ProcessFallbackAsync` |
| P1-4 後 | `if (_handlers.TryGetValue(lang, out var h)) h.Process() else ProcessFallbackAsync()` |
| P1-5 後 | `if (_handlers.TryGetValue(lang, out var h)) h.Process() else LogWarning + continue` |
| P1-6 後 | 同上（P1-6 はこのメソッドに変更なし） |

この追跡により、P1-5 の変更が P1-4 の成果物のどこに作用するかが明確になる。

#### DI 戦略: IPhonemizerBackend 廃止の Phase 1 全体への影響

`IPhonemizerBackend` は Phase 1 の DI 設計において「廃止されるインターフェース」として位置づけられる。Phase 1 で導入/維持/廃止されるインターフェースを整理する:

| インターフェース | Phase 1 での扱い | 理由 |
|---------------|----------------|------|
| `ILanguageG2PHandler` (P1-4) | **新規導入** | Strategy パターンで言語別処理を分離 |
| `IPhonemizerBackend` (P1-5) | **廃止** | 非同期フォールバックの実需消滅 |
| `IPuaTokenMapper` (P1-1 で不採用) | **見送り** | モック需要なし、YAGNI |

この表が示す通り、Phase 1 のインターフェース戦略は「必要なものだけ導入し、不要なものは廃止する」最小主義。P1-1 セクション 6.3 案 B で検討された `IPuaTokenMapper` の不採用と、P1-5 の `IPhonemizerBackend` 廃止は、同じ YAGNI 原則の表裏である。

#### テスト戦略: スタブ移行の横断整理

P1-5 の主要テスト作業は `StubPhonemizerBackend` から `StubG2PHandler` への移行。これは P1-4 で導入された `StubG2PHandler` を使用する。Phase 1 全体のテストスタブ移行を整理する:

| テストファイル | v1.4.0 のスタブ | P1-4 後 | P1-5 後 | P1-6 後 |
|-------------|---------------|---------|---------|---------|
| `MultilingualPhonemizerEosTests.cs` | `StubPhonemizerBackend` | `StubPhonemizerBackend` 残存 | **`StubG2PHandler` に移行** | 変更なし |
| `MultilingualPhonemizerPhase5Tests.cs` | 旧コンストラクタ直接 | Options 版に移行済み | 変更なし | pragma 除去 |
| その他テスト6ファイル | 旧コンストラクタ直接 | 一部 Options 移行済み | 変更なし | **Options 版に統一** |

P1-5 でスタブ移行が完了した後、P1-6 では `StubPhonemizerBackend` クラス定義が完全に消滅していることを前提として作業できる。この前提が崩れる（P1-5 で移行漏れがある）場合、P1-6 でコンパイルエラーが発生するため、早期検出が可能。

P1-3 セクション 6.6 で指摘した `StubG2PHandler` のテストヘルパーディレクトリ配置（`Tests/Editor/Helpers/`）は、P1-5 でのスタブ移行作業を簡素化する。P1-4 でローカルに定義された `StubG2PHandler` を共通ヘルパーに昇格させるのは P1-5 の責務とするのが自然。

#### piper-plus 互換性の観点

P1-5 は piper-plus との互換性に直接的な影響はない（`IPhonemizerBackend` は C# 固有のインターフェース）。ただし、`IPhonemizerBackend` の廃止は「外部プロセス（espeak 等）への非同期呼び出し」という拡張ポイントの削除を意味する。piper-plus は espeak を直接呼び出す Python/Rust コードを持つが、uPiper は DotNetG2P に完全移行済みであり、espeak パスは不要。この判断は P1-2 セクション 6.5 の piper-plus 互換性整理（「P1-5: C# 固有の掃除、piper-plus は最初から同期 G2P」）と整合する。

#### Phase 1 完了後の理想 vs 現実

**理想**: P1-4 + P1-5 + P1-6 が統合され、`ILanguageG2PHandler` 導入と同時に `IPhonemizerBackend` 廃止 + Obsolete 削除が完了。`MultilingualPhonemizer` は一度のリファクタリングでクリーンな状態に到達。テストの `#pragma warning disable` は一切発生しない。

**現実の妥協**: P1-4 → P1-3 → P1-5 → P1-6 の段階的移行により、`MultilingualPhonemizer` は M2 完了まで「新旧混在」状態が続く（P1-4 セクション 6.7 参照）。P1-5 はこの中間状態を「レガシーコードの物理削除」により段階的に縮小する役割を担う。

---

## 7. 後続タスクへの連絡事項

### P1-6（Obsolete コンストラクタ削除）への連絡

- P1-5 完了後、`MultilingualPhonemizer` の14引数 `[Obsolete]` コンストラクタからは `IPhonemizerBackend` パラメータ（`enPhonemizer`, `koPhonemizer`）が削除されている。P1-6 ではこのコンストラクタ自体を物理削除すること
- `MultilingualPhonemizerEosTests.cs` の `StubPhonemizerBackend` は P1-5 で完全に `ILanguageG2PHandler` スタブに移行済み。P1-6 では `StubPhonemizerBackend` クラスの残存がないことを前提として作業可能
- `MultilingualPhonemizerOptions` からは `EnPhonemizer` / `KoPhonemizer` が P1-5 で削除済み。P1-6 では残りの個別エンジンプロパティ（`JaPhonemizer`, `EnEngine`, `EsEngine`, `FrEngine`, `PtEngine`, `ZhEngine`, `KoG2PEngine`）を削除すること
- P1-5 完了後の `Backend/` ディレクトリには `PhonemeOptions.cs`（`PhonemeResult` / `PhonemeOptions`）と `README.md` のみが残る。P1-6 ではこのディレクトリに変更を加えない
- `#pragma warning disable CS0618` は P1-5 で `IPhonemizerBackend` 関連のもののみ除去。Obsolete コンストラクタに関連する pragma は P1-6 で除去すること

### P1-3（Dictionary Registry 化）との関係

- P1-3 と P1-5 は両方とも `MultilingualPhonemizer.cs` を変更する。M2 のマージ順序は `P1-3 -> P1-5` であるため、P1-5 はP1-3 マージ後にリベースして実施すること
- P1-3 で `HandlerEntry` struct（`ILanguageG2PHandler` + `IsOwned`）が導入された後、P1-5 で削除する `_enPhonemizer` / `_koPhonemizer` の所有権管理（`_ownsEn` / `_ownsKo` フラグ）は `HandlerEntry.IsOwned` に移行済みのはず。P1-5 では旧フラグの残存がないことを確認

### CLAUDE.md 更新内容

P1-5 完了後に以下の記述を更新すること:
- 主要コンポーネント表の `IPhonemizerBackend` 行:「音素化バックエンド抽象（テストスタブのみ使用）」-> 削除
- アーキテクチャセクションの `IPhonemizerBackend` 関連の説明を削除
- `Backend/` ディレクトリの説明を「`PhonemeResult` / `PhonemeOptions` 共有型定義」に更新
