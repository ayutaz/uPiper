using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using uPiper.Phonemizers.Backend;
using uPiper.Phonemizers.Data;
using uPiper.Phonemizers.Threading;
using uPiper.Phonemizers.Caching;

namespace uPiper.Phonemizers.Unity
{
    /// <summary>
    /// Unity-specific phonemizer service with coroutine support and mobile optimization
    /// </summary>
    public class UnityPhonemizerService : MonoBehaviour
    {
        private static UnityPhonemizerService instance;
        public static UnityPhonemizerService Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("UnityPhonemizerService");
                    instance = go.AddComponent<UnityPhonemizerService>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        [Header("Performance Settings")]
        [SerializeField] private int maxConcurrentOperations = 2;
        [SerializeField] private int cacheSize = 1000;
        [SerializeField] private float cacheMemoryLimitMB = 50f;

        [Header("Mobile Optimization")]
        [SerializeField] private bool enableMobileOptimization = true;
        [SerializeField] private bool reduceCacheOnLowMemory = true;
        [SerializeField] private bool pauseOnApplicationPause = true;

        private ThreadSafePhonemizerPool phonemizerPool;
        private PhonemizerDataManager dataManager;
        private LRUCache<string, PhonemeResult> cache;
        private readonly object syncLock = new object();
        private bool isInitialized = false;
        private CancellationTokenSource applicationCancellation;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }

        private void Initialize()
        {
            applicationCancellation = new CancellationTokenSource();
            
            // Initialize cache with memory limit
            cache = new LRUCache<string, PhonemeResult>(
                cacheSize, 
                (long)(cacheMemoryLimitMB * 1024 * 1024)
            );

            // Initialize data manager
            string dataPath = Application.persistentDataPath + "/uPiper/PhonemizerData";
            dataManager = new PhonemizerDataManager(dataPath);

            // Initialize phonemizer pool with platform-specific settings
            int poolSize = GetOptimalPoolSize();
            phonemizerPool = new ThreadSafePhonemizerPool(poolSize);

            isInitialized = true;
            Debug.Log($"UnityPhonemizerService initialized with pool size: {poolSize}");
        }

        private int GetOptimalPoolSize()
        {
            if (!enableMobileOptimization)
                return maxConcurrentOperations;

            // Mobile optimization: reduce pool size based on device capabilities
            if (Application.isMobilePlatform)
            {
                int processorCount = SystemInfo.processorCount;
                int memoryGB = SystemInfo.systemMemorySize / 1024;
                
                // Conservative settings for mobile
                if (memoryGB <= 2) return 1;
                if (memoryGB <= 4) return Math.Min(2, processorCount / 2);
                return Math.Min(maxConcurrentOperations, processorCount / 2);
            }

            return maxConcurrentOperations;
        }

        /// <summary>
        /// Phonemize text using coroutine (Unity-friendly)
        /// </summary>
        public void PhonemizeAsync(string text, string language, Action<PhonemeResult> onComplete, Action<Exception> onError = null)
        {
            StartCoroutine(PhonemizeCoroutine(text, language, onComplete, onError));
        }

        private IEnumerator PhonemizeCoroutine(string text, string language, Action<PhonemeResult> onComplete, Action<Exception> onError)
        {
            if (!isInitialized)
            {
                onError?.Invoke(new InvalidOperationException("Service not initialized"));
                yield break;
            }

            // Check cache first
            string cacheKey = $"{language}:{text}";
            if (cache.TryGet(cacheKey, out PhonemeResult cachedResult))
            {
                onComplete?.Invoke(cachedResult);
                yield break;
            }

            // Start async operation
            var task = PhonemizeInternalAsync(text, language);
            
            // Wait for completion
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                onError?.Invoke(task.Exception?.InnerException ?? new Exception("Unknown error"));
            }
            else
            {
                var result = task.Result;
                cache.Set(cacheKey, result);
                onComplete?.Invoke(result);
            }
        }

        /// <summary>
        /// Phonemize text using async/await (for advanced users)
        /// </summary>
        public async Task<PhonemeResult> PhonemizeAsync(string text, string language, CancellationToken cancellationToken = default)
        {
            if (!isInitialized)
                throw new InvalidOperationException("Service not initialized");

            // Check cache
            string cacheKey = $"{language}:{text}";
            if (cache.TryGet(cacheKey, out PhonemeResult cachedResult))
            {
                return cachedResult;
            }

            // Perform phonemization
            var result = await PhonemizeInternalAsync(text, language, cancellationToken);
            
            // Cache result
            cache.Set(cacheKey, result);
            
            return result;
        }

        private async Task<PhonemeResult> PhonemizeInternalAsync(string text, string language, CancellationToken cancellationToken = default)
        {
            // Combine with application cancellation
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(applicationCancellation.Token, cancellationToken))
            {
                IPhonemizerBackend backend = null;
                try
                {
                    backend = phonemizerPool.Rent();
                    if (backend == null)
                    {
                        throw new InvalidOperationException("No available phonemizer backend");
                    }

                    var options = new PhonemeOptions
                    {
                        IncludeWordBoundaries = true,
                        IncludeStress = true
                    };

                    return await backend.PhonemizeAsync(text, language, options, cts.Token);
                }
                finally
                {
                    if (backend != null)
                    {
                        phonemizerPool.Return(backend);
                    }
                }
            }
        }

        /// <summary>
        /// Download language data with Unity progress callback
        /// </summary>
        public void DownloadLanguageData(string language, Action<float> onProgress, Action<bool> onComplete)
        {
            StartCoroutine(DownloadLanguageDataCoroutine(language, onProgress, onComplete));
        }

        private IEnumerator DownloadLanguageDataCoroutine(string language, Action<float> onProgress, Action<bool> onComplete)
        {
            var progress = new Progress<float>(value => onProgress?.Invoke(value));
            var task = dataManager.DownloadDataAsync(language, progress, applicationCancellation.Token);

            while (!task.IsCompleted)
            {
                yield return null;
            }

            onComplete?.Invoke(!task.IsFaulted && task.Result);
        }

        /// <summary>
        /// Check if language data is available
        /// </summary>
        public bool IsLanguageDataAvailable(string language)
        {
            return dataManager.IsDataAvailable(language);
        }

        /// <summary>
        /// Get available languages
        /// </summary>
        public string[] GetAvailableLanguages()
        {
            return phonemizerPool.GetSupportedLanguages();
        }

        /// <summary>
        /// Clear cache (useful for memory management)
        /// </summary>
        public void ClearCache()
        {
            cache.Clear();
        }

        /// <summary>
        /// Get cache statistics
        /// </summary>
        public (int count, long memoryBytes, float hitRate) GetCacheStatistics()
        {
            return cache.GetStatistics();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (!pauseOnApplicationPause) return;

            if (pauseStatus)
            {
                // Reduce memory usage when paused
                if (reduceCacheOnLowMemory)
                {
                    cache.TrimToSize(cacheSize / 2);
                }
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (enableMobileOptimization && !hasFocus)
            {
                // Reduce activity when app loses focus
                phonemizerPool.SetMaxPoolSize(1);
            }
            else
            {
                // Restore normal pool size
                phonemizerPool.SetMaxPoolSize(GetOptimalPoolSize());
            }
        }

        private void OnDestroy()
        {
            applicationCancellation?.Cancel();
            applicationCancellation?.Dispose();
            phonemizerPool?.Dispose();
            cache?.Clear();
            
            if (instance == this)
            {
                instance = null;
            }
        }

        /// <summary>
        /// Mobile-optimized batch phonemization
        /// </summary>
        public void PhonemizeBatch(List<string> texts, string language, Action<List<PhonemeResult>> onComplete, Action<float> onProgress = null)
        {
            StartCoroutine(PhonemizeBatchCoroutine(texts, language, onComplete, onProgress));
        }

        private IEnumerator PhonemizeBatchCoroutine(List<string> texts, string language, Action<List<PhonemeResult>> onComplete, Action<float> onProgress)
        {
            var results = new List<PhonemeResult>();
            int completed = 0;

            // Process in smaller batches on mobile
            int batchSize = Application.isMobilePlatform ? 5 : 10;
            
            for (int i = 0; i < texts.Count; i += batchSize)
            {
                var batch = texts.GetRange(i, Math.Min(batchSize, texts.Count - i));
                var tasks = new List<Task<PhonemeResult>>();

                foreach (var text in batch)
                {
                    tasks.Add(PhonemizeAsync(text, language));
                }

                // Wait for batch completion
                var batchTask = Task.WhenAll(tasks);
                while (!batchTask.IsCompleted)
                {
                    yield return null;
                }

                if (!batchTask.IsFaulted)
                {
                    results.AddRange(batchTask.Result);
                    completed += batch.Count;
                    onProgress?.Invoke((float)completed / texts.Count);
                }

                // Small delay between batches on mobile
                if (Application.isMobilePlatform)
                {
                    yield return new WaitForSeconds(0.1f);
                }
            }

            onComplete?.Invoke(results);
        }
    }
}