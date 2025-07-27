using System;
using NUnit.Framework;
using uPiper.Core.Phonemizers;
using uPiper.Core.Phonemizers.Backend;

namespace uPiper.Tests.Runtime.Core.Phonemizers
{
    public class PhonemeResultTest
    {
        [Test]
        public void Constructor_InitializesWithEmptyArrays()
        {
            var result = new PhonemeResult();

            Assert.IsNotNull(result.Phonemes);
            Assert.IsNotNull(result.PhonemeIds);
            Assert.IsNotNull(result.Durations);
            Assert.IsNotNull(result.Pitches);
            Assert.AreEqual(0, result.Phonemes.Length);
            Assert.AreEqual(0, result.PhonemeIds.Length);
            Assert.AreEqual(0, result.Durations.Length);
            Assert.AreEqual(0, result.Pitches.Length);
        }

        [Test]
        public void Properties_CanBeSetAndRetrieved()
        {
            var result = new PhonemeResult
            {
                OriginalText = "test text",
                Phonemes = new[] { "t", "e", "s", "t" },
                PhonemeIds = new[] { 1, 2, 3, 4 },
                Durations = new[] { 0.1f, 0.2f, 0.1f, 0.15f },
                Pitches = new[] { 1.0f, 1.1f, 0.9f, 1.0f },
                Language = "en",
                ProcessingTime = TimeSpan.FromMilliseconds(50),
                ProcessingTimeMs = 50,
                FromCache = true,
                Metadata = new System.Collections.Generic.Dictionary<string, object> { { "test", "metadata" } }
            };

            Assert.AreEqual("test text", result.OriginalText);
            CollectionAssert.AreEqual(new[] { "t", "e", "s", "t" }, result.Phonemes);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, result.PhonemeIds);
            CollectionAssert.AreEqual(new[] { 0.1f, 0.2f, 0.1f, 0.15f }, result.Durations);
            CollectionAssert.AreEqual(new[] { 1.0f, 1.1f, 0.9f, 1.0f }, result.Pitches);
            Assert.AreEqual("en", result.Language);
            Assert.AreEqual(TimeSpan.FromMilliseconds(50), result.ProcessingTime);
            Assert.IsTrue(result.FromCache);
            Assert.IsNotNull(result.Metadata);
            Assert.AreEqual("metadata", result.Metadata["test"]);
        }

        [Test]
        public void Clone_CreatesDeepCopy()
        {
            var original = new PhonemeResult
            {
                OriginalText = "original",
                Phonemes = new[] { "a", "b", "c" },
                PhonemeIds = new[] { 1, 2, 3 },
                Durations = new[] { 0.1f, 0.2f, 0.3f },
                Pitches = new[] { 1.0f, 1.1f, 1.2f },
                Language = "ja",
                ProcessingTime = TimeSpan.FromMilliseconds(100),
                ProcessingTimeMs = 100,
                FromCache = true,
                Metadata = new System.Collections.Generic.Dictionary<string, object> { { "key", "metadata" } }
            };

            var clone = original.Clone();

            // Check values are copied
            Assert.AreEqual(original.OriginalText, clone.OriginalText);
            CollectionAssert.AreEqual(original.Phonemes, clone.Phonemes);
            CollectionAssert.AreEqual(original.PhonemeIds, clone.PhonemeIds);
            CollectionAssert.AreEqual(original.Durations, clone.Durations);
            CollectionAssert.AreEqual(original.Pitches, clone.Pitches);
            Assert.AreEqual(original.Language, clone.Language);
            Assert.AreEqual(original.ProcessingTime, clone.ProcessingTime);
            Assert.AreEqual(original.FromCache, clone.FromCache);
            // Metadata is copied but reference may be different
            Assert.IsNotNull(clone.Metadata);
            Assert.AreEqual(original.Metadata["key"], clone.Metadata["key"]);

            // Check arrays are different instances (deep copy)
            Assert.AreNotSame(original.Phonemes, clone.Phonemes);
            Assert.AreNotSame(original.PhonemeIds, clone.PhonemeIds);
            Assert.AreNotSame(original.Durations, clone.Durations);
            Assert.AreNotSame(original.Pitches, clone.Pitches);

            // Modify original arrays to ensure they're independent
            original.Phonemes[0] = "z";
            original.PhonemeIds[0] = 99;

            Assert.AreEqual("z", original.Phonemes[0]);
            Assert.AreEqual("a", clone.Phonemes[0]);
            Assert.AreEqual(99, original.PhonemeIds[0]);
            Assert.AreEqual(1, clone.PhonemeIds[0]);
        }

        [Test]
        public void Clone_HandlesNullArrays()
        {
            var original = new PhonemeResult
            {
                OriginalText = "test",
                Phonemes = null,
                PhonemeIds = null,
                Durations = null,
                Pitches = null
            };

            var clone = original.Clone();

            Assert.AreEqual("test", clone.OriginalText);
            Assert.IsNull(clone.Phonemes);
            Assert.IsNull(clone.PhonemeIds);
            Assert.IsNull(clone.Durations);
            Assert.IsNull(clone.Pitches);
        }

        [Test]
        public void ToString_FormatsCorrectly()
        {
            var result = new PhonemeResult
            {
                OriginalText = "test",
                Phonemes = new[] { "t", "e", "s", "t" },
                Language = "en",
                ProcessingTime = TimeSpan.FromMilliseconds(50.5),
                FromCache = false
            };

            var str = result.ToString();
            // Use regex to handle floating point formatting differences
            StringAssert.IsMatch(@"PhonemeResult: ""test"" -> \[t e s t\] \(en, 5\d\.\dms\)", str);

            result.FromCache = true;
            str = result.ToString();
            StringAssert.IsMatch(@"PhonemeResult: ""test"" -> \[t e s t\] \(en, 5\d\.\dms, cached\)", str);
        }

        [Test]
        public void ToString_HandlesNullPhonemes()
        {
            var result = new PhonemeResult
            {
                OriginalText = "test",
                Phonemes = null,
                Language = "en",
                ProcessingTime = TimeSpan.FromMilliseconds(10)
            };

            var str = result.ToString();
            Assert.AreEqual("PhonemeResult: \"test\" -> [null] (en, 10.0ms)", str);
        }

        [Test]
        public void ToString_HandlesEmptyPhonemes()
        {
            var result = new PhonemeResult
            {
                OriginalText = "test",
                Phonemes = new string[0],
                Language = "en",
                ProcessingTime = TimeSpan.FromMilliseconds(10)
            };

            var str = result.ToString();
            Assert.AreEqual("PhonemeResult: \"test\" -> [] (en, 10.0ms)", str);
        }
    }
}