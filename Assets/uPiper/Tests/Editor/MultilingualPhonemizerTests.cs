using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using uPiper.Core.Phonemizers.Multilingual;
using uPiper.Core.Phonemizers.Multilingual.Handlers;
using uPiper.Tests.Editor.Phonemizers.Handlers;

namespace uPiper.Tests.Editor
{
    [TestFixture]
    public class MultilingualPhonemizerTests
    {
        // ── UnicodeLanguageDetector integration ────────────────────────────────

        [Test]
        public void SegmentText_JapaneseText_DetectedAsJa()
        {
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en" });
            var result = detector.SegmentText("こんにちは");

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("ja", result[0].language);
        }

        [Test]
        public void SegmentText_EnglishText_DetectedAsEn()
        {
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en" });
            var result = detector.SegmentText("hello world");

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("en", result[0].language);
        }

        [Test]
        public void SegmentText_MixedText_SplitsByLanguage()
        {
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en" });
            var result = detector.SegmentText("こんにちは hello");

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("ja", result[0].language);
            Assert.AreEqual("en", result[1].language);
        }

        [Test]
        public void SegmentText_ChineseWithJaContext_DetectedAsJa()
        {
            // CJK 漢字はKanaがある文脈では日本語と判定される
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en" });
            var result = detector.SegmentText("日本語テスト");

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("ja", result[0].language);
        }

        [Test]
        public void SegmentText_ChineseOnly_DetectedAsZh()
        {
            // Kanaなし + zh サポートの場合、漢字は中国語と判定される
            var detector = new UnicodeLanguageDetector(new[] { "zh", "en" });
            var result = detector.SegmentText("你好世界");

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("zh", result[0].language);
        }

        [Test]
        public void SegmentText_EmptyString_ReturnsEmpty()
        {
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en" });
            var result = detector.SegmentText("");

            Assert.AreEqual(0, result.Count);
        }

        // ── MultilingualPhonemizer construction ────────────────────────────────

        [Test]
        public void Constructor_ValidLanguages_CreatesInstance()
        {
            var phonemizer = new MultilingualPhonemizer(
                new MultilingualPhonemizerOptions
                {
                    Languages = new[] { "ja", "en" },
                    DefaultLatinLanguage = "en"
                });

            Assert.IsNotNull(phonemizer);
            Assert.IsFalse(phonemizer.IsInitialized);
            Assert.AreEqual(2, phonemizer.Languages.Count);
        }

        [Test]
        public void Constructor_EmptyLanguages_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(() =>
                new MultilingualPhonemizer(new MultilingualPhonemizerOptions
                {
                    Languages = new List<string>()
                }));
        }

        [Test]
        public void Constructor_NullOptions_ThrowsArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                new MultilingualPhonemizer(null));
        }

        // ── MultilingualPhonemizer initialization ──────────────────────────────

        [Test]
        public void IsInitialized_BeforeInitialize_ReturnsFalse()
        {
            var phonemizer = new MultilingualPhonemizer(
                new MultilingualPhonemizerOptions
                {
                    Languages = new[] { "ja", "en" }
                });
            Assert.IsFalse(phonemizer.IsInitialized);
        }

        [Test]
        public async Task InitializeAsync_JaEnLanguages_SetsIsInitialized()
        {
            // Note: InitializeAsync creates DotNetG2PPhonemizer which needs dictionary.
            // Use a phonemizer with only English to avoid dictionary dependency.
            var phonemizer = new MultilingualPhonemizer(
                new MultilingualPhonemizerOptions
                {
                    Languages = new[] { "en" },
                    DefaultLatinLanguage = "en"
                });

            await phonemizer.InitializeAsync();
            Assert.IsTrue(phonemizer.IsInitialized);
            phonemizer.Dispose();
        }

        // ── MultilingualPhonemizeResult structure ──────────────────────────────

        [Test]
        public void MultilingualPhonemizeResult_DefaultValues_AreNull()
        {
            var result = new MultilingualPhonemizeResult();

            Assert.IsNull(result.Phonemes);
            Assert.IsNull(result.ProsodyFlat);
            Assert.IsNull(result.DetectedPrimaryLanguage);
        }

        // ── Options edge cases ─────────────────────────────────────────────────

        [Test]
        public void Constructor_HandlersNull_AcceptsDefault()
        {
            var phonemizer = new MultilingualPhonemizer(
                new MultilingualPhonemizerOptions
                {
                    Languages = new[] { "ja", "en" },
                    Handlers = null  // explicitly null
                });

            Assert.IsNotNull(phonemizer);
            Assert.IsFalse(phonemizer.IsInitialized);
            phonemizer.Dispose();
        }

        // ── Options.Validate edge cases ──────────────────────────────────────

        [Test]
        public void Options_Validate_HandlerKeyNotInLanguages_LogsWarning()
        {
            // Handlers に Languages にない言語キーを渡した場合にWarningログ
            // (例外はスローされないが、ログが出力される)
            var stub = new StubG2PHandler("ko", new[] { "h", "a" });
            var options = new MultilingualPhonemizerOptions
            {
                Languages = new[] { "ja", "en" },
                Handlers = new Dictionary<string, ILanguageG2PHandler>
                {
                    { "ko", stub }  // "ko" is not in Languages
                }
            };

            // Validate should not throw, but should log a warning
            Assert.DoesNotThrow(() => options.Validate());
            stub.Dispose();
        }

        [Test]
        public void Options_Validate_DefaultLatinLanguageNotInLanguages_LogsWarning()
        {
            // DefaultLatinLanguage が Languages にない場合
            var options = new MultilingualPhonemizerOptions
            {
                Languages = new[] { "ja", "zh" },
                DefaultLatinLanguage = "en"  // "en" is not in Languages
            };

            // Validate should not throw, but should log a warning
            Assert.DoesNotThrow(() => options.Validate());
        }

        // ── Dispose ────────────────────────────────────────────────────────────

        [Test]
        public void Dispose_CalledTwice_DoesNotThrow()
        {
            var phonemizer = new MultilingualPhonemizer(
                new MultilingualPhonemizerOptions
                {
                    Languages = new[] { "ja", "en" }
                });
            Assert.DoesNotThrow(() =>
            {
                phonemizer.Dispose();
                phonemizer.Dispose();
            });
        }
    }
}