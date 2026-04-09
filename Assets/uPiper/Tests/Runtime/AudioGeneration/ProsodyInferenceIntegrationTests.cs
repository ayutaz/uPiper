#if !UNITY_WEBGL
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using Unity.InferenceEngine;
using uPiper.Core;
using uPiper.Core.AudioGeneration;
using uPiper.Core.Phonemizers;
using uPiper.Core.Phonemizers.Implementations;
using uPiper.Core.Phonemizers.Multilingual;

namespace uPiper.Tests.Runtime.AudioGeneration
{
    /// <summary>
    /// Prosody対応モデルでの推論統合テスト
    /// </summary>
    [TestFixture]
    [Category("RequiresProsodyModel")]
    public class ProsodyInferenceIntegrationTests
    {
        private const string MODEL_NAME = "multilingual-test-medium";
        private InferenceAudioGenerator _generator;
        private DotNetG2PPhonemizer _phonemizer;
        private PhonemeEncoder _encoder;
        private PuaTokenMapper _mapper;
        private ModelAsset _prosodyModelAsset;
        private PiperVoiceConfig _voiceConfig;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // All tests in this class run ONNX inference which is too slow for CI
            if (Application.isBatchMode)
            {
                Assert.Ignore("Skipping heavy inference tests in CI batch mode");
                return;
            }

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
                    PhonemeIdMap = new Dictionary<string, int[]>()
                };
                Debug.LogWarning("Voice config JSON not found, using default config without phoneme map");
            }
        }

        [SetUp]
        public void Setup()
        {
            _generator = new InferenceAudioGenerator();
            _mapper = new PuaTokenMapper();
            _encoder = new PhonemeEncoder(_voiceConfig, _mapper);

            // Initialize OpenJTalk phonemizer
            try
            {
                _phonemizer = new DotNetG2PPhonemizer();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to create DotNetG2PPhonemizer: {ex.Message}");
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
                PhonemeIdMap = new Dictionary<string, int[]>()
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
                            config.PhonemeIdMap[kvp.Key] = idArray.ToObject<int[]>();
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

            // Flatten A1/A2/A3 from DotNetG2P into stride=3 flat array
            var prosodyFlat = PhonemeEncoder.FlattenProsody(
                prosodyResult.ProsodyA1, prosodyResult.ProsodyA2, prosodyResult.ProsodyA3,
                piperPhonemes.Length);

            // Encode phonemes with prosody (this expands prosody flat array to match phoneme IDs length)
            var encodingResult = _encoder.EncodeWithProsody(piperPhonemes, prosodyFlat);

            Debug.Log($"PhonemeIds ({encodingResult.PhonemeIds.Length}): [{string.Join(", ", encodingResult.PhonemeIds)}]");
            Debug.Log($"ExpandedA1 ({encodingResult.ExpandedProsodyFlat.Length}): [{string.Join(", ", encodingResult.ExpandedProsodyFlat)}]");

            // Generate audio with prosody
            var nativeAudioData = await _generator.GenerateAudioAsync(
                encodingResult.PhonemeIds,
                encodingResult.ExpandedProsodyFlat
            );

            try
            {
                // Convert to managed array for LINQ validation
                var audioData = nativeAudioData.ToArray();

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
            finally
            {
                if (nativeAudioData.IsCreated)
                    nativeAudioData.Dispose();
            }
        }

        [Test]
        [Timeout(300000)]
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
            var prosodyFlat = PhonemeEncoder.FlattenProsody(
                prosodyResult.ProsodyA1, prosodyResult.ProsodyA2, prosodyResult.ProsodyA3,
                piperPhonemes.Length);
            var encodingResult = _encoder.EncodeWithProsody(piperPhonemes, prosodyFlat);

            // Generate WITH prosody
            var nativeAudioWithProsody = await _generator.GenerateAudioAsync(
                encodingResult.PhonemeIds,
                encodingResult.ExpandedProsodyFlat
            );

            // Generate with ZERO prosody (all zeros, same length as expanded arrays)
            var zeroProsody = new int[encodingResult.ExpandedProsodyFlat.Length];
            var nativeAudioWithoutProsody = await _generator.GenerateAudioAsync(
                encodingResult.PhonemeIds,
                zeroProsody
            );

            try
            {
                // Both should produce valid audio
                Assert.Greater(nativeAudioWithProsody.Length, 0,
                    "Audio with prosody should have samples");
                Assert.Greater(nativeAudioWithoutProsody.Length, 0,
                    "Audio without prosody should have samples");

                Debug.Log($"With prosody: {nativeAudioWithProsody.Length} samples " +
                    $"({nativeAudioWithProsody.Length / 22050.0f:F2}s)");
                Debug.Log($"Without prosody: {nativeAudioWithoutProsody.Length} samples " +
                    $"({nativeAudioWithoutProsody.Length / 22050.0f:F2}s)");

                // Note: The audio may differ in length and content due to prosody features
                // This test validates that both paths work correctly
            }
            finally
            {
                if (nativeAudioWithProsody.IsCreated)
                    nativeAudioWithProsody.Dispose();
                if (nativeAudioWithoutProsody.IsCreated)
                    nativeAudioWithoutProsody.Dispose();
            }
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
            var prosodyFlat = PhonemeEncoder.FlattenProsody(
                prosodyResult.ProsodyA1, prosodyResult.ProsodyA2, prosodyResult.ProsodyA3,
                piperPhonemes.Length);
            var encodingResult = _encoder.EncodeWithProsody(piperPhonemes, prosodyFlat);

            Debug.Log($"[InputTypeTest] Testing with {encodingResult.PhonemeIds.Length} phoneme IDs");

            // この呼び出しが成功すれば、Float型が正しいことが証明される
            var nativeAudioData = await _generator.GenerateAudioAsync(
                encodingResult.PhonemeIds,
                encodingResult.ExpandedProsodyFlat
            );

            try
            {
                Assert.IsTrue(nativeAudioData.IsCreated,
                    "Audio generation should succeed with Float prosody tensor");
                Assert.Greater(nativeAudioData.Length, 0, "Audio should have samples");

                Debug.Log($"[InputTypeTest] Successfully generated audio with Float prosody tensor: " +
                    $"{nativeAudioData.Length} samples ({nativeAudioData.Length / 22050.0f:F2}s)");
            }
            finally
            {
                if (nativeAudioData.IsCreated)
                    nativeAudioData.Dispose();
            }
        }

        /// <summary>
        /// JSON "phoneme_id_map" の配列値が int[] として正しくデシリアライズされることを検証する。
        /// 複数ID（例: "a": [10, 11, 12]）が情報損失なく保持されることを確認（P2-1）。
        /// </summary>
        [Test]
        public void ParseConfig_MultiIdPhoneme_DeserializesArray()
        {
            // Arrange: JSON with multi-element arrays in phoneme_id_map
            var json = @"{
                ""audio"": { ""sample_rate"": 22050 },
                ""phoneme_type"": ""openjtalk"",
                ""phoneme_id_map"": {
                    ""_"": [0],
                    ""^"": [1],
                    ""$"": [2],
                    ""a"": [10, 11, 12],
                    ""b"": [20, 21]
                }
            }";

            // Act
            var config = ParseConfig(json, "multi-id-test");

            // Assert: arrays are preserved without information loss
            Assert.IsNotNull(config.PhonemeIdMap);
            Assert.AreEqual(5, config.PhonemeIdMap.Count);

            // Single-element arrays
            CollectionAssert.AreEqual(new[] { 0 }, config.PhonemeIdMap["_"]);
            CollectionAssert.AreEqual(new[] { 1 }, config.PhonemeIdMap["^"]);
            CollectionAssert.AreEqual(new[] { 2 }, config.PhonemeIdMap["$"]);

            // Multi-element arrays (P2-1: no information loss)
            CollectionAssert.AreEqual(new[] { 10, 11, 12 }, config.PhonemeIdMap["a"]);
            CollectionAssert.AreEqual(new[] { 20, 21 }, config.PhonemeIdMap["b"]);
        }
    }
}
#endif