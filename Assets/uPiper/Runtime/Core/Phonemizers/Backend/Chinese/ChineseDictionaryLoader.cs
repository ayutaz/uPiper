using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

namespace uPiper.Core.Phonemizers.Backend.Chinese
{
    /// <summary>
    /// Loads Chinese dictionary data from StreamingAssets
    /// </summary>
    public class ChineseDictionaryLoader
    {
        private const string DICTIONARY_BASE_PATH = "uPiper/Chinese/";
        private const string CHARACTER_DICT_FILE = "character_pinyin.json";
        private const string PHRASE_DICT_FILE = "phrase_pinyin.json";
        private const string IPA_MAP_FILE = "pinyin_ipa_map.json";
        private const string WORD_FREQ_FILE = "word_frequency.json";
        
        // Phase 2 expanded dictionary files
        private const string CHARACTER_DICT_EXPANDED_FILE = "character_pinyin_expanded.json";
        private const string PHRASE_DICT_EXPANDED_FILE = "phrase_pinyin_expanded.json";
        private const string IPA_MAP_EXPANDED_FILE = "pinyin_ipa_map_expanded.json";
        private const string WORD_FREQ_EXPANDED_FILE = "word_frequency_expanded.json";
        
        // Flag to use expanded dictionaries (Phase 2)
        private bool useExpandedDictionaries = true;

        /// <summary>
        /// Load all dictionary data asynchronously
        /// </summary>
        public async Task<ChinesePinyinDictionary> LoadAsync(CancellationToken cancellationToken = default)
        {
            var dictionary = new ChinesePinyinDictionary();
            var dictionaryData = new ChineseDictionaryData();

            try
            {
                // Load all dictionary files in parallel
                var loadTasks = new[]
                {
                    LoadCharacterDictionary(cancellationToken),
                    LoadPhraseDictionary(cancellationToken),
                    LoadIPAMapping(cancellationToken),
                    LoadWordFrequency(cancellationToken)
                };

                await Task.WhenAll(loadTasks);

                // Assign loaded data
                dictionaryData.characterEntries = loadTasks[0].Result as ChineseDictionaryData.CharacterPinyinEntry[];
                dictionaryData.phraseEntries = loadTasks[1].Result as ChineseDictionaryData.PhrasePinyinEntry[];
                dictionaryData.pinyinIPAEntries = loadTasks[2].Result as ChineseDictionaryData.PinyinIPAEntry[];
                dictionaryData.wordFrequencies = loadTasks[3].Result as ChineseDictionaryData.WordFrequencyEntry[];

                // Load into runtime dictionary
                dictionary.LoadFromData(dictionaryData);

                Debug.Log($"Chinese dictionary loaded: {dictionary.CharacterCount} characters, " +
                         $"{dictionary.PhraseCount} phrases, {dictionary.IPACount} IPA mappings, " +
                         $"{dictionary.WordCount} word frequencies");

                return dictionary;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load Chinese dictionary: {ex.Message}");

                // Return dictionary with fallback data
                LoadFallbackData(dictionary);
                return dictionary;
            }
        }

        private async Task<object> LoadCharacterDictionary(CancellationToken cancellationToken)
        {
            var filename = useExpandedDictionaries ? CHARACTER_DICT_EXPANDED_FILE : CHARACTER_DICT_FILE;
            var json = await LoadJsonFile(filename, cancellationToken);
            if (string.IsNullOrEmpty(json) && useExpandedDictionaries)
            {
                // Fallback to basic dictionary if expanded not found
                Debug.LogWarning($"Expanded character dictionary not found, falling back to basic dictionary");
                json = await LoadJsonFile(CHARACTER_DICT_FILE, cancellationToken);
            }
            if (string.IsNullOrEmpty(json))
            {
                // Return basic character mappings as fallback
                return GetFallbackCharacterEntries();
            }
            return JsonConvert.DeserializeObject<ChineseDictionaryData.CharacterPinyinEntry[]>(json);
        }

        private async Task<object> LoadPhraseDictionary(CancellationToken cancellationToken)
        {
            var filename = useExpandedDictionaries ? PHRASE_DICT_EXPANDED_FILE : PHRASE_DICT_FILE;
            var json = await LoadJsonFile(filename, cancellationToken);
            if (string.IsNullOrEmpty(json) && useExpandedDictionaries)
            {
                // Fallback to basic dictionary if expanded not found
                Debug.LogWarning($"Expanded phrase dictionary not found, falling back to basic dictionary");
                json = await LoadJsonFile(PHRASE_DICT_FILE, cancellationToken);
            }
            if (string.IsNullOrEmpty(json))
            {
                // Return basic phrase mappings as fallback
                return GetFallbackPhraseEntries();
            }
            return JsonConvert.DeserializeObject<ChineseDictionaryData.PhrasePinyinEntry[]>(json);
        }

        private async Task<object> LoadIPAMapping(CancellationToken cancellationToken)
        {
            var filename = useExpandedDictionaries ? IPA_MAP_EXPANDED_FILE : IPA_MAP_FILE;
            var json = await LoadJsonFile(filename, cancellationToken);
            if (string.IsNullOrEmpty(json) && useExpandedDictionaries)
            {
                // Fallback to basic dictionary if expanded not found
                Debug.LogWarning($"Expanded IPA mapping not found, falling back to basic mapping");
                json = await LoadJsonFile(IPA_MAP_FILE, cancellationToken);
            }
            if (string.IsNullOrEmpty(json))
            {
                // Return basic IPA mappings as fallback
                return GetFallbackIPAEntries();
            }
            return JsonConvert.DeserializeObject<ChineseDictionaryData.PinyinIPAEntry[]>(json);
        }

        private async Task<object> LoadWordFrequency(CancellationToken cancellationToken)
        {
            var filename = useExpandedDictionaries ? WORD_FREQ_EXPANDED_FILE : WORD_FREQ_FILE;
            var json = await LoadJsonFile(filename, cancellationToken);
            if (string.IsNullOrEmpty(json) && useExpandedDictionaries)
            {
                // Fallback to basic dictionary if expanded not found
                Debug.LogWarning($"Expanded word frequency not found, falling back to basic frequency");
                json = await LoadJsonFile(WORD_FREQ_FILE, cancellationToken);
            }
            if (string.IsNullOrEmpty(json))
            {
                return Array.Empty<ChineseDictionaryData.WordFrequencyEntry>();
            }
            return JsonConvert.DeserializeObject<ChineseDictionaryData.WordFrequencyEntry[]>(json);
        }

        private async Task<string> LoadJsonFile(string filename, CancellationToken cancellationToken)
        {
            var path = Path.Combine(Application.streamingAssetsPath, DICTIONARY_BASE_PATH, filename);

#if UNITY_ANDROID && !UNITY_EDITOR
            // Android requires UnityWebRequest for StreamingAssets
            using (var request = UnityWebRequest.Get(path))
            {
                var operation = request.SendWebRequest();
                
                while (!operation.isDone && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Yield();
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    request.Abort();
                    throw new OperationCanceledException();
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    return request.downloadHandler.text;
                }
                else
                {
                    Debug.LogWarning($"Failed to load {filename}: {request.error}");
                    return null;
                }
            }
#else
            // Desktop platforms can use direct file access
            if (File.Exists(path))
            {
                return await Task.Run(() => File.ReadAllText(path), cancellationToken);
            }
            else
            {
                Debug.LogWarning($"File not found: {path}");
                return null;
            }
#endif
        }

        private void LoadFallbackData(ChinesePinyinDictionary dictionary)
        {
            var fallbackData = new ChineseDictionaryData
            {
                characterEntries = GetFallbackCharacterEntries(),
                phraseEntries = GetFallbackPhraseEntries(),
                pinyinIPAEntries = GetFallbackIPAEntries(),
                wordFrequencies = new ChineseDictionaryData.WordFrequencyEntry[0]
            };

            dictionary.LoadFromData(fallbackData);
        }

        private ChineseDictionaryData.CharacterPinyinEntry[] GetFallbackCharacterEntries()
        {
            return new[]
            {
                new ChineseDictionaryData.CharacterPinyinEntry { character = "你", pinyin = new[] { "ni3" } },
                new ChineseDictionaryData.CharacterPinyinEntry { character = "好", pinyin = new[] { "hao3", "hao4" } },
                new ChineseDictionaryData.CharacterPinyinEntry { character = "中", pinyin = new[] { "zhong1", "zhong4" } },
                new ChineseDictionaryData.CharacterPinyinEntry { character = "国", pinyin = new[] { "guo2" } },
                new ChineseDictionaryData.CharacterPinyinEntry { character = "人", pinyin = new[] { "ren2" } },
                new ChineseDictionaryData.CharacterPinyinEntry { character = "我", pinyin = new[] { "wo3" } },
                new ChineseDictionaryData.CharacterPinyinEntry { character = "是", pinyin = new[] { "shi4" } },
                new ChineseDictionaryData.CharacterPinyinEntry { character = "的", pinyin = new[] { "de5", "di4" } },
                new ChineseDictionaryData.CharacterPinyinEntry { character = "一", pinyin = new[] { "yi1", "yi2", "yi4" } },
                new ChineseDictionaryData.CharacterPinyinEntry { character = "不", pinyin = new[] { "bu4", "bu2" } },
                new ChineseDictionaryData.CharacterPinyinEntry { character = "了", pinyin = new[] { "le5", "liao3" } },
                new ChineseDictionaryData.CharacterPinyinEntry { character = "在", pinyin = new[] { "zai4" } },
                new ChineseDictionaryData.CharacterPinyinEntry { character = "有", pinyin = new[] { "you3" } },
                new ChineseDictionaryData.CharacterPinyinEntry { character = "这", pinyin = new[] { "zhe4", "zhei4" } },
                new ChineseDictionaryData.CharacterPinyinEntry { character = "个", pinyin = new[] { "ge4", "ge5" } }
            };
        }

        private ChineseDictionaryData.PhrasePinyinEntry[] GetFallbackPhraseEntries()
        {
            return new[]
            {
                new ChineseDictionaryData.PhrasePinyinEntry { phrase = "你好", pinyin = "ni3 hao3" },
                new ChineseDictionaryData.PhrasePinyinEntry { phrase = "中国", pinyin = "zhong1 guo2" },
                new ChineseDictionaryData.PhrasePinyinEntry { phrase = "中国人", pinyin = "zhong1 guo2 ren2" },
                new ChineseDictionaryData.PhrasePinyinEntry { phrase = "不是", pinyin = "bu2 shi4" },
                new ChineseDictionaryData.PhrasePinyinEntry { phrase = "好的", pinyin = "hao3 de5" }
            };
        }

        private ChineseDictionaryData.PinyinIPAEntry[] GetFallbackIPAEntries()
        {
            return new[]
            {
                // Basic consonants
                new ChineseDictionaryData.PinyinIPAEntry { pinyin = "b", ipa = "p" },
                new ChineseDictionaryData.PinyinIPAEntry { pinyin = "p", ipa = "pʰ" },
                new ChineseDictionaryData.PinyinIPAEntry { pinyin = "m", ipa = "m" },
                new ChineseDictionaryData.PinyinIPAEntry { pinyin = "f", ipa = "f" },
                new ChineseDictionaryData.PinyinIPAEntry { pinyin = "d", ipa = "t" },
                new ChineseDictionaryData.PinyinIPAEntry { pinyin = "t", ipa = "tʰ" },
                new ChineseDictionaryData.PinyinIPAEntry { pinyin = "n", ipa = "n" },
                new ChineseDictionaryData.PinyinIPAEntry { pinyin = "l", ipa = "l" },
                new ChineseDictionaryData.PinyinIPAEntry { pinyin = "g", ipa = "k" },
                new ChineseDictionaryData.PinyinIPAEntry { pinyin = "k", ipa = "kʰ" },
                new ChineseDictionaryData.PinyinIPAEntry { pinyin = "h", ipa = "x" },
                
                // Retroflex consonants
                new ChineseDictionaryData.PinyinIPAEntry { pinyin = "zh", ipa = "ʈʂ" },
                new ChineseDictionaryData.PinyinIPAEntry { pinyin = "ch", ipa = "ʈʂʰ" },
                new ChineseDictionaryData.PinyinIPAEntry { pinyin = "sh", ipa = "ʂ" },
                new ChineseDictionaryData.PinyinIPAEntry { pinyin = "r", ipa = "ʐ" },
                
                // Basic vowels
                new ChineseDictionaryData.PinyinIPAEntry { pinyin = "a", ipa = "a" },
                new ChineseDictionaryData.PinyinIPAEntry { pinyin = "o", ipa = "o" },
                new ChineseDictionaryData.PinyinIPAEntry { pinyin = "e", ipa = "ɤ" },
                new ChineseDictionaryData.PinyinIPAEntry { pinyin = "i", ipa = "i" },
                new ChineseDictionaryData.PinyinIPAEntry { pinyin = "u", ipa = "u" },
                new ChineseDictionaryData.PinyinIPAEntry { pinyin = "ü", ipa = "y" },
                
                // Common syllables
                new ChineseDictionaryData.PinyinIPAEntry { pinyin = "ni", ipa = "ni" },
                new ChineseDictionaryData.PinyinIPAEntry { pinyin = "hao", ipa = "xau" },
                new ChineseDictionaryData.PinyinIPAEntry { pinyin = "zhong", ipa = "ʈʂʊŋ" },
                new ChineseDictionaryData.PinyinIPAEntry { pinyin = "guo", ipa = "kuo" },
                new ChineseDictionaryData.PinyinIPAEntry { pinyin = "ren", ipa = "ʐən" },
                new ChineseDictionaryData.PinyinIPAEntry { pinyin = "wo", ipa = "uo" },
                new ChineseDictionaryData.PinyinIPAEntry { pinyin = "shi", ipa = "ʂʅ" },
                new ChineseDictionaryData.PinyinIPAEntry { pinyin = "de", ipa = "tɤ" },
                new ChineseDictionaryData.PinyinIPAEntry { pinyin = "yi", ipa = "i" },
                new ChineseDictionaryData.PinyinIPAEntry { pinyin = "bu", ipa = "pu" },
                new ChineseDictionaryData.PinyinIPAEntry { pinyin = "le", ipa = "lɤ" },
                new ChineseDictionaryData.PinyinIPAEntry { pinyin = "zai", ipa = "tsai" },
                new ChineseDictionaryData.PinyinIPAEntry { pinyin = "you", ipa = "jou" },
                new ChineseDictionaryData.PinyinIPAEntry { pinyin = "zhe", ipa = "ʈʂɤ" },
                new ChineseDictionaryData.PinyinIPAEntry { pinyin = "ge", ipa = "kɤ" }
            };
        }
    }
}