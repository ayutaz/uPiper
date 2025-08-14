# Asian Language Support Implementation Guide

## Overview

This guide outlines the implementation strategy for Chinese and Korean language support in uPiper without using GPL-licensed components.

## 1. Chinese (Mandarin) Support

### Implementation Strategy

**Option 1: Pinyin-based Approach (Recommended)**
- Use MIT-licensed pinyin libraries
- Convert Chinese characters → Pinyin → Phonemes
- Support both Simplified and Traditional Chinese

**Resources:**
- **pypinyin-dict** (MIT) - Character to pinyin mapping
- **CC-CEDICT** (Creative Commons) - Chinese-English dictionary with pinyin
- Custom pinyin-to-phoneme mapping

### Data Sources

```bash
# 1. pypinyin dictionary data (MIT licensed)
# Contains 42,000+ Chinese characters with pinyin mappings
https://github.com/mozillazg/pinyin-data

# 2. CC-CEDICT (Creative Commons)
# 120,000+ entries with pinyin
https://www.mdbg.net/chinese/dictionary?page=cc-cedict
```

### Implementation Plan

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

### Pinyin to IPA Mapping

```
Example mappings:
ma1 → ma˥ (high tone)
ma2 → ma˧˥ (rising tone)
ma3 → ma˨˩˦ (dipping tone)
ma4 → ma˥˩ (falling tone)
ma → ma (neutral tone)

Initials: b[p], p[pʰ], m[m], f[f], d[t], t[tʰ], n[n], l[l]...
Finals: a[a], o[o], e[ɤ], i[i], u[u], ü[y]...
```

## 2. Korean Support

### Implementation Strategy

**Hangul Decomposition Approach**
- Decompose Hangul syllables into Jamo (consonants/vowels)
- Apply rule-based G2P for Korean
- No external dependencies needed

### Korean Phoneme Rules

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

### Hangul Decomposition Algorithm

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

## 3. Common Infrastructure

### Text Segmentation

Chinese requires word segmentation:
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

### Tone Handling

Both Chinese and Korean (to some extent) are tonal:
```csharp
public class ToneInfo
{
    public int ToneNumber { get; set; }      // 1-5 for Mandarin
    public string ToneMarking { get; set; }  // IPA tone marks
    public float PitchContour { get; set; } // For synthesis
}
```

## 4. Implementation Priority

1. **Chinese (Mandarin)** - Higher demand, larger user base
   - Start with Simplified Chinese (zh-CN)
   - Add Traditional Chinese (zh-TW) support
   - Implement tone handling

2. **Korean** - Simpler implementation
   - Hangul decomposition
   - Rule-based G2P
   - Handle sound changes

## 5. Testing Requirements

### Chinese Tests
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

### Korean Tests
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

## 6. Resource Requirements

### Chinese
- Pinyin dictionary: ~2MB
- Segmentation dictionary: ~5MB
- Total: ~7-10MB

### Korean
- Rule tables: ~100KB
- Exception dictionary: ~500KB
- Total: <1MB

## 7. Alternative Approaches

### For Chinese
1. **Character-based approach**: Direct character to phoneme mapping
2. **Bopomofo support**: For Traditional Chinese (Taiwan)
3. **Cantonese support**: Different phoneme set

### For Korean
1. **Romanization-based**: Use Korean romanization systems
2. **Exception dictionary**: For irregular pronunciations
3. **Dialect support**: Seoul vs regional pronunciations

## 8. Integration with Existing System

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

## 9. Performance Considerations

- Chinese: Word segmentation can be expensive
  - Use caching for segmented text
  - Pre-segment common phrases
  
- Korean: Hangul decomposition is fast
  - Direct algorithmic approach
  - No dictionary lookups needed

## 10. Future Enhancements

1. **Polyglot support**: Mixed Chinese-English text
2. **Dialect support**: Cantonese, Taiwanese, other Chinese dialects
3. **Prosody**: Better tone and intonation modeling
4. **Name handling**: Special rules for names
5. **Number/date reading**: Localized number pronunciation