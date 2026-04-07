# P1-5: G2P 全同期化（ProcessFallbackAsync / IPhonemizerBackend 廃止）

**作成日**: 2026-04-08
**ステータス**: 設計完了・実装待ち
**依存**: P1-4（ILanguageG2PHandler 全面移行）
**v2.0-plan 参照**: Phase 1 基盤リファクタリング > P1-5

---

## 1. 目的

`IPhonemizerBackend` インターフェースと `ProcessFallbackAsync` メソッドを削除し、MultilingualPhonemizer 内の非同期フォールバックパスを排除する。未対応言語はハンドラ未登録として警告ログのみに簡素化する。

---

## 2. 現状分析

### 2.1 IPhonemizerBackend の定義

**ファイル**: `Assets/uPiper/Runtime/Core/Phonemizers/Backend/IPhonemizerBackend.cs`

```csharp
public interface IPhonemizerBackend : IDisposable
{
    Task<bool> InitializeAsync(PhonemizerBackendOptions options, CancellationToken ct);
    Task<PhonemeResult> PhonemizeAsync(string text, string language, PhonemeOptions options, CancellationToken ct);
}
```

全メソッドが `Task<T>` 返却の非同期インターフェース。v1.4.0 時点で実プロダクションコードからの呼び出しは全てDotNetG2Pエンジンの同期呼び出しに移行済みであり、`IPhonemizerBackend` はテストスタブ注入専用のレガシーパスとしてのみ残存している。

### 2.2 Backend ディレクトリの全ファイル

| ファイル | 役割 | P1-5 での扱い |
|---------|------|--------------|
| `IPhonemizerBackend.cs` | 非同期バックエンドインターフェース | **削除** |
| `PhonemeOptions.cs` | `PhonemeOptions`, `PhonemeResult` クラス定義 | **保持**（PhonemeResult は他コンポーネントで広く使用） |
| `README.md` | ディレクトリ説明 | **更新**（IPhonemizerBackend 記述を削除） |
| `*.meta` (3ファイル) | Unity メタファイル | IPhonemizerBackend.cs.meta のみ **削除** |

### 2.3 MultilingualPhonemizer での使用箇所

#### 2.3.1 フィールド宣言（2箇所）

```csharp
// L60
private IPhonemizerBackend _enPhonemizer;        // English legacy (for test stub injection)
// L65
private IPhonemizerBackend _koPhonemizer;        // Korean (legacy backend, kept for backward compatibility)
```

#### 2.3.2 コンストラクタ（Options経由、L100-103）

```csharp
#pragma warning disable CS0618
_enPhonemizer = options.EnPhonemizer;
_koPhonemizer = options.KoPhonemizer;
#pragma warning restore CS0618
```

#### 2.3.3 Obsolete コンストラクタ（L114-142）

14引数コンストラクタが `IPhonemizerBackend enPhonemizer` と `IPhonemizerBackend koPhonemizer` を受け取る。このコンストラクタ自体が `[Obsolete]` 済み（P1-6 で削除対象）。

#### 2.3.4 InitializeAsync 内の参照（L168, L271-273）

```csharp
// L168: English エンジン生成判定
if (ContainsLanguage("en") && _enEngine == null && _enPhonemizer == null)

// L271-273: 全バックエンド未初期化チェック
if (_jaPhonemizer == null && _enEngine == null && _enPhonemizer == null &&
    _esEngine == null && _frEngine == null && _ptEngine == null &&
    _zhEngine == null && _koG2PEngine == null && _koPhonemizer == null)
```

#### 2.3.5 PhonemizeWithProsodyAsync 内の fallback（L372-377）

```csharp
default:
    var fallbackResult = await ProcessFallbackAsync(lang, segText, cancellationToken);
    if (fallbackResult.phonemes == null)
        continue;
    (segPhonemes, segA1, segA2, segA3) = fallbackResult;
    break;
```

switch 文の `default` ケースが `ProcessFallbackAsync` を呼び出す唯一の箇所。

#### 2.3.6 ProcessFallbackAsync メソッド（L647-664）

```csharp
private async Task<(string[] phonemes, int[] a1, int[] a2, int[] a3)> ProcessFallbackAsync(
    string lang, string text, CancellationToken cancellationToken)
{
    var backend = GetBackendForLanguage(lang);
    if (backend != null)
    {
        var result = await backend.PhonemizeAsync(text, lang, null, cancellationToken);
        // ... PhonemeResult から配列を取り出して返却
    }
    PiperLogger.LogWarning($"[MultilingualPhonemizer] No backend for '{lang}', skipping segment.");
    return (null, null, null, null);
}
```

#### 2.3.7 GetBackendForLanguage メソッド（L668-678）

```csharp
private IPhonemizerBackend GetBackendForLanguage(string lang)
{
    return lang switch
    {
        "en" => _enPhonemizer,
        "ko" => _koPhonemizer,
        _ => _enPhonemizer // fallback to English legacy backend
    };
}
```

注意: 不明言語に対して `_enPhonemizer` にフォールバックする設計。実プロダクションでは `_enPhonemizer` は null のため、`ProcessFallbackAsync` 内の null チェックで警告ログ出力に到達する。

#### 2.3.8 Dispose 内の参照（L433-445）

```csharp
if (_ownsEn)
{
    _enEngine?.Dispose();
    _enPhonemizer?.Dispose();
}
// ...
if (_ownsKo)
{
    _koG2PEngine?.Dispose();
    _koPhonemizer?.Dispose();
}
```

### 2.4 MultilingualPhonemizerOptions の [Obsolete] プロパティ

**ファイル**: `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/MultilingualPhonemizerOptions.cs`

```csharp
// L32-33
[Obsolete("Use EnEngine instead. This property will be removed in v2.0.")]
public IPhonemizerBackend EnPhonemizer { get; set; }

// L48-49
[Obsolete("Use KoG2PEngine instead. This property will be removed in v2.0.")]
public IPhonemizerBackend KoPhonemizer { get; set; }
```

両プロパティとも v2.0 削除を明示する `[Obsolete]` メッセージ付き。

### 2.5 using 参照の全体像（Backend 名前空間）

`using uPiper.Core.Phonemizers.Backend` を参照している全ファイル:

| ファイル | 参照理由 | P1-5 での対応 |
|---------|---------|-------------|
| `MultilingualPhonemizer.cs` | `IPhonemizerBackend` フィールド/メソッド | using 削除（IPhonemizerBackend 参照全廃後） |
| `MultilingualPhonemizerOptions.cs` | `IPhonemizerBackend` プロパティ | using 削除 |
| `IPhonemizer.cs` | `PhonemeResult` 戻り値型 | using **保持**（PhonemeResult は Backend 名前空間に残存） |
| `PhonemeCache.cs` | `PhonemeResult` キャッシュ | using **保持** |
| `DotNetG2PPhonemizer.cs` | `PhonemeResult` 生成 | using **保持** |
| `PiperTTS.cs` | `PhonemeResult` 使用 | using **保持** |
| `MultilingualPhonemizerEosTests.cs` | `IPhonemizerBackend` テストスタブ | using 削除（テスト書き換え後） |
| `PhonemeResultTest.cs` | `PhonemeResult` テスト | using **保持** |

**重要**: `PhonemeResult` / `PhonemeOptions` は `IPhonemizerBackend` とは独立した型であり、P1-5 後も `Backend` 名前空間に残存する。削除対象は `IPhonemizerBackend` インターフェースと `PhonemizerBackendOptions` クラスのみ。

---

## 3. 削除対象の完全リスト

### 3.1 ファイル削除

| ファイル | 理由 |
|---------|------|
| `Assets/uPiper/Runtime/Core/Phonemizers/Backend/IPhonemizerBackend.cs` | インターフェース本体 + `PhonemizerBackendOptions` クラス |
| `Assets/uPiper/Runtime/Core/Phonemizers/Backend/IPhonemizerBackend.cs.meta` | Unity メタファイル |

### 3.2 メソッド・フィールド削除（MultilingualPhonemizer.cs）

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

### 3.3 プロパティ削除（MultilingualPhonemizerOptions.cs）

| 対象 | 行番号 |
|------|--------|
| `EnPhonemizer` プロパティ + `[Obsolete]` 属性 + XML doc | L31-33 |
| `KoPhonemizer` プロパティ + `[Obsolete]` 属性 + XML doc | L47-49 |
| `using uPiper.Core.Phonemizers.Backend` | L9 |

### 3.4 switch default ケースの置換（MultilingualPhonemizer.cs）

L372-377 の `default:` ケースを警告ログ + `continue` に置換:

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

---

## 4. 未対応言語の処理設計

### 4.1 発生条件

P1-4 完了後は `_handlers[lang]` のルックアップで未登録言語が検出される。P1-5 単独の場合は switch 文の `default` ケースが該当。

以下の条件が全て成立した場合に `default` に到達:
1. `UnicodeLanguageDetector` がセグメントに言語コードを割り当てた
2. その言語コードが `_languages` リストに含まれているが、対応するエンジンが null
3. または、検出器が未知の言語コードを返した（通常は発生しない）

### 4.2 警告ログのフォーマット

```
[MultilingualPhonemizer] Unsupported language '{lang}', skipping segment: "{segText}"
```

- `{lang}`: 検出された言語コード（例: "de", "it"）
- `{segText}`: スキップされるテキストセグメント（短縮なし、デバッグ情報として有用）
- ログレベル: `Warning`（`PiperLogger.LogWarning`）
- 1セグメントにつき1回出力（ループ内のため同一テキストで複数回出力される可能性あり）

### 4.3 例外の扱い

- **例外は投げない**: 未対応言語のセグメントは無音（スキップ）として扱う
- **既存動作との一貫性**: 現状の `ProcessFallbackAsync` でも `backend == null` の場合は `(null, null, null, null)` を返却し、呼び出し元で `continue` している。新設計はこの挙動を維持
- **戻り値への影響**: スキップされたセグメントの音素は出力に含まれない。他セグメントの音素は正常に連結される

### 4.4 P1-4 完了後の設計（ハンドラベース）

P1-4 で `ILanguageG2PHandler` レジストリが導入された後の `default` ケース相当:

```csharp
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

---

## 5. テストへの影響

### 5.1 書き換えが必要なテスト

#### MultilingualPhonemizerEosTests.cs

**ファイル**: `Assets/uPiper/Tests/Editor/Phonemizers/MultilingualPhonemizerEosTests.cs`

**影響**: 大。テスト内に `StubPhonemizerBackend` クラス（`IPhonemizerBackend` 実装）が定義され、12箇所で使用。

**現状の構造**:
```csharp
private sealed class StubPhonemizerBackend : IPhonemizerBackend { ... }

// 使用例（多数）
var enStub = new StubPhonemizerBackend(enResult);
var mp = new MultilingualPhonemizer(
    new[] { "en", "ja" },
    enPhonemizer: enStub,     // IPhonemizerBackend 注入
    jaPhonemizer: jaPhonemizer);
```

**P1-4 完了後の書き換え方針**:
- `StubPhonemizerBackend` を `ILanguageG2PHandler` 実装の `StubEnglishHandler` に置換
- テストスタブは同期メソッド `Process(string text)` のみ実装
- Obsolete コンストラクタ経由の注入を Options + Handlers 辞書経由に変更

**使用箇所一覧**（StubPhonemizerBackend を使うテストメソッド）:

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

#### MultilingualPhonemizerDeepTests.cs

**ファイル**: `Assets/uPiper/Tests/Editor/Phonemizers/MultilingualPhonemizerDeepTests.cs`

**影響**: 小。`GetBackendForLanguage_UnknownLang_FallsBackToEnglish` テスト（L493-510）のみ。

**書き換え方針**: テスト内容を「未登録言語のセグメントがスキップされること」の検証に変更。または P1-4 のハンドラレジストリに対する「未登録キーの挙動」テストに置換。

### 5.2 影響なしのテスト

| テストファイル | 理由 |
|-------------|------|
| `PhonemeResultTest.cs` | `PhonemeResult` のみ使用（`IPhonemizerBackend` 非参照） |
| `MultilingualPhonemizerTests.cs` | 実エンジン使用（`IPhonemizerBackend` 非参照） |
| `DotNetG2PPhonemizerTests.cs` 等 | 個別言語テスト（`IPhonemizerBackend` 非参照） |

### 5.3 新規テスト追加

| テスト | 内容 |
|-------|------|
| `UnsupportedLanguage_LogsWarningAndSkips` | 未登録言語コードのセグメントがスキップされ、警告ログが出力されること |
| `UnsupportedLanguage_OtherSegmentsPreserved` | 未対応セグメントがスキップされても、他言語セグメントの音素が正常に含まれること |

---

## 6. P1-4 との依存関係

### 6.1 依存の概要

P1-5 は P1-4（ILanguageG2PHandler 全面移行）の **完了後** に実施する。

理由:
- P1-4 で switch 文が `_handlers[lang].Process(text)` に置換されると、`default` ケース（= `ProcessFallbackAsync` 呼び出し）が自然に「ハンドラ未登録」のパスに変わる
- テストスタブも `IPhonemizerBackend` から `ILanguageG2PHandler` に移行されるため、`IPhonemizerBackend` の参照が全て解消される
- P1-5 は「P1-4 で不要になった残骸の掃除」という性質が強い

### 6.2 P1-4 により自然に解消される部分

| 項目 | P1-4 での変化 | P1-5 での残作業 |
|------|-------------|---------------|
| `ProcessFallbackAsync` | switch 文消滅により呼び出し箇所がなくなる | メソッド定義の物理削除 |
| `GetBackendForLanguage` | 上記に伴い呼び出し箇所がなくなる | メソッド定義の物理削除 |
| `_enPhonemizer` / `_koPhonemizer` フィールド | ハンドラレジストリに移行、代入箇所消滅 | フィールド定義の物理削除 |
| テスト内 `StubPhonemizerBackend` | `ILanguageG2PHandler` スタブに移行済み | 旧スタブクラスの物理削除 |
| `MultilingualPhonemizerOptions.EnPhonemizer` / `KoPhonemizer` | Options 構造がハンドラ辞書に変更 | プロパティ定義の物理削除 |

### 6.3 P1-5 固有の作業（P1-4 では解消されない部分）

| 項目 | 説明 |
|------|------|
| `IPhonemizerBackend.cs` ファイル削除 | インターフェース定義自体の削除 |
| `PhonemizerBackendOptions` クラス削除 | `IPhonemizerBackend.cs` 内に定義されている |
| `Backend/README.md` 更新 | IPhonemizerBackend の記述を削除 |
| 未対応言語の警告ログ実装 | `ProcessFallbackAsync` 内の既存ログを新しいパスに移植 |
| CLAUDE.md 更新 | IPhonemizerBackend の記述を削除 |

### 6.4 P1-5 を P1-4 なしで実施する場合

技術的には可能だが非推奨。理由:
- テスト（MultilingualPhonemizerEosTests）が `IPhonemizerBackend` スタブに強く依存しており、P1-4 のハンドラ化なしにテストを書き換えると中間的なスタブ実装が必要になる
- switch 文が残った状態で `ProcessFallbackAsync` を削除すると、`default` ケースのフォールバック先がなくなり、未対応言語の処理パスを switch 内に直書きする必要がある（P1-4 でハンドラ化された後に再度書き換えが発生）

---

## 7. 実施手順（推奨順序）

P1-4 完了を前提とした手順:

1. **`IPhonemizerBackend.cs` + meta 削除**
2. **`MultilingualPhonemizerOptions` から `EnPhonemizer` / `KoPhonemizer` 削除**、using 削除
3. **`MultilingualPhonemizer` から以下を削除**:
   - `_enPhonemizer` / `_koPhonemizer` フィールド
   - `ProcessFallbackAsync` メソッド
   - `GetBackendForLanguage` メソッド
   - Options 経由の代入 + pragma
   - InitializeAsync / Dispose 内の参照
   - using 文
4. **ハンドラ未登録時の警告ログ実装**（`_handlers.TryGetValue` の else 節）
5. **テスト書き換え**:
   - `MultilingualPhonemizerEosTests`: `StubPhonemizerBackend` を `ILanguageG2PHandler` スタブに置換
   - `MultilingualPhonemizerDeepTests`: `GetBackendForLanguage_UnknownLang_FallsBackToEnglish` を未登録言語スキップテストに置換
6. **新規テスト追加**: 未対応言語の警告ログ + スキップ検証
7. **ドキュメント更新**: `Backend/README.md`, `CLAUDE.md`

---

## 8. PhonemeResult / PhonemeOptions の扱い

`PhonemeResult` と `PhonemeOptions` は `Backend/PhonemeOptions.cs` に定義されているが、`IPhonemizerBackend` とは独立した共有型である。

**使用箇所**:
- `IPhonemizer.PhonemizeAsync()` の戻り値型（`Task<PhonemeResult>`）
- `DotNetG2PPhonemizer.PhonemizeInternal()` の生成
- `PiperTTS.GetPhonemesAsync()` / `GenerateAudioAsync()` での参照
- `PhonemeCache` のキャッシュ対象
- `PhonemeResultTest` でのユニットテスト
- `PhonemeResultExtensions.Clone()` 拡張メソッド

**結論**: `PhonemeResult` / `PhonemeOptions` は削除しない。`Backend` ディレクトリは `PhonemeOptions.cs` のために存続する。将来的に名前空間を `uPiper.Core.Phonemizers` に移動する検討は P1-5 のスコープ外とする。

---

## 9. リスクと緩和策

| リスク | 影響 | 緩和策 |
|-------|------|--------|
| EOS テストのカバレッジ低下 | `StubPhonemizerBackend` 削除でテストが減る可能性 | ハンドラスタブで同等のテストを確実に移植 |
| 未対応言語でのサイレント無音 | ユーザーが未対応言語を入力しても音声が生成されず、原因が分かりにくい | 警告ログを確実に出力 + ドキュメントで対応言語を明示 |
| Obsolete 削除忘れ | `#pragma warning disable CS0618` が残存 | CI で Obsolete 警告をエラー化（将来対応） |

---

## 10. 完了基準

- [ ] `IPhonemizerBackend.cs` + meta が削除されている
- [ ] `PhonemizerBackendOptions` クラスが削除されている
- [ ] `MultilingualPhonemizer` 内の `ProcessFallbackAsync`, `GetBackendForLanguage` が削除されている
- [ ] `MultilingualPhonemizerOptions.EnPhonemizer` / `KoPhonemizer` が削除されている
- [ ] `MultilingualPhonemizer` の Obsolete コンストラクタ内の `IPhonemizerBackend` パラメータが削除されている（P1-6 と同時の場合はコンストラクタごと削除）
- [ ] 未対応言語に対して `PiperLogger.LogWarning` が出力される
- [ ] 未対応言語のセグメントがスキップされ、他セグメントが正常に連結される
- [ ] 全既存テスト（EditMode + PlayMode）がパスする
- [ ] `dotnet format --verify-no-changes` がパスする
- [ ] `Backend/README.md` が更新されている
