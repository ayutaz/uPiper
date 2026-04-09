using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using uPiper.Core.Logging;
using uPiper.Core.Platform;
using UnityEngine;

namespace uPiper.Core.Phonemizers.Multilingual
{
    /// <summary>
    /// Loads trigram language profiles from StreamingAssets.
    /// Supports synchronous (desktop) and asynchronous (WebGL/Android) loading.
    /// Returns null when the profile file is not found (Unicode-only fallback).
    /// </summary>
    internal static class TrigramProfileLoader
    {
        private const string RelativePath = "uPiper/LanguageProfiles/trigram_profiles.json";

        /// <summary>
        /// Asynchronously loads trigram profiles from StreamingAssets.
        /// Works on all platforms including WebGL and Android.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// Dictionary mapping language codes to their trigram profiles,
        /// or null if the profile file is not found.
        /// </returns>
        public static async Task<Dictionary<string, TrigramProfile>> LoadAsync(
            CancellationToken cancellationToken = default)
        {
            string json;
            try
            {
                json = await WebGLStreamingAssetsLoader.LoadTextAsync(RelativePath, cancellationToken);
            }
            catch (FileNotFoundException)
            {
                PiperLogger.LogDebug(
                    "[TrigramProfileLoader] Profile file not found. " +
                    "Trigram detection will be disabled.");
                return null;
            }
            catch (Exception ex)
            {
                PiperLogger.LogWarning(
                    $"[TrigramProfileLoader] Failed to load profiles: {ex.Message}");
                return null;
            }

            return ParseJson(json);
        }

        /// <summary>
        /// Synchronously loads trigram profiles from StreamingAssets.
        /// Not available on WebGL or Android at runtime.
        /// </summary>
        /// <returns>
        /// Dictionary mapping language codes to their trigram profiles,
        /// or null if the profile file is not found.
        /// </returns>
        public static Dictionary<string, TrigramProfile> LoadSync()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            PiperLogger.LogWarning(
                "[TrigramProfileLoader] Synchronous loading is not supported on WebGL. " +
                "Use LoadAsync instead.");
            return null;
#elif UNITY_ANDROID && !UNITY_EDITOR
            PiperLogger.LogWarning(
                "[TrigramProfileLoader] Synchronous loading is not supported on Android. " +
                "Use LoadAsync instead.");
            return null;
#else
            var fullPath = Path.Combine(Application.streamingAssetsPath, RelativePath);
            if (!File.Exists(fullPath))
            {
                PiperLogger.LogDebug(
                    "[TrigramProfileLoader] Profile file not found at: " + fullPath);
                return null;
            }

            try
            {
                var json = File.ReadAllText(fullPath);
                return ParseJson(json);
            }
            catch (Exception ex)
            {
                PiperLogger.LogWarning(
                    $"[TrigramProfileLoader] Failed to load profiles: {ex.Message}");
                return null;
            }
#endif
        }

        /// <summary>
        /// Parses the trigram profiles JSON string into a dictionary of TrigramProfile objects.
        /// Returns null if the JSON is invalid, missing the 'version' field (0), or has no profiles.
        /// </summary>
        internal static Dictionary<string, TrigramProfile> ParseJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                var wrapper = JsonUtility.FromJson<TrigramProfilesJson>(json);
                if (wrapper == null || wrapper.version == 0)
                {
                    PiperLogger.LogWarning(
                        "[TrigramProfileLoader] Invalid JSON structure: 'version' field missing or zero.");
                    return null;
                }

                if (wrapper.profiles == null)
                {
                    PiperLogger.LogWarning(
                        "[TrigramProfileLoader] Invalid JSON structure: 'profiles' field missing.");
                    return null;
                }

                var result = new Dictionary<string, TrigramProfile>();

                if (wrapper.profiles.en != null && wrapper.profiles.en.Length > 0)
                    result["en"] = new TrigramProfile("en", wrapper.profiles.en);

                if (wrapper.profiles.es != null && wrapper.profiles.es.Length > 0)
                    result["es"] = new TrigramProfile("es", wrapper.profiles.es);

                if (wrapper.profiles.fr != null && wrapper.profiles.fr.Length > 0)
                    result["fr"] = new TrigramProfile("fr", wrapper.profiles.fr);

                if (wrapper.profiles.pt != null && wrapper.profiles.pt.Length > 0)
                    result["pt"] = new TrigramProfile("pt", wrapper.profiles.pt);

                if (result.Count == 0)
                {
                    PiperLogger.LogWarning(
                        "[TrigramProfileLoader] No valid profiles found in JSON.");
                    return null;
                }

                PiperLogger.LogInfo(
                    $"[TrigramProfileLoader] Loaded {result.Count} language profile(s): " +
                    string.Join(", ", result.Keys));

                return result;
            }
            catch (Exception ex)
            {
                PiperLogger.LogWarning(
                    $"[TrigramProfileLoader] JSON parse error: {ex.Message}");
                return null;
            }
        }

        // ── JSON serialization types (JsonUtility-compatible) ──────────────

        [Serializable]
        private class TrigramProfilesJson
        {
            public int version;
            public ProfilesData profiles;
        }

        [Serializable]
        private class ProfilesData
        {
            public string[] en;
            public string[] es;
            public string[] fr;
            public string[] pt;
        }
    }
}
