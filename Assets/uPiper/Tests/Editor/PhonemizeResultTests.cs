using NUnit.Framework;
using uPiper.Core.AudioGeneration;

namespace uPiper.Tests.Editor
{
    /// <summary>
    /// Unit tests for <see cref="PhonemizeResult"/>.
    /// Covers property access and HasProsody logic.
    /// </summary>
    [TestFixture]
    public class PhonemizeResultTests
    {
        [Test]
        public void PhonemizeResult_Properties_ReturnCorrectValues()
        {
            var phonemes = new[] { "k", "o", "N_uvular" };
            var prosodyFlat = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };

            var result = new PhonemizeResult(phonemes, prosodyFlat, "ja", 0);

            Assert.AreEqual(phonemes, result.Phonemes);
            Assert.AreEqual(prosodyFlat, result.ProsodyFlat);
            Assert.AreEqual("ja", result.DetectedLanguage);
            Assert.AreEqual(0, result.ResolvedLanguageId);
            Assert.IsTrue(result.HasProsody);
        }

        [Test]
        public void PhonemizeResult_HasProsody_NullProsodyFlat_ReturnsFalse()
        {
            var result = new PhonemizeResult(
                new[] { "h", "e", "l", "o" }, null, "en", 1);

            Assert.IsFalse(result.HasProsody);
            Assert.IsNull(result.ProsodyFlat);
        }

        [Test]
        public void PhonemizeResult_HasProsody_ValidProsodyFlat_ReturnsTrue()
        {
            var prosodyFlat = new[] { 0, 0, 0 };
            var result = new PhonemizeResult(
                new[] { "a" }, prosodyFlat, "ja", 0);

            Assert.IsTrue(result.HasProsody);
            Assert.IsNotNull(result.ProsodyFlat);
        }
    }
}