using System;
using uPiper.Core.AudioGeneration;

namespace uPiper.Core.Phonemizers.Multilingual.Handlers
{
    /// <summary>
    /// Shared utility methods for G2P handler implementations.
    /// </summary>
    internal static class G2PHandlerUtils
    {
        /// <summary>
        /// Extracts prosody values from a typed prosody array and returns a flat
        /// stride=3 array: [a1_0, a2_0, a3_0, a1_1, a2_1, a3_1, ...].
        /// Used by ES/FR/PT handlers that share the same extraction pattern.
        /// </summary>
        internal static int[] ExtractProsodyFlat<T>(
            T[] prosody, Func<T, (int a1, int a2, int a3)> accessor, int phonemeCount)
        {
            var flat = new int[phonemeCount * PhonemeEncoder.ProsodyStride];
            for (var i = 0; i < Math.Min(phonemeCount, prosody.Length); i++)
            {
                var (pa1, pa2, pa3) = accessor(prosody[i]);
                flat[i * PhonemeEncoder.ProsodyStride + 0] = pa1;
                flat[i * PhonemeEncoder.ProsodyStride + 1] = pa2;
                flat[i * PhonemeEncoder.ProsodyStride + 2] = pa3;
            }

            return flat;
        }
    }
}