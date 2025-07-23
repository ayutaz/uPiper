using System;
using UnityEngine;

namespace uPiper.Core
{
    /// <summary>
    /// GPU-specific inference settings
    /// </summary>
    [Serializable]
    public class GPUInferenceSettings
    {
        /// <summary>
        /// Maximum batch size for GPU inference
        /// </summary>
        [Tooltip("Maximum batch size for GPU operations")]
        [Range(1, 16)]
        public int MaxBatchSize = 1;

        /// <summary>
        /// Use half precision (FP16) for better GPU performance
        /// </summary>
        [Tooltip("Use FP16 instead of FP32 for better performance (may reduce quality slightly)")]
        public bool UseFloat16 = false;

        /// <summary>
        /// Maximum GPU memory allocation in MB
        /// </summary>
        [Tooltip("Maximum GPU memory to allocate for inference in MB")]
        [Range(128, 2048)]
        public int MaxMemoryMB = 512;

        /// <summary>
        /// GPU synchronization mode
        /// </summary>
        [Tooltip("How to synchronize GPU operations")]
        public GPUSyncMode SyncMode = GPUSyncMode.Automatic;

        /// <summary>
        /// Enable GPU profiling
        /// </summary>
        [Tooltip("Enable GPU performance profiling (may impact performance)")]
        public bool EnableProfiling = false;

        /// <summary>
        /// Preferred GPU device index (for multi-GPU systems)
        /// </summary>
        [Tooltip("GPU device index to use (-1 for automatic selection)")]
        public int PreferredDeviceIndex = -1;

        /// <summary>
        /// Validate GPU settings
        /// </summary>
        public void Validate()
        {
            MaxBatchSize = Mathf.Clamp(MaxBatchSize, 1, 16);
            MaxMemoryMB = Mathf.Clamp(MaxMemoryMB, 128, 2048);

            if (PreferredDeviceIndex < -1)
            {
                PreferredDeviceIndex = -1;
            }
        }
    }

    /// <summary>
    /// GPU synchronization modes
    /// </summary>
    public enum GPUSyncMode
    {
        /// <summary>
        /// Automatically choose based on platform
        /// </summary>
        Automatic,

        /// <summary>
        /// Synchronous execution (wait for GPU)
        /// </summary>
        Synchronous,

        /// <summary>
        /// Asynchronous execution (don't wait)
        /// </summary>
        Asynchronous
    }
}