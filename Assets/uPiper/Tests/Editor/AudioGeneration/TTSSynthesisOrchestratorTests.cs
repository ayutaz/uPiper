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
            var config = CreateValidatedConfig(enableSilence: false);
            var orchestrator = new TTSSynthesisOrchestrator(
                _stubGenerator, _splitOrchestrator, _phonemeEncoder, _audioClipBuilder,
                config, _voiceConfig);
            var request = new SynthesisRequest(
                new[] { "k", "o", "N", "n", "i", "t", "i", "w", "a" },
                null, null, null,
                1.0f, 0.667f, 0.8f,
                0, 0);

            // Act
            var clip = await orchestrator.SynthesizeAsync(request);

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
            var config = CreateValidatedConfig(enableSilence: false);
            var orchestrator = new TTSSynthesisOrchestrator(
                _stubGenerator, _splitOrchestrator, _phonemeEncoder, _audioClipBuilder,
                config, _voiceConfig);
            var request = new SynthesisRequest(
                new[] { "k", "o", "N" },
                new[] { 0, 1, 2 },
                new[] { 1, 1, 1 },
                new[] { 0, 0, 0 },
                1.0f, 0.667f, 0.8f,
                0, 0);

            // Act
            var clip = await orchestrator.SynthesizeAsync(request);

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
            var config = CreateValidatedConfig(enableSilence: true, silenceSpec: "_ 0.5");
            var orchestrator = new TTSSynthesisOrchestrator(
                _stubGenerator, _splitOrchestrator, _phonemeEncoder, _audioClipBuilder,
                config, _voiceConfig);
            // "_" を含む音素列（"_" がPAD/沈黙トークンとして句分割のトリガーになる）
            var request = new SynthesisRequest(
                new[] { "a", "_", "k", "o" },
                null, null, null,
                1.0f, 0.667f, 0.8f,
                0, 0);

            // Act
            var clip = await orchestrator.SynthesizeAsync(request);

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
            var config = CreateValidatedConfig(enableSilence: false);
            var orchestrator = new TTSSynthesisOrchestrator(
                _stubGenerator, _splitOrchestrator, _phonemeEncoder, _audioClipBuilder,
                config, _voiceConfig);
            var request = new SynthesisRequest(
                null,
                null, null, null,
                1.0f, 0.667f, 0.8f,
                0, 0);

            // Act & Assert
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await orchestrator.SynthesizeAsync(request));
        }

        [Test]
        public void SynthesizeAsync_EmptyPhonemes_ThrowsArgumentException()
        {
            // Arrange
            var config = CreateValidatedConfig(enableSilence: false);
            var orchestrator = new TTSSynthesisOrchestrator(
                _stubGenerator, _splitOrchestrator, _phonemeEncoder, _audioClipBuilder,
                config, _voiceConfig);
            var request = new SynthesisRequest(
                Array.Empty<string>(),
                null, null, null,
                1.0f, 0.667f, 0.8f,
                0, 0);

            // Act & Assert
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await orchestrator.SynthesizeAsync(request));
        }

        // ── コンストラクタ null チェック ──────────────────────────

        [Test]
        public void Constructor_NullGenerator_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new TTSSynthesisOrchestrator(null, _splitOrchestrator, _phonemeEncoder, _audioClipBuilder,
                    null, _voiceConfig));
        }

        [Test]
        public void Constructor_NullSplitOrchestrator_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new TTSSynthesisOrchestrator(_stubGenerator, null, _phonemeEncoder, _audioClipBuilder,
                    null, _voiceConfig));
        }

        [Test]
        public void Constructor_NullPhonemeEncoder_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new TTSSynthesisOrchestrator(_stubGenerator, _splitOrchestrator, null, _audioClipBuilder,
                    null, _voiceConfig));
        }

        [Test]
        public void Constructor_NullAudioClipBuilder_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new TTSSynthesisOrchestrator(_stubGenerator, _splitOrchestrator, _phonemeEncoder, null,
                    null, _voiceConfig));
        }

        // ── パラメータ伝播 ──────────────────────────────────────────

        [Test]
        public async Task SynthesizeAsync_CustomParams_PropagatedToGenerator()
        {
            // Arrange: 非デフォルトパラメータを設定
            var config = CreateValidatedConfig(enableSilence: false);
            var orchestrator = new TTSSynthesisOrchestrator(
                _stubGenerator, _splitOrchestrator, _phonemeEncoder, _audioClipBuilder,
                config, _voiceConfig);
            var request = new SynthesisRequest(
                new[] { "a", "i" },
                null, null, null,
                2.0f, 0.5f, 0.9f,  // non-default values
                3, 1);

            // Act
            await orchestrator.SynthesizeAsync(request);

            // Assert
            Assert.AreEqual(2.0f, _stubGenerator.LastLengthScale, "lengthScale が伝播されること");
            Assert.AreEqual(0.5f, _stubGenerator.LastNoiseScale, "noiseScale が伝播されること");
            Assert.AreEqual(0.9f, _stubGenerator.LastNoiseW, "noiseW が伝播されること");
            Assert.AreEqual(3, _stubGenerator.LastSpeakerId, "speakerId が伝播されること");
            Assert.AreEqual(1, _stubGenerator.LastLanguageId, "languageId が伝播されること");
        }

        // ── エンコード結果検証 ──────────────────────────────────────

        [Test]
        public async Task SynthesizeAsync_NoProsody_EncodesPhonemeIdsCorrectly()
        {
            // Arrange
            var config = CreateValidatedConfig(enableSilence: false);
            var orchestrator = new TTSSynthesisOrchestrator(
                _stubGenerator, _splitOrchestrator, _phonemeEncoder, _audioClipBuilder,
                config, _voiceConfig);
            // PhonemeIdMap: a=3, i=4 + BOS(1) + EOS(2) + PAD(0) intersperse
            // → expected: [1, 0, 3, 0, 4, 0, 2]
            var request = new SynthesisRequest(
                new[] { "a", "i" },
                null, null, null,
                1.0f, 0.667f, 0.8f,
                0, 0);

            // Act
            await orchestrator.SynthesizeAsync(request);

            // Assert: PhonemeEncoder adds BOS(^=1), PAD(0) intersperse, EOS($=2)
            var ids = _stubGenerator.LastPhonemeIds;
            Assert.IsNotNull(ids);
            Assert.AreEqual(7, ids.Length, "BOS + PAD + a + PAD + i + PAD + EOS = 7要素");
            Assert.AreEqual(1, ids[0], "先頭が BOS(1) であること");
            Assert.AreEqual(0, ids[1], "BOS後にPAD(0)が挿入されること");
            Assert.AreEqual(3, ids[2], "a が ID=3 にエンコードされること");
            Assert.AreEqual(4, ids[4], "i が ID=4 にエンコードされること");
            Assert.AreEqual(2, ids[^1], "末尾が EOS(2) であること");
        }

        // ── config=null ハンドリング ─────────────────────────────────

        [Test]
        public async Task SynthesizeAsync_NullConfig_UsesDirectGeneration()
        {
            // Arrange: config=null の場合は句分割なしで直接生成
            var orchestrator = new TTSSynthesisOrchestrator(
                _stubGenerator, _splitOrchestrator, _phonemeEncoder, _audioClipBuilder,
                null, _voiceConfig);
            var request = new SynthesisRequest(
                new[] { "a" },
                null, null, null,
                1.0f, 0.667f, 0.8f,
                0, 0);

            // Act
            var clip = await orchestrator.SynthesizeAsync(request);

            // Assert
            Assert.IsNotNull(clip);
            Assert.AreEqual(1, _stubGenerator.GenerateCallCount);
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