using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using uPiper.Core;
using uPiper.Core.AudioGeneration;

namespace uPiper.Tests.Editor.AudioGeneration
{
    /// <summary>
    /// TTSSynthesisOrchestrator の統合テスト。
    /// 通常パス・Prosodyパス・句分割パスの3分岐を検証する。
    /// </summary>
    [TestFixture]
    public class TTSSynthesisOrchestratorTests
    {
        private StubInferenceAudioGenerator _stubGenerator;
        private SplitInferenceOrchestrator _splitOrchestrator;
        private PhonemeEncoder _phonemeEncoder;
        private AudioClipBuilder _audioClipBuilder;
        private PiperVoiceConfig _voiceConfig;

        /// <summary>
        /// PhonemeEncoder が動作する最小限の PhonemeIdMap を構築する。
        /// PAD=0, BOS=1, EOS=2 の特殊トークンに加え、基本音素を含む。
        /// </summary>
        private static Dictionary<string, int> CreateMinimalPhonemeIdMap()
        {
            return new Dictionary<string, int>
            {
                ["_"] = 0,  // PAD
                ["^"] = 1,  // BOS
                ["$"] = 2,  // EOS
                ["a"] = 3,
                ["i"] = 4,
                ["u"] = 5,
                ["e"] = 6,
                ["o"] = 7,
                ["k"] = 8,
                ["s"] = 9,
                ["t"] = 10,
                ["n"] = 11,
                ["h"] = 12,
                ["m"] = 13,
                ["r"] = 14,
                ["w"] = 15,
                ["N"] = 16,
                [" "] = 17,
            };
        }

        [SetUp]
        public void SetUp()
        {
            _stubGenerator = new StubInferenceAudioGenerator();
            _splitOrchestrator = new SplitInferenceOrchestrator(_stubGenerator);
            _audioClipBuilder = new AudioClipBuilder();

            _voiceConfig = new PiperVoiceConfig
            {
                VoiceId = "test-voice",
                Language = "ja",
                PhonemeIdMap = CreateMinimalPhonemeIdMap(),
                // PhonemeType が null の場合、日本語モデル扱い（intersperse PAD なし）
                PhonemeType = null,
            };
            _phonemeEncoder = new PhonemeEncoder(_voiceConfig);
        }

        // ── 通常パス（Prosodyなし）────────────────────────────────

        [Test]
        public async Task SynthesizeAsync_NoProsody_ReturnsAudioClip()
        {
            // Arrange
            var orchestrator = new TTSSynthesisOrchestrator(
                _stubGenerator, _splitOrchestrator, _phonemeEncoder, _audioClipBuilder);
            var phonemes = new[] { "k", "o", "N", "n", "i", "t", "i", "w", "a" };
            var config = CreateValidatedConfig(enableSilence: false);

            // Act
            var clip = await orchestrator.SynthesizeAsync(
                phonemes,
                prosodyA1: null, prosodyA2: null, prosodyA3: null,
                lengthScale: 1.0f, noiseScale: 0.667f, noiseW: 0.8f,
                speakerId: 0, languageId: 0,
                config, _voiceConfig);

            // Assert
            Assert.IsNotNull(clip, "AudioClip を返すこと");
            Assert.AreEqual(1, _stubGenerator.GenerateCallCount,
                "GenerateAudioAsync が1回呼ばれること");
            Assert.IsNull(_stubGenerator.LastProsodyA1,
                "Prosodyなしの場合、prosodyA1 は null であること");
        }

        // ── Prosodyパス ──────────────────────────────────────────

        [Test]
        public async Task SynthesizeAsync_WithProsody_ReturnsAudioClip()
        {
            // Arrange
            _stubGenerator.SupportsProsody = true;
            var orchestrator = new TTSSynthesisOrchestrator(
                _stubGenerator, _splitOrchestrator, _phonemeEncoder, _audioClipBuilder);
            var phonemes = new[] { "k", "o", "N" };
            var prosodyA1 = new[] { 0, 1, 2 };
            var prosodyA2 = new[] { 1, 1, 1 };
            var prosodyA3 = new[] { 0, 0, 0 };
            var config = CreateValidatedConfig(enableSilence: false);

            // Act
            var clip = await orchestrator.SynthesizeAsync(
                phonemes,
                prosodyA1, prosodyA2, prosodyA3,
                lengthScale: 1.0f, noiseScale: 0.667f, noiseW: 0.8f,
                speakerId: 0, languageId: 0,
                config, _voiceConfig);

            // Assert
            Assert.IsNotNull(clip, "Prosody付きでも AudioClip を返すこと");
            Assert.AreEqual(1, _stubGenerator.GenerateCallCount,
                "GenerateAudioAsync が1回呼ばれること");
            Assert.IsNotNull(_stubGenerator.LastProsodyA1,
                "Prosodyありの場合、prosodyA1 が渡されること");
        }

        // ── 句分割パス ───────────────────────────────────────────

        [Test]
        public async Task SynthesizeAsync_WithSilenceSplit_UsesOrchestrator()
        {
            // Arrange: 沈黙トークン "_" に0.5秒の無音を設定
            var orchestrator = new TTSSynthesisOrchestrator(
                _stubGenerator, _splitOrchestrator, _phonemeEncoder, _audioClipBuilder);
            // "_" を含む音素列（"_" がPAD/沈黙トークンとして句分割のトリガーになる）
            var phonemes = new[] { "a", "_", "k", "o" };
            var config = CreateValidatedConfig(enableSilence: true, silenceSpec: "_ 0.5");

            // Act
            var clip = await orchestrator.SynthesizeAsync(
                phonemes,
                prosodyA1: null, prosodyA2: null, prosodyA3: null,
                lengthScale: 1.0f, noiseScale: 0.667f, noiseW: 0.8f,
                speakerId: 0, languageId: 0,
                config, _voiceConfig);

            // Assert
            Assert.IsNotNull(clip, "句分割パスでも AudioClip を返すこと");
            // 句分割により複数回の推論呼び出しが発生する可能性がある
            Assert.GreaterOrEqual(_stubGenerator.GenerateCallCount, 1,
                "GenerateAudioAsync が少なくとも1回は呼ばれること");
        }

        // ── 入力バリデーション ─────────────────────────────────────

        [Test]
        public void SynthesizeAsync_NullPhonemes_ThrowsArgumentException()
        {
            // Arrange
            var orchestrator = new TTSSynthesisOrchestrator(
                _stubGenerator, _splitOrchestrator, _phonemeEncoder, _audioClipBuilder);
            var config = CreateValidatedConfig(enableSilence: false);

            // Act & Assert
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await orchestrator.SynthesizeAsync(
                    null,
                    prosodyA1: null, prosodyA2: null, prosodyA3: null,
                    lengthScale: 1.0f, noiseScale: 0.667f, noiseW: 0.8f,
                    speakerId: 0, languageId: 0,
                    config, _voiceConfig));
        }

        [Test]
        public void SynthesizeAsync_EmptyPhonemes_ThrowsArgumentException()
        {
            // Arrange
            var orchestrator = new TTSSynthesisOrchestrator(
                _stubGenerator, _splitOrchestrator, _phonemeEncoder, _audioClipBuilder);
            var config = CreateValidatedConfig(enableSilence: false);

            // Act & Assert
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await orchestrator.SynthesizeAsync(
                    Array.Empty<string>(),
                    prosodyA1: null, prosodyA2: null, prosodyA3: null,
                    lengthScale: 1.0f, noiseScale: 0.667f, noiseW: 0.8f,
                    speakerId: 0, languageId: 0,
                    config, _voiceConfig));
        }

        // ── コンストラクタ null チェック ──────────────────────────

        [Test]
        public void Constructor_NullGenerator_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new TTSSynthesisOrchestrator(null, _splitOrchestrator, _phonemeEncoder, _audioClipBuilder));
        }

        [Test]
        public void Constructor_NullSplitOrchestrator_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new TTSSynthesisOrchestrator(_stubGenerator, null, _phonemeEncoder, _audioClipBuilder));
        }

        [Test]
        public void Constructor_NullPhonemeEncoder_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new TTSSynthesisOrchestrator(_stubGenerator, _splitOrchestrator, null, _audioClipBuilder));
        }

        [Test]
        public void Constructor_NullAudioClipBuilder_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new TTSSynthesisOrchestrator(_stubGenerator, _splitOrchestrator, _phonemeEncoder, null));
        }

        // ── ヘルパー ─────────────────────────────────────────────

        /// <summary>
        /// テスト用 ValidatedPiperConfig を生成する。
        /// PiperConfig.ToValidated() 内で Validate() が走り、
        /// WorkerThreads=0 が自動検出されるため、手動で1以上を設定する。
        /// </summary>
        private static ValidatedPiperConfig CreateValidatedConfig(
            bool enableSilence, string silenceSpec = "_ 0.5")
        {
            var piperConfig = new PiperConfig
            {
                EnablePhonemeSilence = enableSilence,
                PhonemeSilenceSpec = silenceSpec,
                WorkerThreads = 1,
            };
            return piperConfig.ToValidated();
        }
    }
}