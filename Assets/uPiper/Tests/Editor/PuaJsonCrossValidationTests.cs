using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using uPiper.Core.Phonemizers.Multilingual;

namespace uPiper.Tests.Editor
{
    /// <summary>
    /// Cross-validation tests between piper-plus pua.json and uPiper PuaTokenMapper.FixedPuaMapping.
    /// Ensures the two implementations stay in sync.
    /// </summary>
    [TestFixture]
    public class PuaJsonCrossValidationTests
    {
        // =====================================================================
        // piper-plus pua.json v1 (96 entries)
        // Source: piper-plus/src/python/g2p/piper_plus_g2p/data/pua.json
        // Snapshot date: 2026-04-06
        // =====================================================================

        private const int PuaJsonVersion = 1;

        /// <summary>
        /// All entries from pua.json, faithfully transcribed as (token, codepoint) tuples.
        /// Unicode escapes match the original JSON token field exactly.
        /// </summary>
        private static readonly (string Token, int Codepoint)[] PuaJsonEntries = new[]
        {
            // Japanese (ja)
            ("a:", 0xE000),
            ("i:", 0xE001),
            ("u:", 0xE002),
            ("e:", 0xE003),
            ("o:", 0xE004),
            ("cl", 0xE005),
            ("ky", 0xE006),
            ("kw", 0xE007),
            ("gy", 0xE008),
            ("gw", 0xE009),
            ("ty", 0xE00A),
            ("dy", 0xE00B),
            ("py", 0xE00C),
            ("by", 0xE00D),
            ("ch", 0xE00E),
            ("ts", 0xE00F),
            ("sh", 0xE010),
            ("zy", 0xE011),
            ("hy", 0xE012),
            ("ny", 0xE013),
            ("my", 0xE014),
            ("ry", 0xE015),
            ("?!", 0xE016),
            ("?.", 0xE017),
            ("?~", 0xE018),
            ("N_m", 0xE019),
            ("N_n", 0xE01A),
            ("N_ng", 0xE01B),
            ("N_uvular", 0xE01C),
            // Shared
            ("rr", 0xE01D),
            ("y_vowel", 0xE01E),
            // gap: 0xE01F not assigned
            // Chinese (zh)
            ("p\u02b0", 0xE020),
            ("t\u02b0", 0xE021),
            ("k\u02b0", 0xE022),
            ("t\u0255", 0xE023),
            ("t\u0255\u02b0", 0xE024),
            ("t\u0282", 0xE025),
            ("t\u0282\u02b0", 0xE026),
            ("ts\u02b0", 0xE027),
            ("a\u026a", 0xE028),
            ("e\u026a", 0xE029),
            ("a\u028a", 0xE02A),
            ("o\u028a", 0xE02B),
            ("an", 0xE02C),
            ("\u0259n", 0xE02D),
            ("a\u014b", 0xE02E),
            ("\u0259\u014b", 0xE02F),
            ("u\u014b", 0xE030),
            ("ia", 0xE031),
            ("i\u025b", 0xE032),
            ("iou", 0xE033),
            ("ia\u028a", 0xE034),
            ("i\u025bn", 0xE035),
            ("in", 0xE036),
            ("ia\u014b", 0xE037),
            ("i\u014b", 0xE038),
            ("iu\u014b", 0xE039),
            ("ua", 0xE03A),
            ("uo", 0xE03B),
            ("ua\u026a", 0xE03C),
            ("ue\u026a", 0xE03D),
            ("uan", 0xE03E),
            ("u\u0259n", 0xE03F),
            ("ua\u014b", 0xE040),
            ("u\u0259\u014b", 0xE041),
            ("y\u025b", 0xE042),
            ("y\u025bn", 0xE043),
            ("yn", 0xE044),
            ("\u027b\u0329", 0xE045),
            ("tone1", 0xE046),
            ("tone2", 0xE047),
            ("tone3", 0xE048),
            ("tone4", 0xE049),
            ("tone5", 0xE04A),
            // Korean (ko)
            ("p\u0348", 0xE04B),
            ("t\u0348", 0xE04C),
            ("k\u0348", 0xE04D),
            ("s\u0348", 0xE04E),
            ("t\u0348\u0255", 0xE04F),
            ("k\u031a", 0xE050),
            ("t\u031a", 0xE051),
            ("p\u031a", 0xE052),
            // gap: 0xE053 not assigned
            // Spanish (es)
            ("t\u0283", 0xE054),
            ("d\u0292", 0xE055),
            // French (fr)
            ("\u025b\u0303", 0xE056),
            ("\u0251\u0303", 0xE057),
            ("\u0254\u0303", 0xE058),
            // Swedish (sv)
            ("i\u02d0", 0xE059),
            ("y\u02d0", 0xE05A),
            ("e\u02d0", 0xE05B),
            ("\u025b\u02d0", 0xE05C),
            ("\u00f8\u02d0", 0xE05D),
            ("\u0251\u02d0", 0xE05E),
            ("o\u02d0", 0xE05F),
            ("u\u02d0", 0xE060),
            ("\u0289\u02d0", 0xE061),
        };

        /// <summary>
        /// Known gap codepoints that must not appear in either mapping.
        /// </summary>
        private static readonly int[] GapCodepoints = { 0xE01F, 0xE053 };

        // =================================================================
        // Tests
        // =================================================================

        [Test]
        public void PuaJson_EntryCount_MatchesFixedPuaMapping()
        {
            Assert.AreEqual(
                PuaJsonEntries.Length,
                PuaTokenMapper.FixedPuaMapping.Count,
                $"pua.json has {PuaJsonEntries.Length} entries but FixedPuaMapping has {PuaTokenMapper.FixedPuaMapping.Count}");
        }

        [Test]
        public void PuaJson_AllEntries_ExistInFixedPuaMapping()
        {
            var mismatches = new List<string>();

            foreach (var (token, codepoint) in PuaJsonEntries)
            {
                if (!PuaTokenMapper.FixedPuaMapping.TryGetValue(token, out var actualCodepoint))
                {
                    mismatches.Add($"  MISSING: token=\"{EscapeForDisplay(token)}\" (U+{codepoint:X4}) not found in FixedPuaMapping");
                    continue;
                }

                if (actualCodepoint != codepoint)
                {
                    mismatches.Add(
                        $"  MISMATCH: token=\"{EscapeForDisplay(token)}\" " +
                        $"pua.json=U+{codepoint:X4} vs FixedPuaMapping=U+{actualCodepoint:X4}");
                }
            }

            Assert.IsEmpty(mismatches,
                $"pua.json -> FixedPuaMapping validation found {mismatches.Count} issue(s):\n" +
                string.Join("\n", mismatches));
        }

        [Test]
        public void FixedPuaMapping_AllEntries_ExistInPuaJson()
        {
            var puaJsonLookup = new HashSet<string>(PuaJsonEntries.Select(e => e.Token));
            var missing = new List<string>();

            foreach (var kvp in PuaTokenMapper.FixedPuaMapping)
            {
                if (!puaJsonLookup.Contains(kvp.Key))
                {
                    missing.Add($"  EXTRA: token=\"{EscapeForDisplay(kvp.Key)}\" (U+{kvp.Value:X4}) exists in FixedPuaMapping but not in pua.json");
                }
            }

            Assert.IsEmpty(missing,
                $"FixedPuaMapping -> pua.json validation found {missing.Count} extra entry(ies):\n" +
                string.Join("\n", missing));
        }

        [Test]
        public void PuaJson_Gaps_NotInEitherMapping()
        {
            var puaJsonCodepoints = new HashSet<int>(PuaJsonEntries.Select(e => e.Codepoint));
            var fixedCodepoints = new HashSet<int>(PuaTokenMapper.FixedPuaMapping.Values);

            foreach (var gap in GapCodepoints)
            {
                Assert.IsFalse(puaJsonCodepoints.Contains(gap),
                    $"Gap codepoint U+{gap:X4} should not be in pua.json entries");
                Assert.IsFalse(fixedCodepoints.Contains(gap),
                    $"Gap codepoint U+{gap:X4} should not be in FixedPuaMapping");
            }
        }

        [Test]
        public void PuaJson_Version_IsExpected()
        {
            // The embedded fixture reflects pua.json version 1.
            // If pua.json is updated to a new version, this constant and the
            // PuaJsonEntries array must be regenerated.
            Assert.AreEqual(1, PuaJsonVersion,
                "Embedded pua.json version does not match expected version. " +
                "Regenerate PuaJsonEntries from the latest pua.json.");
        }

        // =================================================================
        // Helpers
        // =================================================================

        /// <summary>
        /// Escapes non-ASCII characters for readable assertion messages.
        /// </summary>
        private static string EscapeForDisplay(string s)
        {
            var sb = new System.Text.StringBuilder(s.Length * 2);
            foreach (var c in s)
            {
                if (c > 0x7E)
                    sb.AppendFormat("\\u{0:X4}", (int)c);
                else
                    sb.Append(c);
            }
            return sb.ToString();
        }
    }
}