using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using uPiper.Core.AudioGeneration;

namespace uPiper.Tests.Editor.AudioGeneration
{
    [TestFixture]
    public class SynthesisWithTimingResultTests
    {
        [Test]
        public void Constructor_NullAudioClip_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(
                () => new SynthesisWithTimingResult(null, null, 0f));
        }

        [Test]
        public void Constructor_NullTimings_HasTimingsReturnsFalse()
        {
            AudioClip clip = null;
            try
            {
                clip = AudioClip.Create("test", 1024, 1, 22050);
                var result = new SynthesisWithTimingResult(clip, null, 0f);

                Assert.IsFalse(result.HasTimings);
                Assert.IsNull(result.Timings);
            }
            finally
            {
                if (clip != null)
                    Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void Constructor_WithTimings_HasTimingsReturnsTrue()
        {
            AudioClip clip = null;
            try
            {
                clip = AudioClip.Create("test", 1024, 1, 22050);
                var timings = new List<PhonemeTimingEntry>
                {
                    new PhonemeTimingEntry("a", 0f, 0.5f),
                    new PhonemeTimingEntry("k", 0.5f, 0.8f),
                };
                var result = new SynthesisWithTimingResult(clip, timings, 0.8f);

                Assert.IsTrue(result.HasTimings);
                Assert.AreEqual(2, result.Timings.Count);
            }
            finally
            {
                if (clip != null)
                    Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void Constructor_WithTimings_CreatesDefensiveCopy()
        {
            AudioClip clip = null;
            try
            {
                clip = AudioClip.Create("test", 1024, 1, 22050);
                var timings = new List<PhonemeTimingEntry>
                {
                    new PhonemeTimingEntry("a", 0f, 0.5f),
                };
                var result = new SynthesisWithTimingResult(clip, timings, 0.5f);

                // Mutate the original list after construction.
                timings.Add(new PhonemeTimingEntry("k", 0.5f, 0.8f));

                Assert.AreEqual(1, result.Timings.Count);
            }
            finally
            {
                if (clip != null)
                    Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void Timings_IsReadOnly_CannotBeCastToMutableList()
        {
            AudioClip clip = null;
            try
            {
                clip = AudioClip.Create("test", 1024, 1, 22050);
                var timings = new List<PhonemeTimingEntry>
                {
                    new PhonemeTimingEntry("a", 0f, 0.5f),
                };
                var result = new SynthesisWithTimingResult(clip, timings, 0.5f);

                Assert.Throws<NotSupportedException>(
                    () => ((IList<PhonemeTimingEntry>)result.Timings)
                        .Add(new PhonemeTimingEntry("x", 0f, 0.1f)));
            }
            finally
            {
                if (clip != null)
                    Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void TotalDurationSeconds_ReturnsConstructorValue()
        {
            AudioClip clip = null;
            try
            {
                clip = AudioClip.Create("test", 1024, 1, 22050);
                var result = new SynthesisWithTimingResult(clip, null, 3.14f);

                Assert.AreEqual(3.14f, result.TotalDurationSeconds);
            }
            finally
            {
                if (clip != null)
                    Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void Constructor_EmptyTimings_HasTimingsReturnsTrue()
        {
            AudioClip clip = null;
            try
            {
                clip = AudioClip.Create("test", 1024, 1, 22050);
                var timings = new List<PhonemeTimingEntry>();
                var result = new SynthesisWithTimingResult(clip, timings, 0f);

                Assert.IsTrue(result.HasTimings);
                Assert.AreEqual(0, result.Timings.Count);
            }
            finally
            {
                if (clip != null)
                    Object.DestroyImmediate(clip);
            }
        }
        [Test]
        public void AudioClip_ReturnsSameInstance()
        {
            AudioClip clip = null;
            try
            {
                clip = AudioClip.Create("test", 1024, 1, 22050);
                var result = new SynthesisWithTimingResult(clip, null, 0f);

                Assert.AreSame(clip, result.AudioClip);
            }
            finally
            {
                if (clip != null)
                    Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void Constructor_WithTimings_PreservesEntryValues()
        {
            AudioClip clip = null;
            try
            {
                clip = AudioClip.Create("test", 1024, 1, 22050);
                var timings = new List<PhonemeTimingEntry>
                {
                    new PhonemeTimingEntry("a", 0.1f, 0.5f),
                    new PhonemeTimingEntry("k", 0.5f, 0.8f),
                };
                var result = new SynthesisWithTimingResult(clip, timings, 0.8f);

                Assert.AreEqual("a", result.Timings[0].Phoneme);
                Assert.AreEqual(0.1f, result.Timings[0].StartSeconds, 1e-7f);
                Assert.AreEqual(0.5f, result.Timings[0].EndSeconds, 1e-7f);
                Assert.AreEqual("k", result.Timings[1].Phoneme);
                Assert.AreEqual(0.5f, result.Timings[1].StartSeconds, 1e-7f);
                Assert.AreEqual(0.8f, result.Timings[1].EndSeconds, 1e-7f);
            }
            finally
            {
                if (clip != null)
                    Object.DestroyImmediate(clip);
            }
        }
    }
}
