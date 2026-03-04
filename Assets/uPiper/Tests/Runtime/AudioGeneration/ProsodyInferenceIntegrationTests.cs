#if !UNITY_WEBGL
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using Unity.InferenceEngine;
using uPiper.Core;
using uPiper.Core.AudioGeneration;
using uPiper.Core.Phonemizers;
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
        private const string MODEL_NAME = "tsukuyomi-chan";
        private InferenceAudioGenerator _generator;
        private OpenJTalkPhonemizer _phonemizer;
        private PhonemeEncoder _encoder;
        private ModelAsset _prosodyModelAsset;
        private PiperVoiceConfig _voiceConfig;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Load prosody-enabled model (try both paths)
            _prosodyModelAsset = Resources.Load<ModelAsset>($"uPiper/Models/{MODEL_NAME}");
            if (_prosodyModelAsset == null)
            {
                _prosodyModelAsset = Resources.Load<ModelAsset>($"Models/{MODEL_NAME}");
            }
            if (_prosodyModelAsset == null)
            {
                Debug.LogWarning($"Prosody model ({MODEL_NAME}.onnx) not found in Resources. Skipping prosody inference tests.");
            }

            // Load voice config from JSON
            var jsonAsset = Resources.Load<TextAsset>($"uPiper/Models/{MODEL_NAME}.onnx.json");
            if (jsonAsset == null)
            {
                jsonAsset = Resources.Load<TextAsset>($"Models/{MODEL_NAME}.onnx.json");
            }
            if (jsonAsset == null)
            {
                jsonAsset = Resources.Load<TextAsset>($"uPiper/Models/{MODEL_NAME}.onnx");
            }
            if (jsonAsset == null)
            {
                jsonAsset = Resources.Load<TextAsset>($"Models/{MODEL_NAME}.onnx");
            }

            if (jsonAsset != null)
            {
                _voiceConfig = ParseConfig(jsonAsset.text, MODEL_NAME);
                Debug.Log($"Loaded voice config with {_voiceConfig.PhonemeIdMap.Count} phonemes");
            }
            else
            {
                _voiceConfig = new PiperVoiceConfig
                {
                    VoiceId = MODEL_NAME,
                    SampleRate = 22050,
                    Language = "ja",
                    PhonemeIdMap = new Dictionary<string, int>()
                };
                Debug.LogWarning("Voice config JSON not found, using default config without phoneme map");
            }
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

        private static PiperVoiceConfig ParseConfig(string json, string modelName)
        {
            var config = new PiperVoiceConfig
            {
                VoiceId = modelName,
                DisplayName = modelName,
                Language = "ja",
                SampleRate = 22050,
                PhonemeIdMap = new Dictionary<string, int>()
            };

            try
            {
                var jsonObj = JObject.Parse(json);

                if (jsonObj["audio"]?["sample_rate"] != null)
                {
                    config.SampleRate = jsonObj["audio"]["sample_rate"].ToObject<int>();
                }

                // Read phoneme_type (critical for correct encoding - openjtalk vs espeak)
                if (jsonObj["phoneme_type"] != null)
                {
                    config.PhonemeType = jsonObj["phoneme_type"].ToString();
                    Debug.Log($"[ParseConfig] PhonemeType: {config.PhonemeType}");
                }

                if (jsonObj["phoneme_id_map"] is JObject phonemeIdMap)
                {
                    foreach (var kvp in phonemeIdMap)
                    {
                        if (kvp.Value is JArray idArray && idArray.Count > 0)
                        {
                            config.PhonemeIdMap[kvp.Key] = idArray[0].ToObject<int>();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error parsing config JSON: {ex.Message}");
            }

            return config;
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

            if (_voiceConfig.PhonemeIdMap == null || _voiceConfig.PhonemeIdMap.Count == 0)
            {
                Assert.Ignore("Voice config phoneme map not available");
                return;
            }

            // Initialize generator
            await _generator.InitializeAsync(_prosodyModelAsset, _voiceConfig);

            // Get prosody data
            var testText = "今日はとても良い天気ですね";  // 15 character test text
            var prosodyResult = _phonemizer.PhonemizeWithProsody(testText);

            Debug.Log($"Phonemes ({prosodyResult.Phonemes.Length}): [{string.Join(", ", prosodyResult.Phonemes)}]");
            Debug.Log($"ProsodyA1: [{string.Join(", ", prosodyResult.ProsodyA1)}]");
            Debug.Log($"ProsodyA2: [{string.Join(", ", prosodyResult.ProsodyA2)}]");
            Debug.Log($"ProsodyA3: [{string.Join(", ", prosodyResult.ProsodyA3)}]");

            // Convert OpenJTalk phonemes to Piper format
            var piperPhonemes = OpenJTalkToPiperMapping.ConvertToPiperPhonemes(prosodyResult.Phonemes);
            Debug.Log($"Piper phonemes ({piperPhonemes.Length}): [{string.Join(", ", piperPhonemes)}]");

            // Encode phonemes with prosody (this expands prosody arrays to match phoneme IDs length)
            var encodingResult = _encoder.EncodeWithProsody(
                piperPhonemes,
                prosodyResult.ProsodyA1,
                prosodyResult.ProsodyA2,
                prosodyResult.ProsodyA3
            );

            Debug.Log($"PhonemeIds ({encodingResult.PhonemeIds.Length}): [{string.Join(", ", encodingResult.PhonemeIds)}]");
            Debug.Log($"ExpandedA1 ({encodingResult.ExpandedProsodyA1.Length}): [{string.Join(", ", encodingResult.ExpandedProsodyA1)}]");

            // Generate audio with prosody
            var audioData = await _generator.GenerateAudioWithProsodyAsync(
                encodingResult.PhonemeIds,
                encodingResult.ExpandedProsodyA1,
                encodingResult.ExpandedProsodyA2,
                encodingResult.ExpandedProsodyA3
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

            if (_voiceConfig.PhonemeIdMap == null || _voiceConfig.PhonemeIdMap.Count == 0)
            {
                Assert.Ignore("Voice config phoneme map not available");
                return;
            }

            // Initialize generator
            await _generator.InitializeAsync(_prosodyModelAsset, _voiceConfig);

            // Get prosody data
            var testText = "今日はとても良い天気ですね";  // 15 character test text
            var prosodyResult = _phonemizer.PhonemizeWithProsody(testText);

            // Convert and encode with prosody
            var piperPhonemes = OpenJTalkToPiperMapping.ConvertToPiperPhonemes(prosodyResult.Phonemes);
            var encodingResult = _encoder.EncodeWithProsody(
                piperPhonemes,
                prosodyResult.ProsodyA1,
                prosodyResult.ProsodyA2,
                prosodyResult.ProsodyA3
            );

            // Generate WITH prosody
            var audioWithProsody = await _generator.GenerateAudioWithProsodyAsync(
                encodingResult.PhonemeIds,
                encodingResult.ExpandedProsodyA1,
                encodingResult.ExpandedProsodyA2,
                encodingResult.ExpandedProsodyA3
            );

            // Generate with ZERO prosody (all zeros, same length as expanded arrays)
            var zeroProsody = new int[encodingResult.ExpandedProsodyA1.Length];
            var audioWithoutProsody = await _generator.GenerateAudioWithProsodyAsync(
                encodingResult.PhonemeIds,
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

            if (_phonemizer == null)
            {
                Assert.Ignore("OpenJTalk phonemizer not available");
                return;
            }

            if (_voiceConfig.PhonemeIdMap == null || _voiceConfig.PhonemeIdMap.Count == 0)
            {
                Assert.Ignore("Voice config phoneme map not available");
                return;
            }

            // モデルを初期化（初期化時に型検証が行われる）
            // もしprosody_featuresがFloat以外の型を期待する場合、InitializeAsyncで例外がスローされる
            await _generator.InitializeAsync(_prosodyModelAsset, _voiceConfig);

            Assert.IsTrue(_generator.IsInitialized, "Generator should be initialized");
            Assert.IsTrue(_generator.SupportsProsody, "Model should support prosody");

            // 正しいエンコーディングフローでテスト
            var testText = "こんにちは";
            var prosodyResult = _phonemizer.PhonemizeWithProsody(testText);
            var piperPhonemes = OpenJTalkToPiperMapping.ConvertToPiperPhonemes(prosodyResult.Phonemes);
            var encodingResult = _encoder.EncodeWithProsody(
                piperPhonemes,
                prosodyResult.ProsodyA1,
                prosodyResult.ProsodyA2,
                prosodyResult.ProsodyA3
            );

            Debug.Log($"[InputTypeTest] Testing with {encodingResult.PhonemeIds.Length} phoneme IDs");

            // この呼び出しが成功すれば、Float型が正しいことが証明される
            var audioData = await _generator.GenerateAudioWithProsodyAsync(
                encodingResult.PhonemeIds,
                encodingResult.ExpandedProsodyA1,
                encodingResult.ExpandedProsodyA2,
                encodingResult.ExpandedProsodyA3
            );

            Assert.IsNotNull(audioData, "Audio generation should succeed with Float prosody tensor");
            Assert.Greater(audioData.Length, 0, "Audio should have samples");

            Debug.Log($"[InputTypeTest] Successfully generated audio with Float prosody tensor: {audioData.Length} samples ({audioData.Length / 22050.0f:F2}s)");
        }
    }
}
#endif