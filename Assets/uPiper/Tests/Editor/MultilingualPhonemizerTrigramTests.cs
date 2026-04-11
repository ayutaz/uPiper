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
            Assert.That(options.EnableTrigramDetection, Is.True,
                "EnableTrigramDetection should default to true");
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
            Assert.That(phonemizer.IsInitialized, Is.True,
                "Phonemizer should be initialized after InitializeAsync");
            Assert.That(phonemizer.IsTrigramDetectionActive, Is.False,
                "Trigram detection should not be active with only one Latin language");
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
            Assert.That(phonemizer.IsInitialized, Is.True,
                "Phonemizer should be initialized after InitializeAsync");
            Assert.That(phonemizer.IsTrigramDetectionActive, Is.False,
                "Trigram detection should not be active with CJK-only languages");
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
            Assert.That(phonemizer.IsInitialized, Is.True,
                "Phonemizer should be initialized after InitializeAsync");
            Assert.That(phonemizer.IsTrigramDetectionActive, Is.False,
                "Trigram detection should not be active when explicitly disabled");
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
            Assert.That(phonemizer.IsInitialized, Is.True,
                "Phonemizer should be initialized after InitializeAsync");
            Assert.That(phonemizer.IsTrigramDetectionActive, Is.False,
                "Trigram detection should not be active with custom detector");
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
            Assert.That(phonemizer.IsTrigramDetectionActive, Is.False,
                "Trigram detection should not be active after Dispose");
        }

        [Test]
        public void DefaultLatinLanguage_DefaultValue_IsEn()
        {
            var options = new MultilingualPhonemizerOptions
            {
                Languages = new List<string> { "ja", "en" }
            };
            Assert.That(options.DefaultLatinLanguage, Is.EqualTo("en"),
                "DefaultLatinLanguage should default to 'en'");
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
            Assert.That(phonemizer.IsInitialized, Is.True,
                "Phonemizer should be initialized with multiple Latin languages");
            // IsTrigramDetectionActive depends on trigram_profiles.json availability
        }
    }
}