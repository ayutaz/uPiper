# 追加言語サポートガイド

## 概要

このガイドでは、uPiper音素化システムで英語以外の言語サポートを追加する方法を説明します。

## 言語サポート戦略

### 1. 辞書ベースアプローチ（推奨）

各言語には以下が必要です：
- 発音辞書
- G2Pルールまたはモデル
- テキスト正規化ルール
- IPAへの音素マッピング

### 2. 言語別の利用可能なリソース

#### スペイン語 (es-ES)

**オプション1: Santiago辞書**
```bash
# スペイン語発音辞書のダウンロード
wget https://raw.githubusercontent.com/santiagopm/e-spk/master/santiago.dic

# 形式: 単語 音素 (スペイン語 SAMPA)
# 例: HOLA O l a
```

**オプション2: eSpeakデータから作成（MIT互換抽出）**
```python
# extract_spanish_dict.py
# オープンソースから発音データを抽出
import requests

def create_spanish_dictionary():
    # 公開されているスペイン語単語リストを使用
    # スペイン語発音ルールを適用
    pass
```

#### フランス語 (fr-FR)

**Lexique.orgデータベース**
```bash
# フランス語発音データベース (CC-BY-SA)
wget http://www.lexique.org/databases/Lexique383/Lexique383.tsv

# CMU形式に変換
python convert_lexique_to_cmu.py Lexique383.tsv french_dict.txt
```

#### ドイツ語 (de-DE)

**MARY TTSドイツ語辞書**
```bash
# ドイツ語発音データ (LGPL - 注意が必要)
# より良い方法: Wiktionaryデータから作成 (CC-BY-SA)
python extract_german_from_wiktionary.py
```

#### 日本語 (ja-JP)

**MeCab + UniDic統合**
```csharp
// uPiperのOpenJTalk経由で既にサポート済み
// mecab-ipadic辞書を使用 (BSDライセンス)
public class JapanesePhonemizer : IPhonemizerBackend
{
    private OpenJTalkPhonemizer openJTalk;
    
    public async Task<PhonemeResult> PhonemizeAsync(
        string text, string language, 
        PhonemeOptions options, CancellationToken ct)
    {
        // 既存のOpenJTalk実装を使用
        return await openJTalk.ProcessAsync(text);
    }
}
```

#### 中国語 (zh-CN)

**pypinyin-dict (MIT)**
```python
# 中国語文字からピンインへのマッピング
# ピンインを音素に変換
pip install pypinyin

# 音素形式に抽出・変換
python create_chinese_dict.py
```

## 実装手順

### 1. 言語固有バックエンドの作成

```csharp
// SpanishPhonemizer.cs
public class SpanishPhonemizer : PhonemizerBackendBase
{
    private Dictionary<string, string[]> spanishDict;
    private SpanishG2P g2pEngine;
    private SpanishTextNormalizer normalizer;
    
    public override string[] SupportedLanguages => new[] { "es-ES", "es-MX", "es-AR" };
    
    protected override async Task<bool> InitializeInternalAsync(
        PhonemizerBackendOptions options, 
        CancellationToken cancellationToken)
    {
        // スペイン語辞書をロード
        var dictPath = Path.Combine(options.DataPath, "spanish_dict.txt");
        spanishDict = await LoadDictionaryAsync(dictPath);
        
        // G2Pルールを初期化
        g2pEngine = new SpanishG2P();
        
        // ノーマライザーを初期化
        normalizer = new SpanishTextNormalizer();
        
        return true;
    }
    
    public override async Task<PhonemeResult> PhonemizeAsync(
        string text, string language, 
        PhonemeOptions options, CancellationToken ct)
    {
        // 1. スペイン語テキストを正規化（ñ、アクセントなどを処理）
        var normalized = normalizer.Normalize(text);
        
        // 2. トークン化
        var words = TokenizeSpanish(normalized);
        
        // 3. 音素を検索または生成
        var phonemes = new List<string>();
        foreach (var word in words)
        {
            if (spanishDict.TryGetValue(word.ToUpper(), out var prons))
            {
                phonemes.AddRange(prons);
            }
            else
            {
                // G2Pルールを使用
                phonemes.AddRange(g2pEngine.Grapheme2Phoneme(word));
            }
        }
        
        return new PhonemeResult { Phonemes = phonemes };
    }
}
```

### 2. スペイン語G2Pルール

```csharp
public class SpanishG2P
{
    private readonly Dictionary<string, string> rules = new()
    {
        // 母音
        ["a"] = "a",
        ["e"] = "e",
        ["i"] = "i",
        ["o"] = "o",
        ["u"] = "u",
        
        // 子音
        ["b"] = "b",
        ["c"] = "k", // a, o, uの前
        ["ce"] = "θ", // スペインのスペイン語
        ["ci"] = "θ", // スペインのスペイン語
        ["ch"] = "tʃ",
        ["d"] = "d",
        ["f"] = "f",
        ["g"] = "g", // a, o, uの前
        ["ge"] = "x", // スペイン語のj音
        ["gi"] = "x",
        ["h"] = "", // 無音
        ["j"] = "x",
        ["k"] = "k",
        ["l"] = "l",
        ["ll"] = "ʎ", // 一部の方言では"j"
        ["m"] = "m",
        ["n"] = "n",
        ["ñ"] = "ɲ",
        ["p"] = "p",
        ["qu"] = "k",
        ["r"] = "ɾ", // 単一のr
        ["rr"] = "r", // 巻き舌のr
        ["s"] = "s",
        ["t"] = "t",
        ["v"] = "b", // スペイン語ではbと同じ
        ["w"] = "w",
        ["x"] = "ks",
        ["y"] = "j",
        ["z"] = "θ" // スペインのスペイン語
    };
    
    public List<string> Grapheme2Phoneme(string word)
    {
        // 文脈に応じてルールを適用
        // 二重母音、ストレスなどを処理
    }
}
```

### 3. テキスト正規化

```csharp
public class SpanishTextNormalizer
{
    public string Normalize(string text)
    {
        // 数字を処理
        text = NormalizeNumbers(text);
        
        // 略語を展開
        text = ExpandAbbreviations(text);
        
        // 特殊な句読点を処理 (¿ ¡)
        text = HandleSpanishPunctuation(text);
        
        return text;
    }
    
    private string NormalizeNumbers(string text)
    {
        // "123" -> "ciento veintitrés"
        return Regex.Replace(text, @"\d+", match =>
        {
            int number = int.Parse(match.Value);
            return ConvertNumberToSpanishWords(number);
        });
    }
}
```

### 4. 新しい言語の登録

```csharp
// BackendFactoryまたはサービス初期化時
public void RegisterLanguageBackends()
{
    // 既存
    RegisterBackend(new RuleBasedPhonemizer()); // 英語
    RegisterBackend(new OpenJTalkPhonemizer()); // 日本語
    
    // 新しい言語
    RegisterBackend(new SpanishPhonemizer());
    RegisterBackend(new FrenchPhonemizer());
    RegisterBackend(new GermanPhonemizer());
    RegisterBackend(new ChinesePhonemizer());
}
```

### 5. 言語固有テスト

```csharp
[Test]
public async Task Spanish_ShouldHandleAccents()
{
    var phonemizer = new SpanishPhonemizer();
    await phonemizer.InitializeAsync(null);
    
    var testWords = new Dictionary<string, string[]>
    {
        ["mamá"] = new[] { "m", "a", "m", "a" },
        ["niño"] = new[] { "n", "i", "ɲ", "o" },
        ["café"] = new[] { "k", "a", "f", "e" }
    };
    
    foreach (var (word, expected) in testWords)
    {
        var result = await phonemizer.PhonemizeAsync(word, "es-ES");
        CollectionAssert.AreEqual(expected, result.Phonemes);
    }
}
```

## データ形式の標準化

### 統一辞書形式

```
# 言語: es-ES
# 形式: 単語[TAB]音素1 音素2 ...
# エンコーディング: UTF-8

HOLA    o l a
MUNDO   m u n d o
ESPAÑA  e s p a ɲ a
```

### IPAマッピング

一貫性のため、すべての言語はIPAにマップする必要があります：

```csharp
public static class PhonemeMapper
{
    public static Dictionary<string, Dictionary<string, string>> LanguageToIPA = new()
    {
        ["en-US"] = new() { ["AA"] = "ɑ", ["AE"] = "æ", ... },
        ["es-ES"] = new() { ["a"] = "a", ["e"] = "e", ... },
        ["fr-FR"] = new() { ["a"] = "a", ["é"] = "e", ... },
        ["de-DE"] = new() { ["a"] = "a", ["ä"] = "ɛ", ... }
    };
}
```

## リソース要件

### 辞書サイズ

- スペイン語: 約80,000語（約2MB）
- フランス語: 約140,000語（約4MB）
- ドイツ語: 約120,000語（約3.5MB）
- 中国語: 約10,000文字（約1MB）

### メモリ使用量

各言語は以下を追加で使用します：
- 辞書: 5-10MB RAM
- G2Pモデル: 1-5MB RAM
- テキストノーマライザー: 1MB未満 RAM

## クイックスタートテンプレート

### 最小限のスペイン語サポート

1. ダウンロード: [スペイン語スターターパック](link-to-resource)
2. 展開先: `Assets/StreamingAssets/uPiper/Languages/Spanish/`
3. バックエンドを追加: 
   ```csharp
   backendFactory.RegisterBackend(new SpanishPhonemizer());
   ```
4. テスト:
   ```csharp
   var result = await service.PhonemizeAsync("Hola mundo", "es-ES");
   ```

## コミュニティ貢献

新しい言語を貢献するには：

1. 標準形式で辞書を作成
2. G2Pルールを実装
3. テキストノーマライザーを追加
4. テストを作成（最低50テストケース）
5. 以下を含むPRを提出：
   - バックエンド実装
   - テストスイート
   - サンプル辞書（最低1000単語）
   - ドキュメント

## ライセンスに関する考慮事項

辞書のライセンスを常に確認してください：
- ✅ パブリックドメイン、MIT、BSD、Apache 2.0
- ⚠️ CC-BY-SA（帰属表示が必要）
- ❌ GPL、LGPL（商用利用では避ける）