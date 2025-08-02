using System.Collections.Generic;

namespace uPiper.Core.Phonemizers
{
    /// <summary>
    /// Converts Arpabet (CMU) phonemes to IPA/eSpeak phonemes
    /// </summary>
    public static class ArpabetToIPAConverter
    {
        private static readonly Dictionary<string, string> ArpabetToIPA = new()
        {
            // Vowels
            ["AA"] = "ɑ",    // father
            ["AE"] = "æ",    // cat
            ["AH"] = "ʌ",    // cup
            ["AO"] = "ɔ",    // caught
            ["AW"] = "aʊ",   // cow
            ["AY"] = "aɪ",   // bite
            ["EH"] = "ɛ",    // bet
            ["ER"] = "ɚ",    // bird
            ["EY"] = "eɪ",   // bait
            ["IH"] = "ɪ",    // bit
            ["IY"] = "i",    // beat
            ["OW"] = "oʊ",   // boat
            ["OY"] = "ɔɪ",   // boy
            ["UH"] = "ʊ",    // book
            ["UW"] = "u",    // boot

            // Consonants
            ["B"] = "b",
            ["CH"] = "tʃ",   // church
            ["D"] = "d",
            ["DH"] = "ð",    // this
            ["F"] = "f",
            ["G"] = "ɡ",
            ["HH"] = "h",
            ["JH"] = "dʒ",   // judge
            ["K"] = "k",
            ["L"] = "l",
            ["M"] = "m",
            ["N"] = "n",
            ["NG"] = "ŋ",    // sing
            ["P"] = "p",
            ["R"] = "ɹ",
            ["S"] = "s",
            ["SH"] = "ʃ",    // ship
            ["T"] = "t",
            ["TH"] = "θ",    // think
            ["V"] = "v",
            ["W"] = "w",
            ["Y"] = "j",
            ["Z"] = "z",
            ["ZH"] = "ʒ",    // vision
        };

        /// <summary>
        /// Convert Arpabet phoneme to IPA/eSpeak format
        /// </summary>
        public static string Convert(string arpabetPhoneme)
        {
            if (string.IsNullOrEmpty(arpabetPhoneme))
                return "";

            // Remove stress markers (0, 1, 2)
            var basePhoneme = arpabetPhoneme.TrimEnd('0', '1', '2');

            // Try to find mapping
            if (ArpabetToIPA.TryGetValue(basePhoneme.ToUpper(), out var ipa))
            {
                return ipa;
            }

            // Return original if no mapping found
            return arpabetPhoneme.ToLower();
        }

        /// <summary>
        /// Convert array of Arpabet phonemes to IPA/eSpeak format
        /// </summary>
        public static string[] ConvertAll(string[] arpabetPhonemes)
        {
            var result = new string[arpabetPhonemes.Length];
            for (int i = 0; i < arpabetPhonemes.Length; i++)
            {
                result[i] = Convert(arpabetPhonemes[i]);
            }
            return result;
        }
    }
}