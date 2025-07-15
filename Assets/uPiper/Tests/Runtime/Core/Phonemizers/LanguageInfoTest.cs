using System;
using NUnit.Framework;
using uPiper.Core.Phonemizers;

namespace uPiper.Tests.Runtime.Core.Phonemizers
{
    public class LanguageInfoTest
    {
        [Test]
        public void Constructor_InitializesWithEmptyVoices()
        {
            var info = new LanguageInfo();
            
            Assert.IsNotNull(info.AvailableVoices);
            Assert.AreEqual(0, info.AvailableVoices.Length);
            Assert.AreEqual(TextDirection.LeftToRight, info.Direction);
        }

        [Test]
        public void Properties_CanBeSetAndRetrieved()
        {
            var info = new LanguageInfo
            {
                Code = "ja",
                Name = "Japanese",
                NativeName = "日本語",
                RequiresPreprocessing = true,
                AvailableVoices = new[] { "voice1", "voice2", "voice3" },
                DefaultVoice = "voice1",
                PhonemeSetType = "Japanese-Kana",
                SupportsAccent = true,
                Direction = TextDirection.LeftToRight
            };
            
            Assert.AreEqual("ja", info.Code);
            Assert.AreEqual("Japanese", info.Name);
            Assert.AreEqual("日本語", info.NativeName);
            Assert.IsTrue(info.RequiresPreprocessing);
            CollectionAssert.AreEqual(new[] { "voice1", "voice2", "voice3" }, info.AvailableVoices);
            Assert.AreEqual("voice1", info.DefaultVoice);
            Assert.AreEqual("Japanese-Kana", info.PhonemeSetType);
            Assert.IsTrue(info.SupportsAccent);
            Assert.AreEqual(TextDirection.LeftToRight, info.Direction);
        }

        [Test]
        public void Create_StaticMethod_CreatesInstanceCorrectly()
        {
            var info = LanguageInfo.Create("en", "English", "English");
            
            Assert.AreEqual("en", info.Code);
            Assert.AreEqual("English", info.Name);
            Assert.AreEqual("English", info.NativeName);
            Assert.IsFalse(info.RequiresPreprocessing);
            Assert.IsFalse(info.SupportsAccent);
            Assert.IsNull(info.DefaultVoice);
            Assert.IsNull(info.PhonemeSetType);
            Assert.AreEqual(TextDirection.LeftToRight, info.Direction);
        }

        [Test]
        public void ToString_FormatsCorrectly()
        {
            var info = new LanguageInfo
            {
                Code = "ja",
                Name = "Japanese",
                NativeName = "日本語"
            };
            
            Assert.AreEqual("Japanese (ja) - 日本語", info.ToString());
        }

        [Test]
        public void ToString_HandlesNullValues()
        {
            var info = new LanguageInfo
            {
                Code = "unknown",
                Name = null,
                NativeName = null
            };
            
            // Should not throw
            var str = info.ToString();
            Assert.IsNotNull(str);
        }

        [Test]
        public void TextDirection_EnumValues()
        {
            Assert.AreEqual(0, (int)TextDirection.LeftToRight);
            Assert.AreEqual(1, (int)TextDirection.RightToLeft);
        }

        [Test]
        public void RightToLeftLanguage_Example()
        {
            var info = new LanguageInfo
            {
                Code = "ar",
                Name = "Arabic",
                NativeName = "العربية",
                Direction = TextDirection.RightToLeft,
                RequiresPreprocessing = true
            };
            
            Assert.AreEqual(TextDirection.RightToLeft, info.Direction);
            Assert.IsTrue(info.RequiresPreprocessing);
        }

        [Test]
        public void MultipleVoices_WithDefault()
        {
            var info = new LanguageInfo
            {
                Code = "en",
                AvailableVoices = new[] { "en-US-1", "en-US-2", "en-GB-1" },
                DefaultVoice = "en-US-1"
            };
            
            Assert.AreEqual(3, info.AvailableVoices.Length);
            Assert.Contains("en-US-1", info.AvailableVoices);
            Assert.AreEqual("en-US-1", info.DefaultVoice);
        }

        [Test]
        public void PhonemeSetType_Examples()
        {
            var jaInfo = new LanguageInfo
            {
                Code = "ja",
                PhonemeSetType = "Japanese-Kana"
            };
            
            var enInfo = new LanguageInfo
            {
                Code = "en",
                PhonemeSetType = "IPA-English"
            };
            
            Assert.AreEqual("Japanese-Kana", jaInfo.PhonemeSetType);
            Assert.AreEqual("IPA-English", enInfo.PhonemeSetType);
        }

        [Test]
        public void DefaultValues_AreCorrect()
        {
            var info = new LanguageInfo();
            
            Assert.IsNull(info.Code);
            Assert.IsNull(info.Name);
            Assert.IsNull(info.NativeName);
            Assert.IsFalse(info.RequiresPreprocessing);
            Assert.IsNotNull(info.AvailableVoices);
            Assert.AreEqual(0, info.AvailableVoices.Length);
            Assert.IsNull(info.DefaultVoice);
            Assert.IsNull(info.PhonemeSetType);
            Assert.IsFalse(info.SupportsAccent);
            Assert.AreEqual(TextDirection.LeftToRight, info.Direction);
        }
    }
}