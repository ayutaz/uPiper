# プラットフォーム影響分析

## プラットフォーム別制約

### WebGL

| 制約 | 詳細 | 対策 |
|------|------|------|
| ファイルI/O | 同期読み込み不可 | UnityWebRequest非同期 |
| P/Invoke | 不可 | C#純粋実装のみ |
| Task.Run() | 不可 | async/await直接使用 |
| メモリ | 推奨25MB以下 | キャッシュ制限 |
| 大容量ファイル | GitHub Pages 100MB制限 | 90MBチャンク分割 |
| ディレクトリ列挙 | API不可 | 既知ファイル名リスト |

### iOS/Android

| 制約 | 詳細 | 対策 |
|------|------|------|
| IL2CPP必須 | リフレクション制限 | `[Preserve]`属性 |
| メモリ | 推奨50MB以下 | キャッシュ制限 |
| スレッド | IL2CPPでマーシャリングコスト大 | スレッド数制限 |

### Windows/macOS/Linux

| 制約 | 詳細 |
|------|------|
| ファイルI/O | 同期・非同期どちらも可 |
| スレッド | フルサポート |
| メモリ | 最大500MB |

## Phase 5 新言語バックエンドのプラットフォーム互換性

Phase 5で追加された5言語バックエンド（Spanish, French, Portuguese, Chinese, Korean）は全て純粋C#実装であり、ネイティブプラグインを一切必要としない。全プラットフォームでそのまま動作する。

### バックエンド別プラットフォーム特性

| バックエンド | 外部データファイル | ネイティブプラグイン | WebGL対応 | IL2CPP対応 |
|------------|-----------------|-------------------|----------|-----------|
| SpanishPhonemizerBackend | 不要（ルールベース） | 不要 | 対応済み | 対応済み |
| FrenchPhonemizerBackend | 不要（ルールベース） | 不要 | 対応済み | 対応済み |
| PortuguesePhonemizerBackend | 不要（ルールベース） | 不要 | 対応済み | 対応済み |
| ChinesePhonemizerBackend | 不要（インメモリ lookup ~500文字） | 不要 | 対応済み | 対応済み |
| KoreanPhonemizerBackend | 不要（Unicode演算によるHangul分解） | 不要 | 対応済み | 対応済み |

### WebGL対応パターン

全バックエンドが統一された `#if UNITY_WEBGL && !UNITY_EDITOR` パターンを使用:

```csharp
#pragma warning disable CS1998
public override async Task<PhonemeResult> PhonemizeAsync(
    string text, string language, PhonemeOptions options = null,
    CancellationToken cancellationToken = default)
{
    EnsureInitialized();

#if UNITY_WEBGL && !UNITY_EDITOR
    // WebGL: Task.Run不可 → 同期実行（lock付き）
    lock (_syncLock)
    {
        return PhonemizeInternal(text, language);
    }
#else
    // 非WebGL: Task.Runでバックグラウンドスレッド実行
    return await Task.Run(() =>
    {
        lock (_syncLock)
        {
            return PhonemizeInternal(text, language);
        }
    }, cancellationToken);
#endif
}
#pragma warning restore CS1998
```

**設計上のポイント**:
- WebGLでは `Task.Run` を回避し、メインスレッドで同期実行
- 非WebGLでは `Task.Run` + `lock(_syncLock)` でスレッドセーフなバックグラウンド実行
- `#pragma warning disable CS1998` で WebGL 分岐時の async 警告を抑制
- es/fr/pt は外部辞書ファイル不要のため、WebGL非同期ロード問題が発生しない
- zh はインメモリ lookup テーブル (~500文字) のため同様に外部ファイル不要
- ko は Unicode 演算 (U+AC00基点の除算・剰余) で Hangul 分解するため外部データ不要

### スレッドセーフティ

| コンポーネント | 方式 | 詳細 |
|--------------|------|------|
| 各PhonemizerBackend | `lock(_syncLock)` | インスタンスレベルの排他制御 |
| PuaTokenMapper | `ConcurrentDictionary` | ロックフリーなスレッドセーフ lookup |
| 静的データ (ルール/マッピング) | `readonly` / `static readonly` | 初期化後不変 |

## WebGL対応で必要な追加実装

### 新言語辞書の非同期ロード

Phase 5のバックエンドはルールベースまたはインメモリデータを使用するため、StreamingAssetsからの辞書ロードは不要。
将来、外部辞書ファイルを必要とするバックエンドを追加する場合は、以下のパターンに従う:

```csharp
#if UNITY_WEBGL && !UNITY_EDITOR
public async Task InitializeAsync(CancellationToken cancellationToken = default)
{
    // 言語別辞書を非同期ダウンロード
    var dictData = await WebGLStreamingAssetsLoader.LoadTextAsync(
        $"uPiper/Dictionaries/{_language}_dict.json", cancellationToken);
    LoadDictionaryFromJson(dictData);
}
#else
public void Initialize()
{
    var path = Path.Combine(Application.streamingAssetsPath, ...);
    LoadDictionaryFromPath(path);
}
#endif
```

### モデルサイズの影響

| 構成 | サイズ | WebGLチャンク数 |
|------|--------|----------------|
| 単一言語モデル×1 | 61MB | 1 |
| 6言語モデル×1 | ~100-120MB | 2 |
| 個別モデル×6 | ~366MB | 5 |

**推奨**: 多言語モデル1つ（最小サイズ）+ 遅延ロード

### IndexedDBキャッシュの活用

```csharp
// 初回: ダウンロード → IndexedDBに保存
// 2回目以降: IndexedDBから即座にロード
await IndexedDBCache.StoreAsync($"model_{language}", modelData, version);
var cached = await IndexedDBCache.LoadAsync($"model_{language}");
```

## IL2CPP環境での注意点

### リフレクション保護

新言語Phonemizer追加時に `[Preserve]` が必要:

```csharp
[Preserve]
public class SpanishPhonemizer : IPhonemizerBackend { ... }

[Preserve]
public class FrenchPhonemizer : IPhonemizerBackend { ... }
```

### 型保護

```csharp
[Preserve]
static void PreserveMultilingualTypes()
{
    _ = new Dictionary<string, SpanishPhonemizer>();
    _ = new Dictionary<string, FrenchPhonemizer>();
    _ = new List<MultilingualPhonemizeResult>();
}
```

### スレッド数制限

| 環境 | 推奨スレッド数 |
|------|--------------|
| Mono | `Max(2, processorCount - 1)` |
| IL2CPP | `Min(2, processorCount)` |
| WebGL | 1（シングルスレッド） |

## 辞書データのロード戦略

### Phase 5バックエンドのデータ戦略

Phase 5の新言語バックエンドはStreamingAssetsに追加の辞書ファイルを必要としない:

| バックエンド | データ戦略 | StreamingAssets追加 |
|------------|----------|-------------------|
| Spanish (es) | 正書法ルールベース（静的データ埋め込み） | なし |
| French (fr) | 正書法ルールベース（静的データ埋め込み） | なし |
| Portuguese (pt) | 正書法ルールベース（静的データ埋め込み） | なし |
| Chinese (zh) | インメモリ漢字→ピンイン lookup (~500文字) | なし |
| Korean (ko) | Unicode演算によるHangul Jamo分解 | なし |

### 既存言語の辞書ファイル（変更なし）

```
StreamingAssets/uPiper/Dictionaries/
├── ja/  (日本語辞書 - 既存)
├── en/  (英語辞書 - Flite LTS)
├── additional_tech_dict.json  (カスタム辞書)
├── default_common_dict.json   (カスタム辞書)
├── default_tech_dict.json     (カスタム辞書)
└── user_custom_dict.json      (カスタム辞書)
```

ロード方式: `Directory.GetFiles()` → 言語別に同期読み込み（日本語・英語のみ）

### WebGLプラットフォーム

Phase 5バックエンドは外部ファイル不要のため、WebGL非同期ロードの追加対応は不要。
既存の日本語・英語辞書のみ非同期ロード対象:

ロード方式: 既知ファイル名リスト → `WebGLStreamingAssetsLoader.LoadTextAsync()` → 非同期

## パフォーマンス影響予測

### 初期化時間

| コンポーネント | WebGL | Windows | iOS/Android |
|--------------|-------|---------|-------------|
| MeCab初期化(ja) | 1秒 | 1秒 | 1-2秒 |
| Flite LTS初期化(en) | 0.5秒 | 0.1秒 | 0.2秒 |
| 新言語バックエンド(es/fr/pt) | <0.01秒 | <0.01秒 | <0.01秒 |
| 中国語バックエンド(zh) | <0.01秒 | <0.01秒 | <0.01秒 |
| 韓国語バックエンド(ko) | <0.01秒 | <0.01秒 | <0.01秒 |
| 辞書ダウンロード(WebGLのみ) | 2-5秒 | - | - |
| 合計(7言語) | 4-7秒 | 1.2秒 | 1.5-2.5秒 |

Phase 5バックエンドは外部データ不要のため初期化は事実上即座に完了する。

### 推論パフォーマンス

多言語モデルは単一言語モデルと同等の推論速度（`lid`テンソル追加のオーバーヘッドは無視可能）。

### メモリ使用量

| 構成 | Phonemizer | Model | Dictionary | Cache | Total |
|------|-----------|-------|-----------|-------|-------|
| 1言語(ja) | 5MB | 61MB | 2MB | 10MB | 78MB |
| 2言語(ja+en) | 7MB | 61MB | 2MB | 10MB | 80MB |
| 7言語(全部) | 10MB | 100MB | 2MB | 10MB | 122MB |

Phase 5の新言語バックエンドはそれぞれ軽量:
- es/fr/pt: 静的ルール + 小規模ランタイム状態 (<0.5MB/言語)
- zh: インメモリ lookup テーブル (~500文字分, <0.5MB)
- ko: ランタイム状態のみ、静的データ最小 (<0.1MB)

**WebGL(25MB制限)**: モデルはGPUメモリに常駐するため、Phonemizer+辞書のみが制約対象。Phase 5バックエンドは外部辞書不要のため追加メモリ負荷は極めて小さい。
