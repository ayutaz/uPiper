using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using uPiper.Phonemizers.Native;

namespace uPiper.Phonemizers
{
    /// <summary>
    /// Japanese phonemizer using OpenJTalk
    /// </summary>
    public class OpenJTalkPhonemizer : BasePhonemizer
    {
        private OpenJTalkInterop.OpenJTalkHandle _handle;
        private readonly object _lockObject = new object();
        private bool _isInitialized;

        public OpenJTalkPhonemizer(bool useCache = true, int maxCacheEntries = 1000) 
            : base(useCache, maxCacheEntries)
        {
        }

        /// <summary>
        /// Initialize the OpenJTalk phonemizer
        /// </summary>
        public async Task<bool> InitializeAsync()
        {
            if (_isInitialized)
            {
                return true;
            }

            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    if (_isInitialized)
                    {
                        return true;
                    }

                    try
                    {
                        // Check if OpenJTalk is available
                        if (!OpenJTalkInterop.IsAvailable())
                        {
                            Debug.LogError("[uPiper] OpenJTalk is not installed or not in PATH. " +
                                "Please install OpenJTalk or set OPENJTALK_PATH environment variable.");
                            return false;
                        }

                        // Ensure dictionary is available
                        if (!OpenJTalkInterop.EnsureDictionary())
                        {
                            Debug.LogError("[uPiper] OpenJTalk dictionary not found. " +
                                "Please install OpenJTalk dictionary or set OPENJTALK_DICTIONARY_DIR.");
                            return false;
                        }

                        // Create OpenJTalk instance
                        _handle = OpenJTalkInterop.Create();
                        if (!_handle.IsValid)
                        {
                            Debug.LogError("[uPiper] Failed to create OpenJTalk instance.");
                            return false;
                        }

                        _isInitialized = true;
                        Debug.Log($"[uPiper] OpenJTalk phonemizer initialized. Version: {OpenJTalkInterop.GetVersion()}");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[uPiper] Failed to initialize OpenJTalk: {ex.Message}");
                        return false;
                    }
                }
            });
        }

        protected override string[] PerformPhonemization(string text, string language)
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("OpenJTalk phonemizer is not initialized. Call InitializeAsync first.");
            }

            if (language != "ja" && language != "jp" && language != "japanese")
            {
                Debug.LogWarning($"[uPiper] OpenJTalk phonemizer is designed for Japanese. Language '{language}' may not work correctly.");
            }

            lock (_lockObject)
            {
                try
                {
                    string phonemesStr = OpenJTalkInterop.TextToPhonemes(_handle, text);
                    
                    if (string.IsNullOrEmpty(phonemesStr))
                    {
                        return new string[0];
                    }

                    // Split phonemes by space and filter out empty entries
                    var phonemes = phonemesStr.Split(new[] { ' ', '\t', '\n', '\r' }, 
                        StringSplitOptions.RemoveEmptyEntries);

                    // Map OpenJTalk phonemes to Piper-compatible format if needed
                    return MapPhonemes(phonemes);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[uPiper] Phonemization failed: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Map OpenJTalk phonemes to Piper-compatible format
        /// </summary>
        private string[] MapPhonemes(string[] openJTalkPhonemes)
        {
            // OpenJTalk phoneme mapping (if needed)
            // For now, we'll pass through the phonemes as-is
            // In the future, we may need to map specific phonemes

            var mappedPhonemes = new List<string>();
            
            foreach (var phoneme in openJTalkPhonemes)
            {
                // Skip empty phonemes
                if (string.IsNullOrWhiteSpace(phoneme))
                    continue;

                // Apply any necessary mappings here
                // For example, OpenJTalk might use different phoneme symbols than Piper expects
                string mapped = phoneme;
                
                // Example mappings (adjust based on actual requirements):
                switch (phoneme.ToLowerInvariant())
                {
                    case "sil": // silence
                    case "pau": // pause
                        mapped = "_"; // Piper silence token
                        break;
                    // Add more mappings as needed
                }

                mappedPhonemes.Add(mapped);
            }

            return mappedPhonemes.ToArray();
        }

        public override void Dispose()
        {
            if (_isInitialized)
            {
                lock (_lockObject)
                {
                    if (_handle.IsValid)
                    {
                        OpenJTalkInterop.Destroy(_handle);
                        _handle = OpenJTalkInterop.OpenJTalkHandle.Invalid;
                    }
                    _isInitialized = false;
                }
            }

            base.Dispose();
        }

        /// <summary>
        /// Check if OpenJTalk is available on the system
        /// </summary>
        public static bool IsOpenJTalkAvailable()
        {
            try
            {
                return OpenJTalkInterop.IsAvailable();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get OpenJTalk wrapper version
        /// </summary>
        public static string GetWrapperVersion()
        {
            try
            {
                return OpenJTalkInterop.GetVersion();
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}