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
        /// Validate GPU settings
        /// </summary>
        public void Validate()
        {
            MaxMemoryMB = Mathf.Clamp(MaxMemoryMB, 128, 2048);
        }
    }
}