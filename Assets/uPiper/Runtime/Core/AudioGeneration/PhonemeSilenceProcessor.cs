using System;
using System.Collections.Generic;
using System.Globalization;

namespace uPiper.Core.AudioGeneration
{
    /// <summary>
    /// Splits a phoneme-ID sequence into phrases at positions where a designated
    /// "silence phoneme" occurs, and computes the number of zero-valued PCM
    /// samples to insert between each phrase.
    /// <para>
    /// This mirrors the C++ implementation in <c>piper.cpp</c> where
    /// <c>phonemeSilenceSeconds</c> causes the phoneme stream to be split into
    /// sub-phrases.  Each sub-phrase is synthesised independently and the
    /// resulting audio segments are concatenated with silence gaps.
    /// </para>
    /// Ported from piper-plus PhonemeSilenceProcessor.
    /// </summary>
    public static class PhonemeSilenceProcessor
    {
        /// <summary>
        /// One contiguous segment of the original phoneme-ID sequence, together
        /// with its matching prosody slices and the number of silence samples to
        /// append <b>after</b> the synthesised audio for this phrase.
        /// </summary>
        public readonly struct Phrase
        {
            /// <summary>Phoneme IDs for this phrase.</summary>
            public readonly int[] PhonemeIds;

            /// <summary>
            /// Prosody A1 values for this phrase (length = <c>PhonemeIds.Length</c>),
            /// or <c>null</c> when no prosody data is available.
            /// </summary>
            public readonly int[] ProsodyA1;

            /// <summary>
            /// Prosody A2 values for this phrase (length = <c>PhonemeIds.Length</c>),
            /// or <c>null</c> when no prosody data is available.
            /// </summary>
            public readonly int[] ProsodyA2;

            /// <summary>
            /// Prosody A3 values for this phrase (length = <c>PhonemeIds.Length</c>),
            /// or <c>null</c> when no prosody data is available.
            /// </summary>
            public readonly int[] ProsodyA3;

            /// <summary>
            /// Number of zero-valued PCM samples to insert after this phrase.
            /// The last phrase (or any phrase not ending on a silence phoneme) has <c>0</c>.
            /// </summary>
            public readonly int SilenceSamples;

            /// <summary>
            /// Initializes a new <see cref="Phrase"/>.
            /// </summary>
            /// <param name="phonemeIds">Phoneme IDs for this phrase.</param>
            /// <param name="prosodyA1">Prosody A1 values, or <c>null</c>.</param>
            /// <param name="prosodyA2">Prosody A2 values, or <c>null</c>.</param>
            /// <param name="prosodyA3">Prosody A3 values, or <c>null</c>.</param>
            /// <param name="silenceSamples">Number of silence samples to append.</param>
            public Phrase(int[] phonemeIds, int[] prosodyA1, int[] prosodyA2, int[] prosodyA3,
                int silenceSamples)
            {
                PhonemeIds = phonemeIds;
                ProsodyA1 = prosodyA1;
                ProsodyA2 = prosodyA2;
                ProsodyA3 = prosodyA3;
                SilenceSamples = silenceSamples;
            }
        }

        // ------------------------------------------------------------------
        // Parse
        // ------------------------------------------------------------------

        /// <summary>
        /// Parse one or more phoneme-silence specifications into a dictionary.
        /// <para>
        /// Accepted formats (mirrors the C++ CLI <c>--phoneme_silence</c> flag):
        /// <list type="bullet">
        ///   <item><c>"_ 0.5"</c> — single phoneme, space-separated.</item>
        ///   <item><c>"_ 0.5,# 0.3"</c> — multiple phonemes, comma-separated.</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="specification">
        /// A string such as <c>"_ 0.5"</c> or <c>"_ 0.5,# 0.3"</c>.
        /// </param>
        /// <returns>
        /// A dictionary mapping phoneme strings to seconds of silence.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="specification"/> is null/empty or contains
        /// an entry that cannot be parsed.
        /// </exception>
        public static Dictionary<string, float> Parse(string specification)
        {
            if (string.IsNullOrWhiteSpace(specification))
            {
                throw new ArgumentException(
                    "Phoneme silence specification must not be empty.",
                    nameof(specification));
            }

            var result = new Dictionary<string, float>();

            // Split on comma for multi-phoneme specifications.
            // Note: StringSplitOptions.TrimEntries requires .NET 5+; not available in all Unity versions.
            var rawEntries = specification.Split(',', StringSplitOptions.RemoveEmptyEntries);

            foreach (var rawEntry in rawEntries)
            {
                var entry = rawEntry.Trim();
                if (string.IsNullOrEmpty(entry))
                    continue;

                // Each entry is "<phoneme> <seconds>".
                // Split on whitespace; the phoneme is everything before the last
                // whitespace-delimited token (the seconds value).
                int lastSpace = entry.LastIndexOf(' ');
                if (lastSpace <= 0)
                {
                    throw new ArgumentException(
                        $"Cannot parse phoneme silence entry: '{entry}'. " +
                        "Expected format: '<phoneme> <seconds>'.",
                        nameof(specification));
                }

                var phoneme = entry.Substring(0, lastSpace).Trim();
                var secondsStr = entry.Substring(lastSpace + 1).Trim();

                if (string.IsNullOrEmpty(phoneme))
                {
                    throw new ArgumentException(
                        $"Empty phoneme in entry: '{entry}'.",
                        nameof(specification));
                }

                if (!float.TryParse(secondsStr, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out var seconds))
                {
                    throw new ArgumentException(
                        $"Cannot parse seconds value '{secondsStr}' in entry: '{entry}'.",
                        nameof(specification));
                }

                if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f)
                {
                    throw new ArgumentException(
                        $"Seconds value '{secondsStr}' in entry: '{entry}' must be a finite, non-negative number.",
                        nameof(specification));
                }

                result[phoneme] = seconds;
            }

            return result;
        }

        // ------------------------------------------------------------------
        // SplitAtPhonemeSilence
        // ------------------------------------------------------------------

        /// <summary>
        /// Split a phoneme-ID sequence into phrases at every position where one
        /// of the designated silence phonemes occurs.
        /// <para>
        /// Processing mirrors the C++ implementation:
        /// <list type="number">
        ///   <item>Build a reverse map from individual phoneme IDs to their
        ///         phoneme strings.</item>
        ///   <item>Walk the phoneme-ID array.  When a silence-phoneme ID is
        ///         encountered the current phrase is closed (the phoneme is
        ///         included in it) and a new phrase is started.</item>
        ///   <item>The silence duration in samples is recorded for the closed
        ///         phrase; the trailing phrase gets 0 silence samples.</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="phonemeIds">
        /// Complete phoneme-ID sequence (may include BOS/EOS/padding).
        /// </param>
        /// <param name="prosodyA1">
        /// Prosody A1 values of length <c>phonemeIds.Length</c>, or
        /// <c>null</c> when prosody is not used.
        /// </param>
        /// <param name="prosodyA2">
        /// Prosody A2 values of length <c>phonemeIds.Length</c>, or
        /// <c>null</c> when prosody is not used.
        /// </param>
        /// <param name="prosodyA3">
        /// Prosody A3 values of length <c>phonemeIds.Length</c>, or
        /// <c>null</c> when prosody is not used.
        /// </param>
        /// <param name="phonemeSilence">
        /// Mapping from phoneme strings to silence duration in seconds, as
        /// returned by <see cref="Parse"/>.
        /// </param>
        /// <param name="phonemeIdMap">
        /// The <c>phoneme_id_map</c> from <c>config.json</c>, mapping phoneme
        /// strings to integer IDs.
        /// </param>
        /// <param name="sampleRate">Audio sample rate in Hz.</param>
        /// <returns>
        /// An ordered list of <see cref="Phrase"/> segments.  Empty phrases
        /// (those with zero phoneme IDs) are included in the list — the caller
        /// should skip them, matching the C++ behaviour.
        /// </returns>
        public static List<Phrase> SplitAtPhonemeSilence(
            int[] phonemeIds,
            int[] prosodyA1,
            int[] prosodyA2,
            int[] prosodyA3,
            Dictionary<string, float> phonemeSilence,
            Dictionary<string, int> phonemeIdMap,
            int sampleRate)
        {
            if (phonemeIds == null)
            {
                throw new ArgumentNullException(nameof(phonemeIds));
            }

            if (phonemeSilence == null)
            {
                throw new ArgumentNullException(nameof(phonemeSilence));
            }

            if (phonemeIdMap == null)
            {
                throw new ArgumentNullException(nameof(phonemeIdMap));
            }

            // Build reverse map: phoneme-ID → (phoneme string, silence seconds).
            var silenceById = BuildSilenceIdMap(phonemeSilence, phonemeIdMap);

            bool hasProsody = prosodyA1 != null
                && prosodyA1.Length == phonemeIds.Length
                && prosodyA2 != null
                && prosodyA2.Length == phonemeIds.Length
                && prosodyA3 != null
                && prosodyA3.Length == phonemeIds.Length;

            int initialCapacity = Math.Max(10, phonemeIds.Length / 4);

            var phrases = new List<Phrase>(8);
            var currentIds = new List<int>(initialCapacity);
            List<int> currentA1 = hasProsody ? new List<int>(initialCapacity) : null;
            List<int> currentA2 = hasProsody ? new List<int>(initialCapacity) : null;
            List<int> currentA3 = hasProsody ? new List<int>(initialCapacity) : null;

            for (int i = 0; i < phonemeIds.Length; i++)
            {
                int id = phonemeIds[i];
                currentIds.Add(id);

                if (hasProsody)
                {
                    currentA1.Add(prosodyA1[i]);
                    currentA2.Add(prosodyA2[i]);
                    currentA3.Add(prosodyA3[i]);
                }

                if (silenceById.TryGetValue(id, out var seconds))
                {
                    // Close the current phrase with the computed silence.
                    int silenceSamples = (int)(seconds * sampleRate);

                    phrases.Add(new Phrase(
                        currentIds.ToArray(),
                        currentA1?.ToArray(),
                        currentA2?.ToArray(),
                        currentA3?.ToArray(),
                        silenceSamples));

                    // Start a new phrase.
                    currentIds = new List<int>(initialCapacity);
                    currentA1 = hasProsody ? new List<int>(initialCapacity) : null;
                    currentA2 = hasProsody ? new List<int>(initialCapacity) : null;
                    currentA3 = hasProsody ? new List<int>(initialCapacity) : null;
                }
            }

            // Trailing phrase (after the last split point, or all phonemes when
            // no split occurred).  Silence samples = 0.
            phrases.Add(new Phrase(
                currentIds.ToArray(),
                currentA1?.ToArray(),
                currentA2?.ToArray(),
                currentA3?.ToArray(),
                0));

            return phrases;
        }

        // ------------------------------------------------------------------
        // Private helpers
        // ------------------------------------------------------------------

        /// <summary>
        /// Build a lookup from individual phoneme IDs to the silence duration
        /// in seconds that should follow them.
        /// </summary>
        private static Dictionary<int, float> BuildSilenceIdMap(
            Dictionary<string, float> phonemeSilence,
            Dictionary<string, int> phonemeIdMap)
        {
            var map = new Dictionary<int, float>();

            foreach (var (phoneme, seconds) in phonemeSilence)
            {
                if (phonemeIdMap.TryGetValue(phoneme, out var id))
                {
                    map[id] = seconds;
                }
            }

            return map;
        }
    }
}