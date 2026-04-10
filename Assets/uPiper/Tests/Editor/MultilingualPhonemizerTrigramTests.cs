using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using uPiper.Core.Phonemizers.Multilingual;

namespace uPiper.Tests.Editor
{
    [TestFixture]
    public class MultilingualPhonemizerTrigramTests
    {
        [Test]
        public void EnableTrigramDetection_DefaultValue_IsTrue()
        {
            var options = new MultilingualPhonemizerOptions
            {
                Languages = new List<string> { "ja", "en" }
            };
            Assert.IsTrue(options.EnableTrigramDetection);
        }

        [Test]
        public async Task InitializeAsync_SingleLatinLanguage_TrigramDetectionNotActive()
        {
            var options = new MultilingualPhonemizerOptions
            {
                Languages = new List<string> { "ja", "en" }
            };
            using var phonemizer = new MultilingualPhonemizer(options);
            await phonemizer.InitializeAsync();
            Assert.IsTrue(phonemizer.IsInitialized);
            Assert.IsFalse(phonemizer.IsTrigramDetectionActive);
        }

        [Test]
        public async Task InitializeAsync_CjkOnly_TrigramDetectionNotActive()
        {
            var options = new MultilingualPhonemizerOptions
            {
                Languages = new List<string> { "ja", "zh", "ko" }
            };
            using var phonemizer = new MultilingualPhonemizer(options);
            await phonemizer.InitializeAsync();
            Assert.IsTrue(phonemizer.IsInitialized);
            Assert.IsFalse(phonemizer.IsTrigramDetectionActive);
        }

        [Test]
        public async Task InitializeAsync_TrigramDisabled_NotActive()
        {
            var options = new MultilingualPhonemizerOptions
            {
                Languages = new List<string> { "ja", "en", "fr", "es" },
                EnableTrigramDetection = false
            };
            using var phonemizer = new MultilingualPhonemizer(options);
            await phonemizer.InitializeAsync();
            Assert.IsTrue(phonemizer.IsInitialized);
            Assert.IsFalse(phonemizer.IsTrigramDetectionActive);
        }

        [Test]
        public async Task InitializeAsync_CustomDetector_NotActive()
        {
            var languages = new List<string> { "ja", "en", "fr", "es" };
            var customDetector = new UnicodeLanguageDetector(languages, "en");
            var options = new MultilingualPhonemizerOptions
            {
                Languages = languages,
                LanguageDetector = customDetector
            };
            using var phonemizer = new MultilingualPhonemizer(options);
            await phonemizer.InitializeAsync();
            Assert.IsTrue(phonemizer.IsInitialized);
            Assert.IsFalse(phonemizer.IsTrigramDetectionActive);
        }

        [Test]
        public async Task Dispose_ResetsTrigramDetectionActive()
        {
            var options = new MultilingualPhonemizerOptions
            {
                Languages = new List<string> { "ja", "en", "fr" }
            };
            var phonemizer = new MultilingualPhonemizer(options);
            await phonemizer.InitializeAsync();
            phonemizer.Dispose();
            Assert.IsFalse(phonemizer.IsTrigramDetectionActive);
        }

        [Test]
        public void DefaultLatinLanguage_DefaultValue_IsEn()
        {
            var options = new MultilingualPhonemizerOptions
            {
                Languages = new List<string> { "ja", "en" }
            };
            Assert.AreEqual("en", options.DefaultLatinLanguage);
        }

        [Test]
        public async Task InitializeAsync_MultipleLatinLanguages_InitializesSuccessfully()
        {
            var options = new MultilingualPhonemizerOptions
            {
                Languages = new List<string> { "ja", "en", "fr", "es" }
            };
            using var phonemizer = new MultilingualPhonemizer(options);
            await phonemizer.InitializeAsync();
            Assert.IsTrue(phonemizer.IsInitialized);
            // IsTrigramDetectionActive depends on trigram_profiles.json availability
        }
    }
}