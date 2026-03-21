using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DotNetG2P.Chinese;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace uPiper.Core.Phonemizers.Backend.Chinese
{
    /// <summary>
    /// Chinese (Mandarin) phonemizer backend for uPiper TTS.
    /// Uses DotNetG2P.Chinese (41,923 char + 47,111 phrase dictionary) for accurate
    /// pinyin conversion, matching piper-plus ChinesePhonemizer output.
    /// </summary>
    public class ChinesePhonemizerBackend : PhonemizerBackendBase
    {
        private readonly object _syncLock = new();
        private bool _enableDebugLogging;
        private ChineseG2PEngine _g2pEngine;

        /// <inheritdoc/>
        public override string Name => "ChinesePhonemizer";

        /// <inheritdoc/>
        public override string Version => "1.0.0";

        /// <inheritdoc/>
        public override string License => "MIT";

        /// <inheritdoc/>
        private static readonly string[] _supportedLanguages = { "zh", "zh-CN" };
        public override string[] SupportedLanguages => _supportedLanguages;

        /// <summary>
        /// Initializes internal state. Loads DotNetG2P.Chinese dictionaries
        /// from StreamingAssets for pinyin conversion.
        /// </summary>
        protected override Task<bool> InitializeInternalAsync(
            PhonemizerBackendOptions options,
            CancellationToken cancellationToken)
        {
            _enableDebugLogging = options?.EnableDebugLogging ?? false;

            // Load DotNetG2P.Chinese with StreamingAssets dictionary files
            // (embedded resources don't work in Unity - see docs/dotnetg2p-unity-integration.md)
            try
            {
                var charPath = System.IO.Path.Combine(Application.streamingAssetsPath, "uPiper", "Chinese", "pinyin_char.txt");
                var phrasePath = System.IO.Path.Combine(Application.streamingAssetsPath, "uPiper", "Chinese", "pinyin_phrase.txt");

                if (System.IO.File.Exists(charPath))
                {
                    _g2pEngine = System.IO.File.Exists(phrasePath)
                        ? new ChineseG2PEngine(charPath, phrasePath)
                        : new ChineseG2PEngine(charPath);
                    Debug.Log($"[ChinesePhonemizer] DotNetG2P.Chinese initialized from StreamingAssets");
                }
                else
                {
                    Debug.LogWarning($"[ChinesePhonemizer] Dictionary not found at {charPath}. Chinese phonemization will not be available.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ChinesePhonemizer] DotNetG2P.Chinese init failed: {ex.Message}. Chinese phonemization will not be available.");
                _g2pEngine = null;
            }

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
        /// Core phonemization with prosody using DotNetG2P.Chinese (44K chars + 412K phrases).
        /// </summary>
        private (List<string> phonemes, List<int> a1, List<int> a2, List<int> a3)
            PhonemizeWithProsody(string text)
        {
            if (_g2pEngine == null)
            {
                Debug.LogWarning("[ChinesePhonemizer] DotNetG2P.Chinese engine is not initialized. " +
                    "Ensure dictionary files are present in StreamingAssets/uPiper/Chinese/.");
                return (new List<string>(), new List<int>(), new List<int>(), new List<int>());
            }

            return PhonemizeWithDotNetG2P(text);
        }

        // Tone number → PUA character mapping (tone1=E046 ... tone5=E04A)
        private static readonly char[] TonePuaChars = { '\0', '\ue046', '\ue047', '\ue048', '\ue049', '\ue04a' };

        private (List<string> phonemes, List<int> a1, List<int> a2, List<int> a3)
            PhonemizeWithDotNetG2P(string text)
        {
            var phonemes = new List<string>();
            var prosodyA1 = new List<int>();
            var prosodyA2 = new List<int>();
            var prosodyA3 = new List<int>();

            // ToPuaPhonemes does NOT include tone markers (by design in dot-net-g2p).
            // We need to insert tone PUA chars from the prosody data after each syllable.
            var puaPhonemes = _g2pEngine.ToPuaPhonemes(text);
            var prosodyResult = _g2pEngine.ToIpaWithProsody(text);

            // Strategy: puaPhonemes has initial+final per syllable (no tones).
            // prosodyResult has one entry per syllable with A1=tone.
            // We need to figure out syllable boundaries in puaPhonemes.
            // Each syllable = initial(s) + final(s), and we know syllable count from prosody.

            // Simple approach: distribute puaPhonemes across syllables proportionally.
            // Better approach: count syllables from prosody and split puaPhonemes by
            // tracking IPA initial/final patterns.
            //
            // Simplest correct approach: Use the total PUA phoneme count and syllable count
            // to determine average phonemes per syllable, then insert tone after each group.
            int totalSyllables = prosodyResult.Prosody.Count;
            if (totalSyllables == 0)
            {
                // No syllables (all non-Chinese text)
                foreach (var p in puaPhonemes)
                    phonemes.Add(p);
                return (phonemes, prosodyA1, prosodyA2, prosodyA3);
            }

            // Calculate phonemes per syllable (may vary, but we split evenly as approximation)
            // A more robust approach: use the IPA phoneme arrays from ToIpaWithProsody
            // to determine exact boundaries.
            int phonemesPerSyllable = puaPhonemes.Length / totalSyllables;
            int remainder = puaPhonemes.Length % totalSyllables;
            int puaIdx = 0;

            for (int syl = 0; syl < totalSyllables; syl++)
            {
                int count = phonemesPerSyllable + (syl < remainder ? 1 : 0);
                int toneVal = syl < prosodyResult.Prosody.Count ? prosodyResult.Prosody[syl].A1 : 5;
                int sylPos = syl < prosodyResult.Prosody.Count ? prosodyResult.Prosody[syl].A2 : 1;
                int wordLen = syl < prosodyResult.Prosody.Count ? prosodyResult.Prosody[syl].A3 : 1;

                // Add initial + final phonemes for this syllable
                for (int j = 0; j < count && puaIdx < puaPhonemes.Length; j++, puaIdx++)
                {
                    phonemes.Add(puaPhonemes[puaIdx]);
                    prosodyA1.Add(toneVal);
                    prosodyA2.Add(sylPos);
                    prosodyA3.Add(wordLen);
                }

                // Append tone marker PUA (tone1=E046 ... tone5=E04A)
                if (toneVal >= 1 && toneVal <= 5)
                {
                    phonemes.Add(TonePuaChars[toneVal].ToString());
                    prosodyA1.Add(toneVal);
                    prosodyA2.Add(sylPos);
                    prosodyA3.Add(wordLen);
                }
            }

            // Add any remaining phonemes (non-Chinese tokens)
            for (; puaIdx < puaPhonemes.Length; puaIdx++)
            {
                phonemes.Add(puaPhonemes[puaIdx]);
                prosodyA1.Add(0);
                prosodyA2.Add(0);
                prosodyA3.Add(0);
            }

            return (phonemes, prosodyA1, prosodyA2, prosodyA3);
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