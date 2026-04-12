using System;
using System.Collections.Generic;
using NUnit.Framework;
using uPiper.Core.AudioGeneration;
using uPiper.Core.Phonemizers.Multilingual;

namespace uPiper.Tests.Editor.AudioGeneration
{
    [TestFixture]
    public class TimingCalculatorTests
    {
        private const int SampleRate = 22050;
        private const int HopSize = 256;

        private static Dictionary<string, int[]> CreateTimingTestPhonemeIdMap()
        {
            return new Dictionary<string, int[]>
            {
                ["_"] = new[] { 0 },      // PAD
                ["^"] = new[] { 1 },      // BOS
                ["$"] = new[] { 2 },      // EOS
                ["a"] = new[] { 10 },
                ["k"] = new[] { 12 },
                ["\uE000"] = new[] { 17 }, // PUA for "a:"
                ["\uE019"] = new[] { 40 }, // PUA for "N_m"
            };
        }

        // ================================================================
        // T1-1: Basic Calculation Tests
        // ================================================================

        [Test]
        public void Calculate_BasicDurations_ReturnsCorrectTiming()
        {
            var map = CreateTimingTestPhonemeIdMap();
            var phonemeIds = new[] { 10, 12 };
            var durations = new[] { 10.0f, 5.0f };
            var frameLength = (float)HopSize / SampleRate;

            var entries = TimingCalculator.Calculate(
                phonemeIds, durations, map, null, SampleRate, HopSize);

            Assert.AreEqual(2, entries.Count);
            Assert.AreEqual("a", entries[0].Phoneme);
            Assert.AreEqual(0f, entries[0].StartSeconds, 1e-5f);
            Assert.AreEqual(10 * frameLength, entries[0].EndSeconds, 1e-5f);
            Assert.AreEqual(10 * frameLength, entries[0].DurationSeconds, 1e-5f);
            Assert.AreEqual("k", entries[1].Phoneme);
            Assert.AreEqual(10 * frameLength, entries[1].StartSeconds, 1e-5f);
            Assert.AreEqual(15 * frameLength, entries[1].EndSeconds, 1e-5f);
            Assert.AreEqual(5 * frameLength, entries[1].DurationSeconds, 1e-5f);
        }

        [Test]
        public void Calculate_BasicDurations_CumulativeTimeIsAccurate()
        {
            var map = CreateTimingTestPhonemeIdMap();
            var phonemeIds = new[] { 10, 12, 10 };
            var durations = new[] { 8.0f, 3.0f, 12.0f };
            var frameLength = (float)HopSize / SampleRate;

            var entries = TimingCalculator.Calculate(
                phonemeIds, durations, map, null, SampleRate, HopSize);

            Assert.AreEqual((8 + 3 + 12) * frameLength, entries[2].EndSeconds, 1e-5f);
        }

        [Test]
        public void Calculate_SinglePhoneme_ReturnsOneEntry()
        {
            var map = CreateTimingTestPhonemeIdMap();
            var phonemeIds = new[] { 10 };
            var durations = new[] { 7.0f };

            var entries = TimingCalculator.Calculate(
                phonemeIds, durations, map, null, SampleRate, HopSize);

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(0f, entries[0].StartSeconds, 1e-5f);
        }

        [Test]
        public void Calculate_ReturnsCorrectPhonemeStrings()
        {
            var map = CreateTimingTestPhonemeIdMap();
            var phonemeIds = new[] { 10, 12 };
            var durations = new[] { 5.0f, 3.0f };

            var entries = TimingCalculator.Calculate(
                phonemeIds, durations, map, null, SampleRate, HopSize);

            Assert.AreEqual("a", entries[0].Phoneme);
            Assert.AreEqual("k", entries[1].Phoneme);
        }

        // ================================================================
        // T1-2: Special Token Skip Tests
        // ================================================================

        [Test]
        public void Calculate_SkipsSpecialTokens_PadBosEos()
        {
            var map = CreateTimingTestPhonemeIdMap();
            var phonemeIds = new[] { 0, 1, 10, 2 };
            var durations = new[] { 2.0f, 3.0f, 4.0f, 1.0f };
            var frameLength = (float)HopSize / SampleRate;

            var entries = TimingCalculator.Calculate(
                phonemeIds, durations, map, null, SampleRate, HopSize);

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("a", entries[0].Phoneme);
            Assert.AreEqual((2 + 3) * frameLength, entries[0].StartSeconds, 1e-5f);
            Assert.AreEqual((2 + 3 + 4) * frameLength, entries[0].EndSeconds, 1e-5f);
        }

        [Test]
        public void Calculate_SkipsPadOnly_TimeAdvances()
        {
            var map = CreateTimingTestPhonemeIdMap();
            var phonemeIds = new[] { 10, 0, 12 };
            var durations = new[] { 5.0f, 3.0f, 4.0f };
            var frameLength = (float)HopSize / SampleRate;

            var entries = TimingCalculator.Calculate(
                phonemeIds, durations, map, null, SampleRate, HopSize);

            Assert.AreEqual(2, entries.Count);
            Assert.AreEqual((5 + 3) * frameLength, entries[1].StartSeconds, 1e-5f);
        }

        [Test]
        public void Calculate_SkipsBosOnly_TimeAdvances()
        {
            var map = CreateTimingTestPhonemeIdMap();
            var phonemeIds = new[] { 1, 10 };
            var durations = new[] { 5.0f, 8.0f };
            var frameLength = (float)HopSize / SampleRate;

            var entries = TimingCalculator.Calculate(
                phonemeIds, durations, map, null, SampleRate, HopSize);

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(5 * frameLength, entries[0].StartSeconds, 1e-5f);
        }

        [Test]
        public void Calculate_SkipsEosOnly_TimeAdvances()
        {
            var map = CreateTimingTestPhonemeIdMap();
            var phonemeIds = new[] { 10, 2 };
            var durations = new[] { 8.0f, 3.0f };
            var frameLength = (float)HopSize / SampleRate;

            var entries = TimingCalculator.Calculate(
                phonemeIds, durations, map, null, SampleRate, HopSize);

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(8 * frameLength, entries[0].EndSeconds, 1e-5f);
        }

        [Test]
        public void Calculate_AllSpecialTokens_ReturnsEmpty()
        {
            var map = CreateTimingTestPhonemeIdMap();
            var phonemeIds = new[] { 0, 1, 2 };
            var durations = new[] { 1.0f, 2.0f, 3.0f };

            var entries = TimingCalculator.Calculate(
                phonemeIds, durations, map, null, SampleRate, HopSize);

            Assert.AreEqual(0, entries.Count);
        }

        [Test]
        public void Calculate_MultiplePads_CorrectTimeGaps()
        {
            var map = CreateTimingTestPhonemeIdMap();
            var phonemeIds = new[] { 1, 0, 10, 0, 12, 0, 2 };
            var durations = new[] { 2.0f, 1.0f, 5.0f, 1.0f, 3.0f, 1.0f, 1.0f };
            var frameLength = (float)HopSize / SampleRate;

            var entries = TimingCalculator.Calculate(
                phonemeIds, durations, map, null, SampleRate, HopSize);

            Assert.AreEqual(2, entries.Count);
            Assert.AreEqual((2 + 1) * frameLength, entries[0].StartSeconds, 1e-5f);
            Assert.AreEqual((2 + 1 + 5) * frameLength, entries[0].EndSeconds, 1e-5f);
            Assert.AreEqual((2 + 1 + 5 + 1) * frameLength, entries[1].StartSeconds, 1e-5f);
            Assert.AreEqual((2 + 1 + 5 + 1 + 3) * frameLength, entries[1].EndSeconds, 1e-5f);
        }

        // ================================================================
        // T1-3: PUA Reverse Mapping Tests
        // ================================================================

        [Test]
        public void Calculate_PuaReverseMapping_ResolvesToMultiCharToken()
        {
            var map = CreateTimingTestPhonemeIdMap();
            var puaTokenMapper = new PuaTokenMapper();
            var phonemeIds = new[] { 17, 40 };
            var durations = new[] { 3.0f, 2.0f };

            var entries = TimingCalculator.Calculate(
                phonemeIds, durations, map, puaTokenMapper, SampleRate, HopSize);

            Assert.AreEqual("a:", entries[0].Phoneme);
            Assert.AreEqual("N_m", entries[1].Phoneme);
        }

        [Test]
        public void Calculate_PuaAndRegularMixed_CorrectPhonemes()
        {
            var map = CreateTimingTestPhonemeIdMap();
            var puaTokenMapper = new PuaTokenMapper();
            var phonemeIds = new[] { 10, 17, 12 };
            var durations = new[] { 3.0f, 4.0f, 2.0f };

            var entries = TimingCalculator.Calculate(
                phonemeIds, durations, map, puaTokenMapper, SampleRate, HopSize);

            Assert.AreEqual("a", entries[0].Phoneme);
            Assert.AreEqual("a:", entries[1].Phoneme);
            Assert.AreEqual("k", entries[2].Phoneme);
        }

        [Test]
        public void Calculate_UnknownId_FallbackToQuestionMark()
        {
            var map = CreateTimingTestPhonemeIdMap();
            var phonemeIds = new[] { 999 };
            var durations = new[] { 5.0f };

            var entries = TimingCalculator.Calculate(
                phonemeIds, durations, map, null, SampleRate, HopSize);

            Assert.AreEqual("?", entries[0].Phoneme);
        }

        [Test]
        public void Calculate_AsciiPrintableUnknownId_ReturnsAsciiChar()
        {
            var map = CreateTimingTestPhonemeIdMap();
            var phonemeIds = new[] { 65 };
            var durations = new[] { 5.0f };

            var entries = TimingCalculator.Calculate(
                phonemeIds, durations, map, null, SampleRate, HopSize);

            Assert.AreEqual("A", entries[0].Phoneme);
        }

        [Test]
        public void Calculate_MultiplePuaEntries_AllResolved()
        {
            var map = CreateTimingTestPhonemeIdMap();
            map["\uE005"] = new[] { 18 };
            map["\uE00E"] = new[] { 19 };
            var puaTokenMapper = new PuaTokenMapper();
            var phonemeIds = new[] { 17, 18, 19 };
            var durations = new[] { 3.0f, 2.0f, 4.0f };

            var entries = TimingCalculator.Calculate(
                phonemeIds, durations, map, puaTokenMapper, SampleRate, HopSize);

            Assert.AreEqual("a:", entries[0].Phoneme);
            Assert.AreEqual("cl", entries[1].Phoneme);
            Assert.AreEqual("ch", entries[2].Phoneme);
        }

        // ================================================================
        // T1-4: Edge Case Tests
        // ================================================================

        [Test]
        public void Calculate_EmptyInput_ReturnsEmpty()
        {
            var map = CreateTimingTestPhonemeIdMap();
            var phonemeIds = new int[0];
            var durations = new float[0];

            var entries = TimingCalculator.Calculate(
                phonemeIds, durations, map, null, SampleRate, HopSize);

            Assert.AreEqual(0, entries.Count);
        }

        [Test]
        public void Calculate_NullPhonemeIds_ThrowsArgumentNullException()
        {
            var map = CreateTimingTestPhonemeIdMap();
            var durations = new[] { 1.0f };

            Assert.Throws<ArgumentNullException>(() =>
                TimingCalculator.Calculate(
                    null, durations, map, null, SampleRate, HopSize));
        }

        [Test]
        public void Calculate_NullDurations_ThrowsArgumentNullException()
        {
            var map = CreateTimingTestPhonemeIdMap();
            var phonemeIds = new[] { 10 };

            Assert.Throws<ArgumentNullException>(() =>
                TimingCalculator.Calculate(
                    phonemeIds, null, map, null, SampleRate, HopSize));
        }

        [Test]
        public void Calculate_NullPhonemeIdMap_ThrowsArgumentNullException()
        {
            var phonemeIds = new[] { 10 };
            var durations = new[] { 1.0f };

            Assert.Throws<ArgumentNullException>(() =>
                TimingCalculator.Calculate(
                    phonemeIds, durations, null, null, SampleRate, HopSize));
        }

        [Test]
        public void Calculate_LengthMismatch_PhonemeIdsLonger_UsesMinLength()
        {
            var map = CreateTimingTestPhonemeIdMap();
            var phonemeIds = new[] { 10, 12, 10 };
            var durations = new[] { 5.0f, 3.0f };

            var entries = TimingCalculator.Calculate(
                phonemeIds, durations, map, null, SampleRate, HopSize);

            Assert.AreEqual(2, entries.Count);
        }

        [Test]
        public void Calculate_LengthMismatch_DurationsLonger_UsesMinLength()
        {
            var map = CreateTimingTestPhonemeIdMap();
            var phonemeIds = new[] { 10 };
            var durations = new[] { 5.0f, 3.0f, 2.0f };

            var entries = TimingCalculator.Calculate(
                phonemeIds, durations, map, null, SampleRate, HopSize);

            Assert.AreEqual(1, entries.Count);
        }

        [Test]
        public void Calculate_AllZeroDurations_NoCrash()
        {
            var map = CreateTimingTestPhonemeIdMap();
            var phonemeIds = new[] { 10, 12 };
            var durations = new[] { 0.0f, 0.0f };

            var entries = TimingCalculator.Calculate(
                phonemeIds, durations, map, null, SampleRate, HopSize);

            Assert.AreEqual(2, entries.Count);
            Assert.AreEqual(0f, entries[0].DurationSeconds, 1e-5f);
            Assert.AreEqual(0f, entries[1].DurationSeconds, 1e-5f);
        }

        [Test]
        public void Calculate_NegativeDurations_ClampsToZero()
        {
            var map = CreateTimingTestPhonemeIdMap();
            var phonemeIds = new[] { 10, 12 };
            var durations = new[] { -5.0f, 3.0f };

            var entries = TimingCalculator.Calculate(
                phonemeIds, durations, map, null, SampleRate, HopSize);

            Assert.IsTrue(entries[0].DurationSeconds >= 0f);
            Assert.IsTrue(entries[1].StartSeconds >= entries[0].EndSeconds);
        }

        [Test]
        public void Calculate_ZeroSampleRate_ThrowsArgumentOutOfRangeException()
        {
            var map = CreateTimingTestPhonemeIdMap();
            var phonemeIds = new[] { 10 };
            var durations = new[] { 1.0f };

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                TimingCalculator.Calculate(
                    phonemeIds, durations, map, null, 0, HopSize));
        }

        [Test]
        public void Calculate_ZeroHopSize_ThrowsArgumentOutOfRangeException()
        {
            var map = CreateTimingTestPhonemeIdMap();
            var phonemeIds = new[] { 10 };
            var durations = new[] { 1.0f };

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                TimingCalculator.Calculate(
                    phonemeIds, durations, map, null, SampleRate, 0));
        }

        [Test]
        public void Calculate_NegativeSampleRate_ThrowsArgumentOutOfRangeException()
        {
            var map = CreateTimingTestPhonemeIdMap();
            var phonemeIds = new[] { 10 };
            var durations = new[] { 1.0f };

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                TimingCalculator.Calculate(
                    phonemeIds, durations, map, null, -1, HopSize));
        }

        [Test]
        public void Calculate_NegativeHopSize_ThrowsArgumentOutOfRangeException()
        {
            var map = CreateTimingTestPhonemeIdMap();
            var phonemeIds = new[] { 10 };
            var durations = new[] { 1.0f };

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                TimingCalculator.Calculate(
                    phonemeIds, durations, map, null, SampleRate, -1));
        }

        [Test]
        public void Calculate_VeryLargeDurations_NoOverflow()
        {
            var map = CreateTimingTestPhonemeIdMap();
            var phonemeIds = new[] { 10, 12 };
            var durations = new[] { float.MaxValue / 2f, 1.0f };

            var entries = TimingCalculator.Calculate(
                phonemeIds, durations, map, null, SampleRate, HopSize);

            Assert.IsFalse(float.IsNaN(entries[0].EndSeconds));
            Assert.IsFalse(float.IsInfinity(entries[0].EndSeconds));
            Assert.IsFalse(float.IsNaN(entries[1].StartSeconds));
            Assert.IsFalse(float.IsInfinity(entries[1].StartSeconds));
            Assert.IsFalse(float.IsNaN(entries[1].EndSeconds));
            Assert.IsFalse(float.IsInfinity(entries[1].EndSeconds));
        }

        [Test]
        public void Calculate_NaNDuration_ClampsToZero()
        {
            var map = CreateTimingTestPhonemeIdMap();
            var phonemeIds = new[] { 10, 12 };
            var durations = new[] { float.NaN, 3.0f };
            var frameLength = (float)HopSize / SampleRate;

            var entries = TimingCalculator.Calculate(
                phonemeIds, durations, map, null, SampleRate, HopSize);

            Assert.AreEqual(0f, entries[0].DurationSeconds, 1e-5f);
            Assert.IsFalse(float.IsNaN(entries[1].StartSeconds));
            Assert.AreEqual(3 * frameLength, entries[1].DurationSeconds, 1e-5f);
        }

        [Test]
        public void Calculate_InfinityDuration_ClampsToZero()
        {
            var map = CreateTimingTestPhonemeIdMap();
            var phonemeIds = new[] { 10, 12 };
            var durations = new[] { float.PositiveInfinity, 3.0f };
            var frameLength = (float)HopSize / SampleRate;

            var entries = TimingCalculator.Calculate(
                phonemeIds, durations, map, null, SampleRate, HopSize);

            Assert.AreEqual(0f, entries[0].DurationSeconds, 1e-5f);
            Assert.IsFalse(float.IsInfinity(entries[1].StartSeconds));
            Assert.AreEqual(3 * frameLength, entries[1].DurationSeconds, 1e-5f);
        }

        // ================================================================
        // Review: BuildReverseIdMap Direct Tests
        // ================================================================

        [Test]
        public void BuildReverseIdMap_EmptyIntArray_SkipsEntry()
        {
            var map = new Dictionary<string, int[]>
            {
                ["_"] = new[] { 0 },
                ["a"] = new int[0],
                ["k"] = new[] { 12 },
            };

            var reverse = TimingCalculator.BuildReverseIdMap(map, null);

            Assert.IsFalse(reverse.ContainsKey(-1));
            Assert.IsTrue(reverse.ContainsKey(0));
            Assert.IsTrue(reverse.ContainsKey(12));
            Assert.AreEqual(2, reverse.Count);
        }

        [Test]
        public void BuildReverseIdMap_NullMapper_PuaCharKeptAsIs()
        {
            var map = new Dictionary<string, int[]>
            {
                ["\uE000"] = new[] { 17 },
            };

            var reverse = TimingCalculator.BuildReverseIdMap(map, null);

            Assert.AreEqual("\uE000", reverse[17]);
        }

        [Test]
        public void BuildReverseIdMap_WithMapper_PuaCharResolved()
        {
            var map = new Dictionary<string, int[]>
            {
                ["\uE000"] = new[] { 17 },
            };
            var puaTokenMapper = new PuaTokenMapper();

            var reverse = TimingCalculator.BuildReverseIdMap(map, puaTokenMapper);

            Assert.AreEqual("a:", reverse[17]);
        }

        // ================================================================
        // Review: ResolvePhonemeString Boundary Tests
        // ================================================================

        [Test]
        public void Calculate_Id128_ReturnsQuestionMark()
        {
            var map = CreateTimingTestPhonemeIdMap();
            var phonemeIds = new[] { 128 };
            var durations = new[] { 5.0f };

            var entries = TimingCalculator.Calculate(
                phonemeIds, durations, map, null, SampleRate, HopSize);

            Assert.AreEqual("?", entries[0].Phoneme);
        }

        // ================================================================
        // T1-5: PhonemeTimingEntry Struct Tests
        // ================================================================

        [Test]
        public void PhonemeTimingEntry_DurationEqualsEndMinusStart()
        {
            var entry1 = new PhonemeTimingEntry("test", 0f, 0.5f);
            Assert.AreEqual(0.5f, entry1.DurationSeconds, 1e-7f);

            var entry2 = new PhonemeTimingEntry("test", 1.234f, 2.567f);
            Assert.AreEqual(1.333f, entry2.DurationSeconds, 1e-7f);

            var entry3 = new PhonemeTimingEntry("test", 0f, 0f);
            Assert.AreEqual(0f, entry3.DurationSeconds, 1e-7f);
        }

        [Test]
        public void PhonemeTimingEntry_ConstructedValues_AreCorrect()
        {
            var entry = new PhonemeTimingEntry("k", 1.5f, 2.3f);

            Assert.AreEqual("k", entry.Phoneme);
            Assert.AreEqual(1.5f, entry.StartSeconds, 1e-7f);
            Assert.AreEqual(2.3f, entry.EndSeconds, 1e-7f);
            Assert.AreEqual(0.8f, entry.DurationSeconds, 1e-7f);
        }

        [Test]
        public void PhonemeTimingEntry_Default_HasNullPhonemeAndZeroTimes()
        {
            var entry = default(PhonemeTimingEntry);

            Assert.IsNull(entry.Phoneme);
            Assert.AreEqual(0f, entry.StartSeconds, 1e-7f);
            Assert.AreEqual(0f, entry.EndSeconds, 1e-7f);
            Assert.AreEqual(0f, entry.DurationSeconds, 1e-7f);
        }

        [Test]
        public void PhonemeTimingEntry_EqualityByValue()
        {
            // readonly struct はデフォルトの ValueType.Equals (リフレクション) を使用。
            // IEquatable<T> 未実装のため、ボクシング経由のフィールド比較に依存する。
            var entry1 = new PhonemeTimingEntry("a", 0.1f, 0.2f);
            var entry2 = new PhonemeTimingEntry("a", 0.1f, 0.2f);

            Assert.IsTrue(entry1.Equals(entry2));
        }

        [Test]
        public void PhonemeTimingEntry_DifferentValues_NotEqual()
        {
            var entry1 = new PhonemeTimingEntry("a", 0.1f, 0.2f);
            var entryDiffPhoneme = new PhonemeTimingEntry("k", 0.1f, 0.2f);
            var entryDiffStart = new PhonemeTimingEntry("a", 0.15f, 0.2f);

            Assert.IsFalse(entry1.Equals(entryDiffPhoneme));
            Assert.IsFalse(entry1.Equals(entryDiffStart));
        }

        [Test]
        public void PhonemeTimingEntry_ZeroDuration_IsValid()
        {
            var entry = new PhonemeTimingEntry("a", 0.5f, 0.5f);

            Assert.AreEqual(0f, entry.DurationSeconds, 1e-7f);
        }

        [Test]
        public void PhonemeTimingEntry_DurationConsistency_AfterTimingCalculation()
        {
            var map = CreateTimingTestPhonemeIdMap();
            var phonemeIds = new[] { 1, 10, 0, 12, 0, 10, 2 };
            var durations = new[] { 2.0f, 8.0f, 1.0f, 5.0f, 1.0f, 12.0f, 1.0f };

            var entries = TimingCalculator.Calculate(
                phonemeIds, durations, map, null, SampleRate, HopSize);

            foreach (var entry in entries)
            {
                Assert.AreEqual(
                    entry.EndSeconds - entry.StartSeconds,
                    entry.DurationSeconds,
                    1e-7f);
            }
        }
    }
}