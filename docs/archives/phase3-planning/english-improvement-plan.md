# 英語音素化改善計画

## 現状分析

### 実装済み
- CMU辞書（134,000語）
- 基本的なG2Pルール
- RuleBasedPhonemizer

### 課題
1. 辞書にない単語の音素化精度が低い
2. 新語・固有名詞への対応不足
3. Letter-to-Sound (LTS) ルールが簡易的

## Flite統合実装計画

### Phase 1: Fliteビルド環境構築（2-3日）

#### 1.1 ソースコード取得
```bash
# Flite 2.2 (最新安定版)
git clone https://github.com/festvox/flite.git
cd flite
git checkout v2.2
```

#### 1.2 ビルドスクリプト作成
```cmake
# NativePlugins/Flite/CMakeLists.txt
cmake_minimum_required(VERSION 3.10)
project(flite_unity)

# Flite core
add_library(flite_unity SHARED
    src/flite_unity.c
    # Fliteのコアファイル
)

# Unity向けの軽量化設定
add_definitions(-DFLITE_LEAN_AND_MEAN)
```

### Phase 2: Unity統合（3-4日）

#### 2.1 P/Invokeインターフェース
```csharp
namespace uPiper.Core.Phonemizers.Native
{
    public static class FliteNative
    {
        [DllImport("flite_unity")]
        public static extern IntPtr flite_init();
        
        [DllImport("flite_unity")]
        public static extern IntPtr flite_text_to_phones(
            IntPtr flite, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string text
        );
        
        [DllImport("flite_unity")]
        public static extern void flite_cleanup(IntPtr flite);
    }
}
```

#### 2.2 FlitePhonemizerBackend実装
```csharp
public class FlitePhonemizerBackend : PhonemizerBackendBase
{
    private IntPtr fliteHandle;
    private CMUDictionary cmuDictionary; // 併用
    
    public override string Name => "Flite";
    public override string Version => "2.2";
    public override string License => "MIT-CMU";
    
    protected override async Task<bool> InitializeInternalAsync(
        PhonemizerBackendOptions options,
        CancellationToken cancellationToken)
    {
        // Fliteネイティブライブラリ初期化
        fliteHandle = FliteNative.flite_init();
        
        // CMU辞書も併用（高速化のため）
        cmuDictionary = new CMUDictionary();
        await cmuDictionary.LoadAsync(GetDictionaryPath());
        
        return fliteHandle != IntPtr.Zero;
    }
    
    public override async Task<PhonemeResult> PhonemizeAsync(
        string text, 
        string language,
        PhonemeOptions options = null,
        CancellationToken cancellationToken = default)
    {
        // 1. まずCMU辞書で検索（高速）
        var words = TokenizeText(text);
        var phonemes = new List<string>();
        
        foreach (var word in words)
        {
            if (cmuDictionary.TryGetPronunciation(word, out var dictPhonemes))
            {
                phonemes.AddRange(dictPhonemes);
            }
            else
            {
                // 2. 辞書にない場合はFliteのLTSを使用
                var flitePhonemes = GetFlitePhonemes(word);
                phonemes.AddRange(flitePhonemes);
            }
        }
        
        return new PhonemeResult
        {
            Phonemes = phonemes.ToArray(),
            Language = language,
            Success = true
        };
    }
}
```

### Phase 3: 品質向上とテスト（2日）

#### 3.1 テストケース作成
```csharp
[Test]
public void TestNewWords()
{
    var testWords = new[]
    {
        "ChatGPT",      // 新語
        "COVID-19",     // ハイフン付き
        "iPhone",       // ブランド名
        "cryptocurrency", // 長い新語
        "TikTok",       // 固有名詞
    };
    
    foreach (var word in testWords)
    {
        var result = phonemizer.Phonemize(word);
        Assert.IsNotEmpty(result.Phonemes);
        Debug.Log($"{word}: {string.Join(" ", result.Phonemes)}");
    }
}
```

#### 3.2 パフォーマンス最適化
- 辞書キャッシュ
- Flite LTSの結果キャッシュ
- 並列処理対応

### Phase 4: 日英混在対応（3日）

#### 4.1 言語検出器
```csharp
public class LanguageDetector
{
    public List<LanguageSegment> DetectSegments(string text)
    {
        var segments = new List<LanguageSegment>();
        var regex = new Regex(@"[\u3040-\u309F\u30A0-\u30FF\u4E00-\u9FAF]+|[a-zA-Z0-9]+|\W+");
        
        foreach (Match match in regex.Matches(text))
        {
            var value = match.Value;
            var lang = IsJapanese(value) ? "ja" : 
                      IsEnglish(value) ? "en" : 
                      "unknown";
            
            segments.Add(new LanguageSegment 
            { 
                Text = value, 
                Language = lang 
            });
        }
        
        return segments;
    }
}
```

#### 4.2 混合言語音素化
```csharp
public class MixedLanguagePhonemizer : IPhonemizerBackend
{
    private readonly OpenJTalkPhonemizer jaPhonemizerBackend;
    private readonly FlitePhonemizerBackend enPhonemizer;
    private readonly LanguageDetector languageDetector;
    
    public async Task<PhonemeResult> PhonemizeAsync(string text)
    {
        var segments = languageDetector.DetectSegments(text);
        var allPhonemes = new List<string>();
        
        foreach (var segment in segments)
        {
            PhonemeResult result;
            
            switch (segment.Language)
            {
                case "ja":
                    result = await jaPhonemizerBackend.PhonemizeAsync(segment.Text, "ja");
                    break;
                case "en":
                    result = await enPhonemizer.PhonemizeAsync(segment.Text, "en");
                    break;
                default:
                    // 記号等はスキップまたは無音
                    continue;
            }
            
            allPhonemes.AddRange(result.Phonemes);
        }
        
        return new PhonemeResult
        {
            Phonemes = allPhonemes.ToArray(),
            Success = true
        };
    }
}
```

## 期待される成果

### 品質向上
- 英語音素化精度: 60-70% → 90%以上
- 新語対応: ✅
- 固有名詞: ✅
- 日英混在: ✅

### 使用例
```csharp
// Before (現在)
"ChatGPT is amazing" → 不正確な音素列

// After (Flite統合後)
"ChatGPT is amazing" → CH AE T JH IY P IY T IY IH Z AH M EY Z IH NG

// 日英混在
"今日はmeeting at 3pmです" → 
  きょうは + M IY T IH NG + AE T + TH R IY + P IY EH M + です
```

## 実装スケジュール

| タスク | 期間 | 優先度 |
|--------|------|--------|
| Fliteビルド環境 | 2-3日 | 高 |
| Unity統合 | 3-4日 | 高 |
| テスト・最適化 | 2日 | 中 |
| 日英混在対応 | 3日 | 中 |
| **合計** | **10-12日** | - |

## 次のアクション

1. Fliteソースコード取得とライセンス確認
2. ビルドスクリプト作成
3. 最小限の統合テスト