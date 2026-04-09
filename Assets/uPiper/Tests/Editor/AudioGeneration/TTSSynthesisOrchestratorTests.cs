using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using uPiper.Core;
using uPiper.Core.AudioGeneration;
using uPiper.Core.Phonemizers.Multilingual;

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
        private static Dictionary<string, int[]> CreateMinimalPhonemeIdMap()
        {
            return new Dictionary<string, int[]>
            {
                ["_"] = new[] { 0 },  // PAD
                ["^"] = new[] { 1 },  // BOS
                ["$"] = new[] { 2 },  // EOS
                ["a"] = new[] { 3 },
                ["i"] = new[] { 4 },
                ["u"] = new[] { 5 },
                ["e"] = new[] { 6 },
                ["o"] = new[] { 7 },
                ["k"] = new[] { 8 },
                ["s"] = new[] { 9 },
                ["t"] = new[] { 10 },
                ["n"] = new[] { 11 },
                ["h"] = new[] { 12 },
                ["m"] = new[] { 13 },
                ["r"] = new[] { 14 },
                ["w"] = new[] { 15 },
                ["N"] = new[] { 16 },
                [" "] = new[] { 17 },
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
                // PhonemeType が null かつ VoiceId に "ja_JP" を含まない場合、
                // intersperse PAD あり（NeedsInterspersePadding() = true）
                PhonemeType = null,
            };
            _phonemeEncoder = new PhonemeEncoder(_voiceConfig, new PuaTokenMapper());
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
                null,
                1.0f, 0.667f, 0.8f,
                0, 0);

            // Act
            var clip = await orchestrator.SynthesizeAsync(request);

            // Assert
            Assert.IsNotNull(clip, "AudioClip を返すこと");
            Assert.AreEqual(1, _stubGenerator.GenerateCallCount,
                "GenerateAudioAsync が1回呼ばれること");
            Assert.IsNull(_stubGenerator.LastProsodyFlat,
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
            // stride=3 flat: [a1_0, a2_0, a3_0, a1_1, a2_1, a3_1, a1_2, a2_2, a3_2]
            var prosodyFlat = new[] { 0, 1, 0, 1, 1, 0, 2, 1, 0 };
            var request = new SynthesisRequest(
                new[] { "k", "o", "N" },
                prosodyFlat,
                1.0f, 0.667f, 0.8f,
                0, 0);

            // Act
            var clip = await orchestrator.SynthesizeAsync(request);

            // Assert
            Assert.IsNotNull(clip, "Prosody付きでも AudioClip を返すこと");
            Assert.AreEqual(1, _stubGenerator.GenerateCallCount,
                "GenerateAudioAsync が1回呼ばれること");
            Assert.IsNotNull(_stubGenerator.LastProsodyFlat,
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
                null,
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
                null,
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
                null,
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
                null,
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
                null,
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
                null,
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
        /// テスト用 IPiperConfigReadOnly を生成する。
        /// ToValidated() で WorkerThreads=0 が自動検出されるため、手動で1以上を設定する。
        /// </summary>
        private static IPiperConfigReadOnly CreateValidatedConfig(
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

        // ── IPiperConfigReadOnly 型検証 ─────────────────────────────

        [Test]
        public void ValidatedPiperConfig_Implements_IPiperConfigReadOnly()
        {
            // Arrange & Act
            var piperConfig = new PiperConfig { WorkerThreads = 1 };
            var validated = piperConfig.ToValidated();

            // Assert
            Assert.IsInstanceOf<IPiperConfigReadOnly>(validated,
                "ValidatedPiperConfig が IPiperConfigReadOnly を実装していること");
        }

        [Test]
        public void IPiperConfigReadOnly_AllProperties_Accessible()
        {
            // Arrange
            var piperConfig = new PiperConfig { WorkerThreads = 1 };
            IPiperConfigReadOnly config = piperConfig.ToValidated();

            // Act & Assert: 全6プロパティにインターフェース経由でアクセスできること
            Assert.IsNotNull(config.Language.DefaultLanguage, "Language.DefaultLanguage にアクセスできること");
            Assert.GreaterOrEqual(config.Performance.MaxCacheSizeMB, 0, "Performance.MaxCacheSizeMB にアクセスできること");
            Assert.IsTrue(System.Enum.IsDefined(typeof(InferenceBackend), config.Inference.Backend),
                "Inference.Backend にアクセスできること");
            Assert.Greater(config.Audio.SampleRate, 0, "Audio.SampleRate にアクセスできること");
            Assert.IsFalse(config.Silence.EnablePhonemeSilence, "Silence.EnablePhonemeSilence にアクセスできること");
            Assert.GreaterOrEqual(config.General.TimeoutMs, 0, "General.TimeoutMs にアクセスできること");
        }

        [Test]
        public void TTSSynthesisOrchestrator_AcceptsIPiperConfigReadOnly()
        {
            // Arrange
            IPiperConfigReadOnly config = CreateValidatedConfig(enableSilence: false);

            // Act & Assert: IPiperConfigReadOnly 型でコンストラクタに渡せること
            Assert.DoesNotThrow(() =>
                new TTSSynthesisOrchestrator(
                    _stubGenerator, _splitOrchestrator, _phonemeEncoder, _audioClipBuilder,
                    config, _voiceConfig));
        }

        // ── Short text mitigation ──────────────────────────────────

        [Test]
        public async Task SynthesizeAsync_ShortPhonemes_PadsPhonemeIds()
        {
            // Arrange: 5音素 → エンコード後もMinPhonemeIds(40)未満 → パディングが適用される
            var config = CreateValidatedConfig(enableSilence: false);
            var orchestrator = new TTSSynthesisOrchestrator(
                _stubGenerator, _splitOrchestrator, _phonemeEncoder, _audioClipBuilder,
                config, _voiceConfig);
            var request = new SynthesisRequest(
                new[] { "a", "i", "u", "e", "o" },
                null,
                1.0f, 0.667f, 0.8f,
                0, 0);

            // Act
            await orchestrator.SynthesizeAsync(request);

            // Assert
            var ids = _stubGenerator.LastPhonemeIds;
            Assert.IsNotNull(ids, "PhonemeIds が記録されていること");
            Assert.GreaterOrEqual(ids.Length, 40,
                "短い音素列はMinPhonemeIds(40)以上にパディングされること");
            Assert.AreEqual(1, ids[0], "先頭が BOS(1) であること");
            Assert.AreEqual(2, ids[^1], "末尾が EOS(2) であること");
        }

        [Test]
        public async Task SynthesizeAsync_LongPhonemes_NoShortTextMitigation()
        {
            // Arrange: 50音素 → エンコード後にMinPhonemeIds(40)以上 → パディングなし
            var config = CreateValidatedConfig(enableSilence: false);
            var orchestrator = new TTSSynthesisOrchestrator(
                _stubGenerator, _splitOrchestrator, _phonemeEncoder, _audioClipBuilder,
                config, _voiceConfig);
            // 50個の既知の音素を用意（PhonemeIdMap に存在する音素を使用）
            var phonemes = new string[50];
            var vowels = new[] { "a", "i", "u", "e", "o" };
            for (var i = 0; i < 50; i++)
                phonemes[i] = vowels[i % vowels.Length];

            var request = new SynthesisRequest(
                phonemes,
                null,
                1.0f, 0.667f, 0.8f,
                0, 0);

            // Act
            await orchestrator.SynthesizeAsync(request);

            // Assert: intersperse PAD あり → BOS(1) + PAD(0) + 50*(phoneme+PAD) + EOS(2) = 103
            var ids = _stubGenerator.LastPhonemeIds;
            Assert.IsNotNull(ids, "PhonemeIds が記録されていること");
            var expectedLength = 1 + 1 + 50 * 2 + 1; // BOS + PAD_after_BOS + 50*(phoneme+PAD) + EOS = 103
            Assert.AreEqual(expectedLength, ids.Length,
                "長い音素列はパディングなしでエンコードされること（BOS + PAD + 50*(phoneme+PAD) + EOS）");
            Assert.AreEqual(1, ids[0], "先頭が BOS(1) であること");
            Assert.AreEqual(2, ids[^1], "末尾が EOS(2) であること");
        }
    }
}