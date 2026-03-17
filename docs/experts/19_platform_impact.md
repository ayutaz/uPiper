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

## WebGL対応で必要な追加実装

### 新言語辞書の非同期ロード

各新言語のPhonemizerは辞書/ルールデータを必要とする可能性がある。
WebGLでは全て非同期でロードする必要がある。

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

### 非WebGLプラットフォーム

```
StreamingAssets/uPiper/Dictionaries/
├── ja/  (日本語辞書 - 既存)
├── en/  (英語辞書 - Flite LTS)
├── es/  (スペイン語ルール - 新規)
├── fr/  (フランス語ルール - 新規)
├── pt/  (ポルトガル語ルール - 新規)
├── zh/  (中国語ピンイン辞書 - 新規)
└── ko/  (韓国語Jamo辞書 - 新規)
```

ロード方式: `Directory.GetFiles()` → 言語別に同期読み込み

### WebGLプラットフォーム

ロード方式: 既知ファイル名リスト → `WebGLStreamingAssetsLoader.LoadTextAsync()` → 非同期

```csharp
private static readonly Dictionary<string, string[]> LanguageDictFiles = new()
{
    ["es"] = new[] { "es_rules.json" },
    ["fr"] = new[] { "fr_rules.json" },
    ["pt"] = new[] { "pt_rules.json" },
    ["zh"] = new[] { "zh_pinyin_dict.json" },
    ["ko"] = new[] { "ko_jamo_rules.json" },
};
```

## パフォーマンス影響予測

### 初期化時間

| フェーズ | WebGL | Windows | iOS/Android |
|---------|-------|---------|-------------|
| 辞書ダウンロード | 2-5秒 | 0秒 | 0秒 |
| MeCab初期化(ja) | 1秒 | 1秒 | 1-2秒 |
| 新言語Phonemizer初期化 | 0.5秒/言語 | 0.1秒/言語 | 0.2秒/言語 |
| 合計(6言語) | 5-10秒 | 1.5秒 | 2-3秒 |

### 推論パフォーマンス

多言語モデルは単一言語モデルと同等の推論速度（`lid`テンソル追加のオーバーヘッドは無視可能）。

### メモリ使用量

| 構成 | Phonemizer | Model | Dictionary | Cache | Total |
|------|-----------|-------|-----------|-------|-------|
| 1言語(ja) | 5MB | 61MB | 2MB | 10MB | 78MB |
| 6言語(全部) | 15MB | 100MB | 8MB | 10MB | 133MB |

**WebGL(25MB制限)**: モデルはGPUメモリに常駐するため、Phonemizer+辞書のみが制約対象
