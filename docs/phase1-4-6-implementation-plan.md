# Phase 1.4-1.6: 音素化システム実装計画

## 概要
音素化システムは、テキストを音素（音の最小単位）に変換する重要なコンポーネントです。
このフェーズでは、拡張可能なインターフェースと基本実装を作成します。

## タスク詳細（合計2人日）

### Phase 1.4: IPhonemizer インターフェース設計（0.5人日）

#### 1.4.1 インターフェース定義（2時間）
```csharp
namespace uPiper.Core.Phonemizers
{
    public interface IPhonemizer : IDisposable
    {
        // 基本情報
        string Name { get; }
        string Version { get; }
        string[] SupportedLanguages { get; }
        
        // 音素化メソッド
        Task<PhonemeResult> PhonemizeAsync(string text, string language = "ja", CancellationToken cancellationToken = default);
        PhonemeResult Phonemize(string text, string language = "ja");
        
        // バッチ処理
        Task<PhonemeResult[]> PhonemizeBatchAsync(string[] texts, string language = "ja", CancellationToken cancellationToken = default);
        
        // 設定とキャッシュ
        bool UseCache { get; set; }
        void ClearCache();
        CacheStatistics GetCacheStatistics();
        
        // 言語サポート確認
        bool IsLanguageSupported(string language);
        LanguageInfo GetLanguageInfo(string language);
    }
}
```

#### 1.4.2 データ構造定義（2時間）
```csharp
// 音素化結果
public class PhonemeResult
{
    public string OriginalText { get; set; }
    public string[] Phonemes { get; set; }
    public int[] PhonemeIds { get; set; }
    public float[] Durations { get; set; }
    public float[] Pitches { get; set; }
    public string Language { get; set; }
    public TimeSpan ProcessingTime { get; set; }
    public bool FromCache { get; set; }
}

// 言語情報
public class LanguageInfo
{
    public string Code { get; set; }
    public string Name { get; set; }
    public string NativeName { get; set; }
    public bool RequiresPreprocessing { get; set; }
    public string[] AvailableVoices { get; set; }
}
```

### Phase 1.5: BasePhonemizer 実装（1人日）

#### 1.5.1 基本構造実装（2時間）
```csharp
public abstract class BasePhonemizer : IPhonemizer
{
    private readonly LRUCache<string, PhonemeResult> _cache;
    private readonly ITextNormalizer _textNormalizer;
    private readonly CacheStatistics _cacheStats;
    
    protected BasePhonemizer(int cacheSize = 1000)
    {
        _cache = new LRUCache<string, PhonemeResult>(cacheSize);
        _textNormalizer = new TextNormalizer();
        _cacheStats = new CacheStatistics();
    }
    
    // テンプレートメソッドパターン
    public virtual async Task<PhonemeResult> PhonemizeAsync(string text, string language = "ja", CancellationToken cancellationToken = default)
    {
        // 1. 入力検証
        // 2. キャッシュチェック
        // 3. テキスト正規化
        // 4. 実際の音素化（派生クラスで実装）
        // 5. 後処理
        // 6. キャッシュ保存
    }
}
```

#### 1.5.2 LRUキャッシュ実装（3時間）
```csharp
public class LRUCache<TKey, TValue>
{
    private readonly int _capacity;
    private readonly Dictionary<TKey, LinkedListNode<CacheItem>> _cacheMap;
    private readonly LinkedList<CacheItem> _lruList;
    private readonly ReaderWriterLockSlim _lock;
    
    // スレッドセーフな実装
    public bool TryGet(TKey key, out TValue value) { }
    public void Add(TKey key, TValue value) { }
    public void Clear() { }
    public int Count { get; }
}
```

#### 1.5.3 テキスト正規化実装（3時間）
```csharp
public interface ITextNormalizer
{
    string Normalize(string text, string language);
}

public class TextNormalizer : ITextNormalizer
{
    // 正規化処理
    // - 全角・半角統一
    // - 改行・空白処理
    // - 特殊文字処理
    // - 言語別の特殊処理
}
```

### Phase 1.6: MockPhonemizer 実装とテスト（0.5人日）

#### 1.6.1 MockPhonemizer実装（2時間）
```csharp
public class MockPhonemizer : BasePhonemizer
{
    private readonly Dictionary<string, PhonemeResult> _mockResults;
    private readonly TimeSpan _simulatedDelay;
    
    public MockPhonemizer(TimeSpan? simulatedDelay = null)
    {
        _simulatedDelay = simulatedDelay ?? TimeSpan.FromMilliseconds(10);
        _mockResults = new Dictionary<string, PhonemeResult>();
        SetupDefaultMockData();
    }
    
    // テスト用の設定可能な動作
    public void SetMockResult(string text, PhonemeResult result) { }
    public void SimulateError(string text, Exception exception) { }
}
```

#### 1.6.2 ユニットテスト作成（2時間）
- IPhonemizer インターフェーステスト
- BasePhonemizer 基本機能テスト
- LRUCache テスト
- TextNormalizer テスト
- MockPhonemizer テスト
- 統合テスト

## ディレクトリ構造

```
Assets/uPiper/Runtime/Core/Phonemizers/
├── IPhonemizer.cs
├── BasePhonemizer.cs
├── PhonemeResult.cs
├── LanguageInfo.cs
├── Cache/
│   ├── ICache.cs
│   ├── LRUCache.cs
│   └── CacheItem.cs
├── Text/
│   ├── ITextNormalizer.cs
│   └── TextNormalizer.cs
└── Implementations/
    └── MockPhonemizer.cs

Assets/uPiper/Tests/Runtime/Core/Phonemizers/
├── IPhonemizerTest.cs
├── BasePhonemizerTest.cs
├── LRUCacheTest.cs
├── TextNormalizerTest.cs
└── MockPhonemizerTest.cs
```

## 実装順序

1. **ディレクトリ構造作成**（15分）
2. **インターフェースと基本データ構造**（1時間）
   - IPhonemizer.cs
   - PhonemeResult.cs
   - LanguageInfo.cs
3. **キャッシュ実装**（2時間）
   - ICache.cs
   - CacheItem.cs
   - LRUCache.cs
   - LRUCacheTest.cs
4. **テキスト正規化**（1.5時間）
   - ITextNormalizer.cs
   - TextNormalizer.cs
   - TextNormalizerTest.cs
5. **BasePhonemizer実装**（3時間）
   - BasePhonemizer.cs
   - BasePhonemizerTest.cs
6. **MockPhonemizer実装**（1.5時間）
   - MockPhonemizer.cs
   - MockPhonemizerTest.cs
7. **統合テストとドキュメント**（1時間）

## 成功基準

1. **機能要件**
   - [ ] 非同期・同期両方の音素化が可能
   - [ ] キャッシュが正しく動作する
   - [ ] テキスト正規化が適切に行われる
   - [ ] 言語サポートの確認ができる

2. **非機能要件**
   - [ ] スレッドセーフな実装
   - [ ] 95%以上のテストカバレッジ
   - [ ] 100ms以下の処理時間（キャッシュヒット時）
   - [ ] メモリリークがない

3. **テスト要件**
   - [ ] 全ての公開APIのテスト
   - [ ] エラーケースのテスト
   - [ ] パフォーマンステスト
   - [ ] スレッドセーフティテスト

## 次のステップ

Phase 1.7以降で実際の音素化エンジン（OpenJTalk）を統合しますが、
このフェーズで作成する基盤により、容易に統合が可能になります。