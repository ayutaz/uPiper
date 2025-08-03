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
                _cachedDictionary = GetFallbackDictionary();
                Debug.Log($"[DictionaryCache] Created fallback dictionary with {_cachedDictionary.CharacterCount} characters, {_cachedDictionary.PhraseCount} phrases");
                return _cachedDictionary;
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
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "的", pinyin = new[] { "de5", "di2", "di4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "了", pinyin = new[] { "le5", "liao3" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "在", pinyin = new[] { "zai4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "有", pinyin = new[] { "you3" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "一", pinyin = new[] { "yi1", "yi2", "yi4" } },
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
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "统", pinyin = new[] { "tong3" } },
                    // Daily life terms for test coverage  
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "吃", pinyin = new[] { "chi1" } },
                    // Add missing characters for tests
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "银", pinyin = new[] { "yin2" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "行", pinyin = new[] { "xing2", "hang2" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "重", pinyin = new[] { "zhong4", "chong2" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "长", pinyin = new[] { "chang2", "zhang3" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "动", pinyin = new[] { "dong4" } },
                    // Add more test characters
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "机", pinyin = new[] { "ji1" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "器", pinyin = new[] { "qi4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "人", pinyin = new[] { "ren2" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "智", pinyin = new[] { "zhi4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "能", pinyin = new[] { "neng2" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "技", pinyin = new[] { "ji4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "自", pinyin = new[] { "zi4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "然", pinyin = new[] { "ran2" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "处", pinyin = new[] { "chu3" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "理", pinyin = new[] { "li3" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "视", pinyin = new[] { "shi4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "饭", pinyin = new[] { "fan4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "睡", pinyin = new[] { "shui4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "觉", pinyin = new[] { "jue2", "jiao4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "工", pinyin = new[] { "gong1" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "作", pinyin = new[] { "zuo4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "活", pinyin = new[] { "huo2" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "家", pinyin = new[] { "jia1" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "庭", pinyin = new[] { "ting2" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "朋", pinyin = new[] { "peng2" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "友", pinyin = new[] { "you3" } },
                    // Geography terms for test coverage
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "美", pinyin = new[] { "mei3" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "日", pinyin = new[] { "ri4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "本", pinyin = new[] { "ben3" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "英", pinyin = new[] { "ying1" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "法", pinyin = new[] { "fa3" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "德", pinyin = new[] { "de2" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "俄", pinyin = new[] { "e2" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "罗", pinyin = new[] { "luo2" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "斯", pinyin = new[] { "si1" } },
                    // Culture terms for test coverage
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "文", pinyin = new[] { "wen2" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "化", pinyin = new[] { "hua4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "历", pinyin = new[] { "li4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "史", pinyin = new[] { "shi3" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "艺", pinyin = new[] { "yi4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "术", pinyin = new[] { "shu4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "音", pinyin = new[] { "yin1" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "乐", pinyin = new[] { "yue4", "le4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "影", pinyin = new[] { "ying3" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "诗", pinyin = new[] { "shi1" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "歌", pinyin = new[] { "ge1" } },
                    // Characters for tone sandhi tests
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "对", pinyin = new[] { "dui4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "错", pinyin = new[] { "cuo4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "去", pinyin = new[] { "qu4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "到", pinyin = new[] { "dao4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "变", pinyin = new[] { "bian4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "要", pinyin = new[] { "yao4", "yao1" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "起", pinyin = new[] { "qi3" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "定", pinyin = new[] { "ding4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "般", pinyin = new[] { "ban1" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "确", pinyin = new[] { "que4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "目", pinyin = new[] { "mu4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "书", pinyin = new[] { "shu1" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "小", pinyin = new[] { "xiao3" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "心", pinyin = new[] { "xin1" } },
                    // Add missing characters for tests
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "谢", pinyin = new[] { "xie4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "再", pinyin = new[] { "zai4" } },
                    new ChineseDictionaryData.CharacterPinyinEntry { character = "见", pinyin = new[] { "jian4" } }
                },
                phraseEntries = new[]
                {
                    new ChineseDictionaryData.PhrasePinyinEntry { phrase = "你好", pinyin = "ni3 hao3" },
                    new ChineseDictionaryData.PhrasePinyinEntry { phrase = "世界", pinyin = "shi4 jie4" },
                    new ChineseDictionaryData.PhrasePinyinEntry { phrase = "中国", pinyin = "zhong1 guo2" },
                    // Add phrases for tests
                    new ChineseDictionaryData.PhrasePinyinEntry { phrase = "银行", pinyin = "yin2 hang2" },
                    new ChineseDictionaryData.PhrasePinyinEntry { phrase = "行动", pinyin = "xing2 dong4" },
                    new ChineseDictionaryData.PhrasePinyinEntry { phrase = "长大", pinyin = "zhang3 da4" },
                    new ChineseDictionaryData.PhrasePinyinEntry { phrase = "长度", pinyin = "chang2 du4" },
                    new ChineseDictionaryData.PhrasePinyinEntry { phrase = "重要", pinyin = "zhong4 yao4" },
                    new ChineseDictionaryData.PhrasePinyinEntry { phrase = "重新", pinyin = "chong2 xin1" },
                    // Phrases for tone sandhi tests
                    new ChineseDictionaryData.PhrasePinyinEntry { phrase = "不是", pinyin = "bu2 shi4" },
                    new ChineseDictionaryData.PhrasePinyinEntry { phrase = "不对", pinyin = "bu2 dui4" },
                    new ChineseDictionaryData.PhrasePinyinEntry { phrase = "不去", pinyin = "bu2 qu4" },
                    new ChineseDictionaryData.PhrasePinyinEntry { phrase = "不到", pinyin = "bu2 dao4" },
                    new ChineseDictionaryData.PhrasePinyinEntry { phrase = "不错", pinyin = "bu2 cuo4" },
                    new ChineseDictionaryData.PhrasePinyinEntry { phrase = "不变", pinyin = "bu2 bian4" },
                    new ChineseDictionaryData.PhrasePinyinEntry { phrase = "不要", pinyin = "bu2 yao4" },
                    new ChineseDictionaryData.PhrasePinyinEntry { phrase = "一起", pinyin = "yi4 qi3" },
                    new ChineseDictionaryData.PhrasePinyinEntry { phrase = "一定", pinyin = "yi2 ding4" },
                    new ChineseDictionaryData.PhrasePinyinEntry { phrase = "一般", pinyin = "yi4 ban1" },
                    new ChineseDictionaryData.PhrasePinyinEntry { phrase = "一个", pinyin = "yi2 ge4" },
                    new ChineseDictionaryData.PhrasePinyinEntry { phrase = "我的", pinyin = "wo3 de5" },
                    new ChineseDictionaryData.PhrasePinyinEntry { phrase = "的确", pinyin = "di2 que4" },
                    new ChineseDictionaryData.PhrasePinyinEntry { phrase = "目的", pinyin = "mu4 di4" },
                    new ChineseDictionaryData.PhrasePinyinEntry { phrase = "一不小心", pinyin = "yi4 bu4 xiao3 xin1" },
                    // Add phrase for "谢谢" test
                    new ChineseDictionaryData.PhrasePinyinEntry { phrase = "谢谢", pinyin = "xie4 xie5" }
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
                    // IPA for new test characters
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "yin", ipa = "in" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "hang", ipa = "xaŋ" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "xing", ipa = "ɕiŋ" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "zhong", ipa = "ʈʂuŋ" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "chong", ipa = "ʈʂʰuŋ" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "chang", ipa = "ʈʂʰaŋ" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "dong", ipa = "tuŋ" },
                    // IPA for additional test characters
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "ji", ipa = "tɕi" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "ren", ipa = "ʐən" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "zhi", ipa = "ʈʂʅ" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "neng", ipa = "nəŋ" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "ran", ipa = "ʐan" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "chu", ipa = "ʈʂʰu" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "li", ipa = "li" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "cheng", ipa = "ʈʂʰəŋ" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "xu", ipa = "ɕy" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "dai", ipa = "tai" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "ma", ipa = "ma" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "tong", ipa = "tʰʊŋ" },
                    // IPA for daily life terms
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "fan", ipa = "fan" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "shui", ipa = "ʂuei" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "jue", ipa = "tɕye" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "jiao", ipa = "tɕiau" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "gong", ipa = "kʊŋ" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "zuo", ipa = "tsuo" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "huo", ipa = "xuo" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "jia", ipa = "tɕia" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "ting", ipa = "tʰiŋ" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "peng", ipa = "pʰəŋ" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "you", ipa = "jou" },
                    // IPA for geography terms
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "mei", ipa = "mei" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "ben", ipa = "pən" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "ying", ipa = "iŋ" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "e", ipa = "ɤ" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "luo", ipa = "luo" },
                    // IPA for culture terms
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "wen", ipa = "wən" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "hua", ipa = "xua" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "li", ipa = "li" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "yin", ipa = "in" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "yue", ipa = "ye" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "le", ipa = "lɤ" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "ying", ipa = "iŋ" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "ge", ipa = "kɤ" },
                    // IPA for tone sandhi test characters
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "dui", ipa = "tuei" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "cuo", ipa = "tsʰuo" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "qu", ipa = "tɕʰy" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "dao", ipa = "tau" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "yao", ipa = "jau" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "qi", ipa = "tɕʰi" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "ding", ipa = "tiŋ" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "di", ipa = "ti" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "ban", ipa = "pan" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "que", ipa = "tɕʰye" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "mu", ipa = "mu" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "xiao", ipa = "ɕiau" },
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "xin", ipa = "ɕin" },
                    // Add IPA for "xie" (谢)
                    new ChineseDictionaryData.PinyinIPAEntry { pinyin = "xie", ipa = "ɕie" }
                },
                wordFrequencies = new ChineseDictionaryData.WordFrequencyEntry[0]
            };
            
            dictionary.LoadFromData(data);
            
            // Log dictionary contents for debugging
            Debug.Log($"[TestDictionary] Loaded {dictionary.CharacterCount} characters, {dictionary.PhraseCount} phrases, {dictionary.IPACount} IPA mappings");
            
            // Check if specific test characters are loaded
            if (dictionary.TryGetCharacterPinyin('谢', out var xiePinyin))
            {
                Debug.Log($"[TestDictionary] Found '谢' with pinyin: {string.Join(", ", xiePinyin)}");
            }
            else
            {
                Debug.LogWarning("[TestDictionary] Character '谢' not found in dictionary!");
            }
            
            if (dictionary.TryGetPhrasePinyin("谢谢", out var xieXiePinyin))
            {
                Debug.Log($"[TestDictionary] Found phrase '谢谢' with pinyin: {xieXiePinyin}");
            }
            else
            {
                Debug.LogWarning("[TestDictionary] Phrase '谢谢' not found in dictionary!");
            }
            
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