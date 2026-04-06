using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using uPiper.Core.AudioGeneration;

namespace uPiper.Tests.Editor
{
    /// <summary>
    /// Unit tests for <see cref="PhonemeSilenceProcessor"/>.
    /// Covers specification parsing, phoneme-ID sequence splitting, prosody
    /// alignment, silence sample calculation, and edge cases.
    /// </summary>
    [TestFixture]
    public class PhonemeSilenceProcessorTests
    {
        // ================================================================
        // Shared test data
        // ================================================================

        /// <summary>
        /// Minimal phoneme_id_map used by split tests.
        /// Mirrors a typical config.json mapping (single int per phoneme).
        /// </summary>
        private static readonly Dictionary<string, int> TestPhonemeIdMap = new()
        {
            { "_", 0 },   // PAD/silence
            { "^", 1 },   // BOS
            { "$", 2 },   // EOS
            { "#", 3 },   // sentence boundary
            { "a", 5 },
            { "b", 6 },
            { "c", 7 },
            { "d", 8 },
            { "e", 9 },
        };

        private const int TestSampleRate = 22050;

        // ================================================================
        // Parse -- valid specifications
        // ================================================================

        [Test]
        public void Parse_SingleEntry()
        {
            var result = PhonemeSilenceProcessor.Parse("_ 0.5");

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(0.5f, result["_"]);
        }

        [Test]
        public void Parse_MultipleEntries()
        {
            var result = PhonemeSilenceProcessor.Parse("_ 0.5,# 0.3");

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(0.5f, result["_"]);
            Assert.AreEqual(0.3f, result["#"]);
        }

        [Test]
        public void Parse_WhitespaceTrimmed()
        {
            var result = PhonemeSilenceProcessor.Parse("  _ 0.5 , # 0.3  ");

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(0.5f, result["_"]);
            Assert.AreEqual(0.3f, result["#"]);
        }

        [Test]
        public void Parse_DecimalPrecision()
        {
            var result = PhonemeSilenceProcessor.Parse("_ 0.125");

            Assert.AreEqual(0.125f, result["_"]);
        }

        [Test]
        public void Parse_IntegerAccepted()
        {
            var result = PhonemeSilenceProcessor.Parse("_ 1");

            Assert.AreEqual(1.0f, result["_"]);
        }

        [Test]
        public void Parse_DuplicateLastWins()
        {
            var result = PhonemeSilenceProcessor.Parse("_ 0.5,_ 0.8");

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(0.8f, result["_"]);
        }

        // ================================================================
        // Parse -- invalid specifications
        // ================================================================

        [Test]
        public void Parse_Null_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => PhonemeSilenceProcessor.Parse(null));
        }

        [Test]
        public void Parse_Empty_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => PhonemeSilenceProcessor.Parse(""));
        }

        [Test]
        public void Parse_WhitespaceOnly_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => PhonemeSilenceProcessor.Parse("   "));
        }

        [Test]
        public void Parse_MissingSeconds_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => PhonemeSilenceProcessor.Parse("_"));
        }

        [Test]
        public void Parse_NonNumeric_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => PhonemeSilenceProcessor.Parse("_ abc"));
        }

        [Test]
        public void Parse_SpaceOnlyEntry_Throws()
        {
            // After comma-split and trim, an entry like " 0.5" would have
            // lastSpace at 0, triggering the <= 0 check.
            Assert.Throws<ArgumentException>(
                () => PhonemeSilenceProcessor.Parse(", 0.5"));
        }

        // ================================================================
        // SplitAtPhonemeSilence -- basic splitting
        // ================================================================

        [Test]
        public void Split_NoSilence_SinglePhrase()
        {
            // Sequence: ^ a b $ -- no silence phoneme present
            var ids = new[] { 1, 5, 6, 2 };
            var silence = new Dictionary<string, float> { { "_", 0.5f } };

            var phrases = PhonemeSilenceProcessor.SplitAtPhonemeSilence(
                ids, null, null, null, silence, TestPhonemeIdMap, TestSampleRate);

            Assert.AreEqual(1, phrases.Count);
            CollectionAssert.AreEqual(new[] { 1, 5, 6, 2 }, phrases[0].PhonemeIds);
            Assert.AreEqual(0, phrases[0].SilenceSamples);
            Assert.IsNull(phrases[0].ProsodyA1);
            Assert.IsNull(phrases[0].ProsodyA2);
            Assert.IsNull(phrases[0].ProsodyA3);
        }

        [Test]
        public void Split_OneSilence_TwoPhrases()
        {
            // Sequence: a _ b  (IDs: 5, 0, 6)
            // "_" maps to ID 0, and has 0.5s silence.
            var ids = new[] { 5, 0, 6 };
            var silence = new Dictionary<string, float> { { "_", 0.5f } };

            var phrases = PhonemeSilenceProcessor.SplitAtPhonemeSilence(
                ids, null, null, null, silence, TestPhonemeIdMap, TestSampleRate);

            Assert.AreEqual(2, phrases.Count);

            // First phrase: [a, _], silence = 0.5 * 22050 = 11025.
            CollectionAssert.AreEqual(new[] { 5, 0 }, phrases[0].PhonemeIds);
            Assert.AreEqual(11025, phrases[0].SilenceSamples);

            // Second phrase: [b], silence = 0.
            CollectionAssert.AreEqual(new[] { 6 }, phrases[1].PhonemeIds);
            Assert.AreEqual(0, phrases[1].SilenceSamples);
        }

        [Test]
        public void Split_MultipleSilence_MultiplePhrases()
        {
            // Sequence: ^ a _ b # c $
            // "_" (ID 0) -> 0.5s, "#" (ID 3) -> 0.3s
            var ids = new[] { 1, 5, 0, 6, 3, 7, 2 };
            var silence = new Dictionary<string, float>
            {
                { "_", 0.5f },
                { "#", 0.3f },
            };

            var phrases = PhonemeSilenceProcessor.SplitAtPhonemeSilence(
                ids, null, null, null, silence, TestPhonemeIdMap, TestSampleRate);

            Assert.AreEqual(3, phrases.Count);

            // Phrase 0: [^, a, _]
            CollectionAssert.AreEqual(new[] { 1, 5, 0 }, phrases[0].PhonemeIds);
            Assert.AreEqual((int)(0.5f * 22050), phrases[0].SilenceSamples);

            // Phrase 1: [b, #]
            CollectionAssert.AreEqual(new[] { 6, 3 }, phrases[1].PhonemeIds);
            Assert.AreEqual((int)(0.3f * 22050), phrases[1].SilenceSamples);

            // Phrase 2: [c, $]
            CollectionAssert.AreEqual(new[] { 7, 2 }, phrases[2].PhonemeIds);
            Assert.AreEqual(0, phrases[2].SilenceSamples);
        }

        [Test]
        public void Split_SilenceAtEnd_TrailingEmpty()
        {
            // Sequence: ^ a _
            // The silence phoneme is the last ID, so the trailing phrase is empty.
            var ids = new[] { 1, 5, 0 };
            var silence = new Dictionary<string, float> { { "_", 0.5f } };

            var phrases = PhonemeSilenceProcessor.SplitAtPhonemeSilence(
                ids, null, null, null, silence, TestPhonemeIdMap, TestSampleRate);

            Assert.AreEqual(2, phrases.Count);

            CollectionAssert.AreEqual(new[] { 1, 5, 0 }, phrases[0].PhonemeIds);
            Assert.AreEqual(11025, phrases[0].SilenceSamples);

            // Trailing phrase is empty with 0 silence.
            Assert.AreEqual(0, phrases[1].PhonemeIds.Length);
            Assert.AreEqual(0, phrases[1].SilenceSamples);
        }

        [Test]
        public void Split_EmptyInput()
        {
            var ids = new int[0];
            var silence = new Dictionary<string, float> { { "_", 0.5f } };

            var phrases = PhonemeSilenceProcessor.SplitAtPhonemeSilence(
                ids, null, null, null, silence, TestPhonemeIdMap, TestSampleRate);

            Assert.AreEqual(1, phrases.Count);
            Assert.AreEqual(0, phrases[0].PhonemeIds.Length);
            Assert.AreEqual(0, phrases[0].SilenceSamples);
        }

        [Test]
        public void Split_SinglePhonemeNonSilence()
        {
            var ids = new[] { 5 }; // just "a"
            var silence = new Dictionary<string, float> { { "_", 0.5f } };

            var phrases = PhonemeSilenceProcessor.SplitAtPhonemeSilence(
                ids, null, null, null, silence, TestPhonemeIdMap, TestSampleRate);

            Assert.AreEqual(1, phrases.Count);
            CollectionAssert.AreEqual(new[] { 5 }, phrases[0].PhonemeIds);
            Assert.AreEqual(0, phrases[0].SilenceSamples);
        }

        [Test]
        public void Split_SinglePhonemeSilence()
        {
            var ids = new[] { 0 }; // just "_"
            var silence = new Dictionary<string, float> { { "_", 0.5f } };

            var phrases = PhonemeSilenceProcessor.SplitAtPhonemeSilence(
                ids, null, null, null, silence, TestPhonemeIdMap, TestSampleRate);

            Assert.AreEqual(2, phrases.Count);
            CollectionAssert.AreEqual(new[] { 0 }, phrases[0].PhonemeIds);
            Assert.AreEqual(11025, phrases[0].SilenceSamples);
            Assert.AreEqual(0, phrases[1].PhonemeIds.Length);
            Assert.AreEqual(0, phrases[1].SilenceSamples);
        }

        [Test]
        public void Split_AllSilencePhonemes()
        {
            var ids = new[] { 0, 0, 0 }; // three "_" in a row
            var silence = new Dictionary<string, float> { { "_", 0.5f } };

            var phrases = PhonemeSilenceProcessor.SplitAtPhonemeSilence(
                ids, null, null, null, silence, TestPhonemeIdMap, TestSampleRate);

            // Each "_" closes a phrase, plus a trailing empty phrase.
            Assert.AreEqual(4, phrases.Count);
            for (var i = 0; i < 3; i++)
            {
                CollectionAssert.AreEqual(new[] { 0 }, phrases[i].PhonemeIds);
                Assert.AreEqual(11025, phrases[i].SilenceSamples);
            }
            Assert.AreEqual(0, phrases[3].PhonemeIds.Length);
            Assert.AreEqual(0, phrases[3].SilenceSamples);
        }

        // ================================================================
        // SplitAtPhonemeSilence -- prosody alignment
        // ================================================================

        [Test]
        public void Split_WithProsody_SlicedCorrectly()
        {
            // Sequence: a _ b (IDs: 5, 0, 6)
            var ids = new[] { 5, 0, 6 };
            var a1 = new[] { 10, 20, 30 };
            var a2 = new[] { 11, 21, 31 };
            var a3 = new[] { 12, 22, 32 };
            var silence = new Dictionary<string, float> { { "_", 0.5f } };

            var phrases = PhonemeSilenceProcessor.SplitAtPhonemeSilence(
                ids, a1, a2, a3, silence, TestPhonemeIdMap, TestSampleRate);

            Assert.AreEqual(2, phrases.Count);

            // Phrase 0: phonemes [a, _], prosody for indices 0 and 1.
            CollectionAssert.AreEqual(new[] { 5, 0 }, phrases[0].PhonemeIds);
            Assert.IsNotNull(phrases[0].ProsodyA1);
            Assert.IsNotNull(phrases[0].ProsodyA2);
            Assert.IsNotNull(phrases[0].ProsodyA3);
            CollectionAssert.AreEqual(new[] { 10, 20 }, phrases[0].ProsodyA1);
            CollectionAssert.AreEqual(new[] { 11, 21 }, phrases[0].ProsodyA2);
            CollectionAssert.AreEqual(new[] { 12, 22 }, phrases[0].ProsodyA3);

            // Phrase 1: phoneme [b], prosody for index 2.
            CollectionAssert.AreEqual(new[] { 6 }, phrases[1].PhonemeIds);
            Assert.IsNotNull(phrases[1].ProsodyA1);
            Assert.IsNotNull(phrases[1].ProsodyA2);
            Assert.IsNotNull(phrases[1].ProsodyA3);
            CollectionAssert.AreEqual(new[] { 30 }, phrases[1].ProsodyA1);
            CollectionAssert.AreEqual(new[] { 31 }, phrases[1].ProsodyA2);
            CollectionAssert.AreEqual(new[] { 32 }, phrases[1].ProsodyA3);
        }

        [Test]
        public void Split_NullProsody_AllNull()
        {
            var ids = new[] { 5, 0, 6 };
            var silence = new Dictionary<string, float> { { "_", 0.5f } };

            var phrases = PhonemeSilenceProcessor.SplitAtPhonemeSilence(
                ids, null, null, null, silence, TestPhonemeIdMap, TestSampleRate);

            foreach (var phrase in phrases)
            {
                Assert.IsNull(phrase.ProsodyA1);
                Assert.IsNull(phrase.ProsodyA2);
                Assert.IsNull(phrase.ProsodyA3);
            }
        }

        [Test]
        public void Split_WrongLengthProsody_TreatedAsNull()
        {
            var ids = new[] { 5, 0, 6 };
            // Wrong length: should be 3 but is 2.
            var a1 = new[] { 10, 20 };
            var a2 = new[] { 11, 21 };
            var a3 = new[] { 12, 22 };
            var silence = new Dictionary<string, float> { { "_", 0.5f } };

            var phrases = PhonemeSilenceProcessor.SplitAtPhonemeSilence(
                ids, a1, a2, a3, silence, TestPhonemeIdMap, TestSampleRate);

            // Prosody length mismatch -> treated as no prosody.
            foreach (var phrase in phrases)
            {
                Assert.IsNull(phrase.ProsodyA1);
                Assert.IsNull(phrase.ProsodyA2);
                Assert.IsNull(phrase.ProsodyA3);
            }
        }

        // ================================================================
        // SplitAtPhonemeSilence -- silence sample calculation
        // ================================================================

        [Test]
        public void Split_SampleRate_22050()
        {
            var ids = new[] { 5, 0 }; // a _
            var silence = new Dictionary<string, float> { { "_", 0.5f } };

            var phrases = PhonemeSilenceProcessor.SplitAtPhonemeSilence(
                ids, null, null, null, silence, TestPhonemeIdMap, 22050);

            // 0.5s * 22050 = 11025 samples
            Assert.AreEqual(11025, phrases[0].SilenceSamples);
        }

        [Test]
        public void Split_SampleRate_44100()
        {
            var ids = new[] { 5, 0 }; // a _
            var silence = new Dictionary<string, float> { { "_", 0.5f } };

            var phrases = PhonemeSilenceProcessor.SplitAtPhonemeSilence(
                ids, null, null, null, silence, TestPhonemeIdMap, 44100);

            // 0.5s * 44100 = 22050 samples
            Assert.AreEqual(22050, phrases[0].SilenceSamples);
        }

        [Test]
        public void Split_SampleRate_16000()
        {
            var ids = new[] { 5, 0 }; // a _
            var silence = new Dictionary<string, float> { { "_", 0.5f } };

            var phrases = PhonemeSilenceProcessor.SplitAtPhonemeSilence(
                ids, null, null, null, silence, TestPhonemeIdMap, 16000);

            // 0.5s * 16000 = 8000 samples
            Assert.AreEqual(8000, phrases[0].SilenceSamples);
        }

        // ================================================================
        // SplitAtPhonemeSilence -- phoneme ID map interaction
        // ================================================================

        [Test]
        public void Split_PhonemeNotInIdMap_NoSplit()
        {
            var ids = new[] { 1, 5, 6, 2 };
            // "z" is in the silence spec but not in the phoneme_id_map.
            var silence = new Dictionary<string, float> { { "z", 0.5f } };

            var phrases = PhonemeSilenceProcessor.SplitAtPhonemeSilence(
                ids, null, null, null, silence, TestPhonemeIdMap, TestSampleRate);

            // No split -- "z" is not in the map so it cannot match any ID.
            Assert.AreEqual(1, phrases.Count);
            CollectionAssert.AreEqual(new[] { 1, 5, 6, 2 }, phrases[0].PhonemeIds);
        }

        [Test]
        public void Split_EmptySilenceMap_SinglePhrase()
        {
            var ids = new[] { 1, 5, 0, 6, 2 };
            var silence = new Dictionary<string, float>();

            var phrases = PhonemeSilenceProcessor.SplitAtPhonemeSilence(
                ids, null, null, null, silence, TestPhonemeIdMap, TestSampleRate);

            Assert.AreEqual(1, phrases.Count);
            CollectionAssert.AreEqual(new[] { 1, 5, 0, 6, 2 }, phrases[0].PhonemeIds);
            Assert.AreEqual(0, phrases[0].SilenceSamples);
        }

        [Test]
        public void Split_EmptyPhonemeIdMap()
        {
            var ids = new[] { 1, 5, 0, 6, 2 };
            var silence = new Dictionary<string, float> { { "_", 0.5f } };
            var emptyMap = new Dictionary<string, int>();

            var phrases = PhonemeSilenceProcessor.SplitAtPhonemeSilence(
                ids, null, null, null, silence, emptyMap, TestSampleRate);

            // No phoneme can be resolved, so no splits.
            Assert.AreEqual(1, phrases.Count);
            CollectionAssert.AreEqual(new[] { 1, 5, 0, 6, 2 }, phrases[0].PhonemeIds);
        }

        // ================================================================
        // SplitAtPhonemeSilence -- argument validation
        // ================================================================

        [Test]
        public void Split_NullPhonemeIds_Throws()
        {
            var silence = new Dictionary<string, float> { { "_", 0.5f } };

            Assert.Throws<ArgumentNullException>(
                () => PhonemeSilenceProcessor.SplitAtPhonemeSilence(
                    null, null, null, null, silence, TestPhonemeIdMap, TestSampleRate));
        }

        [Test]
        public void Split_NullSilenceMap_Throws()
        {
            var ids = new[] { 1, 5, 2 };

            Assert.Throws<ArgumentNullException>(
                () => PhonemeSilenceProcessor.SplitAtPhonemeSilence(
                    ids, null, null, null, null, TestPhonemeIdMap, TestSampleRate));
        }

        [Test]
        public void Split_NullPhonemeIdMap_Throws()
        {
            var ids = new[] { 1, 5, 2 };
            var silence = new Dictionary<string, float> { { "_", 0.5f } };

            Assert.Throws<ArgumentNullException>(
                () => PhonemeSilenceProcessor.SplitAtPhonemeSilence(
                    ids, null, null, null, silence, null, TestSampleRate));
        }

        // ================================================================
        // SplitAtPhonemeSilence -- consecutive different silence markers
        // ================================================================

        [Test]
        public void Split_ConsecutiveDifferentSilenceMarkers()
        {
            // Sequence: a _ # c
            var ids = new[] { 5, 0, 3, 7 };
            var silence = new Dictionary<string, float>
            {
                { "_", 0.5f },
                { "#", 0.3f },
            };

            var phrases = PhonemeSilenceProcessor.SplitAtPhonemeSilence(
                ids, null, null, null, silence, TestPhonemeIdMap, TestSampleRate);

            Assert.AreEqual(3, phrases.Count);

            CollectionAssert.AreEqual(new[] { 5, 0 }, phrases[0].PhonemeIds);
            Assert.AreEqual((int)(0.5f * 22050), phrases[0].SilenceSamples);

            CollectionAssert.AreEqual(new[] { 3 }, phrases[1].PhonemeIds);
            Assert.AreEqual((int)(0.3f * 22050), phrases[1].SilenceSamples);

            CollectionAssert.AreEqual(new[] { 7 }, phrases[2].PhonemeIds);
            Assert.AreEqual(0, phrases[2].SilenceSamples);
        }

        // ================================================================
        // Roundtrip tests
        // ================================================================

        [Test]
        public void Parse_Then_Split_RoundTrip()
        {
            var silence = PhonemeSilenceProcessor.Parse("_ 0.5,# 0.3");
            // Sequence: ^ a _ b # c $
            var ids = new[] { 1, 5, 0, 6, 3, 7, 2 };

            var phrases = PhonemeSilenceProcessor.SplitAtPhonemeSilence(
                ids, null, null, null, silence, TestPhonemeIdMap, TestSampleRate);

            Assert.AreEqual(3, phrases.Count);
            Assert.AreEqual((int)(0.5f * 22050), phrases[0].SilenceSamples);
            Assert.AreEqual((int)(0.3f * 22050), phrases[1].SilenceSamples);
            Assert.AreEqual(0, phrases[2].SilenceSamples);

            // Verify all original phoneme IDs are present across phrases.
            var allIds = phrases.SelectMany(p => p.PhonemeIds).ToArray();
            CollectionAssert.AreEqual(new[] { 1, 5, 0, 6, 3, 7, 2 }, allIds);
        }

        [Test]
        public void Phrase_ValueEquality()
        {
            // readonly struct with array fields: same content, different instances.
            var a = new PhonemeSilenceProcessor.Phrase(
                new[] { 1, 2, 3 }, null, null, null, 100);
            var b = new PhonemeSilenceProcessor.Phrase(
                new[] { 1, 2, 3 }, null, null, null, 100);

            // Value type equality for struct compares fields.
            // With array fields (reference types), different array instances
            // will cause inequality even with same content.
            Assert.AreNotEqual(a, b);

            // Same array reference should yield equality.
            var sharedIds = new[] { 1, 2, 3 };
            var c = new PhonemeSilenceProcessor.Phrase(
                sharedIds, null, null, null, 100);
            var d = new PhonemeSilenceProcessor.Phrase(
                sharedIds, null, null, null, 100);
            Assert.AreEqual(c, d);
        }
    }
}
