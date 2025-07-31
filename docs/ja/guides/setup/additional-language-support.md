# Additional Language Support Guide

## Overview

This guide explains how to add support for languages beyond English in the uPiper phonemizer system.

## Language Support Strategy

### 1. Dictionary-Based Approach (Recommended)

For each language, you need:
- Pronunciation dictionary
- G2P rules or model
- Text normalization rules
- Phoneme mapping to IPA

### 2. Available Resources by Language

#### Spanish (es-ES)

**Option 1: Santiago Lexicon**
```bash
# Download Spanish pronunciation dictionary
wget https://raw.githubusercontent.com/santiagopm/e-spk/master/santiago.dic

# Format: WORD PHONEMES (Spanish SAMPA)
# Example: HOLA O l a
```

**Option 2: Create from eSpeak data (MIT-compatible extraction)**
```python
# extract_spanish_dict.py
# Extract pronunciation data from open sources
import requests

def create_spanish_dictionary():
    # Use publicly available Spanish word lists
    # Apply Spanish pronunciation rules
    pass
```

#### French (fr-FR)

**Lexique.org Database**
```bash
# French pronunciation database (CC-BY-SA)
wget http://www.lexique.org/databases/Lexique383/Lexique383.tsv

# Convert to CMU format
python convert_lexique_to_cmu.py Lexique383.tsv french_dict.txt
```

#### German (de-DE)

**MARY TTS German Dictionary**
```bash
# German pronunciation data (LGPL - be careful)
# Better: Create from Wiktionary data (CC-BY-SA)
python extract_german_from_wiktionary.py
```

#### Japanese (ja-JP)

**MeCab + UniDic Integration**
```csharp
// Already supported via OpenJTalk in uPiper
// Uses mecab-ipadic dictionary (BSD license)
public class JapanesePhonemizer : IPhonemizerBackend
{
    private OpenJTalkPhonemizer openJTalk;
    
    public async Task<PhonemeResult> PhonemizeAsync(
        string text, string language, 
        PhonemeOptions options, CancellationToken ct)
    {
        // Use existing OpenJTalk implementation
        return await openJTalk.ProcessAsync(text);
    }
}
```

#### Chinese (zh-CN)

**pypinyin-dict (MIT)**
```python
# Chinese character to pinyin mapping
# Convert pinyin to phonemes
pip install pypinyin

# Extract and convert to phoneme format
python create_chinese_dict.py
```

## Implementation Steps

### 1. Create Language-Specific Backend

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
        // Load Spanish dictionary
        var dictPath = Path.Combine(options.DataPath, "spanish_dict.txt");
        spanishDict = await LoadDictionaryAsync(dictPath);
        
        // Initialize G2P rules
        g2pEngine = new SpanishG2P();
        
        // Initialize normalizer
        normalizer = new SpanishTextNormalizer();
        
        return true;
    }
    
    public override async Task<PhonemeResult> PhonemizeAsync(
        string text, string language, 
        PhonemeOptions options, CancellationToken ct)
    {
        // 1. Normalize Spanish text (handle ñ, accents, etc.)
        var normalized = normalizer.Normalize(text);
        
        // 2. Tokenize
        var words = TokenizeSpanish(normalized);
        
        // 3. Look up or generate phonemes
        var phonemes = new List<string>();
        foreach (var word in words)
        {
            if (spanishDict.TryGetValue(word.ToUpper(), out var prons))
            {
                phonemes.AddRange(prons);
            }
            else
            {
                // Use G2P rules
                phonemes.AddRange(g2pEngine.Grapheme2Phoneme(word));
            }
        }
        
        return new PhonemeResult { Phonemes = phonemes };
    }
}
```

### 2. Spanish G2P Rules

```csharp
public class SpanishG2P
{
    private readonly Dictionary<string, string> rules = new()
    {
        // Vowels
        ["a"] = "a",
        ["e"] = "e",
        ["i"] = "i",
        ["o"] = "o",
        ["u"] = "u",
        
        // Consonants
        ["b"] = "b",
        ["c"] = "k", // before a, o, u
        ["ce"] = "θ", // Spain Spanish
        ["ci"] = "θ", // Spain Spanish
        ["ch"] = "tʃ",
        ["d"] = "d",
        ["f"] = "f",
        ["g"] = "g", // before a, o, u
        ["ge"] = "x", // Spanish j sound
        ["gi"] = "x",
        ["h"] = "", // silent
        ["j"] = "x",
        ["k"] = "k",
        ["l"] = "l",
        ["ll"] = "ʎ", // or "j" in some dialects
        ["m"] = "m",
        ["n"] = "n",
        ["ñ"] = "ɲ",
        ["p"] = "p",
        ["qu"] = "k",
        ["r"] = "ɾ", // single r
        ["rr"] = "r", // rolled r
        ["s"] = "s",
        ["t"] = "t",
        ["v"] = "b", // same as b in Spanish
        ["w"] = "w",
        ["x"] = "ks",
        ["y"] = "j",
        ["z"] = "θ" // Spain Spanish
    };
    
    public List<string> Grapheme2Phoneme(string word)
    {
        // Apply rules with context
        // Handle diphthongs, stress, etc.
    }
}
```

### 3. Text Normalization

```csharp
public class SpanishTextNormalizer
{
    public string Normalize(string text)
    {
        // Handle numbers
        text = NormalizeNumbers(text);
        
        // Handle abbreviations
        text = ExpandAbbreviations(text);
        
        // Handle special punctuation (¿ ¡)
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

### 4. Register New Language

```csharp
// In BackendFactory or service initialization
public void RegisterLanguageBackends()
{
    // Existing
    RegisterBackend(new RuleBasedPhonemizer()); // English
    RegisterBackend(new OpenJTalkPhonemizer()); // Japanese
    
    // New languages
    RegisterBackend(new SpanishPhonemizer());
    RegisterBackend(new FrenchPhonemizer());
    RegisterBackend(new GermanPhonemizer());
    RegisterBackend(new ChinesePhonemizer());
}
```

### 5. Language-Specific Tests

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

## Data Format Standardization

### Unified Dictionary Format

```
# Language: es-ES
# Format: WORD[TAB]PHONEME1 PHONEME2 ...
# Encoding: UTF-8

HOLA    o l a
MUNDO   m u n d o
ESPAÑA  e s p a ɲ a
```

### IPA Mapping

All languages should map to IPA for consistency:

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

## Resource Requirements

### Dictionary Sizes

- Spanish: ~80,000 words (~2 MB)
- French: ~140,000 words (~4 MB)
- German: ~120,000 words (~3.5 MB)
- Chinese: ~10,000 characters (~1 MB)

### Memory Usage

Each language adds approximately:
- Dictionary: 5-10 MB RAM
- G2P model: 1-5 MB RAM
- Text normalizer: <1 MB RAM

## Quick Start Templates

### Minimal Spanish Support

1. Download: [Spanish starter pack](link-to-resource)
2. Extract to: `Assets/StreamingAssets/uPiper/Languages/Spanish/`
3. Add backend: 
   ```csharp
   backendFactory.RegisterBackend(new SpanishPhonemizer());
   ```
4. Test:
   ```csharp
   var result = await service.PhonemizeAsync("Hola mundo", "es-ES");
   ```

## Community Contributions

To contribute a new language:

1. Create dictionary in standard format
2. Implement G2P rules
3. Add text normalizer
4. Write tests (minimum 50 test cases)
5. Submit PR with:
   - Backend implementation
   - Test suite
   - Sample dictionary (1000 words minimum)
   - Documentation

## License Considerations

Always verify dictionary licenses:
- ✅ Public domain, MIT, BSD, Apache 2.0
- ⚠️ CC-BY-SA (attribution required)
- ❌ GPL, LGPL (avoid for commercial use)