using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using uPiper.Core;
using uPiper.Core.AudioGeneration;
using uPiper.Core.Phonemizers.Multilingual;

namespace uPiper.Tests.Editor.AudioGeneration;

/// <summary>
/// Tests that multilingual model intersperse padding does not produce
/// triple-zero (PAD, PAD, PAD) sequences, which can cause inference artifacts.
/// Uses the same multilingual-test-medium phoneme_id_map subset as
/// PhonemeEncoderMultilingualModelTests.
/// </summary>
[TestFixture]
public class InterspersePaddingTests
{
    private PhonemeEncoder _encoder;
    private PuaTokenMapper _mapper;

    /// <summary>
    /// Build a mock multilingual phoneme_id_map containing all tokens
    /// required by the test phoneme sequences.
    /// Derived from multilingual-test-medium.onnx.json.
    /// </summary>
    private static Dictionary<string, int[]> BuildMultilingualModelMap()
    {
        return new Dictionary<string, int[]>
        {
            // ── Special tokens ──
            { "_", new[] { 0 } },   // PAD
            { "^", new[] { 1 } },   // BOS
            { "$", new[] { 2 } },   // EOS

            // ── Japanese vowels ──
            { "a", new[] { 10 } },
            { "i", new[] { 11 } },
            { "u", new[] { 12 } },
            { "e", new[] { 13 } },
            { "o", new[] { 14 } },

            // ── N variants (PUA) ──
            { "\ue01c", new[] { 29 } }, // N_uvular

            // ── Japanese consonants ──
            { "k", new[] { 32 } },
            { "n", new[] { 57 } },
            { "w", new[] { 63 } },

            // ── Japanese multi-char consonants (PUA) ──
            { "\ue00e", new[] { 46 } }, // ch

            // ── Punctuation ──
            { ",", new[] { 90 } },
        };
    }

    [SetUp]
    public void Setup()
    {
        _mapper = new PuaTokenMapper();

        var config = new PiperVoiceConfig
        {
            VoiceId = "multilingual-test-medium",
            PhonemeType = "multilingual",
            SampleRate = 22050,
            PhonemeIdMap = BuildMultilingualModelMap()
        };
        _encoder = new PhonemeEncoder(config, _mapper);
    }

    /// <summary>
    /// Japanese phoneme sequence with pause punctuation (comma) must not
    /// produce a triple-zero [0, 0, 0] pattern after intersperse PAD insertion.
    /// Input represents "こんにちは" with a comma: k o N_uvular , n i ch w a
    /// </summary>
    [Test]
    public void JapanesePausePunctuation_NoTripleZero()
    {
        // Arrange: "こん、にちは" — comma between mora groups
        // Multi-char phonemes use their PUA codepoints as the encoder expects.
        var phonemes = new[] { "k", "o", "\ue01c", ",", "n", "i", "\ue00e", "w", "a" };

        // Act
        var ids = _encoder.Encode(phonemes);

        // Assert: no [0, 0, 0] pattern anywhere in the result
        for (var i = 0; i < ids.Length - 2; i++)
        {
            Assert.IsFalse(
                ids[i] == 0 && ids[i + 1] == 0 && ids[i + 2] == 0,
                $"Triple-zero found at index {i} in [{string.Join(", ", ids)}]");
        }
    }

    /// <summary>
    /// Phoneme sequence with multiple consecutive pauses (commas) must not
    /// produce more than 2 consecutive zeros. The intersperse logic should
    /// guard against PAD stacking around pause tokens.
    /// </summary>
    [Test]
    public void MultiplePauses_MaxConsecutiveZeroIsTwo()
    {
        // Arrange: vowels separated by multiple commas
        var phonemes = new[] { "a", ",", "i", ",", "u" };

        // Act
        var ids = _encoder.Encode(phonemes);

        // Assert: max consecutive zeros <= 2
        var maxConsecutive = 0;
        var current = 0;
        foreach (var id in ids)
        {
            if (id == 0)
            {
                current++;
                maxConsecutive = Math.Max(maxConsecutive, current);
            }
            else
            {
                current = 0;
            }
        }

        Assert.LessOrEqual(maxConsecutive, 2,
            $"Consecutive zeros should not exceed 2, but found {maxConsecutive} in [{string.Join(", ", ids)}]");
    }

    /// <summary>
    /// Multilingual model encoding must produce the correct intersperse structure:
    /// - First element is BOS (ID 1)
    /// - BOS is immediately followed by PAD (ID 0)
    /// - Last element is EOS (ID 2)
    /// - PAD tokens are interspersed between phoneme IDs
    /// </summary>
    [Test]
    public void InterspersePattern_HasCorrectBosEosPadStructure()
    {
        // Arrange: simple 3-phoneme sequence
        var phonemes = new[] { "a", "k", "o" };

        // Act
        var ids = _encoder.Encode(phonemes);

        // Assert: structural invariants
        // Expected: BOS(1), PAD(0), a(10), PAD(0), k(32), PAD(0), o(14), PAD(0), EOS(2)
        Assert.AreEqual(1, ids[0],
            $"First element should be BOS (1), got {ids[0]} in [{string.Join(", ", ids)}]");

        Assert.AreEqual(0, ids[1],
            $"Second element should be PAD (0) after BOS, got {ids[1]} in [{string.Join(", ", ids)}]");

        Assert.AreEqual(2, ids[ids.Length - 1],
            $"Last element should be EOS (2), got {ids[ids.Length - 1]} in [{string.Join(", ", ids)}]");

        // Verify PAD is interspersed: odd indices (1, 3, 5, ...) before EOS should be PAD
        // Pattern: [BOS, PAD, ph1, PAD, ph2, PAD, ph3, PAD, EOS]
        //  index:    0    1    2    3    4    5    6    7    8
        for (var i = 1; i < ids.Length - 1; i += 2)
        {
            Assert.AreEqual(0, ids[i],
                $"Expected PAD (0) at odd index {i}, got {ids[i]} in [{string.Join(", ", ids)}]");
        }

        // Verify non-PAD phoneme IDs at even indices (2, 4, 6) between BOS and EOS
        for (var i = 2; i < ids.Length - 1; i += 2)
        {
            Assert.AreNotEqual(0, ids[i],
                $"Expected non-PAD phoneme at even index {i}, got 0 in [{string.Join(", ", ids)}]");
        }
    }
}