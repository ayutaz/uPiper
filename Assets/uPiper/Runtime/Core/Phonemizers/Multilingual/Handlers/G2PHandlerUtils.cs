using System;

namespace uPiper.Core.Phonemizers.Multilingual.Handlers
{
    /// <summary>
    /// Shared utility methods for G2P handler implementations.
    /// </summary>
    internal static class G2PHandlerUtils
    {
        /// <summary>
        /// Extracts prosody A1/A2/A3 arrays from a typed prosody array.
        /// Used by ES/FR/PT handlers that share the same extraction pattern.
        /// </summary>
        internal static (int[] A1, int[] A2, int[] A3) ExtractProsodyArrays<T>(
            T[] prosody, Func<T, (int a1, int a2, int a3)> accessor, int phonemeCount)
        {
            var a1 = new int[phonemeCount];
            var a2 = new int[phonemeCount];
            var a3 = new int[phonemeCount];
            for (var i = 0; i < Math.Min(phonemeCount, prosody.Length); i++)
            {
                var (pa1, pa2, pa3) = accessor(prosody[i]);
                a1[i] = pa1;
                a2[i] = pa2;
                a3[i] = pa3;
            }

            return (a1, a2, a3);
        }
    }
}
