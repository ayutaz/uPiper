using System;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using uPiper.Core.AudioGeneration;

namespace uPiper.Tests.Editor.AudioGeneration;

/// <summary>
/// Unit tests for <see cref="ShortTextProcessor"/>.
/// Covers phoneme padding, silence trimming, and scale adjustment for short text inputs.
/// </summary>
[TestFixture]
public class ShortTextProcessorTests
{
    // ================================================================
    // Constants (mirror ShortTextProcessor internals for readability)
    // ================================================================

    private const int MinPhonemeIds = 40;
    private const float TrimThresholdRms = 0.01f;
    private const int TrimMinSamples = 2205;
    private const int TrimWindowSize = 256;
    private const int PadId = 0;
    private const int ProsodyStride = 3; // PhonemeEncoder.ProsodyStride

    // ================================================================
    // Helpers
    // ================================================================

    /// <summary>
    /// Creates a phoneme ID array of specified length with BOS at index 0
    /// and EOS at the last index. Interior elements are filled with sequential
    /// values starting from 10.
    /// </summary>
    private static int[] CreatePhonemeIds(int length, int bos = 1, int eos = 2)
    {
        if (length <= 0)
            return Array.Empty<int>();

        var ids = new int[length];
        ids[0] = bos;
        if (length > 1)
            ids[length - 1] = eos;

        for (var i = 1; i < length - 1; i++)
            ids[i] = 10 + i - 1;

        return ids;
    }

    /// <summary>
    /// Creates a prosody flat array (stride=3) matching a phoneme ID array.
    /// Each phoneme gets (index+1, index+2, index+3) as a1/a2/a3.
    /// </summary>
    private static int[] CreateProsodyFlat(int phonemeCount)
    {
        var prosody = new int[phonemeCount * ProsodyStride];
        for (var i = 0; i < phonemeCount; i++)
        {
            prosody[i * ProsodyStride + 0] = i + 1;
            prosody[i * ProsodyStride + 1] = i + 2;
            prosody[i * ProsodyStride + 2] = i + 3;
        }
        return prosody;
    }

    // ================================================================
    // NeedsPadding tests
    // ================================================================

    [Test]
    public void NeedsPadding_ShortSequence_ReturnsTrue()
    {
        // Arrange: 10 elements, well below MinPhonemeIds (40)
        var ids = CreatePhonemeIds(10);

        // Act
        var result = ShortTextProcessor.NeedsPadding(ids);

        // Assert
        Assert.IsTrue(result, "A 10-element sequence should need padding");
    }

    [Test]
    public void NeedsPadding_ExactMinimum_ReturnsFalse()
    {
        // Arrange: exactly MinPhonemeIds (40) elements
        var ids = CreatePhonemeIds(MinPhonemeIds);

        // Act
        var result = ShortTextProcessor.NeedsPadding(ids);

        // Assert
        Assert.IsFalse(result, "A sequence with exactly MinPhonemeIds elements should not need padding");
    }

    [Test]
    public void NeedsPadding_LongSequence_ReturnsFalse()
    {
        // Arrange: 100 elements, well above MinPhonemeIds
        var ids = CreatePhonemeIds(100);

        // Act
        var result = ShortTextProcessor.NeedsPadding(ids);

        // Assert
        Assert.IsFalse(result, "A 100-element sequence should not need padding");
    }

    [Test]
    public void NeedsPadding_OneBelowMinimum_ReturnsTrue()
    {
        // Arrange: 39 elements, one below MinPhonemeIds
        var ids = CreatePhonemeIds(MinPhonemeIds - 1);

        // Act
        var result = ShortTextProcessor.NeedsPadding(ids);

        // Assert
        Assert.IsTrue(result, "A sequence with MinPhonemeIds-1 elements should need padding");
    }

    // ================================================================
    // PadPhonemeIds tests
    // ================================================================

    [Test]
    public void PadPhonemeIds_ShortSequence_PaddedToMinLength()
    {
        // Arrange: [BOS=1, 10, 11, 12, EOS=2] -> length 5
        var ids = new[] { 1, 10, 11, 12, 2 };

        // Act
        var result = ShortTextProcessor.PadPhonemeIds(ids, null);

        // Assert
        Assert.AreEqual(MinPhonemeIds, result.PaddedIds.Length,
            "Padded result should have exactly MinPhonemeIds elements");
    }

    [Test]
    public void PadPhonemeIds_PreservesBosAndEos()
    {
        // Arrange
        var ids = new[] { 1, 10, 11, 12, 2 };

        // Act
        var result = ShortTextProcessor.PadPhonemeIds(ids, null);

        // Assert
        Assert.AreEqual(1, result.PaddedIds[0],
            "First element should be BOS (1)");
        Assert.AreEqual(2, result.PaddedIds[result.PaddedIds.Length - 1],
            "Last element should be EOS (2)");
    }

    [Test]
    public void PadPhonemeIds_InsertsOnlyPadIds()
    {
        // Arrange
        var ids = new[] { 1, 10, 11, 12, 2 };
        var originalSet = new System.Collections.Generic.HashSet<int>(ids);

        // Act
        var result = ShortTextProcessor.PadPhonemeIds(ids, null);

        // Assert: every element not in the original set must be PadId (0)
        for (var i = 0; i < result.PaddedIds.Length; i++)
        {
            if (!originalSet.Contains(result.PaddedIds[i]))
            {
                Assert.AreEqual(PadId, result.PaddedIds[i],
                    $"Element at index {i} should be PadId (0) but was {result.PaddedIds[i]}");
            }
        }
    }

    [Test]
    public void PadPhonemeIds_BodyPreserved()
    {
        // Arrange: body elements are [10, 11, 12, 13]
        var ids = new[] { 1, 10, 11, 12, 13, 2 };

        // Act
        var result = ShortTextProcessor.PadPhonemeIds(ids, null);
        var paddedIds = result.PaddedIds;

        // Assert: body elements [10, 11, 12, 13] appear in order in the padded result
        var bodyElements = new[] { 10, 11, 12, 13 };
        var searchStart = 0;
        for (var bi = 0; bi < bodyElements.Length; bi++)
        {
            var found = false;
            for (var pi = searchStart; pi < paddedIds.Length; pi++)
            {
                if (paddedIds[pi] == bodyElements[bi])
                {
                    searchStart = pi + 1;
                    found = true;
                    break;
                }
            }
            Assert.IsTrue(found,
                $"Body element {bodyElements[bi]} should appear in padded result in order");
        }
    }

    [Test]
    public void PadPhonemeIds_AlreadyLongEnough_ReturnsOriginal()
    {
        // Arrange: 50 elements, above MinPhonemeIds
        var ids = CreatePhonemeIds(50);

        // Act
        var result = ShortTextProcessor.PadPhonemeIds(ids, null);

        // Assert: same array reference (no copy)
        Assert.AreSame(ids, result.PaddedIds,
            "An already-long-enough array should be returned as-is (same reference)");
    }

    [Test]
    public void PadPhonemeIds_WithProsody_ProsodyExtended()
    {
        // Arrange: 5 phonemes with prosody
        var ids = new[] { 1, 10, 11, 2, 0 };  // BOS + body + EOS + extra
        // Actually keep it simple: 5-element ids
        ids = CreatePhonemeIds(5);
        var prosody = CreateProsodyFlat(5);

        // Act
        var result = ShortTextProcessor.PadPhonemeIds(ids, prosody);

        // Assert: prosody length = padded ids length * ProsodyStride
        Assert.IsNotNull(result.PaddedProsody,
            "PaddedProsody should not be null when input prosody is provided");
        Assert.AreEqual(result.PaddedIds.Length * ProsodyStride, result.PaddedProsody.Length,
            "PaddedProsody length should be PaddedIds.Length * ProsodyStride");

        // BOS prosody (first ProsodyStride values) should be preserved from original
        Assert.AreEqual(prosody[0], result.PaddedProsody[0], "BOS a1 should be preserved");
        Assert.AreEqual(prosody[1], result.PaddedProsody[1], "BOS a2 should be preserved");
        Assert.AreEqual(prosody[2], result.PaddedProsody[2], "BOS a3 should be preserved");

        // EOS prosody (last ProsodyStride values) should be preserved from original
        var lastProsodyStart = (result.PaddedIds.Length - 1) * ProsodyStride;
        var originalEosStart = (ids.Length - 1) * ProsodyStride;
        Assert.AreEqual(prosody[originalEosStart + 0], result.PaddedProsody[lastProsodyStart + 0],
            "EOS a1 should be preserved");
        Assert.AreEqual(prosody[originalEosStart + 1], result.PaddedProsody[lastProsodyStart + 1],
            "EOS a2 should be preserved");
        Assert.AreEqual(prosody[originalEosStart + 2], result.PaddedProsody[lastProsodyStart + 2],
            "EOS a3 should be preserved");
    }

    [Test]
    public void PadPhonemeIds_WithProsody_PaddingPositionsAreZero()
    {
        // Arrange: 5 phonemes with prosody -> will be padded to 40
        var ids = CreatePhonemeIds(5);
        var prosody = CreateProsodyFlat(5);

        // Act
        var result = ShortTextProcessor.PadPhonemeIds(ids, prosody);

        // Assert: padding positions in the prosody array should be zero
        // Padding IDs are PadId (0). Check that prosody at those positions is all zeros.
        for (var i = 0; i < result.PaddedIds.Length; i++)
        {
            if (result.PaddedIds[i] == PadId)
            {
                var baseIdx = i * ProsodyStride;
                Assert.AreEqual(0, result.PaddedProsody[baseIdx + 0],
                    $"Prosody a1 at padding position {i} should be 0");
                Assert.AreEqual(0, result.PaddedProsody[baseIdx + 1],
                    $"Prosody a2 at padding position {i} should be 0");
                Assert.AreEqual(0, result.PaddedProsody[baseIdx + 2],
                    $"Prosody a3 at padding position {i} should be 0");
            }
        }
    }

    [Test]
    public void PadPhonemeIds_NullProsody_ReturnsNull()
    {
        // Arrange
        var ids = CreatePhonemeIds(5);

        // Act
        var result = ShortTextProcessor.PadPhonemeIds(ids, null);

        // Assert
        Assert.IsNull(result.PaddedProsody,
            "PaddedProsody should be null when input prosody is null");
    }

    [Test]
    public void PadPhonemeIds_EvenDistribution()
    {
        // Arrange: 5 elements -> deficit = 40 - 5 = 35
        // afterBos = 35 / 2 = 17, beforeEos = 35 - 17 = 18
        var ids = CreatePhonemeIds(5);

        // Act
        var result = ShortTextProcessor.PadPhonemeIds(ids, null);
        var paddedIds = result.PaddedIds;

        // Assert: count PadId (0) before and after body
        // Structure: [BOS, padding(afterBos), body..., padding(beforeEos), EOS]
        // BOS is at index 0, then afterBos PadIds, then body, then beforeEos PadIds, then EOS

        // Count consecutive PadIds after BOS
        var afterBos = 0;
        for (var i = 1; i < paddedIds.Length; i++)
        {
            if (paddedIds[i] == PadId)
                afterBos++;
            else
                break;
        }

        // Count consecutive PadIds before EOS (scanning backwards from end-1)
        var beforeEos = 0;
        for (var i = paddedIds.Length - 2; i >= 0; i--)
        {
            if (paddedIds[i] == PadId)
                beforeEos++;
            else
                break;
        }

        // The difference between afterBos and beforeEos should be at most 1
        Assert.LessOrEqual(Math.Abs(afterBos - beforeEos), 1,
            $"Padding distribution should be even (afterBos={afterBos}, beforeEos={beforeEos})");
        Assert.AreEqual(MinPhonemeIds, paddedIds.Length,
            "Total padded length should be MinPhonemeIds");
    }

    [Test]
    public void PadPhonemeIds_MinimalInput_BosEosOnly()
    {
        // Arrange: only BOS and EOS -> [1, 2], length 2
        var ids = new[] { 1, 2 };

        // Act
        var result = ShortTextProcessor.PadPhonemeIds(ids, null);

        // Assert
        Assert.AreEqual(MinPhonemeIds, result.PaddedIds.Length,
            "Padded result should have MinPhonemeIds elements");
        Assert.AreEqual(1, result.PaddedIds[0],
            "First element should be BOS (1)");
        Assert.AreEqual(2, result.PaddedIds[result.PaddedIds.Length - 1],
            "Last element should be EOS (2)");
    }

    // ================================================================
    // TrimSilence tests (NativeArray -- always Dispose in finally)
    // ================================================================

    [Test]
    public void TrimSilence_AllSilent_KeepsMinSamples()
    {
        // Arrange: 10000 samples of silence (all zeros)
        var audio = new NativeArray<float>(10000, Allocator.Temp);
        try
        {
            // Act
            var trimmed = ShortTextProcessor.TrimSilence(audio);
            try
            {
                // Assert
                Assert.GreaterOrEqual(trimmed.Length, TrimMinSamples,
                    "All-silent audio should keep at least TrimMinSamples");
            }
            finally
            {
                if (trimmed.IsCreated && trimmed != audio)
                    trimmed.Dispose();
            }
        }
        finally
        {
            if (audio.IsCreated)
                audio.Dispose();
        }
    }

    [Test]
    public void TrimSilence_NoSilence_ReturnsOriginal()
    {
        // Arrange: 5000 samples of constant non-zero value
        var audio = new NativeArray<float>(5000, Allocator.Temp);
        try
        {
            for (var i = 0; i < audio.Length; i++)
                audio[i] = 0.5f;

            // Act
            var trimmed = ShortTextProcessor.TrimSilence(audio);
            try
            {
                // Assert: no trimming needed, length unchanged
                Assert.AreEqual(5000, trimmed.Length,
                    "Non-silent audio should not be trimmed");
            }
            finally
            {
                if (trimmed.IsCreated && trimmed != audio)
                    trimmed.Dispose();
            }
        }
        finally
        {
            if (audio.IsCreated)
                audio.Dispose();
        }
    }

    [Test]
    public void TrimSilence_ShortAudio_ReturnsOriginal()
    {
        // Arrange: TrimMinSamples - 1 samples (too short to trim)
        var length = TrimMinSamples - 1;
        var audio = new NativeArray<float>(length, Allocator.Temp);
        try
        {
            // Act
            var trimmed = ShortTextProcessor.TrimSilence(audio);
            try
            {
                // Assert: short audio should not be modified
                Assert.AreEqual(length, trimmed.Length,
                    "Audio shorter than TrimMinSamples should not be trimmed");
            }
            finally
            {
                if (trimmed.IsCreated && trimmed != audio)
                    trimmed.Dispose();
            }
        }
        finally
        {
            if (audio.IsCreated)
                audio.Dispose();
        }
    }

    [Test]
    public void TrimSilence_LeadingAndTrailingSilence_Trims()
    {
        // Arrange: 1000 silence + 3000 sine wave + 1000 silence = 5000 total
        var total = 5000;
        var audio = new NativeArray<float>(total, Allocator.Temp);
        try
        {
            // Fill middle with sine wave (non-silent)
            for (var i = 1000; i < 4000; i++)
                audio[i] = (float)Math.Sin(2.0 * Math.PI * i / 100.0) * 0.8f;

            // Act
            var trimmed = ShortTextProcessor.TrimSilence(audio);
            try
            {
                // Assert: should be shorter than original
                Assert.Less(trimmed.Length, total,
                    "Audio with leading and trailing silence should be trimmed shorter");
            }
            finally
            {
                if (trimmed.IsCreated && trimmed != audio)
                    trimmed.Dispose();
            }
        }
        finally
        {
            if (audio.IsCreated)
                audio.Dispose();
        }
    }

    [Test]
    public void TrimSilence_VeryShortNonSilent_MaintainsMinSamples()
    {
        // Arrange: mostly silent with a single sample burst in the middle
        var total = 5000;
        var audio = new NativeArray<float>(total, Allocator.Temp);
        try
        {
            // Single loud sample in the middle
            audio[2500] = 1.0f;

            // Act
            var trimmed = ShortTextProcessor.TrimSilence(audio);
            try
            {
                // Assert: result should be at least TrimMinSamples
                Assert.GreaterOrEqual(trimmed.Length, TrimMinSamples,
                    "Even with minimal non-silent content, output should be at least TrimMinSamples");
            }
            finally
            {
                if (trimmed.IsCreated && trimmed != audio)
                    trimmed.Dispose();
            }
        }
        finally
        {
            if (audio.IsCreated)
                audio.Dispose();
        }
    }

    // ================================================================
    // AdjustScales tests
    // ================================================================

    [Test]
    public void AdjustScales_LongSequence_NoChange()
    {
        // Arrange: count at or above MinPhonemeIds -> no adjustment
        var noiseScale = 0.667f;
        var noiseW = 0.8f;

        // Act
        var (adjustedNoise, adjustedNoiseW) =
            ShortTextProcessor.AdjustScales(MinPhonemeIds, noiseScale, noiseW);

        // Assert
        Assert.AreEqual(0.667f, adjustedNoise, 1e-6f,
            "noiseScale should not change for sequences at MinPhonemeIds");
        Assert.AreEqual(0.8f, adjustedNoiseW, 1e-6f,
            "noiseW should not change for sequences at MinPhonemeIds");
    }

    [Test]
    public void AdjustScales_ShortSequence_ReducesScales()
    {
        // Arrange: count=10, well below MinPhonemeIds
        var noiseScale = 0.667f;
        var noiseW = 0.8f;

        // Act
        var (adjustedNoise, adjustedNoiseW) =
            ShortTextProcessor.AdjustScales(10, noiseScale, noiseW);

        // Assert
        Assert.Less(adjustedNoise, noiseScale,
            "noiseScale should be reduced for short sequences");
        Assert.Less(adjustedNoiseW, noiseW,
            "noiseW should be reduced for short sequences");
    }

    [Test]
    public void AdjustScales_VeryShort_FlooredAtMinRatio()
    {
        // Arrange: count=1, scales=1.0 -> ratio clamped to floor
        // Expected: noiseScale = 1.0 * 0.5 = 0.5, noiseW = 1.0 * 0.4 = 0.4
        // (floor ratios: noise=0.5, noiseW=0.4)
        var noiseScale = 1.0f;
        var noiseW = 1.0f;

        // Act
        var (adjustedNoise, adjustedNoiseW) =
            ShortTextProcessor.AdjustScales(1, noiseScale, noiseW);

        // Assert
        Assert.AreEqual(0.5f, adjustedNoise, 1e-6f,
            "noiseScale with count=1 should be floored at 0.5 * input");
        Assert.AreEqual(0.4f, adjustedNoiseW, 1e-6f,
            "noiseW with count=1 should be floored at 0.4 * input");
    }

    [Test]
    public void AdjustScales_HalfMinimum_CorrectRatio()
    {
        // Arrange: count = MinPhonemeIds / 2 = 20 -> ratio = 0.5
        var noiseScale = 0.667f;
        var noiseW = 0.8f;
        var halfMin = MinPhonemeIds / 2; // 20

        // Act
        var (adjustedNoise, adjustedNoiseW) =
            ShortTextProcessor.AdjustScales(halfMin, noiseScale, noiseW);

        // Assert: ratio = 20/40 = 0.5
        Assert.AreEqual(0.5f * noiseScale, adjustedNoise, 1e-5f,
            "noiseScale at half MinPhonemeIds should be 0.5 * original");
        Assert.AreEqual(0.5f * noiseW, adjustedNoiseW, 1e-5f,
            "noiseW at half MinPhonemeIds should be 0.5 * original");
    }

    [Test]
    public void AdjustScales_ZeroLength_UsesFloor()
    {
        // Arrange: count=0 -> ratio clamped to floor (0.5 for noise, 0.4 for noiseW)
        var noiseScale = 0.667f;
        var noiseW = 0.8f;

        // Act
        var (adjustedNoise, adjustedNoiseW) =
            ShortTextProcessor.AdjustScales(0, noiseScale, noiseW);

        // Assert
        Assert.AreEqual(0.667f * 0.5f, adjustedNoise, 1e-5f,
            "noiseScale with count=0 should use floor ratio 0.5");
        Assert.AreEqual(0.8f * 0.4f, adjustedNoiseW, 1e-5f,
            "noiseW with count=0 should use floor ratio 0.4");
    }

    // ================================================================
    // Null and edge case inputs
    // ================================================================

    [Test]
    public void NeedsPadding_NullInput_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ShortTextProcessor.NeedsPadding(null));
    }

    [Test]
    public void PadPhonemeIds_NullInput_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ShortTextProcessor.PadPhonemeIds(null, null));
    }

    [Test]
    public void PadPhonemeIds_SingleElement_ReturnsOriginal()
    {
        // BOS+EOS未満（Length < 2）は安全にスキップ
        var ids = new[] { 1 };
        var (padded, _) = ShortTextProcessor.PadPhonemeIds(ids, null);
        Assert.AreSame(ids, padded, "Length < 2 should return original array");
    }

    [Test]
    public void TrimSilence_UncreatedNativeArray_ReturnsDefault()
    {
        var audio = default(NativeArray<float>);
        var result = ShortTextProcessor.TrimSilence(audio);
        Assert.IsFalse(result.IsCreated, "Uncreated NativeArray should be returned as-is");
    }

    // ================================================================
    // AdjustScales edge cases
    // ================================================================

    [Test]
    public void AdjustScales_NegativeNoiseScale_ReturnsNegativeScaled()
    {
        // 負のnoiseScaleが渡された場合、ratioに基づいて縮小される（ガードはしない）
        var (ns, nw) = ShortTextProcessor.AdjustScales(10, -1.0f, 0.8f);
        Assert.Less(ns, 0f, "Negative noiseScale should remain negative after scaling");
    }

    [Test]
    public void AdjustScales_ZeroNoiseScale_ReturnsZero()
    {
        var (ns, nw) = ShortTextProcessor.AdjustScales(10, 0f, 0f);
        Assert.AreEqual(0f, ns, "Zero noiseScale should remain zero");
        Assert.AreEqual(0f, nw, "Zero noiseW should remain zero");
    }

    // ================================================================
    // Pipeline integration test
    // ================================================================

    [Test]
    public void PadAndTrim_ShortSequence_OutputReasonableLength()
    {
        // PadPhonemeIds → 擬似オーディオ生成 → TrimSilence のパイプライン
        var ids = new[] { 1, 10, 11, 12, 2 };
        var (padded, _) = ShortTextProcessor.PadPhonemeIds(ids, null);
        Assert.GreaterOrEqual(padded.Length, ShortTextProcessor.MinPhonemeIds);

        // パディング分の無音 + 有声部分のオーディオを模擬
        var sampleRate = 22050;
        var totalSamples = sampleRate * 2; // 2秒
        var audio = new NativeArray<float>(totalSamples, Allocator.Persistent);
        try
        {
            // 先頭0.5秒無音 + 1秒sin波 + 0.5秒無音
            var silenceStart = (int)(sampleRate * 0.5f);
            var signalEnd = (int)(sampleRate * 1.5f);
            for (var i = silenceStart; i < signalEnd; i++)
                audio[i] = Mathf.Sin(2f * Mathf.PI * 440f * i / sampleRate) * 0.5f;

            var trimmed = ShortTextProcessor.TrimSilence(audio);
            try
            {
                Assert.Greater(trimmed.Length, ShortTextProcessor.TrimMinSamples,
                    "Trimmed audio should be longer than minimum");
                Assert.Less(trimmed.Length, totalSamples,
                    "Trimmed audio should be shorter than original");
            }
            finally
            {
                if (trimmed.IsCreated && !audio.Equals(trimmed))
                    trimmed.Dispose();
            }
        }
        finally
        {
            if (audio.IsCreated)
                audio.Dispose();
        }
    }

    // ================================================================
    // Contract constants verification
    // ================================================================

    [Test]
    public void Constants_MatchPiperPlusContract()
    {
        // piper-plus short-text-contract.toml の値との一致を検証
        Assert.AreEqual(40, ShortTextProcessor.MinPhonemeIds, "min_phoneme_ids");
        Assert.AreEqual(0.01f, ShortTextProcessor.TrimThresholdRms, "threshold_rms");
        Assert.AreEqual(2205, ShortTextProcessor.TrimMinSamples, "min_samples");
        Assert.AreEqual(256, ShortTextProcessor.TrimWindowSize, "window_size");
        Assert.AreEqual(0, ShortTextProcessor.PadId, "pause_token_id");
        Assert.AreEqual(0.5f, ShortTextProcessor.NoiseScaleMinRatio, "noise_scale_min_ratio");
        Assert.AreEqual(0.4f, ShortTextProcessor.NoiseWMinRatio, "noise_w_min_ratio");
    }
}
