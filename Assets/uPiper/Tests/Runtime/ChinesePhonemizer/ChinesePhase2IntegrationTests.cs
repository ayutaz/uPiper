using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.InferenceEngine;
using uPiper.Core;
using uPiper.Core.AudioGeneration;
using uPiper.Core.Phonemizers.Backend;
using uPiper.Core.Phonemizers.Backend.Chinese;

namespace uPiper.Tests.Runtime.ChinesePhonemizer
{
    /// <summary>
    /// Integration tests for Phase 2 Chinese support with actual TTS pipeline
    /// </summary>
    public class ChinesePhase2IntegrationTests
    {
        private uPiper.Core.Phonemizers.Backend.ChinesePhonemizer phonemizer;
        private ModelAsset chineseModel;
        private PiperVoiceConfig config;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            phonemizer = new uPiper.Core.Phonemizers.Backend.ChinesePhonemizer();
            
            // Initialize phonemizer
            var initTask = Task.Run(async () =>
            {
                await phonemizer.InitializeAsync();
            });
            
            while (!initTask.IsCompleted)
            {
                yield return null;
            }
            
            if (initTask.IsFaulted)
            {
                throw initTask.Exception?.GetBaseException() ?? new System.Exception("Phonemizer init failed");
            }
            
            // Try to load Chinese model
            chineseModel = Resources.Load<ModelAsset>("Models/zh_CN-huayan-medium");
            if (chineseModel == null)
            {
                Debug.LogWarning("[Phase2Integration] Chinese model not found, some tests will be skipped");
            }
            
            // Load config if model exists
            if (chineseModel != null)
            {
                var jsonAsset = Resources.Load<TextAsset>("Models/zh_CN-huayan-medium.onnx");
                if (jsonAsset != null)
                {
                    config = JsonUtility.FromJson<PiperVoiceConfig>(jsonAsset.text);
                }
            }
        }


        [Test]
        public void ExpandedDictionary_ShouldHandleMixedContent()
        {
            var mixedTexts = new[]
            {
                "iPhone 15是苹果公司的最新产品。",
                "ChatGPT可以用中文对话。",
                "Windows 11支持中文输入法。",
                "Python是一种流行的编程语言。",
                "5G网络速度比4G快很多。"
            };

            foreach (var text in mixedTexts)
            {
                var result = phonemizer.PhonemizeAsync(text, "zh").Result;
                
                Assert.IsNotNull(result);
                Assert.Greater(result.Phonemes.Length, 0);
                
                Debug.Log($"[Phase2Integration] Mixed: '{text}' -> {string.Join(" ", result.Phonemes)}");
            }
        }


        [UnityTest]
        public IEnumerator FullPipeline_WithExpandedDictionary_ShouldGenerateAudio()
        {
            if (chineseModel == null || config == null)
            {
                Assert.Ignore("Chinese model not available for integration test");
                yield break;
            }

            var testText = "人工智能和机器学习是未来的发展方向。";
            
            // Phonemize
            var phonemizeTask = phonemizer.PhonemizeAsync(testText, "zh");
            yield return new WaitUntil(() => phonemizeTask.IsCompleted);
            
            var phonemeResult = phonemizeTask.Result;
            Assert.IsNotNull(phonemeResult);
            Assert.Greater(phonemeResult.Phonemes.Length, 0);
            
            Debug.Log($"[Phase2Integration] Phonemes: {string.Join(" ", phonemeResult.Phonemes)}");
            
            // Encode phonemes
            var encoder = new PhonemeEncoder(config);
            var encodedIds = encoder.Encode(phonemeResult.Phonemes);
            
            Assert.IsNotNull(encodedIds);
            Assert.Greater(encodedIds.Length, 0);
            
            // Generate audio
            var generator = new InferenceAudioGenerator();
            
            // Initialize generator on main thread
            Task<bool> initTask = null;
            bool initStarted = false;
            
            // Start initialization
            yield return null; // Ensure we're on main thread
            
            try
            {
                initTask = generator.InitializeAsync(chineseModel, config);
                initStarted = true;
            }
            catch (Exception ex)
            {
                Assert.Fail($"Generator initialization failed: {ex.Message}");
                yield break;
            }
            
            // Wait for initialization to complete
            while (initStarted && !initTask.IsCompleted)
            {
                yield return null;
            }
            
            if (initTask.IsFaulted)
            {
                throw initTask.Exception?.GetBaseException() ?? new System.Exception("Generator init failed");
            }
            
            // Now generate audio
            var generateTask = Task.Run(async () =>
            {
                return await generator.GenerateAudioAsync(encodedIds);
            });
            
            while (!generateTask.IsCompleted)
            {
                yield return null;
            }
            
            Assert.IsFalse(generateTask.IsFaulted, 
                $"Audio generation failed: {generateTask.Exception?.GetBaseException()?.Message}");
            
            var audioData = generateTask.Result;
            Assert.IsNotNull(audioData);
            Assert.Greater(audioData.Length, 0);
            
            // Verify audio quality
            var maxAmplitude = audioData.Max(x => Mathf.Abs(x));
            Assert.Greater(maxAmplitude, 0.01f, "Audio should have meaningful amplitude");
            
            Debug.Log($"[Phase2Integration] Generated audio: {audioData.Length} samples, " +
                     $"max amplitude: {maxAmplitude:F4}");
            
            // Cleanup
            generator.Dispose();
        }

        [Test]
        public void CharacterCoverage_ShouldBeComprehensive()
        {
            // Test coverage of different character categories
            var testCategories = new (string name, string chars)[]
            {
                ("Common", "的一是了我不人在他有这个上们来到时大地为"),
                ("Numbers", "零一二三四五六七八九十百千万亿"),
                ("Technical", "电脑网络软件硬件数据算法程序代码系统"),
                ("Daily", "吃饭睡觉工作学习生活家庭朋友"),
                ("Geography", "中国美国日本英国法国德国俄罗斯"),
                ("Culture", "文化历史艺术音乐电影文学诗歌")
            };

            // Use cached fallback dictionary
            var dictionary = ChineseDictionaryTestCache.GetDictionary();

            foreach (var (category, chars) in testCategories)
            {
                int found = 0;
                foreach (char c in chars)
                {
                    if (dictionary.TryGetCharacterPinyin(c, out _))
                    {
                        found++;
                    }
                }
                
                var coverage = found / (float)chars.Length * 100;
                Debug.Log($"[Phase2Integration] {category} coverage: {found}/{chars.Length} ({coverage:F1}%)");
                
                // Should have high coverage for all categories
                Assert.GreaterOrEqual(coverage, 90f, 
                    $"{category} category should have >=90% coverage, but only has {coverage:F1}%");
            }
        }

        [TearDown]
        public void TearDown()
        {
            phonemizer?.Dispose();
        }
    }
}