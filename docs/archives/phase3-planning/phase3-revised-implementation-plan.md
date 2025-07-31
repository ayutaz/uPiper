# Phase 3: eSpeak-NG統合 改訂版実装計画

## 改訂概要

レビュー結果を踏まえ、以下の重要な変更を加えました：
1. **ライセンス問題を最優先課題として対応**
2. **段階的データ管理システムの実装**
3. **堅牢なエラーハンドリングとフォールバック機構**
4. **スレッドセーフな実装設計**

## 1. 実装戦略の変更

### 1.1 二段階アプローチ

#### Phase 3A: ライセンスセーフな基盤実装（5人日）
- MITライセンスの代替案評価と実装
- 基本的な英語音素化機能
- プラグイン可能なアーキテクチャ

#### Phase 3B: 拡張実装（5人日）
- 多言語サポート
- 高度な音素化機能
- パフォーマンス最適化

### 1.2 代替音素化エンジンの選択肢

```csharp
public enum PhonemizerBackend
{
    RuleBased,      // 独自実装（ライセンスフリー）
    FliteLite,      // Festival Lite（MITライセンス）
    ESpeakNG,       // オプショナル（GPL v3）
    WebService      // クラウドベース（API経由）
}
```

## 2. Day 1: ライセンス評価と基本設計（最重要）

### タスク1.1: ライセンス問題の解決
```markdown
## 評価項目
1. eSpeak-NG GPL v3の影響範囲確認
2. 動的リンクの法的解釈
3. プロセス分離の実現可能性
4. 代替ライブラリの調査

## 決定マトリクス
| オプション | ライセンス | 品質 | 実装工数 | 推奨度 |
|-----------|-----------|------|---------|--------|
| 独自ルール | MIT | 中 | 高 | ◎ |
| Flite | MIT | 良 | 中 | ○ |
| eSpeak分離 | GPL回避 | 優 | 高 | △ |
| Web API | N/A | 優 | 低 | △ |
```

### タスク1.2: プラグイン可能なアーキテクチャ設計
```csharp
// 音素化バックエンドインターフェース
public interface IPhonemizerBackend
{
    string BackendName { get; }
    string[] SupportedLanguages { get; }
    bool IsAvailable();
    Task<PhonemeResult> PhonemizeAsync(string text, string language);
}

// ファクトリーパターンで実装を切り替え
public class PhonemizerBackendFactory
{
    private readonly List<IPhonemizerBackend> backends = new();
    
    public void RegisterBackend(IPhonemizerBackend backend)
    {
        if (backend.IsAvailable())
        {
            backends.Add(backend);
        }
    }
    
    public IPhonemizerBackend GetBackend(string language)
    {
        return backends.FirstOrDefault(b => 
            b.SupportedLanguages.Contains(language)) 
            ?? new FallbackPhonemizer();
    }
}
```

## 3. Day 2-3: 基本実装とエラーハンドリング

### タスク2.1: ルールベース音素化エンジン（フォールバック）
```csharp
public class RuleBasedEnglishPhonemizer : IPhonemizerBackend
{
    private readonly Dictionary<string, string> pronunciationDict;
    private readonly CMUDict cmuDict; // CMU発音辞書
    
    public async Task<PhonemeResult> PhonemizeAsync(string text, string language)
    {
        try
        {
            var words = TokenizeText(text);
            var phonemes = new List<string>();
            
            foreach (var word in words)
            {
                if (pronunciationDict.TryGetValue(word.ToLower(), out var pronunciation))
                {
                    phonemes.AddRange(pronunciation.Split(' '));
                }
                else
                {
                    // G2P (Grapheme-to-Phoneme) ルール適用
                    phonemes.AddRange(ApplyG2PRules(word));
                }
            }
            
            return new PhonemeResult
            {
                Phonemes = phonemes,
                Language = language,
                Success = true
            };
        }
        catch (Exception ex)
        {
            return new PhonemeResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
}
```

### タスク2.2: 堅牢なエラーハンドリング実装
```csharp
public class SafePhonemizerWrapper : BasePhonemizer
{
    private readonly IPhonemizerBackend primaryBackend;
    private readonly IPhonemizerBackend fallbackBackend;
    private readonly CircuitBreaker circuitBreaker;
    
    protected override async Task<PhonemeResult> PhonemizeInternalAsync(
        string text, string language)
    {
        // Circuit Breakerパターンでクラッシュを防ぐ
        if (circuitBreaker.IsOpen)
        {
            return await fallbackBackend.PhonemizeAsync(text, language);
        }
        
        try
        {
            var result = await primaryBackend.PhonemizeAsync(text, language);
            circuitBreaker.OnSuccess();
            return result;
        }
        catch (Exception ex)
        {
            circuitBreaker.OnFailure();
            LogError($"Primary backend failed: {ex.Message}");
            
            // フォールバックを使用
            return await fallbackBackend.PhonemizeAsync(text, language);
        }
    }
}
```

## 4. Day 4-5: データ管理とスレッドセーフティ

### タスク3.1: 段階的データ管理システム
```csharp
public class PhonemizerDataManager
{
    private readonly string dataBasePath;
    private readonly HttpClient httpClient;
    
    public enum DataPackage
    {
        EnglishMinimal,     // 500KB - 基本ルール
        EnglishStandard,    // 2MB - CMU辞書
        EnglishExtended,    // 5MB - 拡張辞書
        Multilingual        // 10MB - 多言語
    }
    
    public async Task<bool> EnsureDataAvailable(DataPackage package)
    {
        var packageInfo = GetPackageInfo(package);
        
        if (IsDataCached(packageInfo))
        {
            return true;
        }
        
        // プログレス付きダウンロード
        using var progress = new Progress<float>(p => 
            OnDownloadProgress?.Invoke(p));
            
        return await DownloadDataPackage(packageInfo, progress);
    }
    
    private async Task<bool> DownloadDataPackage(
        PackageInfo info, IProgress<float> progress)
    {
        try
        {
            var response = await httpClient.GetAsync(
                info.Url, HttpCompletionOption.ResponseHeadersRead);
                
            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            var buffer = new byte[8192];
            var bytesRead = 0L;
            
            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = File.Create(info.LocalPath);
            
            int read;
            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, read);
                bytesRead += read;
                progress?.Report((float)bytesRead / totalBytes);
            }
            
            return true;
        }
        catch (Exception ex)
        {
            LogError($"Failed to download {info.Name}: {ex.Message}");
            return false;
        }
    }
}
```

### タスク3.2: スレッドセーフな実装
```csharp
public class ThreadSafePhonemizerPool
{
    private readonly ObjectPool<IPhonemizerBackend> pool;
    private readonly SemaphoreSlim semaphore;
    private readonly int maxConcurrency;
    
    public ThreadSafePhonemizerPool(int maxConcurrency = 4)
    {
        this.maxConcurrency = maxConcurrency;
        this.semaphore = new SemaphoreSlim(maxConcurrency);
        
        // オブジェクトプールの初期化
        this.pool = new DefaultObjectPool<IPhonemizerBackend>(
            new PhonemizerPoolPolicy(), maxConcurrency);
    }
    
    public async Task<PhonemeResult> PhonemizeAsync(string text, string language)
    {
        await semaphore.WaitAsync();
        var backend = pool.Get();
        
        try
        {
            return await backend.PhonemizeAsync(text, language);
        }
        finally
        {
            pool.Return(backend);
            semaphore.Release();
        }
    }
}
```

## 5. Day 6-7: Unity統合と最適化

### タスク4.1: Unity向け非同期処理
```csharp
public class UnityPhonemizerService : MonoBehaviour
{
    private readonly Queue<PhonemizeRequest> requestQueue = new();
    private readonly ThreadSafePhonemizerPool pool;
    
    private void Start()
    {
        // バックグラウンドワーカーの開始
        StartCoroutine(ProcessRequestsCoroutine());
    }
    
    private IEnumerator ProcessRequestsCoroutine()
    {
        while (true)
        {
            if (requestQueue.Count > 0)
            {
                var request = requestQueue.Dequeue();
                
                // バックグラウンドで処理
                var task = Task.Run(async () => 
                    await pool.PhonemizeAsync(request.Text, request.Language));
                
                yield return new WaitUntil(() => task.IsCompleted);
                
                if (task.IsCompletedSuccessfully)
                {
                    // メインスレッドでコールバック
                    request.OnComplete?.Invoke(task.Result);
                }
                else
                {
                    request.OnError?.Invoke(task.Exception);
                }
            }
            
            yield return null;
        }
    }
}
```

### タスク4.2: モバイル最適化
```csharp
public class MobileOptimizedPhonemizer : BasePhonemizer
{
    private readonly LRUCache<string, PhonemeResult> cache;
    private readonly int maxBatchSize = 10;
    
    public override async Task<PhonemeResult[]> PhonemizeBatchAsync(
        string[] texts, string language)
    {
        // バッチ処理で効率化
        var results = new List<PhonemeResult>();
        var uncachedTexts = new List<string>();
        var uncachedIndices = new List<int>();
        
        // キャッシュチェック
        for (int i = 0; i < texts.Length; i++)
        {
            var cacheKey = GetCacheKey(texts[i], language);
            if (cache.TryGet(cacheKey, out var cached))
            {
                results.Add(cached);
            }
            else
            {
                uncachedTexts.Add(texts[i]);
                uncachedIndices.Add(i);
            }
        }
        
        // バッチ処理
        if (uncachedTexts.Count > 0)
        {
            var batches = uncachedTexts.Chunk(maxBatchSize);
            foreach (var batch in batches)
            {
                var batchResults = await ProcessBatch(batch, language);
                results.AddRange(batchResults);
                
                // キャッシュに保存
                for (int i = 0; i < batch.Length; i++)
                {
                    cache.Add(GetCacheKey(batch[i], language), batchResults[i]);
                }
            }
        }
        
        return results.ToArray();
    }
}
```

## 6. Day 8-9: 多言語展開とFlite統合

### タスク5.1: Flite統合（オプション）
```csharp
public class FlitePhonemizer : IPhonemizerBackend
{
    private IntPtr fliteVoice;
    
    public async Task<PhonemeResult> PhonemizeAsync(string text, string language)
    {
        return await Task.Run(() =>
        {
            if (!FliteNative.IsInitialized())
            {
                throw new InvalidOperationException("Flite not initialized");
            }
            
            var phonemes = FliteNative.TextToPhonemes(fliteVoice, text);
            return ParseFlitePhonemes(phonemes, language);
        });
    }
    
    [DllImport("flite")]
    private static extern IntPtr flite_text_to_phones(IntPtr voice, string text);
}
```

### タスク5.2: Web API フォールバック
```csharp
public class WebApiPhonemizer : IPhonemizerBackend
{
    private readonly HttpClient httpClient;
    private readonly string apiEndpoint;
    
    public async Task<PhonemeResult> PhonemizeAsync(string text, string language)
    {
        var request = new PhonemizeRequest
        {
            Text = text,
            Language = language,
            Format = "ipa"
        };
        
        var response = await httpClient.PostAsJsonAsync(
            $"{apiEndpoint}/phonemize", request);
            
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<PhonemeResponse>();
            return ConvertToPhonemeResult(result);
        }
        
        // ローカルフォールバック
        return await fallbackPhonemizer.PhonemizeAsync(text, language);
    }
}
```

## 7. Day 10: 品質保証と統合テスト

### タスク6.1: 包括的テストスイート
```csharp
[TestFixture]
public class PhonemizerIntegrationTests
{
    [Test]
    [TestCase("Hello world", "en", "hɛloʊ wɜrld")]
    [TestCase("Good morning", "en", "gʊd mɔrnɪŋ")]
    public async Task TestEnglishPhonemization(
        string input, string language, string expected)
    {
        // 各バックエンドでテスト
        foreach (var backend in GetAllBackends())
        {
            var result = await backend.PhonemizeAsync(input, language);
            Assert.IsTrue(result.Success);
            Assert.That(result.PhonemeString, Is.EqualTo(expected));
        }
    }
    
    [Test]
    public async Task TestCrashRecovery()
    {
        var wrapper = new SafePhonemizerWrapper();
        
        // 意図的にクラッシュを誘発
        SimulateCrash();
        
        // フォールバックが動作することを確認
        var result = await wrapper.PhonemizeAsync("test", "en");
        Assert.IsTrue(result.Success);
        Assert.That(result.Backend, Is.EqualTo("Fallback"));
    }
}
```

### タスク6.2: パフォーマンスベンチマーク
```csharp
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net60)]
public class PhonemizerBenchmark
{
    private readonly string[] testTexts = LoadTestTexts();
    
    [Benchmark]
    public async Task BenchmarkRuleBased()
    {
        var phonemizer = new RuleBasedEnglishPhonemizer();
        foreach (var text in testTexts)
        {
            await phonemizer.PhonemizeAsync(text, "en");
        }
    }
    
    [Benchmark]
    public async Task BenchmarkWithCache()
    {
        var phonemizer = new MobileOptimizedPhonemizer();
        foreach (var text in testTexts)
        {
            await phonemizer.PhonemizeAsync(text, "en");
        }
    }
}
```

## 8. 成功基準（改訂版）

### 8.1 必須要件
- ✅ ライセンス問題の完全解決
- ✅ 英語音素化精度: 90%以上（ルールベース）
- ✅ クラッシュ率: 0.1%未満
- ✅ 初期化時間: 500ms以内

### 8.2 性能目標
- 処理速度: 100文字/50ms（キャッシュヒット時）
- メモリ使用量: 基本5MB以内
- APK増分: 2MB以内（英語のみ）

### 8.3 拡張目標
- 5言語以上のサポート
- オフライン動作
- WebGL対応

## 9. リスク管理（強化版）

| リスク | 対策 | 代替案 |
|--------|------|--------|
| ライセンス違反 | プラグイン化 | Web API |
| 性能不足 | キャッシュ強化 | 事前処理 |
| データサイズ | 段階的DL | クラウド化 |
| クラッシュ | Circuit Breaker | フォールバック |

## 10. まとめ

この改訂版計画では：
1. **ライセンス安全性を最優先**
2. **段階的で柔軟な実装**
3. **堅牢性とフォールバック**
4. **モバイル最適化**

を重視し、実用的で保守可能な音素化システムを構築します。