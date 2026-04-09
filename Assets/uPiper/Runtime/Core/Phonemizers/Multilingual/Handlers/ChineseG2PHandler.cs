using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DotNetG2P.Chinese;
using UnityEngine;
using uPiper.Core.AudioGeneration;
using uPiper.Core.Logging;

namespace uPiper.Core.Phonemizers.Multilingual.Handlers
{
    /// <summary>
    /// Chinese G2P handler using DotNetG2P.Chinese (44K character dictionary).
    /// Handles syllable distribution, tone PUA marker insertion, and prosody extraction.
    /// </summary>
    public sealed class ChineseG2PHandler : ILanguageG2PHandler
    {
        // Tone number -> PUA character mapping (tone1=E046 ... tone5=E04A)
        private static readonly char[] TonePuaChars =
            { '\0', '\ue046', '\ue047', '\ue048', '\ue049', '\ue04a' };

        private ChineseG2PEngine _engine;
        private bool _isInitialized;
        private bool _disposed;

        /// <inheritdoc/>
        public string LanguageCode => "zh";

        /// <inheritdoc/>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Creates a handler with an externally provided engine.
        /// Ownership is managed by <see cref="HandlerEntry"/>.
        /// </summary>
        /// <remarks>
        /// The handler does NOT take ownership of the engine. When registered via
        /// <c>MultilingualPhonemizerOptions.Handlers</c>, the <c>HandlerEntry.IsOwned</c>
        /// flag is set to <c>false</c>, so <c>MultilingualPhonemizer.Dispose()</c>
        /// will NOT call this handler's Dispose. The caller retains responsibility
        /// for disposing the engine.
        /// </remarks>
        /// <param name="engine">Pre-built Chinese G2P engine instance.</param>
        public ChineseG2PHandler(ChineseG2PEngine engine)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _isInitialized = true;
        }

        /// <summary>
        /// Creates a handler that will create its own engine on initialization.
        /// </summary>
        public ChineseG2PHandler()
        {
            _isInitialized = false;
        }

        /// <inheritdoc/>
        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_isInitialized)
                return Task.CompletedTask;

            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                PiperLogger.LogWarning(
                    "[ChineseG2PHandler] WebGL: external dictionary not available. Chinese G2P requires async loading.");
                // Chinese G2P disabled on WebGL unless engine is pre-injected
                return Task.CompletedTask;
#elif UNITY_ANDROID && !UNITY_EDITOR
                // Android APK内のStreamingAssetsはFile APIで読めない。
                // ChineseG2PEngineはembedded辞書をサポートしないため、エンジン事前注入が必要。
                PiperLogger.LogWarning(
                    "[ChineseG2PHandler] Android: File API unavailable for StreamingAssets. " +
                    "Chinese G2P requires pre-injected engine via constructor.");
                return Task.CompletedTask;
#else
                var charPath = Path.Combine(
                    Application.streamingAssetsPath, "uPiper", "Chinese", "pinyin_char.txt");
                var phrasePath = Path.Combine(
                    Application.streamingAssetsPath, "uPiper", "Chinese", "pinyin_phrase.txt");

                if (File.Exists(charPath))
                {
                    _engine = File.Exists(phrasePath)
                        ? new ChineseG2PEngine(charPath, phrasePath)
                        : new ChineseG2PEngine(charPath);
                    _isInitialized = true;
                    PiperLogger.LogInfo(
                        "[ChineseG2PHandler] Initialized: DotNetG2P.Chinese");
                }
                else
                {
                    PiperLogger.LogWarning(
                        $"[ChineseG2PHandler] Chinese dictionary not found at {charPath}");
                }
#endif
            }
            catch (Exception ex)
            {
                PiperLogger.LogWarning(
                    $"[ChineseG2PHandler] Failed to initialize: {ex.Message}");
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public (string[] Phonemes, int[] ProsodyFlat) Process(string text)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ChineseG2PHandler));
            if (!_isInitialized || _engine == null)
                throw new InvalidOperationException("Call InitializeAsync() before processing.");

            // NOTE: Calls ToPuaPhonemes and ToIpaWithProsody separately. A unified API would be more
            // efficient but requires DotNetG2P.Chinese changes (separate repository).
            var puaPhonemes = _engine.ToPuaPhonemes(text);
            var prosodyResult = _engine.ToIpaWithProsody(text);

            var phonemeList = new List<string>();
            var prosodyA1List = new List<int>();
            var prosodyA2List = new List<int>();
            var prosodyA3List = new List<int>();

            int totalSyllables = prosodyResult.Prosody.Count;
            if (totalSyllables == 0)
            {
                // No syllables (all non-Chinese text)
                foreach (var p in puaPhonemes)
                    phonemeList.Add(p);
            }
            else
            {
                // Distribute PUA phonemes across syllables and insert tone markers
                // NOTE: Assumes approximately equal phonemes per syllable. Chinese syllables may have
                // variable phoneme counts (e.g., "a" vs "zhuang"). This distribution is an approximation
                // inherited from the original MultilingualPhonemizer.ProcessChinese implementation.
                int phonemesPerSyllable = puaPhonemes.Length / totalSyllables;
                int remainder = puaPhonemes.Length % totalSyllables;
                int puaIdx = 0;

                for (int syl = 0; syl < totalSyllables; syl++)
                {
                    int count = phonemesPerSyllable + (syl < remainder ? 1 : 0);
                    int toneVal = syl < prosodyResult.Prosody.Count
                        ? prosodyResult.Prosody[syl].A1 : 5;
                    int sylPos = syl < prosodyResult.Prosody.Count
                        ? prosodyResult.Prosody[syl].A2 : 1;
                    int wordLen = syl < prosodyResult.Prosody.Count
                        ? prosodyResult.Prosody[syl].A3 : 1;

                    // Add initial + final phonemes for this syllable
                    for (int j = 0; j < count && puaIdx < puaPhonemes.Length; j++, puaIdx++)
                    {
                        phonemeList.Add(puaPhonemes[puaIdx]);
                        prosodyA1List.Add(toneVal);
                        prosodyA2List.Add(sylPos);
                        prosodyA3List.Add(wordLen);
                    }

                    // Append tone marker PUA (tone1=E046 ... tone5=E04A)
                    if (toneVal >= 1 && toneVal <= 5)
                    {
                        phonemeList.Add(TonePuaChars[toneVal].ToString());
                        prosodyA1List.Add(toneVal);
                        prosodyA2List.Add(sylPos);
                        prosodyA3List.Add(wordLen);
                    }
                }

                // Add any remaining phonemes (non-Chinese tokens)
                for (; puaIdx < puaPhonemes.Length; puaIdx++)
                {
                    phonemeList.Add(puaPhonemes[puaIdx]);
                    prosodyA1List.Add(0);
                    prosodyA2List.Add(0);
                    prosodyA3List.Add(0);
                }
            }

            var segPhonemes = phonemeList.ToArray();
            var segA1 = prosodyA1List.ToArray();
            var segA2 = prosodyA2List.ToArray();
            var segA3 = prosodyA3List.ToArray();

            // Ensure prosody arrays are aligned with phoneme count
            if (segA1.Length < segPhonemes.Length)
            {
                Array.Resize(ref segA1, segPhonemes.Length);
                Array.Resize(ref segA2, segPhonemes.Length);
                Array.Resize(ref segA3, segPhonemes.Length);
            }

            // Flatten A1/A2/A3 -> stride=3 flat array
            var flat = PhonemeEncoder.FlattenProsody(
                segA1, segA2, segA3, segPhonemes.Length);
            return (segPhonemes, flat);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _engine?.Dispose();
        }
    }
}
