using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using uPiper.Core.Phonemizers.Multilingual;

namespace uPiper.Tests.Editor.Phonemizers
{
    [TestFixture]
    public class PuaTokenMapperTests
    {
        // ── Fixed PUA Mapping Verification ─────────────────────────────────

        [Test]
        public void FixedPuaMapping_ContainsAllJapaneseEntries()
        {
            var jaTokens = new[]
            {
                "a:", "i:", "u:", "e:", "o:",
                "cl",
                "ky", "kw", "gy", "gw", "ty", "dy", "py", "by",
                "ch", "ts", "sh", "zy", "hy",
                "ny", "my", "ry",
                "?!", "?.", "?~",
                "N_m", "N_n", "N_ng", "N_uvular"
            };

            foreach (var token in jaTokens)
            {
                Assert.IsTrue(
                    PuaTokenMapper.FixedPuaMapping.ContainsKey(token),
                    $"FixedPuaMapping should contain Japanese token '{token}'");
            }
        }

        [Test]
        public void FixedPuaMapping_ContainsAllChineseEntries()
        {
            var zhTokens = new[]
            {
                "p\u02B0", "t\u02B0", "k\u02B0",
                "t\u0255", "t\u0255\u02B0",
                "t\u0282", "t\u0282\u02B0",
                "ts\u02B0",
                "a\u026A", "e\u026A", "a\u028A", "o\u028A",
                "an", "\u0259n", "a\u014B", "\u0259\u014B", "u\u014B",
                "ia", "i\u025B", "iou", "ia\u028A",
                "i\u025Bn", "in", "ia\u014B", "i\u014B", "iu\u014B",
                "ua", "uo", "ua\u026A", "ue\u026A",
                "uan", "u\u0259n", "ua\u014B", "u\u0259\u014B",
                "y\u025B", "y\u025Bn", "yn",
                "\u027B\u0329",
                "tone1", "tone2", "tone3", "tone4", "tone5"
            };

            foreach (var token in zhTokens)
            {
                Assert.IsTrue(
                    PuaTokenMapper.FixedPuaMapping.ContainsKey(token),
                    $"FixedPuaMapping should contain Chinese token '{token}'");
            }
        }

        [Test]
        public void FixedPuaMapping_ContainsAllKoreanEntries()
        {
            var koTokens = new[]
            {
                "p\u0348", "t\u0348", "k\u0348", "s\u0348", "t\u0348\u0255",
                "k\u031A", "t\u031A", "p\u031A"
            };

            foreach (var token in koTokens)
            {
                Assert.IsTrue(
                    PuaTokenMapper.FixedPuaMapping.ContainsKey(token),
                    $"FixedPuaMapping should contain Korean token '{token}'");
            }
        }

        [Test]
        public void FixedPuaMapping_ContainsSpanishPortugueseEntries()
        {
            // tS (voiceless postalveolar affricate) and dZ (voiced postalveolar affricate)
            Assert.IsTrue(PuaTokenMapper.FixedPuaMapping.ContainsKey("t\u0283"),
                "Should contain ES/PT voiceless postalveolar affricate");
            Assert.IsTrue(PuaTokenMapper.FixedPuaMapping.ContainsKey("d\u0292"),
                "Should contain ES/PT voiced postalveolar affricate");
        }

        [Test]
        public void FixedPuaMapping_ContainsFrenchEntries()
        {
            // Nasal vowels
            Assert.IsTrue(PuaTokenMapper.FixedPuaMapping.ContainsKey("\u025B\u0303"),
                "Should contain French nasal open-mid front unrounded");
            Assert.IsTrue(PuaTokenMapper.FixedPuaMapping.ContainsKey("\u0251\u0303"),
                "Should contain French nasal open back unrounded");
            Assert.IsTrue(PuaTokenMapper.FixedPuaMapping.ContainsKey("\u0254\u0303"),
                "Should contain French nasal open-mid back rounded");
        }

        [Test]
        public void FixedPuaMapping_ContainsSharedEntries()
        {
            Assert.IsTrue(PuaTokenMapper.FixedPuaMapping.ContainsKey("rr"),
                "Should contain shared Spanish trill 'rr'");
            Assert.IsTrue(PuaTokenMapper.FixedPuaMapping.ContainsKey("y_vowel"),
                "Should contain shared close front rounded vowel 'y_vowel'");
        }

        [Test]
        public void FixedPuaMapping_ContainsAllSwedishEntries()
        {
            var svTokens = new[]
            {
                "i\u02D0", "y\u02D0", "e\u02D0",
                "\u025B\u02D0", "\u00F8\u02D0", "\u0251\u02D0",
                "o\u02D0", "u\u02D0", "\u0289\u02D0"
            };

            foreach (var token in svTokens)
            {
                Assert.IsTrue(
                    PuaTokenMapper.FixedPuaMapping.ContainsKey(token),
                    $"FixedPuaMapping should contain Swedish token '{token}'");
            }
        }

        [Test]
        public void FixedPuaMapping_DoesNotContain0xE053()
        {
            // 0xE053 is a reserved gap in piper-plus (not assigned to any token)
            var hasE053 = PuaTokenMapper.FixedPuaMapping.Values.Any(v => v == 0xE053);
            Assert.IsFalse(hasE053,
                "0xE053 should be a reserved gap (not assigned to any token)");
        }

        [Test]
        public void FixedPuaMapping_TotalCount_Is96()
        {
            Assert.AreEqual(96, PuaTokenMapper.FixedPuaMapping.Count,
                "FixedPuaMapping should contain exactly 96 entries (matching piper-plus pua.json)");
        }

        [Test]
        public void FixedPuaMapping_NoDuplicateCodepoints()
        {
            var codepoints = PuaTokenMapper.FixedPuaMapping.Values.ToList();
            var uniqueCodepoints = new HashSet<int>(codepoints);

            Assert.AreEqual(codepoints.Count, uniqueCodepoints.Count,
                "All PUA codepoints in FixedPuaMapping must be unique");
        }

        // ── Token2Char / Char2Token ────────────────────────────────────────

        [Test]
        public void Token2Char_ContainsAllFixedMappings()
        {
            foreach (var kvp in PuaTokenMapper.FixedPuaMapping)
            {
                Assert.IsTrue(PuaTokenMapper.Token2Char.ContainsKey(kvp.Key),
                    $"Token2Char should contain fixed token '{kvp.Key}'");
                Assert.AreEqual((char)kvp.Value, PuaTokenMapper.Token2Char[kvp.Key],
                    $"Token2Char['{kvp.Key}'] should map to 0x{kvp.Value:X4}");
            }
        }

        [Test]
        public void Char2Token_ContainsAllFixedMappings()
        {
            foreach (var kvp in PuaTokenMapper.FixedPuaMapping)
            {
                var ch = (char)kvp.Value;
                Assert.IsTrue(PuaTokenMapper.Char2Token.ContainsKey(ch),
                    $"Char2Token should contain PUA char 0x{kvp.Value:X4}");
                Assert.AreEqual(kvp.Key, PuaTokenMapper.Char2Token[ch],
                    $"Char2Token[0x{kvp.Value:X4}] should map to '{kvp.Key}'");
            }
        }

        [Test]
        public void Token2Char_And_Char2Token_AreConsistent()
        {
            foreach (var kvp in PuaTokenMapper.Token2Char)
            {
                var token = kvp.Key;
                var ch = kvp.Value;

                Assert.IsTrue(PuaTokenMapper.Char2Token.ContainsKey(ch),
                    $"Char2Token should contain char mapped from token '{token}'");
                Assert.AreEqual(token, PuaTokenMapper.Char2Token[ch],
                    $"Roundtrip failed: Token2Char['{token}'] = 0x{(int)ch:X4}, " +
                    $"but Char2Token[0x{(int)ch:X4}] = '{PuaTokenMapper.Char2Token[ch]}'");
            }
        }

        // ── MapToken ───────────────────────────────────────────────────────

        [TestCase("a:", '\uE000')]
        [TestCase("cl", '\uE005')]
        [TestCase("ch", '\uE00E')]
        [TestCase("sh", '\uE010')]
        [TestCase("rr", '\uE01D')]
        [TestCase("tone1", '\uE046')]
        public void MapToken_FixedToken_ReturnsCorrectChar(string token, char expected)
        {
            var result = PuaTokenMapper.MapToken(token);
            Assert.AreEqual(expected, result,
                $"MapToken('{token}') should return 0x{(int)expected:X4} but got 0x{(int)result:X4}");
        }

        [TestCase("a")]
        [TestCase("k")]
        [TestCase("o")]
        [TestCase("n")]
        public void MapToken_SingleCharToken_ReturnsSelf(string token)
        {
            var result = PuaTokenMapper.MapToken(token);
            Assert.AreEqual(token[0], result,
                $"Single-char token '{token}' should map to itself");
        }

        [Test]
        public void MapToken_UnknownMultiCharToken_RegistersDynamically()
        {
            var token = "_test_dynamic_maptoken_unique_001";
            var result = PuaTokenMapper.MapToken(token);

            // Should be in the dynamic PUA range (>= 0xE062)
            Assert.GreaterOrEqual((int)result, 0xE062,
                "Dynamically registered token should be in the dynamic PUA range");

            // Should be retrievable via Token2Char
            Assert.IsTrue(PuaTokenMapper.Token2Char.ContainsKey(token),
                "Dynamically registered token should appear in Token2Char");
            Assert.AreEqual(result, PuaTokenMapper.Token2Char[token]);
        }

        [Test]
        public void MapToken_SameTokenTwice_ReturnsSameChar()
        {
            var token = "_test_idempotent_unique_002";
            var first = PuaTokenMapper.MapToken(token);
            var second = PuaTokenMapper.MapToken(token);

            Assert.AreEqual(first, second,
                "Calling MapToken twice with the same token should return the same char");
        }

        // ── MapSequence ────────────────────────────────────────────────────

        [Test]
        public void MapSequence_EmptyList_ReturnsEmpty()
        {
            var result = PuaTokenMapper.MapSequence(new List<string>());

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count, "MapSequence on empty list should return empty list");
        }

        [Test]
        public void MapSequence_SingleTokens_ReturnsSelf()
        {
            var tokens = new List<string> { "a", "b", "c" };
            var result = PuaTokenMapper.MapSequence(tokens);

            Assert.AreEqual(3, result.Count);
            Assert.AreEqual('a', result[0]);
            Assert.AreEqual('b', result[1]);
            Assert.AreEqual('c', result[2]);
        }

        [Test]
        public void MapSequence_MultiCharTokens_ReturnsPuaChars()
        {
            var tokens = new List<string> { "ch", "ts", "sh" };
            var result = PuaTokenMapper.MapSequence(tokens);

            Assert.AreEqual(3, result.Count);
            Assert.AreEqual('\uE00E', result[0], "ch should map to 0xE00E");
            Assert.AreEqual('\uE00F', result[1], "ts should map to 0xE00F");
            Assert.AreEqual('\uE010', result[2], "sh should map to 0xE010");
        }

        [Test]
        public void MapSequence_MixedTokens_CorrectMapping()
        {
            var tokens = new List<string> { "k", "a:", "ch", "i" };
            var result = PuaTokenMapper.MapSequence(tokens);

            Assert.AreEqual(4, result.Count);
            Assert.AreEqual('k', result[0], "Single char 'k' should map to itself");
            Assert.AreEqual('\uE000', result[1], "'a:' should map to 0xE000");
            Assert.AreEqual('\uE00E', result[2], "'ch' should map to 0xE00E");
            Assert.AreEqual('i', result[3], "Single char 'i' should map to itself");
        }

        // ── UnmapChar ──────────────────────────────────────────────────────

        [TestCase('\uE000', "a:")]
        [TestCase('\uE005', "cl")]
        [TestCase('\uE00E', "ch")]
        [TestCase('\uE046', "tone1")]
        [TestCase('\uE058', "\u0254\u0303")]
        public void UnmapChar_FixedPuaChar_ReturnsToken(char ch, string expectedToken)
        {
            var result = PuaTokenMapper.UnmapChar(ch);

            Assert.IsNotNull(result, $"UnmapChar(0x{(int)ch:X4}) should not return null");
            Assert.AreEqual(expectedToken, result,
                $"UnmapChar(0x{(int)ch:X4}) should return '{expectedToken}'");
        }

        [Test]
        public void UnmapChar_NonPuaChar_ReturnsNull()
        {
            // Characters unlikely to be registered
            var result = PuaTokenMapper.UnmapChar('\u0001');

            Assert.IsNull(result,
                "UnmapChar for an unregistered character should return null");
        }

        [Test]
        public void UnmapChar_DynamicallyRegistered_ReturnsToken()
        {
            var token = "_test_unmap_dynamic_unique_003";
            var ch = PuaTokenMapper.Register(token);
            var result = PuaTokenMapper.UnmapChar(ch);

            Assert.IsNotNull(result, "UnmapChar should find dynamically registered char");
            Assert.AreEqual(token, result,
                "UnmapChar should return the original dynamically registered token");
        }

        // ── IsFixedPua ─────────────────────────────────────────────────────

        [TestCase('\uE000', true)]
        [TestCase('\uE02A', true)]
        [TestCase('\uE058', true)]
        [TestCase('\uE059', true)]    // SV PUA start
        [TestCase('\uE061', true)]    // SV last entry
        public void IsFixedPua_InRange_ReturnsTrue(char ch, bool expected)
        {
            Assert.AreEqual(expected, PuaTokenMapper.IsFixedPua(ch),
                $"IsFixedPua(0x{(int)ch:X4}) should return {expected}");
        }

        [TestCase('\uDFFF', false)]
        [TestCase('\uE062', false)]   // first dynamic PUA
        [TestCase('\uE100', false)]
        [TestCase('a', false)]
        [TestCase('\u0000', false)]
        public void IsFixedPua_OutOfRange_ReturnsFalse(char ch, bool expected)
        {
            Assert.AreEqual(expected, PuaTokenMapper.IsFixedPua(ch),
                $"IsFixedPua(0x{(int)ch:X4}) should return {expected}");
        }

        [Test]
        public void IsFixedPua_BoundaryValues()
        {
            // Lower boundary
            Assert.IsTrue(PuaTokenMapper.IsFixedPua('\uE000'),
                "0xE000 (first fixed PUA) should be in range");
            Assert.IsFalse(PuaTokenMapper.IsFixedPua('\uDFFF'),
                "0xDFFF (one below first) should NOT be in range");

            // Upper boundary
            Assert.IsTrue(PuaTokenMapper.IsFixedPua('\uE061'),
                "0xE061 (last fixed PUA) should be in range");
            Assert.IsFalse(PuaTokenMapper.IsFixedPua('\uE062'),
                "0xE062 (first dynamic PUA) should NOT be in range");
        }

        // ── Thread Safety ──────────────────────────────────────────────────

        [Test]
        public void Register_ConcurrentCalls_NoDuplicates()
        {
            const int threadCount = 8;
            const int tokensPerThread = 50;
            var allChars = new List<char>[threadCount];

            var tasks = new Task[threadCount];
            for (var t = 0; t < threadCount; t++)
            {
                var threadIndex = t;
                allChars[threadIndex] = new List<char>(tokensPerThread);
                tasks[t] = Task.Run(() =>
                {
                    for (var i = 0; i < tokensPerThread; i++)
                    {
                        var token = $"_concurrent_{threadIndex}_{i}";
                        var ch = PuaTokenMapper.Register(token);
                        allChars[threadIndex].Add(ch);
                    }
                });
            }

            Task.WaitAll(tasks);

            // Flatten and verify uniqueness
            var flatChars = allChars.SelectMany(list => list).ToList();
            var uniqueChars = new HashSet<char>(flatChars);

            Assert.AreEqual(threadCount * tokensPerThread, flatChars.Count,
                "Should have registered all tokens");
            Assert.AreEqual(flatChars.Count, uniqueChars.Count,
                "All dynamically allocated codepoints should be unique across threads");

            // Verify each mapping is consistent in both directions
            for (var t = 0; t < threadCount; t++)
            {
                for (var i = 0; i < tokensPerThread; i++)
                {
                    var token = $"_concurrent_{t}_{i}";
                    var ch = allChars[t][i];

                    Assert.IsTrue(PuaTokenMapper.Token2Char.ContainsKey(token),
                        $"Token2Char should contain concurrently registered token '{token}'");
                    Assert.AreEqual(ch, PuaTokenMapper.Token2Char[token]);
                    Assert.AreEqual(token, PuaTokenMapper.Char2Token[ch]);
                }
            }
        }

        // ── Specific PUA Value Verification ────────────────────────────────

        [Test]
        public void VerifyJapanesePuaValues()
        {
            // Long vowels
            Assert.AreEqual(0xE000, PuaTokenMapper.FixedPuaMapping["a:"], "a: -> 0xE000");
            Assert.AreEqual(0xE001, PuaTokenMapper.FixedPuaMapping["i:"], "i: -> 0xE001");
            Assert.AreEqual(0xE002, PuaTokenMapper.FixedPuaMapping["u:"], "u: -> 0xE002");
            Assert.AreEqual(0xE003, PuaTokenMapper.FixedPuaMapping["e:"], "e: -> 0xE003");
            Assert.AreEqual(0xE004, PuaTokenMapper.FixedPuaMapping["o:"], "o: -> 0xE004");

            // Special consonants
            Assert.AreEqual(0xE005, PuaTokenMapper.FixedPuaMapping["cl"], "cl -> 0xE005");

            // Affricates
            Assert.AreEqual(0xE00E, PuaTokenMapper.FixedPuaMapping["ch"], "ch -> 0xE00E");
            Assert.AreEqual(0xE00F, PuaTokenMapper.FixedPuaMapping["ts"], "ts -> 0xE00F");
            Assert.AreEqual(0xE010, PuaTokenMapper.FixedPuaMapping["sh"], "sh -> 0xE010");

            // Palatalized consonants
            Assert.AreEqual(0xE006, PuaTokenMapper.FixedPuaMapping["ky"], "ky -> 0xE006");
            Assert.AreEqual(0xE008, PuaTokenMapper.FixedPuaMapping["gy"], "gy -> 0xE008");
            Assert.AreEqual(0xE013, PuaTokenMapper.FixedPuaMapping["ny"], "ny -> 0xE013");

            // Question markers
            Assert.AreEqual(0xE016, PuaTokenMapper.FixedPuaMapping["?!"], "?! -> 0xE016");
            Assert.AreEqual(0xE017, PuaTokenMapper.FixedPuaMapping["?."], "?. -> 0xE017");
            Assert.AreEqual(0xE018, PuaTokenMapper.FixedPuaMapping["?~"], "?~ -> 0xE018");

            // N phoneme variants
            Assert.AreEqual(0xE019, PuaTokenMapper.FixedPuaMapping["N_m"], "N_m -> 0xE019");
            Assert.AreEqual(0xE01A, PuaTokenMapper.FixedPuaMapping["N_n"], "N_n -> 0xE01A");
            Assert.AreEqual(0xE01B, PuaTokenMapper.FixedPuaMapping["N_ng"], "N_ng -> 0xE01B");
            Assert.AreEqual(0xE01C, PuaTokenMapper.FixedPuaMapping["N_uvular"], "N_uvular -> 0xE01C");
        }

        [Test]
        public void VerifyChinesePuaValues()
        {
            // Aspirated initials
            Assert.AreEqual(0xE020, PuaTokenMapper.FixedPuaMapping["p\u02B0"], "ph -> 0xE020");
            Assert.AreEqual(0xE021, PuaTokenMapper.FixedPuaMapping["t\u02B0"], "th -> 0xE021");
            Assert.AreEqual(0xE022, PuaTokenMapper.FixedPuaMapping["k\u02B0"], "kh -> 0xE022");

            // Affricate initials
            Assert.AreEqual(0xE023, PuaTokenMapper.FixedPuaMapping["t\u0255"], "tc -> 0xE023");
            Assert.AreEqual(0xE024, PuaTokenMapper.FixedPuaMapping["t\u0255\u02B0"], "tch -> 0xE024");
            Assert.AreEqual(0xE025, PuaTokenMapper.FixedPuaMapping["t\u0282"], "ts(retroflex) -> 0xE025");
            Assert.AreEqual(0xE027, PuaTokenMapper.FixedPuaMapping["ts\u02B0"], "tsh(alveolar) -> 0xE027");

            // Diphthongs
            Assert.AreEqual(0xE028, PuaTokenMapper.FixedPuaMapping["a\u026A"], "ai -> 0xE028");
            Assert.AreEqual(0xE029, PuaTokenMapper.FixedPuaMapping["e\u026A"], "ei -> 0xE029");

            // Nasal finals
            Assert.AreEqual(0xE02C, PuaTokenMapper.FixedPuaMapping["an"], "an -> 0xE02C");

            // Tone markers
            Assert.AreEqual(0xE046, PuaTokenMapper.FixedPuaMapping["tone1"], "tone1 -> 0xE046");
            Assert.AreEqual(0xE047, PuaTokenMapper.FixedPuaMapping["tone2"], "tone2 -> 0xE047");
            Assert.AreEqual(0xE048, PuaTokenMapper.FixedPuaMapping["tone3"], "tone3 -> 0xE048");
            Assert.AreEqual(0xE049, PuaTokenMapper.FixedPuaMapping["tone4"], "tone4 -> 0xE049");
            Assert.AreEqual(0xE04A, PuaTokenMapper.FixedPuaMapping["tone5"], "tone5 -> 0xE04A");

            // Syllabic consonant
            Assert.AreEqual(0xE045, PuaTokenMapper.FixedPuaMapping["\u027B\u0329"],
                "syllabic retroflex -> 0xE045");
        }

        [Test]
        public void VerifyKoreanPuaValues()
        {
            // Tense consonants (fortis)
            Assert.AreEqual(0xE04B, PuaTokenMapper.FixedPuaMapping["p\u0348"], "tense p -> 0xE04B");
            Assert.AreEqual(0xE04C, PuaTokenMapper.FixedPuaMapping["t\u0348"], "tense t -> 0xE04C");
            Assert.AreEqual(0xE04D, PuaTokenMapper.FixedPuaMapping["k\u0348"], "tense k -> 0xE04D");
            Assert.AreEqual(0xE04E, PuaTokenMapper.FixedPuaMapping["s\u0348"], "tense s -> 0xE04E");
            Assert.AreEqual(0xE04F, PuaTokenMapper.FixedPuaMapping["t\u0348\u0255"],
                "tense tc -> 0xE04F");

            // Unreleased finals
            Assert.AreEqual(0xE050, PuaTokenMapper.FixedPuaMapping["k\u031A"], "unreleased k -> 0xE050");
            Assert.AreEqual(0xE051, PuaTokenMapper.FixedPuaMapping["t\u031A"], "unreleased t -> 0xE051");
            Assert.AreEqual(0xE052, PuaTokenMapper.FixedPuaMapping["p\u031A"], "unreleased p -> 0xE052");
        }

        [Test]
        public void VerifySwedishPuaValues()
        {
            // Long vowels
            Assert.AreEqual(0xE059, PuaTokenMapper.FixedPuaMapping["i\u02D0"], "iː -> 0xE059");
            Assert.AreEqual(0xE05A, PuaTokenMapper.FixedPuaMapping["y\u02D0"], "yː -> 0xE05A");
            Assert.AreEqual(0xE05B, PuaTokenMapper.FixedPuaMapping["e\u02D0"], "eː -> 0xE05B");
            Assert.AreEqual(0xE05C, PuaTokenMapper.FixedPuaMapping["\u025B\u02D0"], "ɛː -> 0xE05C");
            Assert.AreEqual(0xE05D, PuaTokenMapper.FixedPuaMapping["\u00F8\u02D0"], "øː -> 0xE05D");
            Assert.AreEqual(0xE05E, PuaTokenMapper.FixedPuaMapping["\u0251\u02D0"], "ɑː -> 0xE05E");
            Assert.AreEqual(0xE05F, PuaTokenMapper.FixedPuaMapping["o\u02D0"], "oː -> 0xE05F");
            Assert.AreEqual(0xE060, PuaTokenMapper.FixedPuaMapping["u\u02D0"], "uː -> 0xE060");
            Assert.AreEqual(0xE061, PuaTokenMapper.FixedPuaMapping["\u0289\u02D0"], "ʉː -> 0xE061");
        }
    }
}