using NUnit.Framework;
using uPiper.Core.Phonemizers;

namespace uPiper.Tests.Editor
{
    [TestFixture]
    public class PhonemeMappingTest
    {
        [Test]
        public void TestKyouPhonemeMapping()
        {
            // Test that "ky" is correctly converted to PUA character
            var input = new[] { "pau", "ky", "o", "o", "pau" };
            var result = OpenJTalkToPiperMapping.ConvertToPiperPhonemes(input);

            Assert.AreEqual(5, result.Length);
            Assert.AreEqual("_", result[0]); // pau -> _
            Assert.AreEqual("\ue006", result[1]); // ky -> PUA character
            Assert.AreEqual("o", result[2]);
            Assert.AreEqual("o", result[3]);
            Assert.AreEqual("_", result[4]); // pau -> _
        }

        [Test]
        public void TestOtherPalatizedConsonants()
        {
            // Test other palatalized consonants
            var testCases = new[]
            {
                ("gy", "\ue008"),
                ("ny", "\ue013"),
                ("hy", "\ue012"),
                ("by", "\ue00d"),
                ("py", "\ue00c"),
                ("my", "\ue014"),
                ("ry", "\ue015")
            };

            foreach (var (input, expected) in testCases)
            {
                var result = OpenJTalkToPiperMapping.ConvertToPiperPhonemes(new[] { input });
                Assert.AreEqual(1, result.Length, $"Failed for input: {input}");
                Assert.AreEqual(expected, result[0], $"Failed for input: {input}");
            }
        }

        #region Extended Question Markers Tests (piper-plus #210)

        [Test]
        public void TestExtendedQuestionMarkers_PUAMapping()
        {
            // Test that extended question markers are correctly mapped to PUA characters
            var testCases = new[]
            {
                ("?!", "\ue016"),   // Emphatic question
                ("?.", "\ue017"),   // Declarative question
                ("?~", "\ue018")    // Confirmatory question
            };

            foreach (var (input, expected) in testCases)
            {
                var result = OpenJTalkToPiperMapping.ConvertToPiperPhonemes(new[] { input });
                Assert.AreEqual(1, result.Length, $"Failed for input: {input}");
                Assert.AreEqual(expected, result[0], $"Extended question marker '{input}' should map to PUA U+{((int)expected[0]):X4}");
            }
        }

        #endregion

        #region N Phoneme Variants Tests (piper-plus #207/#210)

        [Test]
        public void TestNPhonemeVariants_PUAMapping()
        {
            // Test that N phoneme variants are correctly mapped to PUA characters
            var testCases = new[]
            {
                ("N_m", "\ue019"),      // N before m/b/p (bilabial)
                ("N_n", "\ue01a"),      // N before n/t/d/ts/ch (alveolar)
                ("N_ng", "\ue01b"),     // N before k/g (velar)
                ("N_uvular", "\ue01c")  // N at end/before vowels
            };

            foreach (var (input, expected) in testCases)
            {
                var result = OpenJTalkToPiperMapping.ConvertToPiperPhonemes(new[] { input });
                Assert.AreEqual(1, result.Length, $"Failed for input: {input}");
                Assert.AreEqual(expected, result[0], $"N variant '{input}' should map to PUA U+{((int)expected[0]):X4}");
            }
        }

        [Test]
        public void TestNPhonemeVariants_DefaultIdMapping()
        {
            // Test that N phoneme variant PUA characters have IDs in the default mapping
            var mapping = OpenJTalkToPiperMapping.GetDefaultPhonemeToIdMapping();

            // Extended question markers
            Assert.IsTrue(mapping.ContainsKey("\ue016"), "?! PUA should have default ID");
            Assert.IsTrue(mapping.ContainsKey("\ue017"), "?. PUA should have default ID");
            Assert.IsTrue(mapping.ContainsKey("\ue018"), "?~ PUA should have default ID");

            // N variants
            Assert.IsTrue(mapping.ContainsKey("\ue019"), "N_m PUA should have default ID");
            Assert.IsTrue(mapping.ContainsKey("\ue01a"), "N_n PUA should have default ID");
            Assert.IsTrue(mapping.ContainsKey("\ue01b"), "N_ng PUA should have default ID");
            Assert.IsTrue(mapping.ContainsKey("\ue01c"), "N_uvular PUA should have default ID");

            // Verify IDs are in expected range (58-64)
            Assert.AreEqual(58, mapping["\ue016"], "?! should have ID 58");
            Assert.AreEqual(59, mapping["\ue017"], "?. should have ID 59");
            Assert.AreEqual(60, mapping["\ue018"], "?~ should have ID 60");
            Assert.AreEqual(61, mapping["\ue019"], "N_m should have ID 61");
            Assert.AreEqual(62, mapping["\ue01a"], "N_n should have ID 62");
            Assert.AreEqual(63, mapping["\ue01b"], "N_ng should have ID 63");
            Assert.AreEqual(64, mapping["\ue01c"], "N_uvular should have ID 64");
        }

        #endregion
    }
}