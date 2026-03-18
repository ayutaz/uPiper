using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace uPiper.Core.Phonemizers.Backend.Chinese
{
    /// <summary>
    /// Chinese (Mandarin) phonemizer backend for uPiper TTS.
    /// Converts Chinese text to IPA phonemes via pinyin intermediate representation.
    /// Ported from piper-plus python implementation (chinese.py).
    /// </summary>
    public class ChinesePhonemizerBackend : PhonemizerBackendBase
    {
        private readonly object _syncLock = new();
        private bool _enableDebugLogging;

        /// <inheritdoc/>
        public override string Name => "ChinesePhonemizer";

        /// <inheritdoc/>
        public override string Version => "1.0.0";

        /// <inheritdoc/>
        public override string License => "MIT";

        /// <inheritdoc/>
        public override string[] SupportedLanguages => new[] { "zh", "zh-CN" };

        /// <summary>
        /// Initializes internal state. Chinese phonemizer uses built-in lookup
        /// tables and does not require external data files.
        /// </summary>
        protected override Task<bool> InitializeInternalAsync(
            PhonemizerBackendOptions options,
            CancellationToken cancellationToken)
        {
            _enableDebugLogging = options?.EnableDebugLogging ?? false;
            return Task.FromResult(true);
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators
        /// <inheritdoc/>
        public override async Task<PhonemeResult> PhonemizeAsync(
            string text,
            string language,
            PhonemeOptions options = null,
            CancellationToken cancellationToken = default)
        {
            EnsureInitialized();

            if (!ValidateInput(text, language, out var error))
            {
                return CreateErrorResult(error, language);
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            lock (_syncLock)
            {
                return PhonemizeInternal(text, language, options ?? PhonemeOptions.Default);
            }
#else
            return await Task.Run(() =>
            {
                lock (_syncLock)
                {
                    return PhonemizeInternal(text, language, options ?? PhonemeOptions.Default);
                }
            }, cancellationToken);
#endif
        }
#pragma warning restore CS1998

        /// <summary>
        /// Converts pre-parsed pinyin syllables directly to IPA phonemes with prosody.
        /// Bypasses the internal char-to-pinyin lookup (useful when external pinyin is available).
        /// </summary>
        /// <param name="pinyinSyllables">
        /// List of pinyin with tone numbers, e.g. ["guang3", "zhou1"].
        /// </param>
        /// <param name="chineseText">
        /// Original Chinese text for word boundary prosody detection (optional).
        /// </param>
        /// <returns>A PhonemeResult with PUA-mapped phonemes and prosody.</returns>
        public PhonemeResult PhonemizeFromPinyinSyllables(
            List<string> pinyinSyllables,
            string chineseText = "")
        {
            var sw = Stopwatch.StartNew();
            var phonemes = new List<string>();
            var prosodyA1 = new List<int>();
            var prosodyA2 = new List<int>();
            var prosodyA3 = new List<int>();

            var wordInfo = !string.IsNullOrEmpty(chineseText)
                ? BuildWordInfo(chineseText)
                : new Dictionary<int, (int position, int length)>();

            // Map Chinese char indices to syllable indices
            var chineseCharIndices = new List<int>();
            if (!string.IsNullOrEmpty(chineseText))
            {
                for (int i = 0; i < chineseText.Length; i++)
                {
                    if (IsChinese(chineseText[i]))
                    {
                        chineseCharIndices.Add(i);
                    }
                }
            }

            for (int sylIdx = 0; sylIdx < pinyinSyllables.Count; sylIdx++)
            {
                var syllable = pinyinSyllables[sylIdx];
                if (string.IsNullOrEmpty(syllable))
                    continue;

                // Extract tone number
                int tone = 5;
                string syllableBase;
                if (syllable.Length > 0 && char.IsDigit(syllable[syllable.Length - 1]))
                {
                    tone = syllable[syllable.Length - 1] - '0';
                    syllableBase = syllable.Substring(0, syllable.Length - 1);
                }
                else
                {
                    syllableBase = syllable;
                }

                var normalized = NormalizePinyin(syllableBase);

                // Handle erhua
                string erhuaToken = null;
                if (normalized.Length > 1
                    && normalized.EndsWith("r")
                    && normalized != "er")
                {
                    erhuaToken = "\u025a"; // r-colored schwa
                    normalized = normalized.Substring(0, normalized.Length - 1);
                }

                var ipaTokens = PinyinToIpa(normalized, tone);
                if (erhuaToken != null)
                {
                    InsertErhua(ipaTokens, erhuaToken);
                }

                // Prosody from word_info using original char index
                int charIdx = sylIdx < chineseCharIndices.Count
                    ? chineseCharIndices[sylIdx]
                    : sylIdx;
                int sylPos = 1;
                int wordLen = 1;
                if (wordInfo.TryGetValue(charIdx, out var info))
                {
                    sylPos = info.position;
                    wordLen = info.length;
                }

                foreach (var token in ipaTokens)
                {
                    phonemes.Add(MapToPua(token));
                    prosodyA1.Add(tone);
                    prosodyA2.Add(sylPos);
                    prosodyA3.Add(wordLen);
                }
            }

            sw.Stop();
            return new PhonemeResult
            {
                Phonemes = phonemes.ToArray(),
                ProsodyA1 = prosodyA1.ToArray(),
                ProsodyA2 = prosodyA2.ToArray(),
                ProsodyA3 = prosodyA3.ToArray(),
                Language = "zh",
                Success = true,
                Backend = Name,
                ProcessingTimeMs = (float)sw.Elapsed.TotalMilliseconds,
                ProcessingTime = sw.Elapsed,
            };
        }

        // =====================================================================
        // Internal implementation
        // =====================================================================

        private PhonemeResult PhonemizeInternal(
            string text, string language, PhonemeOptions options)
        {
            var sw = Stopwatch.StartNew();
            var (phonemes, a1, a2, a3) = PhonemizeWithProsody(text);
            sw.Stop();

            return new PhonemeResult
            {
                OriginalText = text,
                Phonemes = phonemes.ToArray(),
                ProsodyA1 = a1.ToArray(),
                ProsodyA2 = a2.ToArray(),
                ProsodyA3 = a3.ToArray(),
                Language = language,
                Success = true,
                Backend = Name,
                ProcessingTimeMs = (float)sw.Elapsed.TotalMilliseconds,
                ProcessingTime = sw.Elapsed,
                Metadata = new Dictionary<string, object>
                {
                    ["backend"] = Name,
                },
            };
        }

        /// <summary>
        /// Core phonemization with prosody, mirroring python phonemize_chinese_with_prosody.
        /// </summary>
        private (List<string> phonemes, List<int> a1, List<int> a2, List<int> a3)
            PhonemizeWithProsody(string text)
        {
            var phonemes = new List<string>();
            var prosodyA1 = new List<int>();
            var prosodyA2 = new List<int>();
            var prosodyA3 = new List<int>();

            // Build word groups for prosody
            var wordInfo = BuildWordInfo(text);

            // ---- First pass: extract char-to-pinyin and tones ----
            var charTones = new Dictionary<int, (string normalized, int tone)>();
            var chineseIndices = new List<int>();

            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (!IsChinese(ch))
                    continue;

                chineseIndices.Add(i);

                string pinyin;
                if (PinyinData.CharToPinyin.TryGetValue(ch, out pinyin))
                {
                    int tone = 5;
                    string syllableBase;
                    if (pinyin.Length > 0 && char.IsDigit(pinyin[pinyin.Length - 1]))
                    {
                        tone = pinyin[pinyin.Length - 1] - '0';
                        syllableBase = pinyin.Substring(0, pinyin.Length - 1);
                    }
                    else
                    {
                        syllableBase = pinyin;
                    }

                    var normalized = NormalizePinyin(syllableBase);
                    charTones[i] = (normalized, tone);
                }
                else
                {
                    if (_enableDebugLogging)
                    {
                        Debug.LogWarning(
                            $"[ChinesePhonemizer] Character not in lookup: " +
                            $"U+{(int)ch:X4} '{ch}'");
                    }
                    // Default to neutral tone placeholder
                    charTones[i] = ("", 5);
                }
            }

            // ---- Apply tone sandhi to consecutive Chinese character sequences ----
            if (chineseIndices.Count > 0)
            {
                var groups = GroupConsecutiveIndices(chineseIndices);
                foreach (var group in groups)
                {
                    var pyTones = new List<(string syllable, int tone)>();
                    foreach (int idx in group)
                    {
                        pyTones.Add(charTones[idx]);
                    }

                    var sandhiResult = ApplyToneSandhi(pyTones);
                    for (int k = 0; k < group.Count; k++)
                    {
                        charTones[group[k]] = sandhiResult[k];
                    }
                }
            }

            // ---- Second pass: generate phonemes ----
            for (int charIdx = 0; charIdx < text.Length; charIdx++)
            {
                char ch = text[charIdx];

                // Handle Chinese punctuation
                if (PinyinData.PunctuationMap.TryGetValue(ch, out char mappedPunct))
                {
                    phonemes.Add(mappedPunct.ToString());
                    prosodyA1.Add(0);
                    prosodyA2.Add(0);
                    prosodyA3.Add(0);
                    continue;
                }

                // Handle Western punctuation
                if (PinyinData.PunctuationSet.Contains(ch))
                {
                    phonemes.Add(ch.ToString());
                    prosodyA1.Add(0);
                    prosodyA2.Add(0);
                    prosodyA3.Add(0);
                    continue;
                }

                // Handle whitespace
                if (char.IsWhiteSpace(ch))
                {
                    phonemes.Add(" ");
                    prosodyA1.Add(0);
                    prosodyA2.Add(0);
                    prosodyA3.Add(0);
                    continue;
                }

                // Handle digits (pass through)
                if (char.IsDigit(ch))
                {
                    phonemes.Add(ch.ToString());
                    prosodyA1.Add(0);
                    prosodyA2.Add(0);
                    prosodyA3.Add(1);
                    continue;
                }

                // Handle non-Chinese characters (pass through if alphabetic)
                if (!IsChinese(ch))
                {
                    if (char.IsLetter(ch))
                    {
                        phonemes.Add(ch.ToString());
                        prosodyA1.Add(0);
                        prosodyA2.Add(0);
                        prosodyA3.Add(1);
                    }
                    continue;
                }

                // ---- Chinese character: use tone-sandhi-corrected data ----
                if (!charTones.TryGetValue(charIdx, out var toneData))
                    continue;

                var normalized = toneData.normalized;
                int toneVal = toneData.tone;

                // Skip characters not in lookup (empty normalized)
                if (string.IsNullOrEmpty(normalized))
                    continue;

                // Erhua handling
                string erhuaToken = null;
                if (normalized.Length > 1
                    && normalized.EndsWith("r")
                    && normalized != "er")
                {
                    erhuaToken = "\u025a"; // r-colored schwa
                    normalized = normalized.Substring(0, normalized.Length - 1);
                }

                // Convert to IPA tokens
                var ipaTokens = PinyinToIpa(normalized, toneVal);
                if (erhuaToken != null)
                {
                    InsertErhua(ipaTokens, erhuaToken);
                }

                // Prosody: a1=tone, a2=position in word, a3=word length
                int sylPos = 1;
                int wordLen = 1;
                if (wordInfo.TryGetValue(charIdx, out var wInfo))
                {
                    sylPos = wInfo.position;
                    wordLen = wInfo.length;
                }

                foreach (var token in ipaTokens)
                {
                    phonemes.Add(MapToPua(token));
                    prosodyA1.Add(toneVal);
                    prosodyA2.Add(sylPos);
                    prosodyA3.Add(wordLen);
                }
            }

            return (phonemes, prosodyA1, prosodyA2, prosodyA3);
        }

        // =====================================================================
        // Pinyin normalization (matches python _normalize_pinyin)
        // =====================================================================

        /// <summary>
        /// Normalize pinyin y/w conventions and v-to-u-umlaut to canonical form.
        /// </summary>
        internal static string NormalizePinyin(string py)
        {
            // v is an alternate representation of u-umlaut
            py = py.Replace("v", "\u00fc");

            // y- initial: represents medial i or u-umlaut
            if (py.StartsWith("yu"))
            {
                return py.Length > 2
                    ? "\u00fc" + py.Substring(2)
                    : "\u00fc";
            }

            if (py.StartsWith("y"))
            {
                var remainder = py.Substring(1);
                if (remainder.StartsWith("i"))
                {
                    return remainder; // yi->i, yin->in, ying->ing
                }
                return "i" + remainder; // ya->ia, ye->ie, yan->ian, etc.
            }

            // w- initial: represents medial u
            if (py.StartsWith("w"))
            {
                var remainder = py.Substring(1);
                if (remainder.StartsWith("u"))
                {
                    return remainder; // wu->u
                }
                return "u" + remainder; // wa->ua, wo->uo, wai->uai, etc.
            }

            return py;
        }

        // =====================================================================
        // Split pinyin into (initial, final)
        // =====================================================================

        /// <summary>
        /// Split normalized pinyin syllable into (initial, final).
        /// </summary>
        internal static (string initial, string final_) SplitPinyin(string pinyin)
        {
            foreach (var init in PinyinData.InitialsOrder)
            {
                if (pinyin.StartsWith(init))
                {
                    var final_ = pinyin.Substring(init.Length);

                    // Syllabic consonant: bare "i" after retroflex or alveolar initials
                    if (final_ == "i")
                    {
                        if (PinyinData.RetroflexInitials.Contains(init))
                        {
                            return (init, "-i_retroflex");
                        }
                        if (PinyinData.AlveolarInitials.Contains(init))
                        {
                            return (init, "-i_alveolar");
                        }
                    }

                    // After j/q/x, u represents u-umlaut
                    if ((init == "j" || init == "q" || init == "x")
                        && final_.StartsWith("u"))
                    {
                        final_ = "\u00fc" + final_.Substring(1);
                    }

                    return (init, final_);
                }
            }

            // No consonant initial
            return ("", pinyin);
        }

        // =====================================================================
        // Pinyin -> IPA conversion
        // =====================================================================

        /// <summary>
        /// Convert a single pinyin syllable (without tone number) to IPA tokens.
        /// Returns a list of IPA tokens including tone marker.
        /// </summary>
        internal static List<string> PinyinToIpa(string pinyinSyllable, int tone)
        {
            var (initial, final_) = SplitPinyin(pinyinSyllable);
            var tokens = new List<string>();

            // Initial consonant
            if (!string.IsNullOrEmpty(initial))
            {
                if (PinyinData.InitialToIpa.TryGetValue(initial, out var ipa))
                {
                    tokens.Add(ipa);
                }
            }

            // Final vowel(s) -- as a single compound token
            if (!string.IsNullOrEmpty(final_))
            {
                if (PinyinData.FinalToIpa.TryGetValue(final_, out var ipa))
                {
                    tokens.Add(ipa);
                }
                else
                {
                    // Fallback: decompose unknown finals character by character
                    foreach (char c in final_)
                    {
                        string cs = c.ToString();
                        if (PinyinData.FinalToIpa.TryGetValue(cs, out var charIpa))
                        {
                            tokens.Add(charIpa);
                        }
                        else if (char.IsLetter(c))
                        {
                            tokens.Add(cs);
                        }
                    }
                }
            }

            // Tone marker
            if (tone >= 1 && tone <= 5)
            {
                tokens.Add("tone" + tone);
            }

            return tokens;
        }

        // =====================================================================
        // Tone sandhi rules
        // =====================================================================

        /// <summary>
        /// Apply basic Mandarin tone sandhi rules.
        /// Rules:
        ///   1. T3 + T3 -> T2 + T3 (third tone sandhi)
        ///   2. yi (T1) before T4 -> T2
        ///   3. yi (T1) before T1/T2/T3 -> T4
        ///   4. bu (T4) before T4 -> T2
        /// </summary>
        internal static List<(string syllable, int tone)> ApplyToneSandhi(
            List<(string syllable, int tone)> pyTones)
        {
            var result = new List<(string syllable, int tone)>(pyTones);

            for (int i = 0; i < result.Count - 1; i++)
            {
                var (syllableI, toneI) = result[i];
                var (_, toneNext) = result[i + 1];

                // Rule 1: third tone sandhi
                if (toneI == 3 && toneNext == 3)
                {
                    result[i] = (syllableI, 2);
                    continue;
                }

                // Rule 2 & 3: yi tone sandhi
                // _normalize_pinyin("yi") -> "i", so match normalized form
                if (syllableI == "i" && toneI == 1)
                {
                    if (toneNext == 4)
                    {
                        result[i] = (syllableI, 2); // T1 -> T2 before T4
                    }
                    else if (toneNext == 1 || toneNext == 2 || toneNext == 3)
                    {
                        result[i] = (syllableI, 4); // T1 -> T4 before T1/T2/T3
                    }
                    continue;
                }

                // Rule 4: bu tone sandhi
                if (syllableI == "bu" && toneI == 4 && toneNext == 4)
                {
                    result[i] = (syllableI, 2); // T4 -> T2 before T4
                }
            }

            return result;
        }

        // =====================================================================
        // Word info for prosody (contiguous Chinese char groups)
        // =====================================================================

        /// <summary>
        /// Build word position info for prosody from contiguous Chinese char groups.
        /// Returns a dict mapping character index to (syllable_position, word_length)
        /// where syllable_position is 1-based.
        /// </summary>
        internal static Dictionary<int, (int position, int length)> BuildWordInfo(
            string text)
        {
            var info = new Dictionary<int, (int position, int length)>();
            var groupIndices = new List<int>();
            bool inGroup = false;

            for (int i = 0; i < text.Length; i++)
            {
                if (IsChinese(text[i]))
                {
                    if (!inGroup)
                    {
                        groupIndices.Clear();
                        inGroup = true;
                    }
                    groupIndices.Add(i);
                }
                else if (inGroup)
                {
                    // End of group -- record it
                    int wordLen = groupIndices.Count;
                    for (int pos = 0; pos < groupIndices.Count; pos++)
                    {
                        info[groupIndices[pos]] = (pos + 1, wordLen);
                    }
                    inGroup = false;
                }
            }

            // Handle trailing group
            if (inGroup)
            {
                int wordLen = groupIndices.Count;
                for (int pos = 0; pos < groupIndices.Count; pos++)
                {
                    info[groupIndices[pos]] = (pos + 1, wordLen);
                }
            }

            return info;
        }

        // =====================================================================
        // PUA mapping
        // =====================================================================

        /// <summary>
        /// Map an IPA token to its PUA single-codepoint representation if available.
        /// Single-character tokens are returned as-is.
        /// </summary>
        internal static string MapToPua(string token)
        {
            if (PinyinData.IpaToPua.TryGetValue(token, out char puaChar))
            {
                return puaChar.ToString();
            }
            // Single-codepoint tokens don't need mapping
            return token;
        }

        // =====================================================================
        // Erhua insertion helper
        // =====================================================================

        /// <summary>
        /// Insert erhua token after vowel tokens but before the tone marker.
        /// </summary>
        private static void InsertErhua(List<string> ipaTokens, string erhuaToken)
        {
            if (ipaTokens.Count > 0
                && ipaTokens[ipaTokens.Count - 1].StartsWith("tone"))
            {
                var toneMarker = ipaTokens[ipaTokens.Count - 1];
                ipaTokens[ipaTokens.Count - 1] = erhuaToken;
                ipaTokens.Add(toneMarker);
            }
            else
            {
                ipaTokens.Add(erhuaToken);
            }
        }

        // =====================================================================
        // Utility helpers
        // =====================================================================

        /// <summary>
        /// Check if a character is a CJK Unified Ideograph (common or extension A).
        /// </summary>
        internal static bool IsChinese(char ch)
        {
            return (ch >= '\u4e00' && ch <= '\u9fff')
                || (ch >= '\u3400' && ch <= '\u4dbf');
        }

        /// <summary>
        /// Group a sorted list of indices into sublists of consecutive integers.
        /// </summary>
        private static List<List<int>> GroupConsecutiveIndices(List<int> indices)
        {
            var groups = new List<List<int>>();
            if (indices.Count == 0) return groups;

            var current = new List<int> { indices[0] };
            for (int k = 1; k < indices.Count; k++)
            {
                if (indices[k] == indices[k - 1] + 1)
                {
                    current.Add(indices[k]);
                }
                else
                {
                    groups.Add(current);
                    current = new List<int> { indices[k] };
                }
            }
            groups.Add(current);
            return groups;
        }

        // =====================================================================
        // IPhonemizerBackend required members
        // =====================================================================

        /// <inheritdoc/>
        public override long GetMemoryUsage()
        {
            // Estimate: CharToPinyin ~500 entries * ~40 bytes + IPA tables
            return 512 * 1024; // ~512 KB estimate
        }

        /// <inheritdoc/>
        public override BackendCapabilities GetCapabilities()
        {
            return new BackendCapabilities
            {
                SupportsIPA = true,
                SupportsStress = false,
                SupportsSyllables = false,
                SupportsTones = true,
                SupportsDuration = false,
                SupportsBatchProcessing = false,
                IsThreadSafe = true,
                RequiresNetwork = false,
            };
        }

        /// <inheritdoc/>
        protected override void DisposeInternal()
        {
            // No unmanaged resources to dispose
        }
    }
}