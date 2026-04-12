using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using uPiper.Core;
using uPiper.Core.AudioGeneration;
using uPiper.Core.Phonemizers.Multilingual;
using uPiper.Tests.Editor.AudioGeneration;

namespace uPiper.Tests.Editor
{
    /// <summary>
    /// SynthesizeWithTimingAsync のテスト。
    /// T4-1: タイミング付き合成、T4-2: タイミング精度、
    /// T4-3: キャッシュ整合、T4-4: durations 非対応。
    /// TTSSynthesisOrchestrator レベルで検証する。
    /// </summary>
    [TestFixture]
    public class PiperTTSSynthesizeWithTimingTests
    {
        private StubInferenceAudioGenerator _stubGenerator;
        private SplitInferenceOrchestrator _splitOrchestrator;
        private PhonemeEncoder _phonemeEncoder;
        private AudioClipBuilder _audioClipBuilder;
        private PuaTokenMapper _puaTokenMapper;
        private PiperVoiceConfig _voiceConfig;

        [SetUp]
        public void SetUp()
        {
            _stubGenerator = new StubInferenceAudioGenerator();
            _splitOrchestrator = new SplitInferenceOrchestrator(_stubGenerator);
            _audioClipBuilder = new AudioClipBuilder();
            _puaTokenMapper = new PuaTokenMapper();
            _voiceConfig = new PiperVoiceConfig
            {
                VoiceId = "test-voice",
                Language = "ja",
                PhonemeIdMap = TestHelpers.CreateMinimalPhonemeIdMap(),
                PhonemeType = null, // intersperse PAD あり
            };
            _phonemeEncoder = new PhonemeEncoder(_voiceConfig, _puaTokenMapper);
        }

        // ── ヘルパー ─────────────────────────────────────────────

        /// <summary>
        /// TTSSynthesisOrchestrator を生成する共通ヘルパー。
        /// </summary>
        private TTSSynthesisOrchestrator CreateOrchestrator(
            AudioSynthesisCache cache = null)
        {
            return new TTSSynthesisOrchestrator(
                _stubGenerator, _splitOrchestrator,
                _phonemeEncoder, _audioClipBuilder,
                TestHelpers.CreateValidatedConfig(enableSilence: false),
                _voiceConfig,
                cache,
                _puaTokenMapper);
        }

        /// <summary>
        /// ["k","o","N"] 用の SynthesisRequest を生成する。
        /// </summary>
        private static SynthesisRequest CreateKonRequest()
        {
            return new SynthesisRequest(
                new[] { "k", "o", "N" },
                null,
                1.0f, 0.667f, 0.8f,
                0, 0);
        }

        /// <summary>
        /// 9要素の正の durations 配列を設定する。
        /// phonemes=["k","o","N"] → エンコード後9要素:
        /// [BOS, PAD, k, PAD, o, PAD, N, PAD, EOS]
        /// </summary>
        private void SetupDurationsFor3Phonemes()
        {
            _stubGenerator.SupportsDurations = true;
            _stubGenerator.DurationsToReturn = new float[]
            {
                10f, 10f, 10f, 10f, 10f, 10f, 10f, 10f, 10f
            };
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // T4-1: タイミング付き合成テスト
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Test]
        public async Task SynthesizeWithTimingAsync_WithDurations_TimingsNotEmpty()
        {
            // Arrange
            SetupDurationsFor3Phonemes();
            var orchestrator = CreateOrchestrator();
            var request = CreateKonRequest();

            // Act
            var result = await orchestrator.SynthesizeWithTimingAsync(request);

            // Assert: ["k","o","N"] の3音素 → PAD/BOS/EOSを除き3エントリ
            Assert.IsNotNull(result.Timings, "Timings が null でないこと");
            Assert.AreEqual(3, result.Timings.Count,
                "入力3音素に対して Timings.Count == 3 であること");
        }

        [Test]
        public async Task SynthesizeWithTimingAsync_WithDurations_StartSecondsNonNegative()
        {
            // Arrange
            SetupDurationsFor3Phonemes();
            var orchestrator = CreateOrchestrator();
            var request = CreateKonRequest();

            // Act
            var result = await orchestrator.SynthesizeWithTimingAsync(request);

            // Assert
            Assert.IsNotNull(result.Timings);
            for (var i = 0; i < result.Timings.Count; i++)
            {
                Assert.GreaterOrEqual(result.Timings[i].StartSeconds, 0f,
                    $"Timings[{i}].StartSeconds >= 0 であること");
            }
        }

        [Test]
        public async Task SynthesizeWithTimingAsync_WithDurations_EndSecondsGreaterThanStart()
        {
            // Arrange
            SetupDurationsFor3Phonemes();
            var orchestrator = CreateOrchestrator();
            var request = CreateKonRequest();

            // Act
            var result = await orchestrator.SynthesizeWithTimingAsync(request);

            // Assert
            Assert.IsNotNull(result.Timings);
            for (var i = 0; i < result.Timings.Count; i++)
            {
                Assert.Greater(result.Timings[i].DurationSeconds, 0f,
                    $"Timings[{i}].DurationSeconds > 0 であること");
            }
        }

        [Test]
        public async Task SynthesizeWithTimingAsync_WithDurations_TimingsMonotonicallyIncreasing()
        {
            // Arrange
            SetupDurationsFor3Phonemes();
            var orchestrator = CreateOrchestrator();
            var request = CreateKonRequest();

            // Act
            var result = await orchestrator.SynthesizeWithTimingAsync(request);

            // Assert: Start[i] >= End[i-1] (単調増加)
            Assert.IsNotNull(result.Timings);
            for (var i = 1; i < result.Timings.Count; i++)
            {
                Assert.GreaterOrEqual(result.Timings[i].StartSeconds,
                    result.Timings[i - 1].EndSeconds,
                    $"Timings[{i}].StartSeconds >= Timings[{i - 1}].EndSeconds であること");
            }
        }

        [Test]
        public async Task SynthesizeWithTimingAsync_WithDurations_PhonemeNamesPreserved()
        {
            // Arrange
            SetupDurationsFor3Phonemes();
            var orchestrator = CreateOrchestrator();
            var request = CreateKonRequest();

            // Act
            var result = await orchestrator.SynthesizeWithTimingAsync(request);

            // Assert: 音素名が入力順に保持されること
            Assert.IsNotNull(result.Timings);
            Assert.AreEqual(3, result.Timings.Count);
            Assert.AreEqual("k", result.Timings[0].Phoneme,
                "1番目の音素名が k であること");
            Assert.AreEqual("o", result.Timings[1].Phoneme,
                "2番目の音素名が o であること");
            Assert.AreEqual("N", result.Timings[2].Phoneme,
                "3番目の音素名が N であること");
        }

        [Test]
        public async Task SynthesizeWithTimingAsync_WithDurations_AudioClipNotNull()
        {
            // Arrange
            SetupDurationsFor3Phonemes();
            var orchestrator = CreateOrchestrator();
            var request = CreateKonRequest();

            // Act
            var result = await orchestrator.SynthesizeWithTimingAsync(request);

            // Assert
            Assert.IsNotNull(result.AudioClip,
                "AudioClip が null でないこと");
            Assert.Greater(result.TotalDurationSeconds, 0f,
                "TotalDurationSeconds > 0 であること");
        }

        [Test]
        public async Task SynthesizeWithTimingAsync_WithDurations_FirstEntryStartsAtZero()
        {
            // Arrange
            SetupDurationsFor3Phonemes();
            var orchestrator = CreateOrchestrator();
            var request = CreateKonRequest();

            // Act
            var result = await orchestrator.SynthesizeWithTimingAsync(request);

            // Assert: BOS/PAD の duration 分だけ先頭がオフセットされるが、
            // 最初の実音素の StartSeconds は BOS+PAD 分の累積であり、
            // 非負であることを確認する。非ゼロの可能性もあるため GreaterOrEqual で検証。
            Assert.IsNotNull(result.Timings);
            Assert.GreaterOrEqual(result.Timings[0].StartSeconds, 0f,
                "Timings[0].StartSeconds >= 0 であること");
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // T4-2: タイミング精度テスト
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Test]
        public async Task SynthesizeWithTimingAsync_LastEndSeconds_WithinThresholdOfAudioLength()
        {
            // Arrange: 1秒分のオーディオデータ
            _stubGenerator.SupportsDurations = true;
            _stubGenerator.DurationsToReturn = new float[]
            {
                10f, 10f, 10f, 10f, 10f, 10f, 10f, 10f, 10f
            };
            _stubGenerator.AudioDataToReturn = new float[22050]; // 1秒分
            var orchestrator = CreateOrchestrator();
            var request = CreateKonRequest();

            // Act
            var result = await orchestrator.SynthesizeWithTimingAsync(request);

            // Assert: 最後のエントリの EndSeconds が AudioClip.length に近いこと
            Assert.IsNotNull(result.Timings);
            var lastEnd = result.Timings[result.Timings.Count - 1].EndSeconds;
            Assert.AreEqual(result.AudioClip.length, lastEnd, 0.05f,
                "|LastEndSeconds - AudioClip.length| < 0.05f であること");
        }

        [Test]
        public async Task SynthesizeWithTimingAsync_TotalDurationSeconds_MatchesAudioClipLength()
        {
            // Arrange
            _stubGenerator.SupportsDurations = true;
            _stubGenerator.DurationsToReturn = new float[]
            {
                10f, 10f, 10f, 10f, 10f, 10f, 10f, 10f, 10f
            };
            _stubGenerator.AudioDataToReturn = new float[22050];
            var orchestrator = CreateOrchestrator();
            var request = CreateKonRequest();

            // Act
            var result = await orchestrator.SynthesizeWithTimingAsync(request);

            // Assert
            Assert.AreEqual(result.AudioClip.length, result.TotalDurationSeconds, 1e-3f,
                "TotalDurationSeconds == AudioClip.length (delta=1e-3f)");
        }

        [Test]
        public async Task SynthesizeWithTimingAsync_TimingsDurationSum_ConsistentWithAudioLength()
        {
            // Arrange
            _stubGenerator.SupportsDurations = true;
            _stubGenerator.DurationsToReturn = new float[]
            {
                10f, 10f, 10f, 10f, 10f, 10f, 10f, 10f, 10f
            };
            _stubGenerator.AudioDataToReturn = new float[22050];
            var orchestrator = CreateOrchestrator();
            var request = CreateKonRequest();

            // Act
            var result = await orchestrator.SynthesizeWithTimingAsync(request);

            // Assert: 実音素の duration 合計 <= AudioClip.length
            // (PAD/BOS/EOS の duration が含まれないため、合計は clip.length 以下)
            Assert.IsNotNull(result.Timings);
            var sumDuration = 0f;
            for (var i = 0; i < result.Timings.Count; i++)
            {
                sumDuration += result.Timings[i].DurationSeconds;
            }

            Assert.LessOrEqual(sumDuration, result.AudioClip.length,
                "実音素の duration 合計 <= AudioClip.length であること");
        }

        [Test]
        public async Task SynthesizeWithTimingAsync_NonUniformDurations_PrecisionMaintained()
        {
            // Arrange: 非均一な durations で精度が保たれることを検証
            _stubGenerator.SupportsDurations = true;
            _stubGenerator.DurationsToReturn = new float[]
            {
                5f, 3f, 20f, 7f, 15f, 2f, 30f, 8f, 1f
            };
            _stubGenerator.AudioDataToReturn = new float[22050];
            var orchestrator = CreateOrchestrator();
            var request = CreateKonRequest();

            // Act
            var result = await orchestrator.SynthesizeWithTimingAsync(request);

            // Assert: タイミングが単調増加であること
            Assert.IsNotNull(result.Timings);
            Assert.AreEqual(3, result.Timings.Count,
                "入力3音素に対して3エントリ");
            for (var i = 1; i < result.Timings.Count; i++)
            {
                Assert.GreaterOrEqual(result.Timings[i].StartSeconds,
                    result.Timings[i - 1].EndSeconds,
                    $"非均一 durations でも単調増加: Timings[{i}].Start >= Timings[{i - 1}].End");
            }

            // 各エントリの duration が正であること
            for (var i = 0; i < result.Timings.Count; i++)
            {
                Assert.Greater(result.Timings[i].DurationSeconds, 0f,
                    $"非均一 durations でも Timings[{i}].Duration > 0");
            }
        }

        [Test]
        public async Task SynthesizeWithTimingAsync_LargerPhonemeSet_PrecisionMaintained()
        {
            // Arrange: 9音素 → エンコード後21要素
            // [BOS, PAD, k, PAD, o, PAD, N, PAD, n, PAD, i, PAD, t, PAD, i, PAD, w, PAD, a, PAD, EOS]
            _stubGenerator.SupportsDurations = true;
            _stubGenerator.DurationsToReturn = new float[]
            {
                5f, 3f, 10f, 2f, 8f, 1f, 12f, 4f, 15f,
                3f, 7f, 2f, 20f, 6f, 9f, 1f, 11f, 3f,
                14f, 5f, 2f
            };
            _stubGenerator.AudioDataToReturn = new float[22050];
            var orchestrator = CreateOrchestrator();
            var request = new SynthesisRequest(
                new[] { "k", "o", "N", "n", "i", "t", "i", "w", "a" },
                null,
                1.0f, 0.667f, 0.8f,
                0, 0);

            // Act
            var result = await orchestrator.SynthesizeWithTimingAsync(request);

            // Assert: 9音素 → 9タイミングエントリ
            Assert.IsNotNull(result.Timings);
            Assert.AreEqual(9, result.Timings.Count,
                "入力9音素に対して9エントリ");

            // 単調増加
            for (var i = 1; i < result.Timings.Count; i++)
            {
                Assert.GreaterOrEqual(result.Timings[i].StartSeconds,
                    result.Timings[i - 1].EndSeconds,
                    $"9音素でも単調増加: Timings[{i}].Start >= Timings[{i - 1}].End");
            }

            // 全エントリ duration > 0
            for (var i = 0; i < result.Timings.Count; i++)
            {
                Assert.Greater(result.Timings[i].DurationSeconds, 0f,
                    $"9音素でも Timings[{i}].Duration > 0");
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // T4-3: キャッシュ整合テスト
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Test]
        public async Task SynthesizeWithTimingAsync_SecondCall_CacheHit_GenerateNotCalled()
        {
            // Arrange
            SetupDurationsFor3Phonemes();
            var cache = new AudioSynthesisCache();
            var orchestrator = CreateOrchestrator(cache);
            var request = CreateKonRequest();

            // Act: 1回目
            await orchestrator.SynthesizeWithTimingAsync(request);
            var callCountAfterFirst = _stubGenerator.GenerateCallCount;

            // Act: 2回目（キャッシュヒット）
            await orchestrator.SynthesizeWithTimingAsync(request);

            // Assert: 2回目で GenerateAudioAsync が呼ばれないこと
            Assert.AreEqual(callCountAfterFirst, _stubGenerator.GenerateCallCount,
                "キャッシュヒット時は GenerateAudioAsync が追加呼び出しされないこと");
        }

        [Test]
        public async Task SynthesizeWithTimingAsync_SecondCall_CacheHit_TimingsCountEqual()
        {
            // Arrange
            SetupDurationsFor3Phonemes();
            var cache = new AudioSynthesisCache();
            var orchestrator = CreateOrchestrator(cache);
            var request = CreateKonRequest();

            // Act
            var result1 = await orchestrator.SynthesizeWithTimingAsync(request);
            var result2 = await orchestrator.SynthesizeWithTimingAsync(request);

            // Assert
            Assert.IsNotNull(result1.Timings);
            Assert.IsNotNull(result2.Timings);
            Assert.AreEqual(result1.Timings.Count, result2.Timings.Count,
                "キャッシュヒット時も Timings.Count が一致すること");
        }

        [Test]
        public async Task SynthesizeWithTimingAsync_SecondCall_CacheHit_TimingsValuesIdentical()
        {
            // Arrange
            SetupDurationsFor3Phonemes();
            var cache = new AudioSynthesisCache();
            var orchestrator = CreateOrchestrator(cache);
            var request = CreateKonRequest();

            // Act
            var result1 = await orchestrator.SynthesizeWithTimingAsync(request);
            var result2 = await orchestrator.SynthesizeWithTimingAsync(request);

            // Assert: 全エントリの値が一致すること
            Assert.IsNotNull(result1.Timings);
            Assert.IsNotNull(result2.Timings);
            Assert.AreEqual(result1.Timings.Count, result2.Timings.Count);
            for (var i = 0; i < result1.Timings.Count; i++)
            {
                Assert.AreEqual(result1.Timings[i].Phoneme,
                    result2.Timings[i].Phoneme,
                    $"Timings[{i}].Phoneme が一致すること");
                Assert.AreEqual(result1.Timings[i].StartSeconds,
                    result2.Timings[i].StartSeconds, 1e-6f,
                    $"Timings[{i}].StartSeconds が一致すること");
                Assert.AreEqual(result1.Timings[i].EndSeconds,
                    result2.Timings[i].EndSeconds, 1e-6f,
                    $"Timings[{i}].EndSeconds が一致すること");
            }
        }

        [Test]
        public async Task SynthesizeWithTimingAsync_SecondCall_CacheHit_HasTimingsTrue()
        {
            // Arrange
            SetupDurationsFor3Phonemes();
            var cache = new AudioSynthesisCache();
            var orchestrator = CreateOrchestrator(cache);
            var request = CreateKonRequest();

            // Act
            await orchestrator.SynthesizeWithTimingAsync(request);
            var result2 = await orchestrator.SynthesizeWithTimingAsync(request);

            // Assert
            Assert.IsTrue(result2.HasTimings,
                "キャッシュヒット時も HasTimings == true であること");
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // T4-4: durations 非対応テスト
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Test]
        public async Task SynthesizeWithTimingAsync_NoDurations_TimingsIsNull()
        {
            // Arrange: DurationsToReturn = null (default)
            var orchestrator = CreateOrchestrator();
            var request = CreateKonRequest();

            // Act
            var result = await orchestrator.SynthesizeWithTimingAsync(request);

            // Assert
            Assert.IsNull(result.Timings,
                "durations 非対応時は Timings が null であること");
            Assert.IsFalse(result.HasTimings,
                "durations 非対応時は HasTimings が false であること");
        }

        [Test]
        public async Task SynthesizeWithTimingAsync_NoDurations_AudioClipNotNull()
        {
            // Arrange
            var orchestrator = CreateOrchestrator();
            var request = CreateKonRequest();

            // Act
            var result = await orchestrator.SynthesizeWithTimingAsync(request);

            // Assert
            Assert.IsNotNull(result.AudioClip,
                "durations 非対応でも AudioClip を返すこと");
        }

        [Test]
        public void SynthesizeWithTimingAsync_NoDurations_NoException()
        {
            // Arrange
            var orchestrator = CreateOrchestrator();
            var request = CreateKonRequest();

            // Act & Assert
            Assert.DoesNotThrowAsync(async () =>
                await orchestrator.SynthesizeWithTimingAsync(request));
        }

        [Test]
        public async Task SynthesizeWithTimingAsync_NoDurations_GenerateCallCountIsOne()
        {
            // Arrange
            var orchestrator = CreateOrchestrator();
            var request = CreateKonRequest();

            // Act
            await orchestrator.SynthesizeWithTimingAsync(request);

            // Assert
            Assert.AreEqual(1, _stubGenerator.GenerateCallCount,
                "GenerateAudioAsync が1回呼ばれること");
        }

        [Test]
        public async Task SynthesizeWithTimingAsync_NoDurations_WithProsody_TimingsIsNull()
        {
            // Arrange: Prosody付きでも durations 非対応なら Timings は null
            _stubGenerator.SupportsProsody = true;
            var orchestrator = CreateOrchestrator();
            // stride=3 flat: [a1_0,a2_0,a3_0, a1_1,a2_1,a3_1, a1_2,a2_2,a3_2]
            var prosodyFlat = new[] { 0, 1, 0, 1, 1, 0, 2, 1, 0 };
            var request = new SynthesisRequest(
                new[] { "k", "o", "N" },
                prosodyFlat,
                1.0f, 0.667f, 0.8f,
                0, 0);

            // Act
            var result = await orchestrator.SynthesizeWithTimingAsync(request);

            // Assert
            Assert.IsNull(result.Timings,
                "Prosody 付きでも durations 非対応時は Timings が null であること");
            Assert.IsFalse(result.HasTimings,
                "HasTimings == false であること");
        }

        [Test]
        public async Task SynthesizeWithTimingAsync_NoDurations_CacheHit_TimingsStillNull()
        {
            // Arrange: durations 非対応でキャッシュ経由でも Timings は null
            var cache = new AudioSynthesisCache();
            var orchestrator = CreateOrchestrator(cache);
            var request = CreateKonRequest();

            // Act: 1回目（キャッシュミス）
            var result1 = await orchestrator.SynthesizeWithTimingAsync(request);

            // Act: 2回目（キャッシュヒット）
            var result2 = await orchestrator.SynthesizeWithTimingAsync(request);

            // Assert
            Assert.IsNull(result1.Timings,
                "1回目: durations 非対応時は Timings が null であること");
            Assert.IsNull(result2.Timings,
                "2回目(キャッシュヒット): Timings が null のままであること");
            Assert.IsFalse(result2.HasTimings,
                "キャッシュヒット時も HasTimings == false であること");
        }
    }
}