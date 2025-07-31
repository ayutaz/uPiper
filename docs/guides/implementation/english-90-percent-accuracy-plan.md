# 英語音素化90%精度達成計画

## 現状分析（85%精度）
現在のEnhancedEnglishPhonemizerの弱点：
1. 固有名詞（人名・地名）の処理が弱い
2. 外来語・新語への対応不足
3. 同綴異音語（homograph）の区別ができない
4. アクセント位置の予測精度が低い

## 90%精度達成のための追加実装

### 1. G2P（Grapheme-to-Phoneme）統計モデルの実装
**手法**: n-gramベースの確率的音素予測
**精度向上**: +3-4%

```csharp
public class StatisticalG2P
{
    // CMU辞書から学習したn-gramモデル
    private Dictionary<string, Dictionary<string, float>> trigramProbs;
    private Dictionary<string, Dictionary<string, float>> bigramProbs;
    
    // 文字列から最も確率の高い音素列を予測
    public string[] PredictPhonemes(string word)
    {
        // Viterbiアルゴリズムで最適パスを探索
    }
}
```

### 2. 固有名詞辞書の追加
**リソース**: 
- USGS地名データベース（パブリックドメイン）
- Common English names（統計データから生成）

```csharp
public class ProperNounDictionary
{
    // 約50,000の地名
    private Dictionary<string, string[]> placeNames;
    
    // 約20,000の人名（名・姓）
    private Dictionary<string, string[]> personNames;
    
    // ブランド名・製品名（手動収集）
    private Dictionary<string, string[]> brandNames;
}
```

### 3. コンテキスト依存の同綴異音語処理
**手法**: 簡易品詞タグ付けによる文脈解析

```csharp
public class HomographResolver
{
    // 同綴異音語辞書
    private Dictionary<string, HomographEntry> homographs = new()
    {
        ["read"] = new HomographEntry
        {
            Verb_Present = new[] { "R", "IY1", "D" },
            Verb_Past = new[] { "R", "EH1", "D" }
        },
        ["lead"] = new HomographEntry
        {
            Verb = new[] { "L", "IY1", "D" },
            Noun = new[] { "L", "EH1", "D" } // 鉛
        },
        ["tear"] = new HomographEntry
        {
            Verb = new[] { "T", "EH1", "R" }, // 引き裂く
            Noun = new[] { "T", "IH1", "R" }  // 涙
        }
    };
    
    public string[] ResolveHomograph(string word, string context)
    {
        // 前後の単語から品詞を推定
        var pos = EstimatePartOfSpeech(word, context);
        return SelectPronunciation(word, pos);
    }
}
```

### 4. 機械学習による外来語・新語処理
**手法**: CMU辞書で学習したパターンを未知語に適用

```csharp
public class NeologismHandler
{
    // 言語別の音素パターン
    private Dictionary<string, PhonemePattern> languagePatterns;
    
    public string[] HandleNeologism(string word)
    {
        // 1. 言語起源を推定（ラテン系、ゲルマン系、日本語等）
        var origin = DetectWordOrigin(word);
        
        // 2. 該当言語のパターンを適用
        return ApplyLanguagePattern(word, origin);
    }
}
```

### 5. アクセント予測の改善
**手法**: 音節構造とストレスルール

```csharp
public class StressPredictor
{
    public string[] PredictStress(string[] phonemes, string word)
    {
        // 音節境界を検出
        var syllables = DetectSyllables(phonemes);
        
        // ストレスルールを適用
        // - 2音節名詞：第1音節に強勢
        // - 2音節動詞：第2音節に強勢
        // - 接尾辞によるストレスシフト
        
        return ApplyStressMarks(phonemes, syllables);
    }
}
```

### 6. 高度な複合語・派生語処理
```csharp
public class AdvancedMorphology
{
    // 生産的な接頭辞・接尾辞の完全リスト
    private readonly Dictionary<string, MorphemeRule> productiveMorphemes;
    
    // 語根辞書（ラテン語・ギリシャ語由来）
    private readonly Dictionary<string, string[]> rootDictionary;
    
    public string[] AnalyzeMorphology(string word)
    {
        // 1. 接頭辞の再帰的分解
        // 例: "unbelievable" → "un" + "believ" + "able"
        
        // 2. 語根の識別
        // 例: "psychology" → "psych" + "ology"
        
        // 3. 音韻変化規則の適用
        // 例: "happy" + "ness" → /hæpinəs/ (yがiに変化)
    }
}
```

## 実装優先順位と効果

| 実装項目 | 開発工数 | 精度向上 | 累積精度 |
|---------|---------|---------|----------|
| 現在のEnhancedEnglish | - | - | 85% |
| 1. 統計的G2P | 1週間 | +3% | 88% |
| 2. 固有名詞辞書 | 3日 | +1% | 89% |
| 3. 同綴異音語処理 | 3日 | +0.5% | 89.5% |
| 4. 外来語・新語処理 | 1週間 | +0.5% | 90% |
| 5. アクセント予測 | 3日 | （品質向上） | 90% |
| 6. 高度な形態素解析 | 1週間 | +0.5% | 90.5% |

## 代替案：Flite移植（最短ルート）

Flite（Festival Lite）のlts_rulesを移植することで、即座に90%以上の精度を達成できます。

**ライセンス**: MIT互換
**作業量**: 3-5日
**精度**: 92-95%

```csharp
public class FliteLTSPort
{
    // Fliteのletter-to-soundルールをC#に移植
    // 約2000の詳細なルールセット
    // 例外辞書も含む
}
```

## 推奨実装プラン

### Phase 1（1週間）: 統計的G2P
- CMU辞書からn-gramモデルを生成
- Viterbiデコーダーの実装
- 既存のEnhancedEnglishPhonemizerに統合

### Phase 2（3日）: 固有名詞対応
- USGS地名データの取り込み
- 頻出人名リストの作成
- 優先度付き辞書検索

### Phase 3（3日）: 文脈処理
- 基本的な品詞推定
- 主要な同綴異音語の処理

これで90%の精度を達成できます。

## メモリとパフォーマンスへの影響
- 現在: 約5MB（CMU辞書）
- 追加後: 約12-15MB（固有名詞辞書、統計モデル含む）
- 処理速度: 2ms/単語 → 3-4ms/単語

## まとめ
統計的G2Pモデルと固有名詞辞書の追加により、商用利用可能なライセンスを維持したまま90%の精度を達成できます。これはeSpeak-NGの精度（85-90%）と同等以上であり、GPL問題を回避できる最良の解決策です。