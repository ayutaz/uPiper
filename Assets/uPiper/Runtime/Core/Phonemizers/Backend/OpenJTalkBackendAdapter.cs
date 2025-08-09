#if !UNITY_WEBGL
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using uPiper.Core.Phonemizers.Implementations;

namespace uPiper.Core.Phonemizers.Backend
{
    /// <summary>
    /// Adapter to make OpenJTalkPhonemizer compatible with IPhonemizerBackend interface.
    /// </summary>
    public class OpenJTalkBackendAdapter : IPhonemizerBackend
    {
        private OpenJTalkPhonemizer openJTalk;
        private bool isInitialized;
        private bool isDisposed;

        public string Name => "OpenJTalk";
        public string Version => openJTalk?.Version ?? "1.0.0";
        public string License => "Modified BSD";
        public string[] SupportedLanguages => new[] { "ja" };
        public int Priority => 200;
        public bool IsAvailable => isInitialized && !isDisposed;

        public OpenJTalkBackendAdapter()
        {
            // Create instance but don't initialize yet
            openJTalk = null;
        }

        public async Task<bool> InitializeAsync(PhonemizerBackendOptions options = null, CancellationToken cancellationToken = default)
        {
            if (isInitialized)
                return true;

            try
            {
                // Create OpenJTalkPhonemizer instance
                var dictPath = options?.DataPath;
                var openJTalkInstance = new OpenJTalkPhonemizer(1000, dictPath);

                // Test if it's working
                var testResult = await Task.Run(() => openJTalkInstance.Phonemize("テスト", "ja"), cancellationToken);

                if (testResult != null && testResult.Phonemes != null && testResult.Phonemes.Length > 0)
                {
                    // Store the instance only if successful
                    openJTalk = openJTalkInstance;
                    isInitialized = true;
                    Debug.Log("OpenJTalkBackendAdapter initialized successfully");
                    return true;
                }
                else
                {
                    openJTalkInstance?.Dispose();
                    Debug.LogError("OpenJTalkBackendAdapter: Test phonemization failed");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to initialize OpenJTalkBackendAdapter: {ex.Message}");
                return false;
            }
        }

        public async Task<PhonemeResult> PhonemizeAsync(string text, string language, PhonemeOptions options = null, CancellationToken cancellationToken = default)
        {
            if (!isInitialized || openJTalk == null)
                throw new InvalidOperationException("OpenJTalkBackendAdapter not initialized");

            if (!SupportsLanguage(language))
            {
                return new PhonemeResult
                {
                    Success = false,
                    OriginalText = text,
                    Language = language,
                    Error = $"Language '{language}' is not supported by OpenJTalk"
                };
            }

            try
            {
                // Use the async method from BasePhonemizer
                var result = await openJTalk.PhonemizeAsync(text, language, cancellationToken);

                // Ensure result has all required fields
                result.Backend = Name;
                result.Success = result.Phonemes != null && result.Phonemes.Length > 0;

                return result;
            }
            catch (Exception ex)
            {
                return new PhonemeResult
                {
                    Success = false,
                    OriginalText = text,
                    Language = language,
                    Error = $"Phonemization failed: {ex.Message}",
                    Backend = Name
                };
            }
        }

        public bool SupportsLanguage(string language)
        {
            return language?.ToLowerInvariant() == "ja";
        }

        public long GetMemoryUsage()
        {
            // Estimate based on dictionary size
            return 50 * 1024 * 1024; // ~50MB estimate
        }

        public BackendCapabilities GetCapabilities()
        {
            return new BackendCapabilities
            {
                SupportsIPA = false,
                SupportsStress = false,
                SupportsSyllables = false,
                SupportsTones = false,
                SupportsDuration = true,
                SupportsBatchProcessing = true,
                IsThreadSafe = false,
                RequiresNetwork = false
            };
        }

        public void Dispose()
        {
            if (!isDisposed)
            {
                openJTalk?.Dispose();
                isDisposed = true;
            }
        }
    }
}
#endif