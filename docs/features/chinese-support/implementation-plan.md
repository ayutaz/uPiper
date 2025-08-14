# 中国語サポート実装計画

## 概要

uPiperに実用的な中国語音素化機能を実装するための詳細な計画書です。現在のスタブ実装を、MITライセンス互換のリソースを使用して本格的な音素化システムに拡張します。

## 実装目標

### 主要目標
1. **高精度な音素化**: 95%以上の正確性（一般的な文章）
2. **実用的なパフォーマンス**: 100文字を50ms以内で処理
3. **メモリ効率**: 辞書データ込みで10MB以内
4. **多音字対応**: 文脈に基づく適切な読み選択

### サポート範囲
- 簡体字（zh-CN）- 第一優先
- 繁体字（zh-TW）- 第二優先
- 数字・句読点の正規化
- 基本的な英中混在テキスト

## Phase 1: 基礎実装（5-7日）

### 1.1 辞書データの準備とローダー実装（2日）

#### タスク
1. pypinyinデータの取得とJSON形式への変換
2. StreamingAssetsへの配置構造設計
3. 非同期辞書ローダーの実装

#### 実装詳細
```csharp
// ディレクトリ構造
Assets/StreamingAssets/uPiper/Chinese/
├── pinyin_dict.json          // 単一文字ピンイン辞書
├── phrase_pinyin_dict.json   // フレーズ辞書（多音字解決用）
├── word_freq.json            // 単語頻度データ
└── pinyin_ipa_map.json       // ピンイン→IPA変換テーブル

// 辞書ローダー
public class ChineseDictionaryLoader
{
    public async Task<ChinesePinyinDictionary> LoadAsync(CancellationToken ct)
    {
        // StreamingAssetsからJSON読み込み
        // Android対応（UnityWebRequest使用）
        // デスクトップ対応（File.ReadAllText）
    }
}
```

### 1.2 基本的なピンイン変換（1日）

#### 実装内容
```csharp
public class PinyinConverter
{
    private readonly Dictionary<char, string[]> charToPinyin;
    private readonly Dictionary<string, string> phraseToPinyin;
    
    public string[] GetPinyin(string text, bool usePhrase = true)
    {
        if (usePhrase)
        {
            // フレーズ辞書を優先的に検索
            return GetPinyinWithPhraseMatching(text);
        }
        else
        {
            // 単一文字変換（最も一般的な読み）
            return GetPinyinPerCharacter(text);
        }
    }
}
```

### 1.3 IPA変換実装（1日）

#### 実装内容
```csharp
public class PinyinToIPAConverter
{
    private readonly Dictionary<string, string> pinyinToIPA;
    private readonly Dictionary<int, string> toneMarks = new()
    {
        [1] = "\u02E5",      // ˥ (55)
        [2] = "\u02E7\u02E5", // ˧˥ (35)
        [3] = "\u02E8\u02E9\u02E6", // ˨˩˦ (214)
        [4] = "\u02E5\u02E9", // ˥˩ (51)
        [5] = ""             // 軽声（マークなし）
    };
    
    public string[] ConvertToIPA(string pinyinWithTone)
    {
        // ma3 → ["m", "a", "˨˩˦"]
        var (syllable, tone) = ExtractTone(pinyinWithTone);
        var ipaBase = pinyinToIPA[syllable];
        return ApplyToneAndSplit(ipaBase, tone);
    }
}
```

### 1.4 テキスト正規化（1日）

#### 実装内容
```csharp
public class ChineseTextNormalizer
{
    // 数字変換: 123 → 一二三 / 一百二十三
    public string NormalizeNumbers(string text, NumberFormat format);
    
    // 句読点正規化
    public string NormalizePunctuation(string text);
    
    // 英語処理: 単語境界で分離
    public (string chinese, string english)[] SplitMixedText(string text);
}
```

### 1.5 統合とテスト（1-2日）

#### 実装内容
```csharp
// ChinesePhonemizer.csの更新
public override async Task<PhonemeResult> PhonemizeAsync(
    string text,
    string language,
    PhonemeOptions options = null,
    CancellationToken cancellationToken = default)
{
    // 1. テキスト正規化
    var normalized = normalizer.Normalize(text);
    
    // 2. ピンイン変換（フレーズマッチング使用）
    var pinyinArray = pinyinConverter.GetPinyin(normalized, usePhrase: true);
    
    // 3. IPA変換
    var phonemes = new List<string>();
    foreach (var pinyin in pinyinArray)
    {
        var ipa = ipaConverter.ConvertToIPA(pinyin);
        phonemes.AddRange(ipa);
    }
    
    return new PhonemeResult
    {
        Phonemes = phonemes.ToArray(),
        Language = language,
        Success = true
    };
}
```

## Phase 2: 高度な機能（3-4日）

### 2.1 テキスト分割実装（2日）

#### jieba風アルゴリズム
```csharp
public class ChineseTextSegmenter
{
    private readonly TrieNode prefixDict;
    private readonly Dictionary<string, float> wordFreq;
    
    // 有向非巡回グラフ構築
    private Dictionary<int, List<int>> BuildDAG(string text)
    {
        var dag = new Dictionary<int, List<int>>();
        for (int i = 0; i < text.Length; i++)
        {
            dag[i] = new List<int>();
            // プレフィックス辞書で可能な単語終了位置を検索
            var possibleEnds = prefixDict.GetPossibleEnds(text, i);
            dag[i].AddRange(possibleEnds);
        }
        return dag;
    }
    
    // 動的計画法で最大確率パスを計算
    private int[] CalculateMaxProbPath(Dictionary<int, List<int>> dag, string text)
    {
        // Viterbiアルゴリズム風の実装
    }
}
```

### 2.2 多音字の文脈判定（1日）

#### 実装内容
```csharp
public class ContextualPinyinSelector
{
    // 例: "银行" → "yin2 hang2"（銀行）
    //     "行人" → "xing2 ren2"（歩行者）
    public string[] SelectPinyinByContext(string[] words, string[][] pinyinOptions)
    {
        // フレーズ辞書とN-gramモデルで選択
    }
}
```

### 2.3 パフォーマンス最適化（1日）

#### キャッシュ実装
```csharp
public class PhonemizationCache
{
    private readonly LRUCache<string, string[]> cache;
    
    public PhonemizationCache(int maxSize = 10000)
    {
        cache = new LRUCache<string, string[]>(maxSize);
    }
}
```

## Phase 3: 品質保証とテスト（2-3日）

### 3.1 包括的なテストスイート

#### テストケース
```csharp
[Test]
public void ChinesePhonemizer_BasicCharacters()
{
    var testCases = new Dictionary<string, string[]>
    {
        ["你好"] = new[] { "n", "i", "˨˩˦", "h", "a", "o", "˨˩˦" },
        ["中国"] = new[] { "ʈ͡ʂ", "o", "ŋ", "˥", "k", "w", "o", "˧˥" },
        ["一二三"] = new[] { "i", "˥", "ɚ", "˥˩", "s", "a", "n", "˥" }
    };
}

[Test]
public void ChinesePhonemizer_MultiToneCharacters()
{
    // 多音字テスト
    var testCases = new Dictionary<string, string[]>
    {
        ["银行"] = new[] { "i", "n", "˧˥", "h", "a", "ŋ", "˧˥" }, // 銀行
        ["行人"] = new[] { "ɕ", "i", "ŋ", "˧˥", "ʐ", "ə", "n", "˧˥" } // 歩行者
    };
}
```

### 3.2 パフォーマンステスト

```csharp
[Test]
public void ChinesePhonemizer_Performance()
{
    var text = "这是一个测试句子，包含标点符号和English words。";
    var stopwatch = Stopwatch.StartNew();
    
    for (int i = 0; i < 100; i++)
    {
        phonemizer.PhonemizeAsync(text).Wait();
    }
    
    stopwatch.Stop();
    var avgMs = stopwatch.ElapsedMilliseconds / 100.0;
    Assert.Less(avgMs, 50); // 50ms以内
}
```

### 3.3 メモリ使用量テスト

```csharp
[Test]
public void ChinesePhonemizer_MemoryUsage()
{
    var memBefore = GC.GetTotalMemory(true);
    var phonemizer = new ChinesePhonemizer();
    await phonemizer.InitializeAsync();
    var memAfter = GC.GetTotalMemory(true);
    
    var usedMB = (memAfter - memBefore) / (1024.0 * 1024.0);
    Assert.Less(usedMB, 10); // 10MB以内
}
```

## 実装スケジュール

### Week 1
- **Day 1-2**: 辞書データ準備とローダー実装
- **Day 3**: 基本的なピンイン変換
- **Day 4**: IPA変換実装
- **Day 5**: テキスト正規化

### Week 2
- **Day 6-7**: 統合とPhase 1テスト
- **Day 8-9**: テキスト分割実装
- **Day 10**: 多音字の文脈判定

### Week 3
- **Day 11**: パフォーマンス最適化
- **Day 12-13**: 包括的なテスト実装
- **Day 14**: ドキュメント作成とレビュー

## リスク管理

### 技術的リスク
1. **辞書サイズ**: 予想より大きい場合 → 圧縮とストリーミング読み込み
2. **分割精度**: jieba完全互換が困難 → 基本的な最長一致法から開始
3. **パフォーマンス**: 目標未達成 → Unity Job Systemの活用

### 対策
- 各Phaseごとにレビューとフィードバック
- 早期のプロトタイプでリスク検証
- 段階的な機能追加（MVP → 完全版）

## 成功基準

1. **機能要件**
   - ✅ 20,000文字以上の辞書カバレッジ
   - ✅ 声調付きIPA出力
   - ✅ 基本的な多音字対応
   - ✅ 数字・句読点の正規化

2. **非機能要件**
   - ✅ 100文字を50ms以内で処理
   - ✅ メモリ使用量10MB以内
   - ✅ 95%以上の音素化精度（一般的な文章）

3. **品質要件**
   - ✅ 包括的なユニットテスト
   - ✅ パフォーマンステスト
   - ✅ ドキュメント完備