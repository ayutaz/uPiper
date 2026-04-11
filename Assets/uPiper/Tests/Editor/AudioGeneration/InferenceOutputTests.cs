using NUnit.Framework;
using Unity.Collections;
using uPiper.Core.AudioGeneration;

namespace uPiper.Tests.Editor.AudioGeneration
{
    /// <summary>
    /// <see cref="InferenceOutput"/> の Dispose パターンおよび HasDurations プロパティのテスト。
    /// InferenceOutput は sealed class のため defensive-copy 問題は発生しない。
    /// </summary>
    [TestFixture]
    public class InferenceOutputTests
    {
        // ================================================================
        // Dispose Tests
        // ================================================================

        /// <summary>
        /// Audio と Durations の両方が有効な NativeArray の場合、
        /// Dispose で両方とも破棄されること。
        /// </summary>
        [Test]
        public void Dispose_BothArraysCreated_DisposesAudioAndDurations()
        {
            var audio = new NativeArray<float>(100, Allocator.Temp);
            var durations = new NativeArray<float>(10, Allocator.Temp);
            try
            {
                var output = new InferenceOutput(audio, durations);
                output.Dispose();

                Assert.IsFalse(audio.IsCreated, "Audio NativeArray should be disposed");
                Assert.IsFalse(durations.IsCreated, "Durations NativeArray should be disposed");
            }
            catch
            {
                if (audio.IsCreated) audio.Dispose();
                if (durations.IsCreated) durations.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Durations が default(未作成)の場合、Audio のみ破棄され例外が出ないこと。
        /// </summary>
        [Test]
        public void Dispose_DurationsNotCreated_DisposesOnlyAudio()
        {
            var audio = new NativeArray<float>(100, Allocator.Temp);
            try
            {
                var output = new InferenceOutput(audio, default);

                Assert.DoesNotThrow(() => output.Dispose());
                Assert.IsFalse(audio.IsCreated, "Audio NativeArray should be disposed");
            }
            catch
            {
                if (audio.IsCreated) audio.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Audio が default(未作成)の場合、Durations のみ破棄され例外が出ないこと。
        /// </summary>
        [Test]
        public void Dispose_AudioNotCreated_DisposesOnlyDurations()
        {
            var durations = new NativeArray<float>(10, Allocator.Temp);
            try
            {
                var output = new InferenceOutput(default, durations);

                Assert.DoesNotThrow(() => output.Dispose());
                Assert.IsFalse(durations.IsCreated, "Durations NativeArray should be disposed");
            }
            catch
            {
                if (durations.IsCreated) durations.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Audio と Durations の両方が default(未作成)の場合、例外が出ないこと。
        /// </summary>
        [Test]
        public void Dispose_BothNotCreated_NoException()
        {
            var output = new InferenceOutput(default, default);

            Assert.DoesNotThrow(() => output.Dispose());
        }

        /// <summary>
        /// sealed class の _disposed フラグにより二重 Dispose が安全であることを検証。
        /// </summary>
        [Test]
        public void Dispose_CalledTwice_NoException()
        {
            var audio = new NativeArray<float>(100, Allocator.Temp);
            var durations = new NativeArray<float>(10, Allocator.Temp);
            try
            {
                var output = new InferenceOutput(audio, durations);
                output.Dispose();

                Assert.DoesNotThrow(() => output.Dispose(), "Second Dispose should be safely ignored");
            }
            catch
            {
                if (audio.IsCreated) audio.Dispose();
                if (durations.IsCreated) durations.Dispose();
                throw;
            }
        }

        // ================================================================
        // HasDurations Tests
        // ================================================================

        /// <summary>
        /// Durations が有効な NativeArray の場合、HasDurations が true を返すこと。
        /// </summary>
        [Test]
        public void HasDurations_WithDurations_ReturnsTrue()
        {
            var audio = new NativeArray<float>(100, Allocator.Temp);
            var durations = new NativeArray<float>(10, Allocator.Temp);
            try
            {
                var output = new InferenceOutput(audio, durations);

                Assert.IsTrue(output.HasDurations);

                output.Dispose();
            }
            catch
            {
                if (audio.IsCreated) audio.Dispose();
                if (durations.IsCreated) durations.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Durations が default の場合、HasDurations が false を返すこと。
        /// </summary>
        [Test]
        public void HasDurations_WithoutDurations_ReturnsFalse()
        {
            var audio = new NativeArray<float>(100, Allocator.Temp);
            try
            {
                var output = new InferenceOutput(audio, default);

                Assert.IsFalse(output.HasDurations);

                output.Dispose();
            }
            catch
            {
                if (audio.IsCreated) audio.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Audio と Durations の両方が default の場合、HasDurations が false を返すこと。
        /// </summary>
        [Test]
        public void HasDurations_BothDefault_ReturnsFalse()
        {
            var output = new InferenceOutput(default, default);

            Assert.IsFalse(output.HasDurations);

            output.Dispose();
        }

        // ================================================================
        // Property Access Tests
        // ================================================================

        /// <summary>
        /// Audio プロパティがコンストラクタで渡した NativeArray と同じ内容を参照できること。
        /// </summary>
        [Test]
        public void Audio_Property_ReturnsSameArray()
        {
            var audio = new NativeArray<float>(4, Allocator.Temp);
            try
            {
                audio[0] = 0.1f;
                audio[1] = 0.2f;
                audio[2] = 0.3f;
                audio[3] = 0.4f;

                var output = new InferenceOutput(audio, default);

                Assert.AreEqual(4, output.Audio.Length);
                Assert.AreEqual(0.1f, output.Audio[0], 1e-7f);
                Assert.AreEqual(0.2f, output.Audio[1], 1e-7f);
                Assert.AreEqual(0.3f, output.Audio[2], 1e-7f);
                Assert.AreEqual(0.4f, output.Audio[3], 1e-7f);

                output.Dispose();
            }
            catch
            {
                if (audio.IsCreated) audio.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Durations プロパティがコンストラクタで渡した NativeArray と同じ内容を参照できること。
        /// </summary>
        [Test]
        public void Durations_Property_ReturnsSameArray()
        {
            var durations = new NativeArray<float>(3, Allocator.Temp);
            try
            {
                durations[0] = 5.0f;
                durations[1] = 10.0f;
                durations[2] = 7.5f;

                var output = new InferenceOutput(default, durations);

                Assert.AreEqual(3, output.Durations.Length);
                Assert.AreEqual(5.0f, output.Durations[0], 1e-7f);
                Assert.AreEqual(10.0f, output.Durations[1], 1e-7f);
                Assert.AreEqual(7.5f, output.Durations[2], 1e-7f);

                output.Dispose();
            }
            catch
            {
                if (durations.IsCreated) durations.Dispose();
                throw;
            }
        }

        // ================================================================
        // DetachAudio / DetachDurations Tests
        // ================================================================

        /// <summary>
        /// DetachAudio が元の Audio を返し、内部フィールドをクリアすること。
        /// Dispose 後も detached Audio が有効であること。
        /// </summary>
        [Test]
        public void DetachAudio_ReturnsAudioAndClearsInternal()
        {
            var audio = new NativeArray<float>(50, Allocator.Temp);
            audio[0] = 42f;
            var durations = new NativeArray<float>(5, Allocator.Temp);
            NativeArray<float> detached = default;
            try
            {
                var output = new InferenceOutput(audio, durations);
                detached = output.DetachAudio();

                Assert.AreEqual(50, detached.Length, "DetachAudio は元の Audio を返すこと");
                Assert.AreEqual(42f, detached[0], "DetachAudio は元の値を保持すること");
                Assert.IsFalse(output.Audio.IsCreated,
                    "Detach 後に Audio プロパティは default を返すこと");

                // Dispose は Durations のみ解放し、detached Audio には影響しない
                output.Dispose();
                Assert.IsTrue(detached.IsCreated,
                    "Dispose 後も detached Audio は有効であること");
                Assert.IsFalse(durations.IsCreated,
                    "Dispose で Durations は解放されること");
            }
            finally
            {
                if (detached.IsCreated) detached.Dispose();
                if (audio.IsCreated) audio.Dispose();
                if (durations.IsCreated) durations.Dispose();
            }
        }

        /// <summary>
        /// DetachDurations が元の Durations を返し、内部フィールドをクリアすること。
        /// Dispose 後も detached Durations が有効であること。
        /// </summary>
        [Test]
        public void DetachDurations_ReturnsDurationsAndClearsInternal()
        {
            var audio = new NativeArray<float>(50, Allocator.Temp);
            var durations = new NativeArray<float>(5, Allocator.Temp);
            durations[0] = 7f;
            NativeArray<float> detached = default;
            try
            {
                var output = new InferenceOutput(audio, durations);
                detached = output.DetachDurations();

                Assert.AreEqual(5, detached.Length);
                Assert.AreEqual(7f, detached[0]);
                Assert.IsFalse(output.HasDurations,
                    "Detach 後に HasDurations は false を返すこと");

                output.Dispose();
                Assert.IsTrue(detached.IsCreated,
                    "Dispose 後も detached Durations は有効であること");
                Assert.IsFalse(audio.IsCreated,
                    "Dispose で Audio は解放されること");
            }
            finally
            {
                if (detached.IsCreated) detached.Dispose();
                if (audio.IsCreated) audio.Dispose();
                if (durations.IsCreated) durations.Dispose();
            }
        }

        /// <summary>
        /// 両方 Detach 後の Dispose が例外を投げないこと。
        /// </summary>
        [Test]
        public void Dispose_AfterDetachBoth_NoException()
        {
            var audio = new NativeArray<float>(10, Allocator.Temp);
            var durations = new NativeArray<float>(5, Allocator.Temp);
            NativeArray<float> detachedAudio = default;
            NativeArray<float> detachedDurations = default;
            try
            {
                var output = new InferenceOutput(audio, durations);
                detachedAudio = output.DetachAudio();
                detachedDurations = output.DetachDurations();

                Assert.DoesNotThrow(() => output.Dispose(),
                    "両方 Detach 後の Dispose は例外を投げないこと");
            }
            finally
            {
                if (detachedAudio.IsCreated) detachedAudio.Dispose();
                if (detachedDurations.IsCreated) detachedDurations.Dispose();
            }
        }
    }
}
