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
            /// Prosody flat array (stride=3) for this phrase,
            /// or <c>null</c> when no prosody data is available.
            /// Length = <c>PhonemeIds.Length * 3</c>.
            /// </summary>
            public readonly int[] ProsodyFlat;

            /// <summary>
            /// Number of zero-valued PCM samples to insert after this phrase.
            /// The last phrase (or any phrase not ending on a silence phoneme) has <c>0</c>.
            /// </summary>
            public readonly int SilenceSamples;

            /// <summary>
            /// Initializes a new <see cref="Phrase"/>.
            /// </summary>
            /// <param name="phonemeIds">Phoneme IDs for this phrase.</param>
            /// <param name="prosodyFlat">Prosody flat array (stride=3), or <c>null</c>.</param>
            /// <param name="silenceSamples">Number of silence samples to append.</param>
            public Phrase(int[] phonemeIds, int[] prosodyFlat, int silenceSamples)
            {
                PhonemeIds = phonemeIds;
                ProsodyFlat = prosodyFlat;
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
        /// </summary>
        /// <param name="phonemeIds">
        /// Complete phoneme-ID sequence (may include BOS/EOS/padding).
        /// </param>
        /// <param name="prosodyFlat">
        /// Prosody flat array (stride=3) of length <c>phonemeIds.Length * 3</c>,
        /// or <c>null</c> when prosody is not used.
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
            int[] prosodyFlat,
            IReadOnlyDictionary<string, float> phonemeSilence,
            IReadOnlyDictionary<string, int[]> phonemeIdMap,
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

            // Build reverse map: phoneme-ID -> (phoneme string, silence seconds).
            var silenceById = BuildSilenceIdMap(phonemeSilence, phonemeIdMap);

            bool hasProsody = prosodyFlat != null
                && prosodyFlat.Length == phonemeIds.Length * PhonemeEncoder.ProsodyStride;

            int initialCapacity = Math.Max(10, phonemeIds.Length / 4);

            var phrases = new List<Phrase>(8);
            var currentIds = new List<int>(initialCapacity);
            List<int> currentProsody = hasProsody ? new List<int>(initialCapacity * PhonemeEncoder.ProsodyStride) : null;

            for (int i = 0; i < phonemeIds.Length; i++)
            {
                int id = phonemeIds[i];
                currentIds.Add(id);

                if (hasProsody)
                {
                    var baseIdx = i * PhonemeEncoder.ProsodyStride;
                    currentProsody.Add(prosodyFlat[baseIdx + 0]);
                    currentProsody.Add(prosodyFlat[baseIdx + 1]);
                    currentProsody.Add(prosodyFlat[baseIdx + 2]);
                }

                if (silenceById.TryGetValue(id, out var seconds))
                {
                    // Close the current phrase with the computed silence.
                    int silenceSamples = (int)(seconds * sampleRate);

                    phrases.Add(new Phrase(
                        currentIds.ToArray(),
                        currentProsody?.ToArray(),
                        silenceSamples));

                    // Start a new phrase.
                    currentIds = new List<int>(initialCapacity);
                    currentProsody = hasProsody
                        ? new List<int>(initialCapacity * PhonemeEncoder.ProsodyStride)
                        : null;
                }
            }

            // Trailing phrase (after the last split point, or all phonemes when
            // no split occurred).  Silence samples = 0.
            phrases.Add(new Phrase(
                currentIds.ToArray(),
                currentProsody?.ToArray(),
                0));

            return phrases;
        }

        // ------------------------------------------------------------------
        // Private helpers
        // ------------------------------------------------------------------

        /// <summary>
        /// Build a lookup from individual phoneme IDs to the silence duration
        /// in seconds that should follow them.
        /// <para>
        /// Uses <c>ids[^1]</c> (last element) as the trigger ID. This is intentionally
        /// different from <see cref="PhonemeEncoder"/> which uses <c>ids[0]</c> (first element)
        /// for encoding. The distinction follows piper-plus convention: encoding uses the
        /// first ID to represent the phoneme in the model input, while silence detection
        /// uses the last ID as the trigger to split phrases. For single-ID phonemes the
        /// two are equivalent; for multi-ID phonemes the last element marks the phoneme
        /// boundary where silence should be inserted.
        /// </para>
        /// </summary>
        private static Dictionary<int, float> BuildSilenceIdMap(
            IReadOnlyDictionary<string, float> phonemeSilence,
            IReadOnlyDictionary<string, int[]> phonemeIdMap)
        {
            var map = new Dictionary<int, float>();

            foreach (var (phoneme, seconds) in phonemeSilence)
            {
                if (phonemeIdMap.TryGetValue(phoneme, out var ids) && ids.Length > 0)
                {
                    // ids[^1]: last element — see summary for why this differs from PhonemeEncoder's ids[0]
                    map[ids[^1]] = seconds;
                }
            }

            return map;
        }
    }
}