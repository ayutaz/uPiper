# CMU Pronouncing Dictionary Setup Guide

## Overview

The CMU Pronouncing Dictionary is a machine-readable pronunciation dictionary for North American English that contains over 134,000 words and their pronunciations.

## Download Instructions

### Method 1: Direct Download (Recommended)

1. Download the dictionary file:
   ```bash
   # Download latest version
   curl -O https://raw.githubusercontent.com/cmusphinx/cmudict/master/cmudict.dict
   
   # Or download specific version (0.7b)
   curl -O http://svn.code.sf.net/p/cmusphinx/code/trunk/cmudict/cmudict-0.7b
   ```

2. Place the file in:
   ```
   Assets/StreamingAssets/uPiper/Phonemizers/cmudict-0.7b.txt
   ```

3. Update `RuleBasedPhonemizer.cs` to use the full dictionary:
   ```csharp
   private string GetDefaultDictionaryPath()
   {
       // Change from cmudict-sample.txt to cmudict-0.7b.txt
       return Path.Combine(Application.streamingAssetsPath, 
           "uPiper", "Phonemizers", "cmudict-0.7b.txt");
   }
   ```

### Method 2: Git Clone

```bash
# Clone the repository
git clone https://github.com/cmusphinx/cmudict.git

# Copy the dictionary file
cp cmudict/cmudict.dict Assets/StreamingAssets/uPiper/Phonemizers/cmudict-0.7b.txt
```

## File Format

The CMU dictionary uses the following format:
```
WORD  W ER1 D
WORD'S  W ER1 D Z
WORD(1)  W ER1 D
```

- First column: Word (uppercase)
- Following columns: ARPABET phonemes
- Numbers indicate stress (0=no stress, 1=primary, 2=secondary)
- (1), (2) etc. indicate alternate pronunciations

## Size Considerations

- Full dictionary: ~4 MB uncompressed
- ~134,000 word entries
- Loads into ~10-15 MB RAM

### Mobile Optimization

For mobile platforms, consider:

1. **Compressed Format**:
   ```csharp
   // Create a binary format for faster loading
   public class CompressedDictionary
   {
       public void CompressDictionary(string inputPath, string outputPath)
       {
           // Convert text to binary format
           // Use string interning for phonemes
           // Create index for fast lookup
       }
   }
   ```

2. **Partial Loading**:
   ```csharp
   // Load only common words initially
   public class PartialDictionary
   {
       private const int CommonWordCount = 10000;
       
       public async Task LoadCommonWordsAsync()
       {
           // Load most frequent words first
       }
       
       public async Task LoadFullDictionaryAsync()
       {
           // Load remaining words in background
       }
   }
   ```

## Integration Testing

After adding the full dictionary:

```csharp
[Test]
public async Task FullDictionary_ShouldLoadSuccessfully()
{
    var phonemizer = new RuleBasedPhonemizer();
    await phonemizer.InitializeAsync(null);
    
    // Test with complex words
    var testWords = new[] 
    {
        "internationalization",
        "pharmaceutical",
        "acknowledgment",
        "entrepreneurship"
    };
    
    foreach (var word in testWords)
    {
        var result = await phonemizer.PhonemizeAsync(word, "en-US");
        Assert.IsNotEmpty(result.Phonemes);
    }
}
```

## Performance Impact

With full dictionary:
- Initial load time: ~100-300ms (desktop), ~500ms-1s (mobile)
- Memory usage: +10-15 MB
- Lookup performance: O(1) with hashtable

## License

The CMU Pronouncing Dictionary is in the **public domain** and can be used freely in commercial projects.