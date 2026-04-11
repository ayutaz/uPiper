using System.Threading.Tasks;
using NUnit.Framework;
using uPiper.Core.AudioGeneration;

namespace uPiper.Tests.Editor.AudioGeneration
{
    /// <summary>
    /// <see cref="ShortTextMitigatingGenerator"/> の durations PAD 除去ロジックを検証するテスト。
    /// 短テキストパディング時に挿入された PAD に対応する durations エントリが
    /// 正しく除去され、元の音素数に一致する durations が返されることを確認する。
    /// </summary>
    [TestFixture]
    public class ShortTextMitigatingGeneratorTimingTests
    {
        private const int MinPhonemeIds = 40; // ShortTextProcessor.MinPhonemeIds と同値

        private StubInferenceAudioGenerator _stubGenerator;
        private ShortTextMitigatingGenerator _mitigator;

        [SetUp]
        public void SetUp()
        {
            _stubGenerator = new StubInferenceAudioGenerator
            {
                SupportsDurations = true,
            };
            _mitigator = new ShortTextMitigatingGenerator(_stubGenerator);
        }

        // ================================================================
        // Test 1: ShortInput — durations が PAD 除去される
        // ================================================================

        [Test]
        public async Task GenerateAudioAsync_ShortInput_DurationsStrippedOfPadding()
        {
            // Arrange: phonemeIds = [1, 10, 11, 12, 2] (5要素, deficit=35)
            // afterBos = 17, beforeEos = 18
            var phonemeIds = new[] { 1, 10, 11, 12, 2 };
            _stubGenerator.AudioDataToReturn = CreateNonSilentAudio(1000);
            _stubGenerator.DurationsToReturn = CreateSequentialDurations(MinPhonemeIds);

            // Act
            var output = await _mitigator.GenerateAudioAsync(phonemeIds);
            try
            {
                // Assert
                Assert.IsTrue(output.HasDurations);
                Assert.AreEqual(5, output.Durations.Length,
                    "Durations should be stripped back to original phoneme count");
                Assert.AreEqual(1.0f, output.Durations[0], 1e-6f,
                    "BOS duration should be DurationsToReturn[0]");
                Assert.AreEqual(40.0f, output.Durations[4], 1e-6f,
                    "EOS duration should be DurationsToReturn[39]");
            }
            finally
            {
                output.Dispose();
            }
        }

        // ================================================================
        // Test 2: ShortInput — durations 長が元の音素数に一致
        // ================================================================

        [Test]
        public async Task GenerateAudioAsync_ShortInput_DurationsCountMatchesOriginal()
        {
            // Arrange: 10要素 (deficit=30, afterBos=15, beforeEos=15)
            var phonemeIds = CreatePhonemeIds(10);
            _stubGenerator.AudioDataToReturn = CreateNonSilentAudio(1000);
            _stubGenerator.DurationsToReturn = CreateSequentialDurations(MinPhonemeIds);

            // Act
            var output = await _mitigator.GenerateAudioAsync(phonemeIds);
            try
            {
                // Assert
                Assert.IsTrue(output.HasDurations);
                Assert.AreEqual(10, output.Durations.Length,
                    "Durations length should match original phoneme count (10)");
            }
            finally
            {
                output.Dispose();
            }
        }

        // ================================================================
        // Test 3: ShortInput — BOS/EOS/body の duration 値が保持される
        // ================================================================

        [Test]
        public async Task GenerateAudioAsync_ShortInput_BosEosDurationsPreserved()
        {
            // Arrange: phonemeIds = [1, 10, 2] (3要素, deficit=37, afterBos=18, beforeEos=19)
            var phonemeIds = new[] { 1, 10, 2 };
            _stubGenerator.AudioDataToReturn = CreateNonSilentAudio(1000);

            var durations = new float[MinPhonemeIds];
            durations[0] = 5.0f;           // BOS
            durations[1 + 18] = 10.0f;     // body (index 19: 1 + afterBos)
            durations[MinPhonemeIds - 1] = 3.0f; // EOS (index 39)
            _stubGenerator.DurationsToReturn = durations;

            // Act
            var output = await _mitigator.GenerateAudioAsync(phonemeIds);
            try
            {
                // Assert
                Assert.IsTrue(output.HasDurations);
                Assert.AreEqual(3, output.Durations.Length);
                Assert.AreEqual(5.0f, output.Durations[0], 1e-6f,
                    "BOS duration should be preserved");
                Assert.AreEqual(10.0f, output.Durations[1], 1e-6f,
                    "Body duration should be preserved");
                Assert.AreEqual(3.0f, output.Durations[2], 1e-6f,
                    "EOS duration should be preserved");
            }
            finally
            {
                output.Dispose();
            }
        }

        // ================================================================
        // Test 4: ExactMinimum — パディング不要で durations 変更なし
        // ================================================================

        [Test]
        public async Task GenerateAudioAsync_ExactMinimum_NoPaddingNoDurationsChange()
        {
            // Arrange: phonemeIds = int[40] (パディング不要)
            var phonemeIds = CreatePhonemeIds(MinPhonemeIds);
            var expectedDurations = CreateSequentialDurations(MinPhonemeIds);
            _stubGenerator.AudioDataToReturn = CreateNonSilentAudio(1000);
            _stubGenerator.DurationsToReturn = expectedDurations;

            // Act
            var output = await _mitigator.GenerateAudioAsync(phonemeIds);
            try
            {
                // Assert
                Assert.IsTrue(output.HasDurations);
                Assert.AreEqual(MinPhonemeIds, output.Durations.Length,
                    "Durations length should remain 40 when no padding needed");
                for (var i = 0; i < MinPhonemeIds; i++)
                {
                    Assert.AreEqual(expectedDurations[i], output.Durations[i], 1e-6f,
                        $"Durations[{i}] should match original value");
                }
            }
            finally
            {
                output.Dispose();
            }
        }

        // ================================================================
        // Test 5: LongInput — パディング不要で durations 変更なし
        // ================================================================

        [Test]
        public async Task GenerateAudioAsync_LongInput_NoPaddingNoDurationsChange()
        {
            // Arrange: phonemeIds = int[60]
            var phonemeIds = CreatePhonemeIds(60);
            var expectedDurations = CreateSequentialDurations(60);
            _stubGenerator.AudioDataToReturn = CreateNonSilentAudio(1000);
            _stubGenerator.DurationsToReturn = expectedDurations;

            // Act
            var output = await _mitigator.GenerateAudioAsync(phonemeIds);
            try
            {
                // Assert
                Assert.IsTrue(output.HasDurations);
                Assert.AreEqual(60, output.Durations.Length,
                    "Durations length should remain 60 when no padding needed");
                for (var i = 0; i < 60; i++)
                {
                    Assert.AreEqual(expectedDurations[i], output.Durations[i], 1e-6f,
                        $"Durations[{i}] should match original value");
                }
            }
            finally
            {
                output.Dispose();
            }
        }

        // ================================================================
        // Test 6: ShortInput, durations なしモデル — 例外が発生しない
        // ================================================================

        [Test]
        public async Task GenerateAudioAsync_ShortInput_NoDurations_HasDurationsFalse()
        {
            // Arrange: phonemeIds = [1, 10, 11, 2] (4要素, パディング対象)
            var phonemeIds = new[] { 1, 10, 11, 2 };
            _stubGenerator.AudioDataToReturn = CreateNonSilentAudio(1000);
            _stubGenerator.DurationsToReturn = null; // durations 非対応モデル
            _stubGenerator.SupportsDurations = false;

            // Act
            var output = await _mitigator.GenerateAudioAsync(phonemeIds);
            try
            {
                // Assert
                Assert.IsFalse(output.HasDurations,
                    "HasDurations should be false when model does not support durations");
            }
            finally
            {
                output.Dispose();
            }
        }

        // ================================================================
        // Test 7: EvenDeficit — 対称パディング除去
        // ================================================================

        [Test]
        public async Task GenerateAudioAsync_ShortInput_EvenDeficit_SymmetricPadRemoval()
        {
            // Arrange: phonemeIds = int[20] (deficit=20, afterBos=10, beforeEos=10)
            var phonemeIds = CreatePhonemeIds(20);
            _stubGenerator.AudioDataToReturn = CreateNonSilentAudio(1000);
            _stubGenerator.DurationsToReturn = CreateSequentialDurations(MinPhonemeIds);

            // Act
            var output = await _mitigator.GenerateAudioAsync(phonemeIds);
            try
            {
                // Assert
                Assert.IsTrue(output.HasDurations);
                Assert.AreEqual(20, output.Durations.Length,
                    "Durations should be stripped back to 20 (symmetric pad removal)");
            }
            finally
            {
                output.Dispose();
            }
        }

        // ================================================================
        // Test 8: OddDeficit — 非対称パディング除去
        // ================================================================

        [Test]
        public async Task GenerateAudioAsync_ShortInput_OddDeficit_AsymmetricPadRemoval()
        {
            // Arrange: phonemeIds = int[21] (deficit=19, afterBos=9, beforeEos=10)
            var phonemeIds = CreatePhonemeIds(21);
            _stubGenerator.AudioDataToReturn = CreateNonSilentAudio(1000);
            _stubGenerator.DurationsToReturn = CreateSequentialDurations(MinPhonemeIds);

            // Act
            var output = await _mitigator.GenerateAudioAsync(phonemeIds);
            try
            {
                // Assert
                Assert.IsTrue(output.HasDurations);
                Assert.AreEqual(21, output.Durations.Length,
                    "Durations should be stripped back to 21 (asymmetric pad removal)");

                // BOS duration: durations[0] = 1.0f
                Assert.AreEqual(1.0f, output.Durations[0], 1e-6f,
                    "BOS duration should come from DurationsToReturn[0]");

                // Body durations: afterBos=9, so body starts at padded index 1+9=10
                // Body has 21 - 2 = 19 elements (indices 1..19 in original)
                // In padded durations, body occupies indices 10..28
                // In cleaned durations, body occupies indices 1..19
                var afterBos = 9;
                for (var i = 1; i < 20; i++)
                {
                    var paddedIndex = i + afterBos;
                    var expected = (float)(paddedIndex + 1); // CreateSequentialDurations: d[j] = j+1
                    Assert.AreEqual(expected, output.Durations[i], 1e-6f,
                        $"Body Durations[{i}] should come from padded index {paddedIndex}");
                }

                // EOS duration: durations[39] = 40.0f
                Assert.AreEqual(40.0f, output.Durations[20], 1e-6f,
                    "EOS duration should come from DurationsToReturn[39]");
            }
            finally
            {
                output.Dispose();
            }
        }

        // ================================================================
        // Test 9: ShortInput + Durations — Dispose が安全に完了する
        // ================================================================

        [Test]
        public async Task GenerateAudioAsync_ShortInput_WithDurations_DisposesCorrectly()
        {
            // Arrange: PAD あり + Durations あり
            var phonemeIds = CreatePhonemeIds(5); // deficit=35
            _stubGenerator.AudioDataToReturn = CreateNonSilentAudio(1000);
            _stubGenerator.DurationsToReturn = CreateSequentialDurations(MinPhonemeIds);
            _stubGenerator.SupportsDurations = true;

            // Act
            var output = await _mitigator.GenerateAudioAsync(phonemeIds);

            // Assert: output が正常で、Dispose しても例外なし
            try
            {
                Assert.IsTrue(output.HasDurations, "PAD除去後もdurationsがあること");
                Assert.AreEqual(5, output.Durations.Length,
                    "除去後のdurations長が元の長さと一致すること");
            }
            finally
            {
                Assert.DoesNotThrow(() => output.Dispose(),
                    "Dispose が安全に完了すること");
            }
        }

        // ================================================================
        // Test 10: ShortInput, 非ゼロ Audio — Dispose が安全に完了する
        // ================================================================

        [Test]
        public async Task GenerateAudioAsync_ShortInput_NonSilentAudio_OutputDisposable()
        {
            // Arrange: 非ゼロの Audio → TrimSilence はトリムしない（元の NativeArray をそのまま返す）
            var phonemeIds = CreatePhonemeIds(5);
            _stubGenerator.AudioDataToReturn = CreateNonSilentAudio(1000);
            _stubGenerator.DurationsToReturn = null; // durations なし
            _stubGenerator.SupportsDurations = false;

            // Act
            var output = await _mitigator.GenerateAudioAsync(phonemeIds);

            // Assert
            try
            {
                Assert.IsFalse(output.HasDurations);
                Assert.IsTrue(output.Audio.IsCreated, "Audio が有効であること");
            }
            finally
            {
                Assert.DoesNotThrow(() => output.Dispose(),
                    "Dispose が安全に完了すること");
            }
        }

        // ================================================================
        // Helpers
        // ================================================================

        /// <summary>
        /// BOS(1) + body(連番10,11,...) + EOS(2) の構造を持つ phonemeIds を構築する。
        /// </summary>
        private static int[] CreatePhonemeIds(int length)
        {
            if (length < 2) return new[] { 1 };
            var ids = new int[length];
            ids[0] = 1; // BOS
            ids[length - 1] = 2; // EOS
            for (var i = 1; i < length - 1; i++)
                ids[i] = 10 + i - 1;
            return ids;
        }

        /// <summary>
        /// 連番の durations 配列を構築する。durations[i] = (float)(i + 1) で一意性を保証。
        /// </summary>
        private static float[] CreateSequentialDurations(int length)
        {
            var durations = new float[length];
            for (var i = 0; i < length; i++)
                durations[i] = (float)(i + 1);
            return durations;
        }

        /// <summary>
        /// 指定値で埋められた固定長オーディオデータを生成する。
        /// TrimSilenceでカットされないよう、非ゼロ値で大きめに作る。
        /// </summary>
        private static float[] CreateNonSilentAudio(int length)
        {
            var data = new float[length];
            for (var i = 0; i < length; i++)
                data[i] = 0.5f;
            return data;
        }
    }
}
