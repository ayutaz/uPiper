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
            Assert.AreEqual(string.Empty, args.SkippedText);
        }

        [Test]
        public void Constructor_LongText_Truncated()
        {
            var longText = new string('a', 300);
            var args = new UnsupportedLanguageEventArgs("ko", longText, new List<string> { "ja" });
            Assert.IsTrue(args.SkippedText.Length <= UnsupportedLanguageEventArgs.MaxTextLength + 3);
            Assert.IsTrue(args.SkippedText.EndsWith("..."));
        }

        [Test]
        public void WasProcessedByFallback_NoFallback_ReturnsFalse()
        {
            var args = new UnsupportedLanguageEventArgs("ko", "text", new List<string> { "ja" });
            Assert.IsFalse(args.WasProcessedByFallback);
        }

        [Test]
        public void WasProcessedByFallback_WithFallback_ReturnsTrue()
        {
            var args = new UnsupportedLanguageEventArgs(
                "ko", "text", new List<string> { "ja", "en" }, "en");
            Assert.IsTrue(args.WasProcessedByFallback);
            Assert.AreEqual("en", args.FallbackLanguageUsed);
        }

        [Test]
        public void Properties_SetCorrectly()
        {
            var langs = new List<string> { "ja", "en", "zh" };
            var args = new UnsupportedLanguageEventArgs("ko", "hello", langs);
            Assert.AreEqual("ko", args.LanguageCode);
            Assert.AreEqual("hello", args.SkippedText);
            Assert.AreEqual(langs, args.SupportedLanguages);
        }
    }
}