using System.Collections.Generic;
using UnityEngine;

namespace uPiper.Phonemizers.Configuration
{
    /// <summary>
    /// ScriptableObject for phonemizer configuration
    /// </summary>
    [CreateAssetMenu(fileName = "PhonemizerSettings", menuName = "uPiper/Phonemizer Settings")]
    public class PhonemizerSettings : ScriptableObject
    {
        [Header("General Settings")]
        [SerializeField] private readonly bool enablePhonemizerService = true;
        [SerializeField] private readonly string defaultLanguage = "en-US";
        [SerializeField] private readonly List<LanguageSettings> languageSettings = new();

        [Header("Performance Settings")]
        [SerializeField] private int maxConcurrentOperations = 2;
        [SerializeField] private int cacheSize = 1000;
        [SerializeField] private float cacheMemoryLimitMB = 50f;
        [SerializeField] private readonly bool enableBatchProcessing = true;
        [SerializeField] private int batchSize = 10;

        [Header("Mobile Optimization")]
        [SerializeField] private readonly bool enableMobileOptimization = true;
        [SerializeField] private readonly bool reduceCacheOnLowMemory = true;
        [SerializeField] private readonly bool pauseOnApplicationPause = true;
        [SerializeField] private int mobileMaxConcurrentOperations = 1;
        [SerializeField] private float mobileCacheMemoryLimitMB = 25f;

        [Header("Data Management")]
        [SerializeField] private readonly string dataPath = "{persistentDataPath}/uPiper/PhonemizerData";
        [SerializeField] private readonly bool autoDownloadEssentialData = true;
        [SerializeField] private readonly bool downloadOverCellular = false;

        [Header("Error Handling")]
        [SerializeField] private int maxRetries = 3;
        [SerializeField] private float retryDelay = 1f;
        [SerializeField] private readonly bool enableFallbackPhonemeizer = true;
        [SerializeField] private float circuitBreakerThreshold = 0.5f;
        [SerializeField] private float circuitBreakerResetTime = 30f;

        [Header("Debug Settings")]
        [SerializeField] private readonly bool enableDebugLogging = false;
        [SerializeField] private readonly bool logPhonemeOutput = false;
        [SerializeField] private readonly bool measurePerformance = false;

        // Properties
        public bool EnablePhonemizerService => enablePhonemizerService;
        public string DefaultLanguage => defaultLanguage;
        public IReadOnlyList<LanguageSettings> LanguageSettings => languageSettings;

        public int MaxConcurrentOperations => Application.isMobilePlatform && enableMobileOptimization
            ? mobileMaxConcurrentOperations
            : maxConcurrentOperations;

        public int CacheSize => cacheSize;

        public float CacheMemoryLimitMB => Application.isMobilePlatform && enableMobileOptimization
            ? mobileCacheMemoryLimitMB
            : cacheMemoryLimitMB;

        public bool EnableBatchProcessing => enableBatchProcessing;
        public int BatchSize => batchSize;

        public bool EnableMobileOptimization => enableMobileOptimization;
        public bool ReduceCacheOnLowMemory => reduceCacheOnLowMemory;
        public bool PauseOnApplicationPause => pauseOnApplicationPause;

        public string DataPath => dataPath.Replace("{persistentDataPath}", Application.persistentDataPath);
        public bool AutoDownloadEssentialData => autoDownloadEssentialData;
        public bool DownloadOverCellular => downloadOverCellular;

        public int MaxRetries => maxRetries;
        public float RetryDelay => retryDelay;
        public bool EnableFallbackPhonemeizer => enableFallbackPhonemeizer;
        public float CircuitBreakerThreshold => circuitBreakerThreshold;
        public float CircuitBreakerResetTime => circuitBreakerResetTime;

        public bool EnableDebugLogging => enableDebugLogging;
        public bool LogPhonemeOutput => logPhonemeOutput;
        public bool MeasurePerformance => measurePerformance;

        /// <summary>
        /// Get settings for a specific language
        /// </summary>
        public LanguageSettings GetLanguageSettings(string language)
        {
            return languageSettings.Find(ls => ls.languageCode == language);
        }

        /// <summary>
        /// Add or update language settings
        /// </summary>
        public void SetLanguageSettings(LanguageSettings settings)
        {
            var index = languageSettings.FindIndex(ls => ls.languageCode == settings.languageCode);
            if (index >= 0)
            {
                languageSettings[index] = settings;
            }
            else
            {
                languageSettings.Add(settings);
            }

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        private void OnValidate()
        {
            // Validate settings
            maxConcurrentOperations = Mathf.Max(1, maxConcurrentOperations);
            mobileMaxConcurrentOperations = Mathf.Max(1, mobileMaxConcurrentOperations);
            cacheSize = Mathf.Max(10, cacheSize);
            cacheMemoryLimitMB = Mathf.Max(1f, cacheMemoryLimitMB);
            mobileCacheMemoryLimitMB = Mathf.Max(1f, mobileCacheMemoryLimitMB);
            batchSize = Mathf.Max(1, batchSize);
            maxRetries = Mathf.Max(0, maxRetries);
            retryDelay = Mathf.Max(0f, retryDelay);
            circuitBreakerThreshold = Mathf.Clamp01(circuitBreakerThreshold);
            circuitBreakerResetTime = Mathf.Max(1f, circuitBreakerResetTime);
        }

        /// <summary>
        /// Create default settings
        /// </summary>
        public static PhonemizerSettings CreateDefault()
        {
            var settings = CreateInstance<PhonemizerSettings>();

            // Add default language settings
            settings.languageSettings.Add(new LanguageSettings
            {
                languageCode = "en-US",
                displayName = "English (US)",
                dataFiles = new List<string> { "cmudict-0.7b.txt", "g2p-model-en.json" },
                priority = DataPriority.Essential
            });

            settings.languageSettings.Add(new LanguageSettings
            {
                languageCode = "ja-JP",
                displayName = "Japanese",
                dataFiles = new List<string> { "ja-phonemes.txt", "ja-g2p-rules.json" },
                priority = DataPriority.Essential
            });

            return settings;
        }
    }

    /// <summary>
    /// Language-specific settings
    /// </summary>
    [System.Serializable]
    public class LanguageSettings
    {
        public string languageCode;
        public string displayName;
        public bool enabled = true;
        public DataPriority priority = DataPriority.Standard;
        public List<string> dataFiles = new();
        public bool useG2P = true;
        public bool cacheEnabled = true;
        public int cacheSize = 500;

        [Header("Performance Tuning")]
        public int maxWordLength = 50;
        public float timeoutSeconds = 5f;
    }

    /// <summary>
    /// Data priority levels
    /// </summary>
    public enum DataPriority
    {
        Essential,  // Core functionality
        Standard,   // Recommended for good quality
        Optional    // Enhanced features
    }
}