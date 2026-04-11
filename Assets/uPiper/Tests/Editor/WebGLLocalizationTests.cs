using NUnit.Framework;
using uPiper.Core.Platform;

namespace uPiper.Tests.Editor
{
    [TestFixture]
    public class WebGLLocalizationTests
    {
        [Test]
        public void GetOverlayMessage_Japanese_ReturnsJapaneseMessage()
        {
            var msg = WebGLLocalization.GetOverlayMessage("ja");
            Assert.That(msg, Does.Contain("音声合成"),
                "Japanese message should contain '音声合成'");
        }

        [Test]
        public void GetOverlayMessage_English_ReturnsEnglishMessage()
        {
            var msg = WebGLLocalization.GetOverlayMessage("en");
            Assert.That(msg, Does.Contain("Text-to-speech"),
                "English message should contain 'Text-to-speech'");
        }

        [Test]
        public void GetOverlayMessage_JaJP_ReturnsJapaneseMessage()
        {
            var msg = WebGLLocalization.GetOverlayMessage("ja-JP");
            Assert.That(msg, Does.Contain("音声合成"),
                "ja-JP should resolve to Japanese message");
        }

        [Test]
        public void GetOverlayMessage_EnUS_ReturnsEnglishMessage()
        {
            var msg = WebGLLocalization.GetOverlayMessage("en-US");
            Assert.That(msg, Does.Contain("Text-to-speech"),
                "en-US should resolve to English message");
        }

        [Test]
        public void GetOverlayMessage_Unknown_FallsBackToEnglish()
        {
            var msg = WebGLLocalization.GetOverlayMessage("de");
            Assert.That(msg, Does.Contain("Text-to-speech"),
                "Unknown language should fall back to English message");
        }

        [Test]
        public void GetOverlayMessage_Null_ReturnsNonEmpty()
        {
            // In non-WebGL editor, GetBrowserLanguage returns "en"
            var msg = WebGLLocalization.GetOverlayMessage(null);
            Assert.That(msg, Is.Not.Empty,
                "Null language should still return a non-empty message");
        }

        [TestCase("zh")]
        [TestCase("es")]
        [TestCase("fr")]
        [TestCase("pt")]
        [TestCase("ko")]
        public void GetOverlayMessage_SupportedLanguage_ReturnsNonEmpty(string lang)
        {
            var msg = WebGLLocalization.GetOverlayMessage(lang);
            Assert.That(msg, Is.Not.Empty,
                $"Message for '{lang}' should not be empty");
            Assert.That(msg, Is.Not.EqualTo(WebGLLocalization.GetOverlayMessage("en")),
                $"Language '{lang}' should have its own message, not English fallback");
        }
    }
}