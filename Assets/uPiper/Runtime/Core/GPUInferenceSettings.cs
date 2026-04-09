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
        /// Maximum GPU memory allocation in MB
        /// </summary>
        [Tooltip("Maximum GPU memory to allocate for inference in MB")]
        [Range(128, 2048)]
        public int MaxMemoryMB = 512;

        /// <summary>
        /// Validate GPU settings.
        /// GPU settings are now validated within ValidatedPiperConfig constructor.
        /// </summary>
        [Obsolete("GPU settings are now validated within ValidatedPiperConfig. Will be removed in v3.0.")]
        public void Validate()
        {
            // クランプロジックは ValidatedPiperConfig コンストラクタに移動済み。
            // 後方互換のためメソッドは残すが、フィールドは変更しない。
        }
    }
}