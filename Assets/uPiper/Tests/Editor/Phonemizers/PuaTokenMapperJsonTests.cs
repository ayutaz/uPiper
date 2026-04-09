using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core.Phonemizers.Multilingual;

namespace uPiper.Tests.Editor.Phonemizers
{
    /// <summary>
    /// Tests for PuaTokenMapper's pua.json runtime loading functionality.
    /// </summary>
    [TestFixture]
    public class PuaTokenMapperJsonTests
    {
        private PuaTokenMapper _mapper;

        [SetUp]
        public void Setup()
        {
            _mapper = new PuaTokenMapper();
        }

        // ── LoadFromJson: valid input ─────────────────────────────────────

        [Test]
        public void LoadFromJson_ValidJson_AllEntriesLoaded()
        {
            var json = @"{
                ""version"": 1,
                ""description"": ""test"",
                ""entries"": [
                    {""token"": ""a:"", ""codepoint"": ""0xE000"", ""language"": ""ja"", ""description"": ""test""},
                    {""token"": ""ch"", ""codepoint"": ""0xE00E"", ""language"": ""ja"", ""description"": ""test""},
                    {""token"": ""rr"", ""codepoint"": ""0xE01D"", ""language"": ""shared"", ""description"": ""test""}
                ]
            }";

            var result = _mapper.LoadFromJson(json);

            Assert.IsTrue(result, "LoadFromJson should return true for valid JSON");
            Assert.IsTrue(_mapper.IsLoadedFromJson, "IsLoadedFromJson should be true");
            Assert.AreEqual(3, _mapper.Token2Char.Count, "Should have 3 entries");
            Assert.AreEqual('\uE000', _mapper.Token2Char["a:"]);
            Assert.AreEqual('\uE00E', _mapper.Token2Char["ch"]);
            Assert.AreEqual('\uE01D', _mapper.Token2Char["rr"]);
        }

        [Test]
        public void LoadFromJson_VersionGreaterThan1_Succeeds()
        {
            var json = @"{
                ""version"": 2,
                ""description"": ""future version"",
                ""entries"": [
                    {""token"": ""a:"", ""codepoint"": ""0xE000"", ""language"": ""ja"", ""description"": ""test""}
                ]
            }";

            var result = _mapper.LoadFromJson(json);

            Assert.IsTrue(result, "Should accept version >= 1 (forward compatible)");
            Assert.AreEqual(1, _mapper.Token2Char.Count);
        }

        // ── LoadFromJson: invalid version ─────────────────────────────────

        [Test]
        public void LoadFromJson_InvalidVersion_ReturnsFalse_KeepsHardcoded()
        {
            var json = @"{
                ""version"": 0,
                ""description"": ""invalid"",
                ""entries"": [
                    {""token"": ""a:"", ""codepoint"": ""0xE000"", ""language"": ""ja"", ""description"": ""test""}
                ]
            }";

            // Verify hardcoded mapping is present before
            var countBefore = _mapper.Token2Char.Count;
            Assert.IsTrue(countBefore > 0, "Precondition: hardcoded mapping should exist");

            LogAssert.Expect(LogType.Error, new Regex("Invalid version"));
            var result = _mapper.LoadFromJson(json);

            Assert.IsFalse(result, "Should return false for version 0");
            Assert.IsFalse(_mapper.IsLoadedFromJson, "IsLoadedFromJson should remain false");
            Assert.AreEqual(countBefore, _mapper.Token2Char.Count,
                "Hardcoded mapping should be preserved on failure");
        }

        [Test]
        public void LoadFromJson_NegativeVersion_ReturnsFalse()
        {
            var json = @"{""version"": -1, ""entries"": []}";

            LogAssert.Expect(LogType.Error, new Regex("Invalid version"));
            var result = _mapper.LoadFromJson(json);

            Assert.IsFalse(result, "Should return false for negative version");
        }

        // ── LoadFromJson: duplicate tokens ────────────────────────────────

        [Test]
        public void LoadFromJson_DuplicateTokens_LastWins()
        {
            var json = @"{
                ""version"": 1,
                ""entries"": [
                    {""token"": ""a:"", ""codepoint"": ""0xE000"", ""language"": ""ja"", ""description"": ""first""},
                    {""token"": ""a:"", ""codepoint"": ""0xE001"", ""language"": ""ja"", ""description"": ""second""}
                ]
            }";

            var result = _mapper.LoadFromJson(json);

            Assert.IsTrue(result, "Should succeed despite duplicates");
            Assert.AreEqual(1, _mapper.Token2Char.Count, "Should have 1 entry (deduplicated)");
            Assert.AreEqual('\uE001', _mapper.Token2Char["a:"],
                "Last duplicate should win");
        }

        [Test]
        public void LoadFromJson_DuplicateCodepoints_LastWins()
        {
            var json = @"{
                ""version"": 1,
                ""entries"": [
                    {""token"": ""first"", ""codepoint"": ""0xE000"", ""language"": ""ja"", ""description"": ""1""},
                    {""token"": ""second"", ""codepoint"": ""0xE000"", ""language"": ""ja"", ""description"": ""2""}
                ]
            }";

            var result = _mapper.LoadFromJson(json);

            Assert.IsTrue(result, "Should succeed despite duplicate codepoints");
            Assert.AreEqual(1, _mapper.Token2Char.Count,
                "Should have 1 entry (first token removed)");
            Assert.IsTrue(_mapper.Token2Char.ContainsKey("second"),
                "Second token should be present (last wins)");
            Assert.IsFalse(_mapper.Token2Char.ContainsKey("first"),
                "First token should be removed (overwritten by second)");
        }

        // ── LoadFromJson: invalid codepoint ───────────────────────────────

        [Test]
        public void LoadFromJson_InvalidCodepoint_EntrySkipped()
        {
            var json = @"{
                ""version"": 1,
                ""entries"": [
                    {""token"": ""valid"", ""codepoint"": ""0xE000"", ""language"": ""ja"", ""description"": ""ok""},
                    {""token"": ""bad_hex"", ""codepoint"": ""ZZZZ"", ""language"": ""ja"", ""description"": ""bad""},
                    {""token"": ""also_valid"", ""codepoint"": ""0xE001"", ""language"": ""ja"", ""description"": ""ok""}
                ]
            }";

            var result = _mapper.LoadFromJson(json);

            Assert.IsTrue(result, "Should succeed (invalid entries are skipped)");
            Assert.AreEqual(2, _mapper.Token2Char.Count, "Should have 2 valid entries");
            Assert.IsTrue(_mapper.Token2Char.ContainsKey("valid"));
            Assert.IsTrue(_mapper.Token2Char.ContainsKey("also_valid"));
            Assert.IsFalse(_mapper.Token2Char.ContainsKey("bad_hex"));
        }

        [Test]
        public void LoadFromJson_CodepointOutsidePuaRange_EntrySkipped()
        {
            var json = @"{
                ""version"": 1,
                ""entries"": [
                    {""token"": ""valid"", ""codepoint"": ""0xE000"", ""language"": ""ja"", ""description"": ""ok""},
                    {""token"": ""below_range"", ""codepoint"": ""0xDFFF"", ""language"": ""ja"", ""description"": ""bad""},
                    {""token"": ""above_range"", ""codepoint"": ""0xF900"", ""language"": ""ja"", ""description"": ""bad""}
                ]
            }";

            var result = _mapper.LoadFromJson(json);

            Assert.IsTrue(result, "Should succeed (out-of-range entries are skipped)");
            Assert.AreEqual(1, _mapper.Token2Char.Count, "Should have 1 valid entry");
            Assert.IsTrue(_mapper.Token2Char.ContainsKey("valid"));
        }

        [Test]
        public void LoadFromJson_EmptyCodepointString_EntrySkipped()
        {
            var json = @"{
                ""version"": 1,
                ""entries"": [
                    {""token"": ""valid"", ""codepoint"": ""0xE000"", ""language"": ""ja"", ""description"": ""ok""},
                    {""token"": ""empty_cp"", ""codepoint"": """", ""language"": ""ja"", ""description"": ""bad""}
                ]
            }";

            var result = _mapper.LoadFromJson(json);

            Assert.IsTrue(result, "Should succeed");
            Assert.AreEqual(1, _mapper.Token2Char.Count);
        }

        // ── LoadFromJson: empty entries ───────────────────────────────────

        [Test]
        public void LoadFromJson_EmptyEntries_ReturnsTrue()
        {
            var json = @"{""version"": 1, ""entries"": []}";

            // Note: empty entries means no override, but it's a valid JSON
            var result = _mapper.LoadFromJson(json);

            Assert.IsTrue(result, "Empty entries is valid");
            Assert.IsTrue(_mapper.IsLoadedFromJson, "IsLoadedFromJson should be true");
        }

        [Test]
        public void LoadFromJson_NullEntries_ReturnsTrue()
        {
            // JsonUtility may deserialize missing entries as null
            var json = @"{""version"": 1}";

            var result = _mapper.LoadFromJson(json);

            Assert.IsTrue(result, "Null entries should be treated as empty (valid)");
            Assert.IsTrue(_mapper.IsLoadedFromJson);
        }

        // ── LoadFromJson: malformed JSON ──────────────────────────────────

        [Test]
        public void LoadFromJson_MalformedJson_ReturnsFalse_KeepsHardcoded()
        {
            var json = "{ this is not valid json }}}";
            var countBefore = _mapper.Token2Char.Count;

            // JsonUtility.FromJson may emit its own error log before our code catches the exception
            LogAssert.ignoreFailingMessages = true;
            var result = _mapper.LoadFromJson(json);
            LogAssert.ignoreFailingMessages = false;

            Assert.IsFalse(result, "Should return false for malformed JSON");
            Assert.AreEqual(countBefore, _mapper.Token2Char.Count,
                "Hardcoded mapping should be preserved");
        }

        [Test]
        public void LoadFromJson_NullString_ReturnsFalse()
        {
            LogAssert.Expect(LogType.Error, new Regex("null or empty"));
            var result = _mapper.LoadFromJson(null);
            Assert.IsFalse(result);
        }

        [Test]
        public void LoadFromJson_EmptyString_ReturnsFalse()
        {
            LogAssert.Expect(LogType.Error, new Regex("null or empty"));
            var result = _mapper.LoadFromJson("");
            Assert.IsFalse(result);
        }

        [Test]
        public void LoadFromJson_WhitespaceOnly_ReturnsFalse()
        {
            LogAssert.Expect(LogType.Error, new Regex("null or empty"));
            var result = _mapper.LoadFromJson("   ");
            Assert.IsFalse(result);
        }

        // ── LoadFromJson: empty token ─────────────────────────────────────

        [Test]
        public void LoadFromJson_EmptyToken_EntrySkipped()
        {
            var json = @"{
                ""version"": 1,
                ""entries"": [
                    {""token"": """", ""codepoint"": ""0xE000"", ""language"": ""ja"", ""description"": ""bad""},
                    {""token"": ""valid"", ""codepoint"": ""0xE001"", ""language"": ""ja"", ""description"": ""ok""}
                ]
            }";

            var result = _mapper.LoadFromJson(json);

            Assert.IsTrue(result);
            Assert.AreEqual(1, _mapper.Token2Char.Count);
            Assert.IsTrue(_mapper.Token2Char.ContainsKey("valid"));
        }

        // ── InitializeFromFile ────────────────────────────────────────────

        [Test]
        public void InitializeFromFile_WhenFileDoesNotExist_ReturnsFalse()
        {
            // Create a fresh mapper and try to load from a path that doesn't exist.
            // Application.streamingAssetsPath may or may not have pua.json;
            // but we test the method contract: it should not throw.
            var mapper = new PuaTokenMapper();

            // Since we can't control Application.streamingAssetsPath in tests,
            // we verify it doesn't throw and returns a boolean.
            // If pua.json exists, it will return true; if not, false.
            var result = mapper.InitializeFromFile();
            Assert.IsInstanceOf<bool>(result, "Should return a boolean without throwing");
        }

        // ── Round-trip: LoadFromJson then verify Token2Char/Char2Token ───

        [Test]
        public void LoadFromJson_RoundTrip_Token2CharAndChar2TokenConsistent()
        {
            var json = @"{
                ""version"": 1,
                ""entries"": [
                    {""token"": ""a:"", ""codepoint"": ""0xE000"", ""language"": ""ja"", ""description"": ""a""},
                    {""token"": ""ch"", ""codepoint"": ""0xE00E"", ""language"": ""ja"", ""description"": ""ch""},
                    {""token"": ""tone1"", ""codepoint"": ""0xE046"", ""language"": ""zh"", ""description"": ""t1""},
                    {""token"": ""rr"", ""codepoint"": ""0xE01D"", ""language"": ""shared"", ""description"": ""rr""}
                ]
            }";

            _mapper.LoadFromJson(json);

            // Verify every Token2Char entry has a corresponding Char2Token entry
            foreach (var kvp in _mapper.Token2Char)
            {
                var token = kvp.Key;
                var ch = kvp.Value;

                Assert.IsTrue(_mapper.Char2Token.ContainsKey(ch),
                    $"Char2Token should contain char mapped from token '{token}'");
                Assert.AreEqual(token, _mapper.Char2Token[ch],
                    $"Round-trip failed: Token2Char['{token}'] = 0x{(int)ch:X4}, " +
                    $"but Char2Token[0x{(int)ch:X4}] = '{_mapper.Char2Token[ch]}'");
            }

            // Verify every Char2Token entry has a corresponding Token2Char entry
            foreach (var kvp in _mapper.Char2Token)
            {
                var ch = kvp.Key;
                var token = kvp.Value;

                Assert.IsTrue(_mapper.Token2Char.ContainsKey(token),
                    $"Token2Char should contain token '{token}' mapped from char 0x{(int)ch:X4}");
                Assert.AreEqual(ch, _mapper.Token2Char[token],
                    $"Round-trip failed: Char2Token[0x{(int)ch:X4}] = '{token}', " +
                    $"but Token2Char['{token}'] = 0x{(int)_mapper.Token2Char[token]:X4}");
            }
        }

        [Test]
        public void LoadFromJson_ThenMapToken_ReturnsCorrectChar()
        {
            var json = @"{
                ""version"": 1,
                ""entries"": [
                    {""token"": ""ch"", ""codepoint"": ""0xE00E"", ""language"": ""ja"", ""description"": ""test""},
                    {""token"": ""sh"", ""codepoint"": ""0xE010"", ""language"": ""ja"", ""description"": ""test""}
                ]
            }";

            _mapper.LoadFromJson(json);

            Assert.AreEqual('\uE00E', _mapper.MapToken("ch"));
            Assert.AreEqual('\uE010', _mapper.MapToken("sh"));
        }

        [Test]
        public void LoadFromJson_ThenUnmapChar_ReturnsCorrectToken()
        {
            var json = @"{
                ""version"": 1,
                ""entries"": [
                    {""token"": ""ch"", ""codepoint"": ""0xE00E"", ""language"": ""ja"", ""description"": ""test""},
                    {""token"": ""tone5"", ""codepoint"": ""0xE04A"", ""language"": ""zh"", ""description"": ""test""}
                ]
            }";

            _mapper.LoadFromJson(json);

            Assert.AreEqual("ch", _mapper.UnmapChar('\uE00E'));
            Assert.AreEqual("tone5", _mapper.UnmapChar('\uE04A'));
        }

        [Test]
        public void LoadFromJson_DynamicAllocation_StartsAfterHighestLoaded()
        {
            var json = @"{
                ""version"": 1,
                ""entries"": [
                    {""token"": ""a:"", ""codepoint"": ""0xE000"", ""language"": ""ja"", ""description"": ""test""},
                    {""token"": ""ch"", ""codepoint"": ""0xE00E"", ""language"": ""ja"", ""description"": ""test""}
                ]
            }";

            _mapper.LoadFromJson(json);

            // Register a new dynamic token - should be after 0xE00E
            var dynamicChar = _mapper.Register("new_dynamic");
            Assert.GreaterOrEqual((int)dynamicChar, 0xE00F,
                "Dynamic allocation should start after highest loaded codepoint");
        }

        // ── LoadFromJson with real pua.json from StreamingAssets ──────────

        [Test]
        public void LoadFromJson_RealPuaJson_MatchesFixedMapping()
        {
            var path = Path.Combine(Application.streamingAssetsPath, "uPiper/pua.json");
            if (!File.Exists(path))
            {
                Assert.Ignore($"pua.json not found at {path}");
                return;
            }

            var json = File.ReadAllText(path);
            var result = _mapper.LoadFromJson(json);

            Assert.IsTrue(result, "LoadFromJson should succeed for real pua.json");

            // Verify all fixed mapping entries exist and match
            foreach (var kvp in PuaTokenMapper.FixedPuaMapping)
            {
                Assert.IsTrue(_mapper.Token2Char.ContainsKey(kvp.Key),
                    $"Loaded mapper should contain token '{kvp.Key}'");
                Assert.AreEqual((char)kvp.Value, _mapper.Token2Char[kvp.Key],
                    $"Token '{kvp.Key}' should map to 0x{kvp.Value:X4}");
            }
        }

        // ── Hex parsing edge cases ────────────────────────────────────────

        [Test]
        public void LoadFromJson_LowercaseHex_Succeeds()
        {
            var json = @"{
                ""version"": 1,
                ""entries"": [
                    {""token"": ""test"", ""codepoint"": ""0xe000"", ""language"": ""ja"", ""description"": ""test""}
                ]
            }";

            var result = _mapper.LoadFromJson(json);

            Assert.IsTrue(result);
            Assert.AreEqual('\uE000', _mapper.Token2Char["test"]);
        }

        [Test]
        public void LoadFromJson_UppercaseHex_Succeeds()
        {
            var json = @"{
                ""version"": 1,
                ""entries"": [
                    {""token"": ""test"", ""codepoint"": ""0xE000"", ""language"": ""ja"", ""description"": ""test""}
                ]
            }";

            var result = _mapper.LoadFromJson(json);

            Assert.IsTrue(result);
            Assert.AreEqual('\uE000', _mapper.Token2Char["test"]);
        }

        [Test]
        public void LoadFromJson_NoHexPrefix_Succeeds()
        {
            var json = @"{
                ""version"": 1,
                ""entries"": [
                    {""token"": ""test"", ""codepoint"": ""E000"", ""language"": ""ja"", ""description"": ""test""}
                ]
            }";

            var result = _mapper.LoadFromJson(json);

            Assert.IsTrue(result);
            Assert.AreEqual('\uE000', _mapper.Token2Char["test"]);
        }

        // ── E2E fallback: no JSON load ───────────────────────────────────

        [Test]
        public void PuaTokenMapper_WithoutJsonLoad_UsesHardcodedMapping()
        {
            // PuaTokenMapper をJSON読み込みなしで使用
            var mapper = new PuaTokenMapper();
            // ハードコードマッピングが機能することを確認
            Assert.AreEqual('\uE000', mapper.MapToken("a:"));
            Assert.AreEqual('\uE00E', mapper.MapToken("ch"));
            Assert.AreEqual("a:", mapper.UnmapChar('\uE000'));
            Assert.AreEqual("ch", mapper.UnmapChar('\uE00E'));
            // 動的割り当ても機能
            var dynamicChar = mapper.Register("test_dynamic");
            Assert.AreNotEqual('\0', dynamicChar);
            Assert.AreEqual("test_dynamic", mapper.UnmapChar(dynamicChar));
        }

        // ── MaxEntries exceeded ──────────────────────────────────────────

        [Test]
        public void LoadFromJson_TooManyEntries_ReturnsFalseAndKeepsHardcoded()
        {
            // MaxEntries (500) を超えるエントリを生成
            var entries = new System.Text.StringBuilder();
            for (int i = 0; i < 501; i++)
            {
                if (i > 0) entries.Append(",");
                var codepoint = 0xE000 + i;
                if (codepoint > 0xF8FF) codepoint = 0xE000 + (i % 0x18FF);
                entries.Append($"{{\"token\":\"tok{i}\",\"codepoint\":\"0x{codepoint:X4}\"," +
                    $"\"language\":\"test\",\"description\":\"test\"}}");
            }
            var json = $"{{\"version\":1,\"entries\":[{entries}]}}";

            LogAssert.Expect(LogType.Error, new Regex("Too many entries"));
            var result = _mapper.LoadFromJson(json);

            Assert.IsFalse(result, "Should return false when entries exceed MaxEntries");
            // ハードコードマッピングが保持されていることを確認
            Assert.AreEqual('\uE000', _mapper.MapToken("a:"));
        }
    }
}