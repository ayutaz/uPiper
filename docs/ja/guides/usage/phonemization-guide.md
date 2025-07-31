# uPiper 音素化ガイド

## 概要

uPiperは日本語と英語のテキストを高品質に音素化し、音声合成を行うことができます。このガイドでは、音素化機能の使い方について説明します。

## 基本的な使い方

### UnifiedPhonemizerの初期化

```csharp
using uPiper.Core.Phonemizers;

// 統一音素化インターフェースを初期化
var phonemizer = new UnifiedPhonemizer();
await phonemizer.InitializeAsync();
```

### テキストの音素化

```csharp
// 自動言語検出
var result = await phonemizer.PhonemizeAsync("こんにちは、世界！");

// 言語を指定
var jaResult = await phonemizer.PhonemizeAsync("こんにちは", "ja");
var enResult = await phonemizer.PhonemizeAsync("Hello world", "en");

// 日英混在テキスト
var mixedResult = await phonemizer.PhonemizeAsync(
    "今日のmeetingは3pmからです", 
    "mixed"
);
```

## 対応言語

### 日本語（ja）
- **バックエンド**: OpenJTalk
- **精度**: 99%以上
- **特徴**: 形態素解析による高精度な読み推定

### 英語（en）
- **バックエンド**: SimpleLTS / RuleBased
- **精度**: 辞書内95%以上、辞書外70-80%
- **特徴**: CMU辞書（134,000語）+ Letter-to-Sound規則

### 混合言語（mixed/auto）
- **機能**: 日英混在テキストの自動処理
- **精度**: 各言語セグメントは個別バックエンドの精度に準拠

## 詳細オプション

### PhonemeOptions

```csharp
var options = new PhonemeOptions
{
    Format = PhonemeFormat.IPA,        // IPA形式で出力
    IncludeStress = true,             // ストレス情報を含む
    IncludeTones = false,             // トーン情報（日本語では未対応）
    NormalizeText = true,             // テキスト正規化
    UseG2PFallback = true            // 辞書にない語のG2P使用
};

var result = await phonemizer.PhonemizeAsync(text, "en", options);
```

### 結果の取得

```csharp
if (result.Success)
{
    // 音素配列
    string[] phonemes = result.Phonemes;
    
    // 処理時間
    float processingMs = result.ProcessingTimeMs;
    
    // キャッシュからの結果か
    bool fromCache = result.FromCache;
    
    // 使用されたバックエンド
    string backend = result.Backend;
}
else
{
    // エラー処理
    Debug.LogError($"音素化エラー: {result.Error}");
}
```

## 日英混在テキストの処理

### 自動言語検出

```csharp
// "auto"を指定すると自動的に言語を検出
var result = await phonemizer.PhonemizeAsync(
    "UnityでAI音声合成を実装する", 
    "auto"
);
```

### 言語セグメントの分析

```csharp
var mixedPhonemizer = new MixedLanguagePhonemizer();
await mixedPhonemizer.InitializeAsync();

// テキストの言語構成を分析
var stats = mixedPhonemizer.AnalyzeText("今日はnice weatherですね");
// 結果: { "segment_count": 5, "language_segments": {"ja": 2, "en": 2}, ... }
```

## パフォーマンス最適化

### キャッシュの活用

uPiperは自動的に音素化結果をキャッシュします：

```csharp
// キャッシュ統計の取得
var cacheStats = PhonemeCache.Instance.GetStatistics();
Debug.Log($"Cache hit rate: {cacheStats.HitRate:P1}");

// キャッシュのクリア（必要な場合）
PhonemeCache.Instance.Clear();
```

### バッチ処理

```csharp
var texts = new[] { "テキスト1", "テキスト2", "テキスト3" };
var tasks = texts.Select(t => phonemizer.PhonemizeAsync(t, "auto"));
var results = await Task.WhenAll(tasks);
```

## 使用例

### 基本的な音声合成

```csharp
// PiperTTSと組み合わせて使用
var tts = GetComponent<PiperTTS>();

// テキストを音素化して音声合成
async void Speak(string text)
{
    var phonemeResult = await phonemizer.PhonemizeAsync(text, "auto");
    if (phonemeResult.Success)
    {
        // 音素列を使って音声を生成
        var audioClip = await tts.GenerateAudioFromPhonemes(phonemeResult);
        audioSource.clip = audioClip;
        audioSource.Play();
    }
}
```

### リアルタイム処理

```csharp
// UIからの入力を処理
public async void OnTextChanged(string newText)
{
    // キャンセレーショントークンで前の処理をキャンセル
    cts?.Cancel();
    cts = new CancellationTokenSource();
    
    try
    {
        var result = await phonemizer.PhonemizeAsync(
            newText, 
            "auto", 
            null, 
            cts.Token
        );
        
        if (result.Success)
        {
            UpdatePhonemeDisplay(result.Phonemes);
        }
    }
    catch (OperationCanceledException)
    {
        // キャンセルされた場合は無視
    }
}
```

## トラブルシューティング

### 音素化が失敗する場合

1. **初期化の確認**
   ```csharp
   if (!phonemizer.IsInitialized)
   {
       await phonemizer.InitializeAsync();
   }
   ```

2. **言語の確認**
   ```csharp
   if (!phonemizer.SupportsLanguage("ja"))
   {
       Debug.LogError("Japanese not supported");
   }
   ```

3. **バックエンドの確認**
   ```csharp
   var backends = phonemizer.GetAvailableBackends();
   foreach (var (lang, list) in backends)
   {
       Debug.Log($"{lang}: {string.Join(", ", list)}");
   }
   ```

### パフォーマンスの問題

1. **キャッシュの活用**
   - 同じテキストの繰り返し処理は自動的にキャッシュされます

2. **適切な言語指定**
   - "auto"より具体的な言語指定の方が高速です

3. **バッチ処理**
   - 複数のテキストは並列処理で高速化できます

## 高度な使い方

### カスタムバックエンドの追加

```csharp
public class MyCustomPhonemizer : IPhonemizerBackend
{
    // カスタム実装
}

// 登録方法は将来のバージョンで提供予定
```

### 音素マッピングのカスタマイズ

```csharp
// 将来のバージョンで提供予定
var customMapping = new PhonemeMapping();
customMapping.AddMapping("custom_phoneme", 100);
```

## まとめ

uPiperの音素化機能は、日本語と英語、そしてその混在テキストを高品質に処理できます。UnifiedPhonemizerを使用することで、言語を意識せずに簡単に音素化を行うことができます。