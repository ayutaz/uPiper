using System.Threading.Tasks;
using UnityEngine;
using uPiper.Core.Phonemizers.Backend.Chinese;

namespace uPiper.Tests.Runtime.ChinesePhonemizer
{
    /// <summary>
    /// Static cache for Chinese dictionary to avoid reloading in tests
    /// </summary>
    public static class ChineseDictionaryTestCache
    {
        private static ChinesePinyinDictionary _cachedDictionary;
        private static readonly object _lock = new object();
        private static bool _isLoading;
        private static bool _loadFailed;
        
        /// <summary>
        /// Get cached dictionary (loads only once)
        /// </summary>
        public static ChinesePinyinDictionary GetDictionary()
        {
            lock (_lock)
            {
                if (_loadFailed)
                {
                    Debug.LogWarning("[DictionaryCache] Previous load failed, returning fallback dictionary");
                    return GetFallbackDictionary();
                }
                
                if (_cachedDictionary != null)
                {
                    return _cachedDictionary;
                }
                
                if (_isLoading)
                {
                    Debug.LogWarning("[DictionaryCache] Dictionary is still loading, returning fallback");
                    return GetFallbackDictionary();
                }
                
                // Return fallback dictionary for now
                Debug.Log("[DictionaryCache] Returning fallback dictionary to avoid loading");
                return GetFallbackDictionary();
            }
        }
        
        /// <summary>
        /// Create a minimal fallback dictionary for testing
        /// </summary>
        private static ChinesePinyinDictionary GetFallbackDictionary()
        {
            var dictionary = new ChinesePinyinDictionary();
            var data = new ChineseDictionaryData
            {
                characterEntries = new[]
                {
                    // Basic test characters
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "你", pinyin = new[] { "ni3" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "好", pinyin = new[] { "hao3" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "世", pinyin = new[] { "shi4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "界", pinyin = new[] { "jie4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "我", pinyin = new[] { "wo3" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "是", pinyin = new[] { "shi4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "中", pinyin = new[] { "zhong1" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "国", pinyin = new[] { "guo2" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "人", pinyin = new[] { "ren2" } },
                    // Common characters needed by tests
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "的", pinyin = new[] { "de5", "di4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "了", pinyin = new[] { "le5", "liao3" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "在", pinyin = new[] { "zai4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "有", pinyin = new[] { "you3" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "一", pinyin = new[] { "yi1" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "个", pinyin = new[] { "ge4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "上", pinyin = new[] { "shang4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "们", pinyin = new[] { "men5" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "来", pinyin = new[] { "lai2" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "到", pinyin = new[] { "dao4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "时", pinyin = new[] { "shi2" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "大", pinyin = new[] { "da4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "地", pinyin = new[] { "di4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "为", pinyin = new[] { "wei2", "wei4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "子", pinyin = new[] { "zi3" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "说", pinyin = new[] { "shuo1" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "生", pinyin = new[] { "sheng1" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "年", pinyin = new[] { "nian2" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "着", pinyin = new[] { "zhe5", "zhao2" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "就", pinyin = new[] { "jiu4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "那", pinyin = new[] { "na4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "这", pinyin = new[] { "zhe4" } },
                    // Additional characters needed by tests
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "不", pinyin = new[] { "bu4", "bu2" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "他", pinyin = new[] { "ta1" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "智", pinyin = new[] { "zhi4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "能", pinyin = new[] { "neng2" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "变", pinyin = new[] { "bian4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "机", pinyin = new[] { "ji1" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "器", pinyin = new[] { "qi4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "学", pinyin = new[] { "xue2" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "习", pinyin = new[] { "xi2" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "深", pinyin = new[] { "shen1" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "度", pinyin = new[] { "du4" } },
                    // Chinese numbers for test coverage
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "零", pinyin = new[] { "ling2" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "二", pinyin = new[] { "er4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "三", pinyin = new[] { "san1" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "四", pinyin = new[] { "si4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "五", pinyin = new[] { "wu3" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "六", pinyin = new[] { "liu4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "七", pinyin = new[] { "qi1" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "八", pinyin = new[] { "ba1" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "九", pinyin = new[] { "jiu3" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "十", pinyin = new[] { "shi2" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "百", pinyin = new[] { "bai3" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "千", pinyin = new[] { "qian1" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "万", pinyin = new[] { "wan4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "亿", pinyin = new[] { "yi4" } },
                    // Technical terms for test coverage
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "电", pinyin = new[] { "dian4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "脑", pinyin = new[] { "nao3" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "网", pinyin = new[] { "wang3" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "络", pinyin = new[] { "luo4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "软", pinyin = new[] { "ruan3" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "件", pinyin = new[] { "jian4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "硬", pinyin = new[] { "ying4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "数", pinyin = new[] { "shu4", "shu3" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "据", pinyin = new[] { "ju4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "算", pinyin = new[] { "suan4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "法", pinyin = new[] { "fa3" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "程", pinyin = new[] { "cheng2" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "序", pinyin = new[] { "xu4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "代", pinyin = new[] { "dai4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "码", pinyin = new[] { "ma3" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "系", pinyin = new[] { "xi4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "统", pinyin = new[] { "tong3" } }
                },
                phraseEntries = new[]
                {
                    new ChineseDictionaryData.PhrasePinyinEntry { phrase = "你好", pinyin = "ni3 hao3" },
                    new ChineseDictionaryData.PhrasePinyinEntry { phrase = "世界", pinyin = "shi4 jie4" },
                    new ChineseDictionaryData.PhrasePinyinEntry { phrase = "中国", pinyin = "zhong1 guo2" }
                },
                pinyinIPAEntries = new[]
                {
                    // Basic IPA mappings
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "ni", ipa = "ni" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "hao", ipa = "xau" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "shi", ipa = "ʂʅ" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "jie", ipa = "tɕie" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "wo", ipa = "uo" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "zhong", ipa = "ʈʂʊŋ" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "guo", ipa = "kuo" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "ren", ipa = "ʐən" },
                    // Additional IPA mappings for tests
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "ma", ipa = "ma" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "mo", ipa = "mo" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "me", ipa = "mɤ" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "mi", ipa = "mi" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "mu", ipa = "mu" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "zhi", ipa = "ʈʂʅ" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "chi", ipa = "ʈʂʰʅ" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "ri", ipa = "ʐʅ" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "zi", ipa = "tsɨ" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "ci", ipa = "tsʰɨ" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "si", ipa = "sɨ" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "ju", ipa = "tɕy" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "qu", ipa = "tɕʰy" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "xu", ipa = "ɕy" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "zhang", ipa = "ʈʂaŋ" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "chuang", ipa = "ʈʂʰuaŋ" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "shuang", ipa = "ʂuaŋ" },
                    // Additional mappings for common characters
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "de", ipa = "tɤ" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "le", ipa = "lɤ" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "zai", ipa = "tsai" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "you", ipa = "jou" },
                    // Additional IPA mappings for test characters
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "bu", ipa = "pu" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "ta", ipa = "tʰa" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "neng", ipa = "nəŋ" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "bian", ipa = "piɛn" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "qi", ipa = "tɕʰi" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "xue", ipa = "ɕye" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "xi", ipa = "ɕi" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "shen", ipa = "ʂən" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "du", ipa = "tu" },
                    // IPA for Chinese numbers
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "ling", ipa = "liŋ" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "er", ipa = "ɤɻ" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "san", ipa = "san" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "wu", ipa = "u" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "liu", ipa = "ljou" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "ba", ipa = "pa" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "jiu", ipa = "tɕjou" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "bai", ipa = "pai" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "qian", ipa = "tɕʰiɛn" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "wan", ipa = "wan" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "yi", ipa = "i" },
                    // IPA for technical terms
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "dian", ipa = "tiɛn" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "nao", ipa = "nau" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "wang", ipa = "waŋ" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "luo", ipa = "luo" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "ruan", ipa = "ʐuan" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "jian", ipa = "tɕiɛn" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "ying", ipa = "iŋ" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "shu", ipa = "ʂu" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "ju", ipa = "tɕy" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "suan", ipa = "suan" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "fa", ipa = "fa" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "cheng", ipa = "ʈʂʰəŋ" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "xu", ipa = "ɕy" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "dai", ipa = "tai" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "ma", ipa = "ma" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "tong", ipa = "tʰʊŋ" }
                },
                wordFrequencies = new ChineseDictionaryData.WordFrequencyEntry[0]
            };
            
            dictionary.LoadFromData(data);
            return dictionary;
        }
        
        /// <summary>
        /// Clear the cache (for testing purposes)
        /// </summary>
        public static void ClearCache()
        {
            lock (_lock)
            {
                _cachedDictionary = null;
                _isLoading = false;
                _loadFailed = false;
            }
        }
    }
}