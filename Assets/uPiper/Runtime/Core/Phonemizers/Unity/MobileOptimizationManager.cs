using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Profiling;
using uPiper.Core.Phonemizers.Unity;

namespace uPiper.Core.Phonemizers.Unity
{
    /// <summary>
    /// Manages mobile-specific optimizations for phonemization
    /// </summary>
    public class MobileOptimizationManager : MonoBehaviour
    {
        // private PhonemizerSettings settings;
        private UnityPhonemizerService phonemizerService;

        [Header("Memory Management")]
        [SerializeField] private readonly float memoryCheckInterval = 30f;
        [SerializeField] private readonly float lowMemoryThresholdMB = 100f;
        [SerializeField] private readonly float criticalMemoryThresholdMB = 50f;

        [Header("Battery Optimization")]
        [SerializeField] private readonly bool throttleOnLowBattery = true;
        [SerializeField] private readonly float lowBatteryThreshold = 0.2f;
        [SerializeField] private readonly bool pauseOnBatteryLow = false;

        [Header("Thermal Management")]
        [SerializeField] private readonly bool enableThermalThrottling = true;
        [SerializeField] private readonly float thermalCheckInterval = 10f;

        private Coroutine memoryMonitorCoroutine;
        private Coroutine thermalMonitorCoroutine;
        private bool isLowMemoryMode = false;
        private bool isThermalThrottled = false;
        private float lastBatteryLevel = 1f;

        private void Start()
        {
            phonemizerService = UnityPhonemizerService.Instance;
            LoadSettings();

            if (Application.isMobilePlatform)
            {
                StartMobileOptimizations();
            }
        }

        private void LoadSettings()
        {
            // Settings loading is disabled for now
            // TODO: Implement PhonemizerSettings class
            /*
            settings = Resources.Load<PhonemizerSettings>("PhonemizerSettings");
            if (settings == null)
            {
                Debug.LogWarning("PhonemizerSettings not found in Resources. Using default settings.");
                settings = PhonemizerSettings.CreateDefault();
            }
            */
        }

        private void StartMobileOptimizations()
        {
            memoryMonitorCoroutine = StartCoroutine(MonitorMemory());

            if (enableThermalThrottling)
            {
                thermalMonitorCoroutine = StartCoroutine(MonitorThermalState());
            }

            if (throttleOnLowBattery)
            {
                StartCoroutine(MonitorBatteryLevel());
            }
        }

        private IEnumerator MonitorMemory()
        {
            var wait = new WaitForSeconds(memoryCheckInterval);

            while (true)
            {
                yield return wait;

                var usedMemory = Profiler.GetTotalAllocatedMemoryLong();
                var totalMemory = SystemInfo.systemMemorySize * 1024L * 1024L;
                var availableMemory = totalMemory - usedMemory;
                var availableMemoryMB = availableMemory / (1024f * 1024f);

                if (availableMemoryMB < criticalMemoryThresholdMB)
                {
                    OnCriticalMemory();
                }
                else if (availableMemoryMB < lowMemoryThresholdMB)
                {
                    OnLowMemory();
                }
                else if (isLowMemoryMode)
                {
                    OnMemoryRecovered();
                }
            }
        }

        private void OnLowMemory()
        {
            if (!isLowMemoryMode)
            {
                isLowMemoryMode = true;
                Debug.Log("Entering low memory mode");

                // Reduce cache size
                // if (settings.ReduceCacheOnLowMemory)
                {
                    // phonemizerService.ClearCache();
                }

                // Reduce concurrent operations
                ApplyMemoryOptimizations();
            }
        }

        private void OnCriticalMemory()
        {
            Debug.LogWarning("Critical memory state detected");

            // Aggressive memory cleanup
            // phonemizerService.ClearCache();
            Resources.UnloadUnusedAssets();
            System.GC.Collect();

            // Pause non-essential operations
            if (pauseOnBatteryLow)
            {
                phonemizerService.enabled = false;
            }
        }

        private void OnMemoryRecovered()
        {
            isLowMemoryMode = false;
            Debug.Log("Memory recovered, restoring normal operations");

            // Restore normal operations
            RestoreNormalOperations();
        }

        private IEnumerator MonitorThermalState()
        {
            var wait = new WaitForSeconds(thermalCheckInterval);

            while (true)
            {
                yield return wait;

                // Unity doesn't have direct thermal API, so we use heuristics
                var temperature = EstimateDeviceTemperature();

                if (temperature > 0.8f && !isThermalThrottled)
                {
                    OnThermalThrottling();
                }
                else if (temperature < 0.6f && isThermalThrottled)
                {
                    OnThermalRecovered();
                }
            }
        }

        private float EstimateDeviceTemperature()
        {
            // Heuristic based on frame rate and CPU usage
            var targetFPS = Application.targetFrameRate > 0 ? Application.targetFrameRate : 60f;
            var currentFPS = 1f / Time.smoothDeltaTime;
            var fpsRatio = currentFPS / targetFPS;

            // If FPS drops significantly, assume thermal throttling
            return 1f - Mathf.Clamp01(fpsRatio);
        }

        private void OnThermalThrottling()
        {
            isThermalThrottled = true;
            Debug.Log("Thermal throttling detected, reducing workload");

            // Reduce processing intensity
            ApplyThermalOptimizations();
        }

        private void OnThermalRecovered()
        {
            isThermalThrottled = false;
            Debug.Log("Thermal state recovered");

            RestoreNormalOperations();
        }

        private IEnumerator MonitorBatteryLevel()
        {
            var wait = new WaitForSeconds(60f); // Check every minute

            while (true)
            {
                yield return wait;

                var batteryLevel = SystemInfo.batteryLevel;
                if (batteryLevel >= 0) // Returns -1 if not supported
                {
                    if (batteryLevel <= lowBatteryThreshold && lastBatteryLevel > lowBatteryThreshold)
                    {
                        OnLowBattery();
                    }
                    else if (batteryLevel > lowBatteryThreshold && lastBatteryLevel <= lowBatteryThreshold)
                    {
                        OnBatteryRecovered();
                    }

                    lastBatteryLevel = batteryLevel;
                }
            }
        }

        private void OnLowBattery()
        {
            Debug.Log("Low battery detected, applying power saving optimizations");

            if (pauseOnBatteryLow)
            {
                phonemizerService.enabled = false;
            }
            else
            {
                ApplyPowerSavingOptimizations();
            }
        }

        private void OnBatteryRecovered()
        {
            Debug.Log("Battery level recovered");

            phonemizerService.enabled = true;
            RestoreNormalOperations();
        }

        private void ApplyMemoryOptimizations()
        {
            // Implement memory-specific optimizations
            var poolSize = Mathf.Max(1, 2); // Default: MobileMaxConcurrentOperations / 2
            UpdatePhonemizerPoolSize(poolSize);
        }

        private void ApplyThermalOptimizations()
        {
            // Reduce workload to prevent overheating
            var poolSize = 1;
            UpdatePhonemizerPoolSize(poolSize);

            // Add delays between operations
            Time.fixedDeltaTime = 0.033f; // Reduce to 30 FPS
        }

        private void ApplyPowerSavingOptimizations()
        {
            // Minimize battery usage
            UpdatePhonemizerPoolSize(1);

            // Disable non-essential features
            // settings.EnableBatchProcessing = false;
        }

        private void RestoreNormalOperations()
        {
            UpdatePhonemizerPoolSize(4); // Default MaxConcurrentOperations
            Time.fixedDeltaTime = 0.02f; // Restore to default
        }

        private void UpdatePhonemizerPoolSize(int size)
        {
            // This would call a method on phonemizerService to update pool size
            // For now, we'll just log it
            Debug.Log($"Updating phonemizer pool size to: {size}");
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            // if (settings != null && settings.PauseOnApplicationPause)
            {
                if (pauseStatus)
                {
                    StopAllCoroutines();
                }
                else
                {
                    if (Application.isMobilePlatform)
                    {
                        StartMobileOptimizations();
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (memoryMonitorCoroutine != null)
            {
                StopCoroutine(memoryMonitorCoroutine);
            }

            if (thermalMonitorCoroutine != null)
            {
                StopCoroutine(thermalMonitorCoroutine);
            }
        }

        /// <summary>
        /// Get current optimization status
        /// </summary>
        public OptimizationStatus GetStatus()
        {
            return new OptimizationStatus
            {
                IsLowMemoryMode = isLowMemoryMode,
                IsThermalThrottled = isThermalThrottled,
                BatteryLevel = lastBatteryLevel,
                IsOptimizationActive = isLowMemoryMode || isThermalThrottled || lastBatteryLevel <= lowBatteryThreshold
            };
        }

        [Serializable]
        public class OptimizationStatus
        {
            public bool IsLowMemoryMode;
            public bool IsThermalThrottled;
            public float BatteryLevel;
            public bool IsOptimizationActive;
        }
    }
}