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
            Assert.IsTrue(msg.Contains("音声合成"));
        }

        [Test]
        public void GetOverlayMessage_English_ReturnsEnglishMessage()
        {
            var msg = WebGLLocalization.GetOverlayMessage("en");
            Assert.IsTrue(msg.Contains("Text-to-speech"));
        }

        [Test]
        public void GetOverlayMessage_JaJP_ReturnsJapaneseMessage()
        {
            var msg = WebGLLocalization.GetOverlayMessage("ja-JP");
            Assert.IsTrue(msg.Contains("音声合成"));
        }

        [Test]
        public void GetOverlayMessage_EnUS_ReturnsEnglishMessage()
        {
            var msg = WebGLLocalization.GetOverlayMessage("en-US");
            Assert.IsTrue(msg.Contains("Text-to-speech"));
        }

        [Test]
        public void GetOverlayMessage_Unknown_FallsBackToEnglish()
        {
            var msg = WebGLLocalization.GetOverlayMessage("de");
            Assert.IsTrue(msg.Contains("Text-to-speech"));
        }

        [Test]
        public void GetOverlayMessage_Null_ReturnsNonEmpty()
        {
            // In non-WebGL editor, GetBrowserLanguage returns "en"
            var msg = WebGLLocalization.GetOverlayMessage(null);
            Assert.IsNotEmpty(msg);
        }

        [TestCase("zh")]
        [TestCase("es")]
        [TestCase("fr")]
        [TestCase("pt")]
        [TestCase("ko")]
        public void GetOverlayMessage_SupportedLanguage_ReturnsNonEmpty(string lang)
        {
            var msg = WebGLLocalization.GetOverlayMessage(lang);
            Assert.IsNotEmpty(msg);
            Assert.AreNotEqual(msg, WebGLLocalization.GetOverlayMessage("en"),
                $"Language '{lang}' should have its own message, not English fallback");
        }
    }
}