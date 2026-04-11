using System;
using System.Collections.Generic;
using NUnit.Framework;
using uPiper.Core;

namespace uPiper.Tests.Editor
{
    [TestFixture]
    public class UnsupportedLanguageEventTests
    {
        [Test]
        public void Constructor_NullLanguageCode_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new UnsupportedLanguageEventArgs(null, "text", new List<string> { "ja" }));
        }

        [Test]
        public void Constructor_NullSupportedLanguages_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new UnsupportedLanguageEventArgs("ko", "text", null));
        }

        [Test]
        public void Constructor_NullSkippedText_DefaultsToEmpty()
        {
            var args = new UnsupportedLanguageEventArgs("ko", null, new List<string> { "ja" });
            Assert.That(args.SkippedText, Is.EqualTo(string.Empty),
                "Null skipped text should default to empty string");
        }

        [Test]
        public void Constructor_LongText_Truncated()
        {
            var longText = new string('a', 300);
            var args = new UnsupportedLanguageEventArgs("ko", longText, new List<string> { "ja" });
            Assert.That(args.SkippedText.Length,
                Is.LessThanOrEqualTo(UnsupportedLanguageEventArgs.MaxTextLength + 3),
                "Long text should be truncated to MaxTextLength + ellipsis");
            Assert.That(args.SkippedText, Does.EndWith("..."),
                "Truncated text should end with '...'");
        }

        [Test]
        public void WasProcessedByFallback_NoFallback_ReturnsFalse()
        {
            var args = new UnsupportedLanguageEventArgs("ko", "text", new List<string> { "ja" });
            Assert.That(args.WasProcessedByFallback, Is.False,
                "WasProcessedByFallback should be false when no fallback is provided");
        }

        [Test]
        public void WasProcessedByFallback_WithFallback_ReturnsTrue()
        {
            var args = new UnsupportedLanguageEventArgs(
                "ko", "text", new List<string> { "ja", "en" }, "en");
            Assert.That(args.WasProcessedByFallback, Is.True,
                "WasProcessedByFallback should be true when fallback is provided");
            Assert.That(args.FallbackLanguageUsed, Is.EqualTo("en"),
                "FallbackLanguageUsed should match the provided fallback language");
        }

        [Test]
        public void Properties_SetCorrectly()
        {
            var langs = new List<string> { "ja", "en", "zh" };
            var args = new UnsupportedLanguageEventArgs("ko", "hello", langs);
            Assert.That(args.LanguageCode, Is.EqualTo("ko"),
                "LanguageCode should match constructor argument");
            Assert.That(args.SkippedText, Is.EqualTo("hello"),
                "SkippedText should match constructor argument");
            Assert.That(args.SupportedLanguages, Is.EqualTo(langs),
                "SupportedLanguages should match constructor argument");
        }
    }
}