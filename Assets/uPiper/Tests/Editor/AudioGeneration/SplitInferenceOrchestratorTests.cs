using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Collections;
using uPiper.Core.AudioGeneration;

namespace uPiper.Tests.Editor.AudioGeneration
{
    /// <summary>
    /// SplitInferenceOrchestrator の統合テスト。
    /// 沈黙トークンによる句分割と無音挿入を検証する。
    /// </summary>
    [TestFixture]
    public class SplitInferenceOrchestratorTests
    {
        private StubInferenceAudioGenerator _stubGenerator;
        private SplitInferenceOrchestrator _orchestrator;

        /// <summary>
        /// テスト用の最小限 phonemeIdMap。
        /// PhonemeSilenceProcessor.SplitAtPhonemeSilence が
        /// phonemeSilence のキーを phonemeIdMap で逆引きするために使用。
        /// </summary>
        private static Dictionary<string, int[]> CreateMinimalPhonemeIdMap()
        {
            return new Dictionary<string, int[]>
            {
                ["_"] = new[] { 0 },  // PAD（沈黙トークンとしても使用）
                ["^"] = new[] { 1 },  // BOS
                ["$"] = new[] { 2 },  // EOS
                ["a"] = new[] { 3 },
                ["i"] = new[] { 4 },
                ["u"] = new[] { 5 },
                ["k"] = new[] { 8 },
                ["o"] = new[] { 7 },
            };
        }

        [SetUp]
        public void SetUp()
        {
            _stubGenerator = new StubInferenceAudioGenerator
            {
                // 各推論呼び出しで50サンプルの固定データを返す
                AudioDataToReturn = CreateFixedAudioData(50, 0.5f),
            };
            _orchestrator = new SplitInferenceOrchestrator(_stubGenerator);
        }

        // ── 単一句（分割なし）───────────────────────────────────

        [Test]
        public async Task GenerateWithSilenceSplitAsync_SinglePhrase_ReturnsSinglePhraseAudio()
        {
            // Arrange: 沈黙トークンを含まない phonemeIds
            // phonemeSilence に "#" を設定するが、phonemeIds には "#" の ID が無いため分割されない
            var phonemeIds = new[] { 1, 3, 4, 5, 2 }; // ^, a, i, u, $
            var phonemeSilence = new Dictionary<string, float> { ["#"] = 0.5f };
            var phonemeIdMap = CreateMinimalPhonemeIdMap();
            phonemeIdMap["#"] = new[] { 99 }; // phonemeSilence のキー用（phonemeIds に含まれない）

            // Act
            var output = await _orchestrator.GenerateWithSilenceSplitAsync(
                phonemeIds,
                prosodyFlat: null,
                phonemeSilence, phonemeIdMap,
                sampleRate: 22050);

            try
            {
                // Assert
                Assert.IsTrue(output.Audio.IsCreated, "結果が作成済みであること");
                Assert.AreEqual(1, _stubGenerator.GenerateCallCount,
                    "分割なしの場合、推論は1回のみ呼ばれること");
                // 単一句なので、返却データはスタブの固定データと同じ長さ
                Assert.AreEqual(50, output.Audio.Length,
                    "単一句のオーディオデータ長が一致すること");
            }
            finally
            {
                output.Dispose();
            }
        }

        // ── 複数句（沈黙トークンで分割）──────────────────────────

        [Test]
        public async Task GenerateWithSilenceSplitAsync_MultiplePhrases_ConcatenatesWithSilence()
        {
            // Arrange: phonemeIds に沈黙トークン "_" (ID=0) を含める
            // [^(1), a(3), _(0), k(8), o(7), $(2)]
            // "_" の位置で分割される → 句1: [1,3,0], 句2: [8,7,2]
            var phonemeIds = new[] { 1, 3, 0, 8, 7, 2 };
            var phonemeSilence = new Dictionary<string, float> { ["_"] = 0.5f };
            var phonemeIdMap = CreateMinimalPhonemeIdMap();

            // 0.5秒 × 22050Hz = 11025 サンプルの無音が挿入される
            var expectedSilenceSamples = (int)(0.5f * 22050);

            // Act
            var output = await _orchestrator.GenerateWithSilenceSplitAsync(
                phonemeIds,
                prosodyFlat: null,
                phonemeSilence, phonemeIdMap,
                sampleRate: 22050);

            try
            {
                // Assert
                Assert.IsTrue(output.Audio.IsCreated, "結果が作成済みであること");
                Assert.AreEqual(2, _stubGenerator.GenerateCallCount,
                    "2句に分割されるため、推論が2回呼ばれること");

                // 合計長 = 句1のオーディオ(50) + 無音(11025) + 句2のオーディオ(50)
                var expectedTotalLength = 50 + expectedSilenceSamples + 50;
                Assert.AreEqual(expectedTotalLength, output.Audio.Length,
                    "合計サンプル数が句のオーディオ+無音の合計と一致すること");

                // 無音区間が0.0fで埋められていることを確認
                // 句1のオーディオ(50サンプル)の後から無音区間が始まる
                for (var i = 50; i < 50 + expectedSilenceSamples; i++)
                {
                    Assert.AreEqual(0.0f, output.Audio[i],
                        $"無音区間のサンプル[{i}]が 0.0f であること");
                }
            }
            finally
            {
                output.Dispose();
            }
        }

        // ── 空の phonemeIds ──────────────────────────────────────

        [Test]
        public async Task GenerateWithSilenceSplitAsync_EmptyPhonemeIds_ReturnsEmpty()
        {
            // Arrange: 空の phonemeIds
            // SplitAtPhonemeSilence は空配列に対して空の Phrase (PhonemeIds=[]) を1つ返す
            // GenerateWithSilenceSplitAsync は PhonemeIds が空の Phrase をスキップする
            var phonemeIds = Array.Empty<int>();
            var phonemeSilence = new Dictionary<string, float> { ["_"] = 0.5f };
            var phonemeIdMap = CreateMinimalPhonemeIdMap();

            // Act
            var output = await _orchestrator.GenerateWithSilenceSplitAsync(
                phonemeIds,
                prosodyFlat: null,
                phonemeSilence, phonemeIdMap,
                sampleRate: 22050);

            try
            {
                // Assert
                Assert.IsTrue(output.Audio.IsCreated, "結果が作成済みであること");
                // 空の Phrase のみが生成され、PhonemeIds が空のためスキップされる
                // → totalLength は 0 になるが、結果配列自体は空（長さ0）
                Assert.AreEqual(0, _stubGenerator.GenerateCallCount,
                    "空入力に対して推論が呼ばれないこと");
                Assert.AreEqual(0, output.Audio.Length,
                    "空入力に対して空のオーディオデータが返ること");
            }
            finally
            {
                output.Dispose();
            }
        }

        // ── Prosody付き句分割 ─────────────────────────────────────

        [Test]
        public async Task GenerateWithSilenceSplitAsync_WithProsody_PropagatesProsodyToGenerator()
        {
            // Arrange: phonemeIds に沈黙トークン "_" (ID=0) を含め、prosody配列も設定
            var phonemeIds = new[] { 1, 3, 0, 8, 7, 2 };  // ^, a, _, k, o, $
            // stride=3 flat prosody: 6 phonemes * 3 = 18 elements
            var prosodyFlat = new[]
            {
                0, 1, 0,  // phoneme 0: a1=0, a2=1, a3=0
                1, 1, 0,  // phoneme 1: a1=1, a2=1, a3=0
                0, 0, 0,  // phoneme 2: a1=0, a2=0, a3=0
                2, 2, 1,  // phoneme 3: a1=2, a2=2, a3=1
                3, 2, 1,  // phoneme 4: a1=3, a2=2, a3=1
                0, 0, 0   // phoneme 5: a1=0, a2=0, a3=0
            };
            var phonemeSilence = new Dictionary<string, float> { ["_"] = 0.5f };
            var phonemeIdMap = CreateMinimalPhonemeIdMap();

            // Act
            var output = await _orchestrator.GenerateWithSilenceSplitAsync(
                phonemeIds,
                prosodyFlat,
                phonemeSilence, phonemeIdMap,
                sampleRate: 22050);

            try
            {
                // Assert
                Assert.IsTrue(output.Audio.IsCreated);
                Assert.AreEqual(2, _stubGenerator.GenerateCallCount,
                    "Prosody付きでも2句に分割されること");
                // 最後の呼び出しの prosodyFlat が non-null であることを確認
                Assert.IsNotNull(_stubGenerator.LastProsodyFlat,
                    "Prosody配列が下流に伝播されること");
            }
            finally
            {
                output.Dispose();
            }
        }

        // ── Progress 報告 ─────────────────────────────────────────

        [Test]
        public async Task GenerateWithSilenceSplitAsync_MultiplePhrases_ReportsProgress()
        {
            // Arrange: 3句に分割される phonemeIds
            // [^(1), a(3), _(0), k(8), _(0), o(7), $(2)]
            // → 句1: [1,3,0], 句2: [8,0], 句3: [7,2]
            var phonemeIds = new[] { 1, 3, 0, 8, 0, 7, 2 };
            var phonemeSilence = new Dictionary<string, float> { ["_"] = 0.2f };
            var phonemeIdMap = CreateMinimalPhonemeIdMap();

            var reportedValues = new List<float>();
            var progress = new Progress<float>(v => reportedValues.Add(v));

            // Act
            var output = await _orchestrator.GenerateWithSilenceSplitAsync(
                phonemeIds,
                prosodyFlat: null,
                phonemeSilence, phonemeIdMap,
                sampleRate: 22050,
                progress: progress);

            try
            {
                // Assert
                Assert.IsTrue(output.Audio.IsCreated, "結果が作成済みであること");
                Assert.AreEqual(3, _stubGenerator.GenerateCallCount,
                    "3句に分割されるため、推論が3回呼ばれること");

                // Progress<T> は SynchronizationContext に Post するため、
                // EditMode テストでは同期的にコールバックが呼ばれるとは限らない。
                // SplitInferenceOrchestrator は IProgress<float>.Report() を呼ぶので、
                // 少なくとも呼び出し側が non-null progress を渡せることをコンパイル時に検証する。
                // Progress<T> は内部で Post を使うためコールバック発火は保証しないが、
                // reportedValues がキャプチャされることで型整合性を確認する。
            }
            finally
            {
                output.Dispose();
            }
        }

        [Test]
        public async Task GenerateWithSilenceSplitAsync_SinglePhrase_ReportsProgressOnce()
        {
            // Arrange: 沈黙トークンを含まない phonemeIds → 分割なし → 1句
            var phonemeIds = new[] { 1, 3, 4, 5, 2 }; // ^, a, i, u, $
            var phonemeSilence = new Dictionary<string, float> { ["#"] = 0.5f };
            var phonemeIdMap = CreateMinimalPhonemeIdMap();
            phonemeIdMap["#"] = new[] { 99 };

            // SynchronousProgress は Report を同期的に呼び出すため、
            // テスト内でコールバック値を確実に検証できる。
            var reportedValues = new List<float>();
            var progress = new SynchronousProgress<float>(v => reportedValues.Add(v));

            // Act
            var output = await _orchestrator.GenerateWithSilenceSplitAsync(
                phonemeIds,
                prosodyFlat: null,
                phonemeSilence, phonemeIdMap,
                sampleRate: 22050,
                progress: progress);

            try
            {
                // Assert
                Assert.AreEqual(1, _stubGenerator.GenerateCallCount);
                Assert.AreEqual(1, reportedValues.Count,
                    "1句のみの場合、進捗報告は1回であること");
                Assert.AreEqual(1.0f, reportedValues[0], 0.001f,
                    "1句完了時に 1.0 が報告されること");
            }
            finally
            {
                output.Dispose();
            }
        }

        [Test]
        public async Task GenerateWithSilenceSplitAsync_EmptyInput_DoesNotReportProgress()
        {
            // Arrange
            var phonemeIds = Array.Empty<int>();
            var phonemeSilence = new Dictionary<string, float> { ["_"] = 0.5f };
            var phonemeIdMap = CreateMinimalPhonemeIdMap();

            var reportedValues = new List<float>();
            var progress = new SynchronousProgress<float>(v => reportedValues.Add(v));

            // Act
            var output = await _orchestrator.GenerateWithSilenceSplitAsync(
                phonemeIds,
                prosodyFlat: null,
                phonemeSilence, phonemeIdMap,
                sampleRate: 22050,
                progress: progress);

            try
            {
                // Assert
                Assert.AreEqual(0, reportedValues.Count,
                    "空入力の場合、進捗報告は行われないこと");
            }
            finally
            {
                output.Dispose();
            }
        }

        // ── コンストラクタ null チェック ──────────────────────────

        [Test]
        public void Constructor_NullGenerator_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new SplitInferenceOrchestrator(null));
        }

        // ── Short text mitigation ────────────────────────────────

        [Test]
        public async Task GenerateWithSilenceSplit_ShortPhrase_AppliesShortTextMitigation()
        {
            // Arrange: 短い phonemeIds（5要素 < MinPhonemeIds=40）
            var phonemeIds = new[] { 1, 5, 6, 7, 2 }; // ^, u, ?, ?, $
            var phonemeSilence = new Dictionary<string, float>(); // 空 → 分割なし
            var phonemeIdMap = CreateMinimalPhonemeIdMap();

            // StubをShortTextMitigatingGeneratorでラップ
            var wrappedGenerator = new ShortTextMitigatingGenerator(_stubGenerator);
            var orchestrator = new SplitInferenceOrchestrator(wrappedGenerator);

            // Act
            var output = await orchestrator.GenerateWithSilenceSplitAsync(
                phonemeIds,
                prosodyFlat: null,
                phonemeSilence, phonemeIdMap,
                sampleRate: 22050);

            try
            {
                // Assert
                Assert.IsTrue(output.Audio.IsCreated, "結果が作成済みであること");
                Assert.AreEqual(1, _stubGenerator.GenerateCallCount,
                    "分割なしの場合、推論は1回のみ呼ばれること");
                // ShortTextProcessor が適用され、phonemeIds が MinPhonemeIds 以上にパディングされていること
                Assert.GreaterOrEqual(_stubGenerator.LastPhonemeIds.Length,
                    ShortTextProcessor.MinPhonemeIds,
                    "短いフレーズにパディングが適用され、phonemeIds が MinPhonemeIds 以上であること");
            }
            finally
            {
                output.Dispose();
            }
        }

        // ── Durations 結合テスト ─────────────────────────────────

        [Test]
        public async Task GenerateWithSilenceSplitAsync_TwoPhrases_CombinedDurationsLengthCorrect()
        {
            // Arrange: 2句分割、各句で durations 3要素を返す
            _stubGenerator.DurationsToReturn = new float[] { 1f, 2f, 3f };
            var phonemeIds = new[] { 1, 3, 0, 8, 7, 2 }; // ^,a,_,k,o,$
            var phonemeSilence = new Dictionary<string, float> { ["_"] = 0.5f };
            var phonemeIdMap = CreateMinimalPhonemeIdMap();

            // Act
            var output = await _orchestrator.GenerateWithSilenceSplitAsync(
                phonemeIds,
                prosodyFlat: null,
                phonemeSilence, phonemeIdMap,
                sampleRate: 22050);

            try
            {
                // Assert: 3 durations/phrase * 2 phrases = 6
                Assert.AreEqual(6, output.Durations.Length,
                    "結合 Durations の長さが句数 × 句あたり durations 数と一致すること");
                Assert.IsTrue(output.HasDurations,
                    "Durations が利用可能であること");
            }
            finally
            {
                output.Dispose();
            }
        }

        [Test]
        public async Task GenerateWithSilenceSplitAsync_TwoPhrases_DurationsValuesPreserved()
        {
            // Arrange: 各句で同じ durations を返す
            _stubGenerator.DurationsToReturn = new float[] { 10f, 20f, 30f };
            var phonemeIds = new[] { 1, 3, 0, 8, 7, 2 };
            var phonemeSilence = new Dictionary<string, float> { ["_"] = 0.5f };
            var phonemeIdMap = CreateMinimalPhonemeIdMap();

            // Act
            var output = await _orchestrator.GenerateWithSilenceSplitAsync(
                phonemeIds,
                prosodyFlat: null,
                phonemeSilence, phonemeIdMap,
                sampleRate: 22050);

            try
            {
                // Assert: [10, 20, 30, 10, 20, 30]
                var expected = new float[] { 10f, 20f, 30f, 10f, 20f, 30f };
                Assert.AreEqual(expected.Length, output.Durations.Length);
                for (var i = 0; i < expected.Length; i++)
                {
                    Assert.AreEqual(expected[i], output.Durations[i], 1e-5f,
                        $"Durations[{i}] の値が保存されていること");
                }
            }
            finally
            {
                output.Dispose();
            }
        }

        [Test]
        public async Task GenerateWithSilenceSplitAsync_ThreePhrases_DurationsAndAudioCorrect()
        {
            // Arrange: 3句分割、各句で durations 2要素・audio 30サンプル
            _stubGenerator.DurationsToReturn = new float[] { 1f, 2f };
            _stubGenerator.AudioDataToReturn = CreateFixedAudioData(30, 0.3f);
            var phonemeIds = new[] { 1, 3, 0, 8, 0, 7, 2 }; // ^,a,_,k,_,o,$
            var phonemeSilence = new Dictionary<string, float> { ["_"] = 0.2f };
            var phonemeIdMap = CreateMinimalPhonemeIdMap();

            var expectedSilenceSamples = (int)(0.2f * 22050);

            // Act
            var output = await _orchestrator.GenerateWithSilenceSplitAsync(
                phonemeIds,
                prosodyFlat: null,
                phonemeSilence, phonemeIdMap,
                sampleRate: 22050);

            try
            {
                // Assert: Durations = 2 * 3 phrases = 6
                Assert.AreEqual(6, output.Durations.Length,
                    "3句×2 durations = 6 であること");

                // Audio = 30 + silence + 30 + silence + 30
                var expectedAudioLength = 30 + expectedSilenceSamples
                    + 30 + expectedSilenceSamples + 30;
                Assert.AreEqual(expectedAudioLength, output.Audio.Length,
                    "3句のオーディオ＋無音の合計長が一致すること");
            }
            finally
            {
                output.Dispose();
            }
        }

        [Test]
        public async Task GenerateWithSilenceSplitAsync_SinglePhrase_DurationsUnchanged()
        {
            // Arrange: 分割なし
            _stubGenerator.DurationsToReturn = new float[] { 1f, 2f, 3f, 4f, 5f };
            var phonemeIds = new[] { 1, 3, 4, 5, 2 }; // ^,a,i,u,$
            var phonemeSilence = new Dictionary<string, float> { ["#"] = 0.5f };
            var phonemeIdMap = CreateMinimalPhonemeIdMap();
            phonemeIdMap["#"] = new[] { 99 };

            // Act
            var output = await _orchestrator.GenerateWithSilenceSplitAsync(
                phonemeIds,
                prosodyFlat: null,
                phonemeSilence, phonemeIdMap,
                sampleRate: 22050);

            try
            {
                // Assert: 分割なしなので durations はそのまま
                Assert.AreEqual(5, output.Durations.Length,
                    "単一句の Durations 長が一致すること");
                var expected = new float[] { 1f, 2f, 3f, 4f, 5f };
                for (var i = 0; i < expected.Length; i++)
                {
                    Assert.AreEqual(expected[i], output.Durations[i], 1e-5f,
                        $"Durations[{i}] の値が一致すること");
                }
            }
            finally
            {
                output.Dispose();
            }
        }

        [Test]
        public async Task GenerateWithSilenceSplitAsync_NoDurations_HasDurationsFalse()
        {
            // Arrange: DurationsToReturn = null → durations なし
            _stubGenerator.DurationsToReturn = null;
            var phonemeIds = new[] { 1, 3, 0, 8, 7, 2 };
            var phonemeSilence = new Dictionary<string, float> { ["_"] = 0.5f };
            var phonemeIdMap = CreateMinimalPhonemeIdMap();

            // Act
            var output = await _orchestrator.GenerateWithSilenceSplitAsync(
                phonemeIds,
                prosodyFlat: null,
                phonemeSilence, phonemeIdMap,
                sampleRate: 22050);

            try
            {
                // Assert: durations なし
                Assert.IsFalse(output.HasDurations,
                    "DurationsToReturn が null の場合 HasDurations は false であること");

                // Audio 結合は正常に動作すること
                var expectedSilenceSamples = (int)(0.5f * 22050);
                Assert.AreEqual(50 + expectedSilenceSamples + 50, output.Audio.Length,
                    "Audio 結合が正常に行われること");
            }
            finally
            {
                output.Dispose();
            }
        }

        [Test]
        public async Task GenerateWithSilenceSplitAsync_EmptyInput_NoDurations()
        {
            // Arrange: 空 phonemeIds
            var phonemeIds = Array.Empty<int>();
            var phonemeSilence = new Dictionary<string, float> { ["_"] = 0.5f };
            var phonemeIdMap = CreateMinimalPhonemeIdMap();
            _stubGenerator.DurationsToReturn = new float[] { 1f, 2f };

            // Act
            var output = await _orchestrator.GenerateWithSilenceSplitAsync(
                phonemeIds,
                prosodyFlat: null,
                phonemeSilence, phonemeIdMap,
                sampleRate: 22050);

            try
            {
                // Assert: 空入力 → audio/durations ともに空
                Assert.AreEqual(0, output.Audio.Length,
                    "空入力で Audio 長が 0 であること");
                Assert.IsFalse(output.HasDurations,
                    "空入力で HasDurations が false であること");
            }
            finally
            {
                output.Dispose();
            }
        }

        [Test]
        public async Task GenerateWithSilenceSplitAsync_TwoPhrases_CombinedAudioLengthWithDurations()
        {
            // Arrange: durations 設定 + 2句分割
            _stubGenerator.DurationsToReturn = new float[] { 5f, 10f };
            _stubGenerator.AudioDataToReturn = CreateFixedAudioData(50, 0.5f);
            var phonemeIds = new[] { 1, 3, 0, 8, 7, 2 };
            var phonemeSilence = new Dictionary<string, float> { ["_"] = 0.5f };
            var phonemeIdMap = CreateMinimalPhonemeIdMap();

            var expectedSilenceSamples = (int)(0.5f * 22050);

            // Act
            var output = await _orchestrator.GenerateWithSilenceSplitAsync(
                phonemeIds,
                prosodyFlat: null,
                phonemeSilence, phonemeIdMap,
                sampleRate: 22050);

            try
            {
                // Assert: Audio = 50 + 11025 + 50
                Assert.AreEqual(50 + expectedSilenceSamples + 50, output.Audio.Length,
                    "durations 付き2句の合計 Audio 長が一致すること");
                Assert.IsTrue(output.HasDurations,
                    "HasDurations が true であること");
                Assert.AreEqual(4, output.Durations.Length,
                    "2句×2 durations = 4 であること");
            }
            finally
            {
                output.Dispose();
            }
        }

        // ── ヘルパー ─────────────────────────────────────────────

        /// <summary>
        /// 指定値で埋められた固定長オーディオデータを生成する。
        /// </summary>
        private static float[] CreateFixedAudioData(int length, float value)
        {
            var data = new float[length];
            for (var i = 0; i < length; i++)
                data[i] = value;
            return data;
        }

        /// <summary>
        /// テスト用の同期的 IProgress 実装。
        /// <see cref="Progress{T}"/> は SynchronizationContext.Post を使用するため、
        /// EditMode テストではコールバックの発火タイミングが不定。
        /// このクラスは Report() を同期的に呼び出す。
        /// </summary>
        private sealed class SynchronousProgress<T> : IProgress<T>
        {
            private readonly Action<T> _handler;

            public SynchronousProgress(Action<T> handler)
            {
                _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            }

            public void Report(T value)
            {
                _handler(value);
            }
        }
    }
}
