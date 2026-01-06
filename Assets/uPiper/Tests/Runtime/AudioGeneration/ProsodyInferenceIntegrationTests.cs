#if !UNITY_WEBGL
using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Unity.InferenceEngine;
using uPiper.Core;
using uPiper.Core.AudioGeneration;
using uPiper.Core.Phonemizers.Implementations;

namespace uPiper.Tests.Runtime.AudioGeneration
{
    /// <summary>
    /// Prosody対応モデルでの推論統合テスト
    /// </summary>
    [TestFixture]
    [Category("RequiresNativeLibrary")]
    [Category("RequiresProsodyModel")]
    public class ProsodyInferenceIntegrationTests
    {
        private InferenceAudioGenerator _generator;
        private OpenJTalkPhonemizer _phonemizer;
        private PhonemeEncoder _encoder;
        private ModelAsset _prosodyModelAsset;
        private PiperVoiceConfig _voiceConfig;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Load prosody-enabled model
            _prosodyModelAsset = Resources.Load<ModelAsset>("Models/tsukuyomi-chan");
            if (_prosodyModelAsset == null)
            {
                Debug.LogWarning("Prosody model (tsukuyomi-chan.onnx) not found in Resources/Models. Skipping prosody inference tests.");
            }

            _voiceConfig = new PiperVoiceConfig
            {
                SampleRate = 22050,
                Language = "ja"
            };
        }

        [SetUp]
        public void Setup()
        {
            _generator = new InferenceAudioGenerator();
            _encoder = new PhonemeEncoder(_voiceConfig);

            // Initialize OpenJTalk phonemizer
            try
            {
                _phonemizer = new OpenJTalkPhonemizer();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to create OpenJTalkPhonemizer: {ex.Message}");
            }
        }

        [TearDown]
        public void TearDown()
        {
            _generator?.Dispose();
            _phonemizer?.Dispose();
        }

        [Test]
        public async Task ProsodyModel_SupportsProsody_ReturnsTrue()
        {
            if (_prosodyModelAsset == null)
            {
                Assert.Ignore("Prosody model not available");
                return;
            }

            await _generator.InitializeAsync(_prosodyModelAsset, _voiceConfig);

            Assert.IsTrue(_generator.IsInitialized, "Generator should be initialized");
            Assert.IsTrue(_generator.SupportsProsody, "Model should support prosody");
        }

        [Test]
        public async Task GenerateAudioWithProsody_ProducesValidAudio()
        {
            if (_prosodyModelAsset == null)
            {
                Assert.Ignore("Prosody model not available");
                return;
            }

            if (_phonemizer == null)
            {
                Assert.Ignore("OpenJTalk phonemizer not available");
                return;
            }

            // Initialize generator
            await _generator.InitializeAsync(_prosodyModelAsset, _voiceConfig);

            // Get prosody data
            var testText = "こんにちは";
            var prosodyResult = _phonemizer.PhonemizeWithProsody(testText);

            Debug.Log($"Phonemes: [{string.Join(", ", prosodyResult.Phonemes)}]");
            Debug.Log($"ProsodyA1: [{string.Join(", ", prosodyResult.ProsodyA1)}]");
            Debug.Log($"ProsodyA2: [{string.Join(", ", prosodyResult.ProsodyA2)}]");
            Debug.Log($"ProsodyA3: [{string.Join(", ", prosodyResult.ProsodyA3)}]");

            // Encode phonemes
            var phonemeIds = _encoder.Encode(prosodyResult.Phonemes);
            Debug.Log($"PhonemeIds: [{string.Join(", ", phonemeIds)}]");

            // Generate audio with prosody
            var audioData = await _generator.GenerateAudioWithProsodyAsync(
                phonemeIds,
                prosodyResult.ProsodyA1,
                prosodyResult.ProsodyA2,
                prosodyResult.ProsodyA3
            );

            // Validate audio
            Assert.IsNotNull(audioData, "Audio data should not be null");
            Assert.Greater(audioData.Length, 0, "Audio data should have samples");

            var min = audioData.Min();
            var max = audioData.Max();
            var absAvg = audioData.Select(Math.Abs).Average();

            Debug.Log($"Generated {audioData.Length} samples ({audioData.Length / 22050.0f:F2} seconds)");
            Debug.Log($"Audio stats - Min: {min:F4}, Max: {max:F4}, AbsAvg: {absAvg:F6}");

            // Audio should have some variation (not silent)
            Assert.Greater(absAvg, 0.0001f, "Audio should not be silent");
            Assert.Greater(max - min, 0.01f, "Audio should have dynamic range");
        }

        [Test]
        public async Task GenerateAudioWithProsody_CompareWithZeroProsody()
        {
            if (_prosodyModelAsset == null)
            {
                Assert.Ignore("Prosody model not available");
                return;
            }

            if (_phonemizer == null)
            {
                Assert.Ignore("OpenJTalk phonemizer not available");
                return;
            }

            // Initialize generator
            await _generator.InitializeAsync(_prosodyModelAsset, _voiceConfig);

            // Get prosody data
            var testText = "こんにちは";
            var prosodyResult = _phonemizer.PhonemizeWithProsody(testText);
            var phonemeIds = _encoder.Encode(prosodyResult.Phonemes);

            // Generate WITH prosody
            var audioWithProsody = await _generator.GenerateAudioWithProsodyAsync(
                phonemeIds,
                prosodyResult.ProsodyA1,
                prosodyResult.ProsodyA2,
                prosodyResult.ProsodyA3
            );

            // Generate with ZERO prosody (all zeros)
            var zeroProsody = new int[prosodyResult.ProsodyA1.Length];
            var audioWithoutProsody = await _generator.GenerateAudioWithProsodyAsync(
                phonemeIds,
                zeroProsody,
                zeroProsody,
                zeroProsody
            );

            // Both should produce valid audio
            Assert.Greater(audioWithProsody.Length, 0, "Audio with prosody should have samples");
            Assert.Greater(audioWithoutProsody.Length, 0, "Audio without prosody should have samples");

            Debug.Log($"With prosody: {audioWithProsody.Length} samples ({audioWithProsody.Length / 22050.0f:F2}s)");
            Debug.Log($"Without prosody: {audioWithoutProsody.Length} samples ({audioWithoutProsody.Length / 22050.0f:F2}s)");

            // Note: The audio may differ in length and content due to prosody features
            // This test validates that both paths work correctly
        }

        /// <summary>
        /// モデルの入力型が実装と一致することを検証するテスト
        /// このテストは、prosody_featuresの型をintに変更するような間違いを事前に検知する
        /// </summary>
        [Test]
        public async Task ProsodyModel_InputTypes_MatchImplementationExpectations()
        {
            if (_prosodyModelAsset == null)
            {
                Assert.Ignore("Prosody model not available");
                return;
            }

            // モデルを初期化（初期化時に型検証が行われる）
            // もしprosody_featuresがFloat以外の型を期待する場合、InitializeAsyncで例外がスローされる
            await _generator.InitializeAsync(_prosodyModelAsset, _voiceConfig);

            Assert.IsTrue(_generator.IsInitialized, "Generator should be initialized");
            Assert.IsTrue(_generator.SupportsProsody, "Model should support prosody");

            // 追加の検証: 実際にProsody付きで音声生成が成功することを確認
            // これにより、Tensor<float>が正しく受け入れられることを検証
            var testPhonemeIds = new[] { 1, 0, 25, 0, 2 }; // Simple test sequence
            var testProsody = new[] { 0, 1, 2, 1, 0 };

            // この呼び出しが成功すれば、Float型が正しいことが証明される
            var audioData = await _generator.GenerateAudioWithProsodyAsync(
                testPhonemeIds,
                testProsody,
                testProsody,
                testProsody
            );

            Assert.IsNotNull(audioData, "Audio generation should succeed with Float prosody tensor");
            Assert.Greater(audioData.Length, 0, "Audio should have samples");

            Debug.Log($"[InputTypeTest] Successfully generated audio with Float prosody tensor: {audioData.Length} samples");
        }
    }
}
#endif
