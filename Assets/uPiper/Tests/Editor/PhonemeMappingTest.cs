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
    }
}