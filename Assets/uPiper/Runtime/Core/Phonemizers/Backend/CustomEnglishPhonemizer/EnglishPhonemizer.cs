using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace uPiper.Core.Phonemizers.Backend.CustomEnglishPhonemizer
{
    public class EnglishPhonemizer
    {
        private readonly SimpleG2P _fallbackG2P;

        private static readonly Dictionary<string, string[]> arpabetToIPAMap = new Dictionary<string, string[]>
        {
            { "AA", new [] { "ɑ" } }, { "AE", new [] { "æ" } }, { "AH", new [] { "ə" } },
            { "AO", new [] { "ɔ" } }, { "AW", new [] { "a", "ʊ" } }, { "AY", new [] { "a", "ɪ" } },
            { "EH", new [] { "ɛ" } }, { "ER", new [] { "ɜ" } }, { "EY", new [] { "e", "ɪ" } },
            { "IH", new [] { "ɪ" } }, { "IY", new [] { "i" } }, { "OW", new [] { "o", "ʊ" } },
            { "OY", new [] { "ɔ", "ɪ" } }, { "UH", new [] { "ʊ" } }, { "UW", new [] { "u" } },
            { "B", new [] { "b" } }, { "CH", new [] { "t", "ʃ" } }, { "D", new [] { "d" } },
            { "DH", new [] { "ð" } }, { "F", new [] { "f" } }, { "G", new [] { "ɡ" } },
            { "HH", new [] { "h" } }, { "JH", new [] { "d", "ʒ" } }, { "K", new [] { "k" } },
            { "L", new [] { "l" } }, { "M", new [] { "m" } }, { "N", new [] { "n" } },
            { "NG", new [] { "ŋ" } }, { "P", new [] { "p" } }, { "R", new [] { "ɹ" } },
            { "S", new [] { "s" } }, { "SH", new [] { "ʃ" } }, { "T", new [] { "t" } },
            { "TH", new [] { "θ" } }, { "V", new [] { "v" } }, { "W", new [] { "w" } },
            { "Y", new [] { "j" } }, { "Z", new [] { "z" } }, { "ZH", new [] { "ʒ" } }
        };

        private static readonly Dictionary<string, string[]> ipaFallbackTokenExpansions = new Dictionary<string, string[]>
        {
            { "ɜ", new [] { "ɚ", "ə", "ɹ" } },
            { "ɡ", new [] { "g" } }
        };

        private static readonly HashSet<string> vowelsThatTakeLengthWhenStressed =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "IY", "UW", "ER", "AO", "AA" };

        // unified reduced forms
        private static readonly Dictionary<string, string[]> reducedWordMap = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "and", new [] { "ə", "n" } },
            { "of",  new [] { "ə", "v" } },
            { "to",  new [] { "t", "ə" } },
            { "for", new [] { "f", "ə" } },
            { "the", new [] { "ð", "ə" } },
            { "a",   new [] { "ə" } },
            { "was", new [] { "w", "ə", "z" } },
            { "her", new [] { "h", "ə" } },
            { "you", new [] { "j", "ə", "ʊ" } },
        };

        private static readonly HashSet<char> pausePunctuation = new HashSet<char> { '.', ',', '!', '?', ';', ':' };

        public EnglishPhonemizer() => _fallbackG2P = new SimpleG2P();

        public async Task<List<string>> TextToIPAPhonemesAsync(CMUDict cmuDict, PiperVoiceConfig voiceConfig, string text)
        {
            List<string> ipaPhonemes = await Task.Run(() => TextToIPASymbols(cmuDict, voiceConfig, text));
            return ipaPhonemes;
        }

        public int[] TextToPhonemeIds(CMUDict cmuDict, PiperVoiceConfig voiceConfig, string text)
        {
            List<string> ipaSymbols = TextToIPASymbols(cmuDict, voiceConfig, text);
            int[] phonemeIds = IPASymbolsToPhonemeIDs(voiceConfig, ipaSymbols);

            return phonemeIds;
        }

        public List<string> TextToIPASymbols(CMUDict cmuDict, PiperVoiceConfig voiceConfig, string text)
        {
            // Replace any digits with words for those numbers, e.g. 7 -> "seven"
            text = DigitsToWords.ReplaceNumbersWithWords(text);

            var ipaSymbols = new List<string>();
            var phonemeIdMap = voiceConfig.PhonemeIdMap;

            AppendIfPresent(phonemeIdMap, "^", ipaSymbols);
            string[] sentences = Regex.Split(text, @"(?<=[.!?])\s+");

            foreach (string sentence in sentences)
            {
                string[] tokens = Regex.Split(sentence, @"(\s+|[,;:.!?()\""]+)");

                for (int tIndex = 0; tIndex < tokens.Length; tIndex++)
                {
                    string token = tokens[tIndex];
                    if (string.IsNullOrWhiteSpace(token)) continue;

                    bool isPunct = Regex.IsMatch(token, @"^[\.,!?;:]$");
                    if (isPunct)
                    {
                        AppendIfPresent(phonemeIdMap, token, ipaSymbols);
                        if (ipaSymbols.Count == 0 || ipaSymbols.Last() != " ")
                            ipaSymbols.Add(" ");
                        continue;
                    }

                    // look ahead
                    string nextToken = tokens.Skip(tIndex + 1).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));
                    bool nextIsPunct = nextToken != null && nextToken.Length == 1 && pausePunctuation.Contains(nextToken[0]);

                    // unified early reduced-form check (works regardless of CMUDict presence)
                    if (!nextIsPunct && reducedWordMap.TryGetValue(token, out var reducedTokens))
                    {
                        ipaSymbols.AddRange(reducedTokens);
                        if (!nextIsPunct) ipaSymbols.Add(" ");
                        continue; // skip all other pronunciation paths
                    }

                    // try CMUDict
                    if (cmuDict.TryGetPhonemes(token.ToUpperInvariant(), out string[] arpabetPhonemes))
                    {
                        foreach (string arp in arpabetPhonemes)
                        {
                            if (string.IsNullOrEmpty(arp)) continue;
                            var stressDigit = Regex.Match(arp, @"\d").Value;
                            var arpabetKey = Regex.Replace(arp, @"\d", "");

                            if (stressDigit == "1") ipaSymbols.Add("ˈ");
                            else if (stressDigit == "2") ipaSymbols.Add("ˌ");

                            if (string.Equals(arpabetKey, "AH", StringComparison.OrdinalIgnoreCase))
                            {
                                ipaSymbols.Add(stressDigit == "1" ? "ʌ" : "ə");
                                continue;
                            }
                            if (string.Equals(arpabetKey, "ER", StringComparison.OrdinalIgnoreCase))
                            {
                                ipaSymbols.Add("ɜ");
                                if (stressDigit == "1") ipaSymbols.Add("ː");
                                continue;
                            }

                            if (arpabetToIPAMap.TryGetValue(arpabetKey, out var mappedTokens))
                            {
                                ipaSymbols.AddRange(mappedTokens);
                                if (vowelsThatTakeLengthWhenStressed.Contains(arpabetKey) && stressDigit == "1")
                                {
                                    if (ipaSymbols.Count == 0 || (ipaSymbols.Last() != "ʊ" && ipaSymbols.Last() != "ɪ"))
                                        ipaSymbols.Add("ː");
                                }
                            }
                            else ipaSymbols.Add(arpabetKey);
                        }
                    }
                    else
                    {
                        // fallback G2P
                        ipaSymbols.AddRange(_fallbackG2P.WordToPhonemes(token));
                    }

                    if (!nextIsPunct) ipaSymbols.Add(" ");
                }
            }

            // cleanup spaces before punctuation or end marker
            for (int i = ipaSymbols.Count - 2; i >= 0; i--)
            {
                if (ipaSymbols[i] == " " && (IsPunctuationSymbol(ipaSymbols[i + 1]) || ipaSymbols[i + 1] == "$"))
                    ipaSymbols.RemoveAt(i);
            }

            AppendIfPresent(phonemeIdMap, "$", ipaSymbols);
            return ipaSymbols;
        }

        private static bool IsPunctuationSymbol(string s)
            => s == "," || s == "." || s == "!" || s == "?" || s == ";" || s == ":";

        private static void AppendIfPresent(Dictionary<string, int> map, string symbol, List<string> outList)
        {
            if (map != null && !string.IsNullOrEmpty(symbol) && map.ContainsKey(symbol))
                outList.Add(symbol);
        }

        public int[] IPASymbolsToPhonemeIDs(PiperVoiceConfig voiceConfig, List<string> ipaSymbols)
        {
            var map = voiceConfig.PhonemeIdMap;
            var ids = new List<int>();

            foreach (string symbol in ipaSymbols)
            {
                if (string.IsNullOrEmpty(symbol)) continue;

                if (map.TryGetValue(symbol, out int id))
                {
                    ids.Add(id);
                    ids.Add(0);
                    continue;
                }

                bool foundFallbackToken = false;

                if (ipaFallbackTokenExpansions.TryGetValue(symbol, out var options))
                {
                    foreach (var opt in options)
                    {
                        if (map.TryGetValue(opt, out int optId))
                        {
                            ids.Add(optId);
                            ids.Add(0);
                            foundFallbackToken = true;
                            break;
                        }
                    }
                }

                if (foundFallbackToken)
                {
                    continue;
                }

                foreach (char ch in symbol)
                {
                    if (map.TryGetValue(ch.ToString(), out int chId))
                    {
                        ids.Add(chId);
                        ids.Add(0);
                    }
                }
            }

            return ids.ToArray();
        }

        public List<string> PhonemeIDsToIPASymbols(PiperVoiceConfig voiceConfig, int[] ids)
        {
            var map = voiceConfig.PhonemeIdMap;
            var result = new List<string>();

            foreach (int id in ids)
            {
                foreach (var kv in map)
                {
                    if (kv.Value == id)
                    {
                        result.Add(kv.Key);
                        break;
                    }
                }
            }
            return result;
        }
    }

    public class SimpleG2P
    {
        private readonly (string pattern, string[] ipa)[] _rules = new[]
        {
            // --- Common suffixes ---
            ("tion", new [] { "ʃ", "ə", "n" }),
            ("sion", new [] { "ʒ", "ə", "n" }),
            ("cian", new [] { "ʃ", "ə", "n" }),
            ("ture", new [] { "t", "ʃ", "ə" }),
            ("sure", new [] { "ʒ", "ə" }),

            // --- Diphthongs and vowels ---
            ("ph", new [] { "f" }),
            ("igh", new [] { "a", "ɪ" }),
            ("ai", new [] { "e", "ɪ" }),
            ("ay", new [] { "e", "ɪ" }),
            ("ei", new [] { "i", "ː" }),
            ("ie", new [] { "a", "ɪ" }),
            ("oa", new [] { "o", "ʊ" }),
            ("oo", new [] { "u", "ː" }),
            ("ou", new [] { "a", "ʊ" }),
            ("ow", new [] { "a", "ʊ" }),
            ("oy", new [] { "ɔ", "ɪ" }),
            ("ear", new [] { "ɪ", "ə" }),
            ("air", new [] { "e", "ə" }),
            ("ar", new [] { "ɑː" }),
            ("or", new [] { "ɔː" }),
            ("er", new [] { "ɜː" }),
            ("ur", new [] { "ɜː" }),
            ("ir", new [] { "ɜː" }),
            ("oo", new [] { "uː" }),

            // --- Simple vowels ---
            ("a", new [] { "æ" }),
            ("e", new [] { "ɛ" }),
            ("i", new [] { "ɪ" }),
            ("o", new [] { "ɒ" }),
            ("u", new [] { "ʌ" }),

            // --- Common consonants and clusters ---
            ("ch", new [] { "t", "ʃ" }),
            ("sh", new [] { "ʃ" }),
            ("th", new [] { "θ" }),
            ("ng", new [] { "ŋ" }),
            ("wh", new [] { "w" }),
            ("x", new [] { "k", "s" }),
            ("c", new [] { "k" }),
            ("g", new [] { "ɡ" }),
            ("j", new [] { "d", "ʒ" }),
            ("q", new [] { "k" }),
            ("y", new [] { "j" })
        };

        public List<string> WordToPhonemes(string word)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(word)) return list;

            word = word.ToLowerInvariant();

            // Simple heuristic: stress before last syllable
            int stressPos = Math.Max(0, CountVowels(word) - 2);
            int vowelCount = 0;

            int i = 0;
            while (i < word.Length)
            {
                bool matched = false;

                foreach (var (pattern, ipa) in _rules.OrderByDescending(r => r.pattern.Length))
                {
                    if (word.AsSpan(i).StartsWith(pattern))
                    {
                        if (ContainsVowel(ipa))
                        {
                            vowelCount++;
                            if (vowelCount == stressPos)
                                list.Add("ˈ");
                        }

                        list.AddRange(ipa);
                        i += pattern.Length;
                        matched = true;
                        break;
                    }
                }

                if (!matched)
                {
                    list.Add(word[i].ToString());
                    i++;
                }
            }

            return list;
        }

        private static bool ContainsVowel(IEnumerable<string> ipaParts)
            => ipaParts.Any(p => Regex.IsMatch(p, "[aeiouɑɒɛɜɪiʊuːə]"));

        private static int CountVowels(string s)
            => s.Count(ch => "aeiou".Contains(ch));
    }
}
