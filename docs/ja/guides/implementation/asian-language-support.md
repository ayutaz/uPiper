# アジア言語サポート実装ガイド

[🇯🇵 **日本語**](../../ja/guides/implementation/asian-language-support.md) | [🇬🇧 English](../../en/guides/implementation/asian-language-support.md)

## 概要

このガイドでは、GPLライセンスのコンポーネントを使用せずに、uPiperで中国語と韓国語のサポートを実装する戦略について説明します。

## 1. 中国語（北京語）サポート

### 実装戦略

**オプション1: ピンインベースのアプローチ（推奨）**
- MITライセンスのピンインライブラリを使用
- 中国語文字 → ピンイン → 音素への変換
- 簡体字と繁体字の両方をサポート

**リソース:**
- **pypinyin-dict** (MIT) - 文字からピンインへのマッピング
- **CC-CEDICT** (Creative Commons) - ピンイン付き中英辞書
- カスタムピンイン音素マッピング

### データソース

```bash
# 1. pypinyin辞書データ (MITライセンス)
# 42,000以上の中国語文字とピンインマッピングを含む
https://github.com/mozillazg/pinyin-data

# 2. CC-CEDICT (Creative Commons)
# ピンイン付き120,000以上のエントリ
https://www.mdbg.net/chinese/dictionary?page=cc-cedict
```

### 実装計画

```csharp
// ChinesePhonemizer.cs
public class ChinesePhonemizer : PhonemizerBackendBase
{
    private Dictionary<char, string[]> pinyinDict;  // Character → Pinyin
    private PinyinToPhonemeMapper phonemeMapper;    // Pinyin → IPA
    private ChineseTextSegmenter segmenter;         // Word segmentation
    
    public override string[] SupportedLanguages => new[] 
    { 
        "zh", "zh-CN", "zh-TW", "zh-HK", "zh-SG" 
    };
}
```

### ピンインからIPAへのマッピング

```
マッピング例:
ma1 → ma˥ (第一声：高平調)
ma2 → ma˧˥ (第二声：上昇調)
ma3 → ma˨˩˦ (第三声：低降昇調)
ma4 → ma˥˩ (第四声：下降調)
ma → ma (軽声)

声母: b[p], p[pʰ], m[m], f[f], d[t], t[tʰ], n[n], l[l]...
韻母: a[a], o[o], e[ɤ], i[i], u[u], ü[y]...
```

## 2. 韓国語サポート

### 実装戦略

**ハングル分解アプローチ**
- ハングル音節を字母（子音/母音）に分解
- 韓国語用のルールベースG2Pを適用
- 外部依存関係不要

### 韓国語音素ルール

```csharp
// KoreanPhonemizer.cs
public class KoreanPhonemizer : PhonemizerBackendBase
{
    // Hangul syllable = Initial + Medial + (Optional) Final
    // Unicode: 0xAC00 + (initial × 588) + (medial × 28) + final
    
    private readonly string[] initials = { "g", "kk", "n", "d", "tt", "r", "m", "b", "pp", 
                                          "s", "ss", "", "j", "jj", "ch", "k", "t", "p", "h" };
    private readonly string[] medials = { "a", "ae", "ya", "yae", "eo", "e", "yeo", "ye", "o", 
                                         "wa", "wae", "oe", "yo", "u", "wo", "we", "wi", "yu", 
                                         "eu", "ui", "i" };
    private readonly string[] finals = { "", "g", "kk", "ks", "n", "nj", "nh", "d", "l", "lg", 
                                        "lm", "lb", "ls", "lt", "lp", "lh", "m", "b", "bs", 
                                        "s", "ss", "ng", "j", "ch", "k", "t", "p", "h" };
}
```

### ハングル分解アルゴリズム

```csharp
public (int initial, int medial, int final) DecomposeHangul(char syllable)
{
    if (syllable < 0xAC00 || syllable > 0xD7A3)
        throw new ArgumentException("Not a Hangul syllable");
        
    int syllableIndex = syllable - 0xAC00;
    int initial = syllableIndex / 588;
    int medial = (syllableIndex % 588) / 28;
    int final = syllableIndex % 28;
    
    return (initial, medial, final);
}
```

## 3. 共通インフラストラクチャ

### テキスト分割

中国語には単語分割が必要です：
```csharp
public interface ITextSegmenter
{
    string[] Segment(string text);
}

public class ChineseSegmenter : ITextSegmenter
{
    // Simple maximum matching algorithm
    // Or integrate jieba-like segmentation
}
```

### 声調処理

中国語と韓国語（ある程度）は声調言語です：
```csharp
public class ToneInfo
{
    public int ToneNumber { get; set; }      // 1-5 for Mandarin
    public string ToneMarking { get; set; }  // IPA tone marks
    public float PitchContour { get; set; } // For synthesis
}
```

## 4. 実装優先順位

1. **中国語（北京語）** - 需要が高く、ユーザーベースが大きい
   - 簡体字（zh-CN）から開始
   - 繁体字（zh-TW）サポートを追加
   - 声調処理を実装

2. **韓国語** - より簡単な実装
   - ハングル分解
   - ルールベースG2P
   - 音韻変化の処理

## 5. テスト要件

### 中国語テスト
```csharp
[Test]
public void Chinese_ShouldHandleBasicCharacters()
{
    var tests = new Dictionary<string, string[]>
    {
        ["你好"] = new[] { "n", "i", "˨˩˦", "h", "a", "o", "˨˩˦" },
        ["中国"] = new[] { "zh", "o", "ng", "˥", "g", "u", "o", "˧˥" },
        ["谢谢"] = new[] { "x", "i", "e", "˥˩", "x", "i", "e", "˥˩" }
    };
}
```

### 韓国語テスト
```csharp
[Test]
public void Korean_ShouldDecomposeHangul()
{
    var tests = new Dictionary<string, string[]>
    {
        ["안녕"] = new[] { "a", "n", "n", "y", "eo", "ng" },
        ["한국"] = new[] { "h", "a", "n", "g", "u", "k" },
        ["사랑"] = new[] { "s", "a", "r", "a", "ng" }
    };
}
```

## 6. リソース要件

### 中国語
- ピンイン辞書: 約2MB
- 分割辞書: 約5MB
- 合計: 約7-10MB

### 韓国語
- ルールテーブル: 約100KB
- 例外辞書: 約500KB
- 合計: 1MB未満

## 7. 代替アプローチ

### 中国語向け
1. **文字ベースアプローチ**: 文字から音素への直接マッピング
2. **注音符号サポート**: 繁体字中国語（台湾）用
3. **広東語サポート**: 異なる音素セット

### 韓国語向け
1. **ローマ字ベース**: 韓国語ローマ字システムを使用
2. **例外辞書**: 不規則な発音用
3. **方言サポート**: ソウル語と地方語の発音

## 8. 既存システムとの統合

```csharp
// In PhonemizerService.cs
private void RegisterDefaultBackends()
{
    // Existing backends...
    
    // Add Asian language support
    backendFactory.RegisterBackend(new ChinesePhonemizer());
    backendFactory.RegisterBackend(new KoreanPhonemizer());
}
```

## 9. パフォーマンスに関する考慮事項

- 中国語: 単語分割はコストが高い可能性
  - 分割済みテキストのキャッシュを使用
  - 一般的なフレーズを事前分割
  
- 韓国語: ハングル分解は高速
  - 直接的なアルゴリズムアプローチ
  - 辞書検索不要

## 10. 将来の拡張

1. **多言語サポート**: 中英混在テキスト
2. **方言サポート**: 広東語、台湾語、その他の中国語方言
3. **韻律**: より良い声調とイントネーションのモデリング
4. **名前の処理**: 名前用の特別なルール
5. **数字/日付の読み上げ**: ローカライズされた数字の発音