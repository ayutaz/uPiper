using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core;
using uPiper.Core.AudioGeneration;
using uPiper.Core.Phonemizers.Backend.Chinese;

namespace uPiper.Tests.Runtime.ChinesePhonemizer
{
    /// <summary>
    /// Integration tests for Phase 2 Chinese support with actual TTS pipeline
    /// </summary>
    public class ChinesePhase2IntegrationTests
    {
        private ChinesePhonemizer.ChinesePhonemizer phonemizer;
        private ModelAsset chineseModel;
        private PiperVoiceConfig config;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            phonemizer = new ChinesePhonemizer.ChinesePhonemizer();
            
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
        public void ExpandedDictionary_ShouldHandleTechnicalTerms()
        {
            var technicalTexts = new[]
            {
                "人工智能正在改变世界。",
                "机器学习是人工智能的一个分支。",
                "深度学习使用神经网络进行模式识别。",
                "自然语言处理帮助计算机理解人类语言。",
                "计算机视觉让机器能够看懂图像。"
            };

            foreach (var text in technicalTexts)
            {
                var result = phonemizer.PhonemizeAsync(text, "zh").Result;
                
                Assert.IsNotNull(result, $"Should phonemize: {text}");
                Assert.Greater(result.Phonemes.Length, 0, $"Should produce phonemes for: {text}");
                
                // Check that technical terms are properly handled
                Debug.Log($"[Phase2Integration] '{text}' -> {string.Join(" ", result.Phonemes)}");
                
                // No phoneme should be the Unicode escape (uXXXX)
                foreach (var phoneme in result.Phonemes)
                {
                    Assert.IsFalse(phoneme.StartsWith("u") && phoneme.Length == 5,
                        $"Phoneme '{phoneme}' looks like Unicode escape - character not in dictionary");
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

        [Test]
        public void ExpandedDictionary_ShouldImprovePhraseCoverage()
        {
            // Test phrases that should be in expanded dictionary
            var testPhrases = new[]
            {
                ("中华人民共和国", "zhong1 hua2 ren2 min2 gong4 he2 guo2"),
                ("人工智能", "ren2 gong1 zhi4 neng2"),
                ("机器学习", "ji1 qi4 xue2 xi2"),
                ("自然语言", "zi4 ran2 yu3 yan2"),
                ("北京大学", "bei3 jing1 da4 xue2")
            };

            var loader = new ChineseDictionaryLoader();
            var dictTask = loader.LoadAsync();
            dictTask.Wait();
            var dictionary = dictTask.Result;

            int foundCount = 0;
            foreach (var (phrase, expectedPinyin) in testPhrases)
            {
                if (dictionary.TryGetPhrasePinyin(phrase, out var pinyin))
                {
                    foundCount++;
                    Debug.Log($"[Phase2Integration] Found phrase: {phrase} -> {pinyin}");
                }
            }

            // Should find most phrases with expanded dictionary
            Assert.Greater(foundCount, testPhrases.Length / 2,
                $"Should find at least half of test phrases, but only found {foundCount}/{testPhrases.Length}");
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
            Assert.Greater(encodedIds.Count, 0);
            
            // Generate audio
            var generator = new InferenceAudioGenerator();
            var generateTask = Task.Run(async () =>
            {
                return await generator.GenerateAudioAsync(chineseModel, encodedIds, config);
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

            var loader = new ChineseDictionaryLoader();
            var dictTask = loader.LoadAsync();
            dictTask.Wait();
            var dictionary = dictTask.Result;

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
                Assert.Greater(coverage, 90f, 
                    $"{category} category should have >90% coverage, but only has {coverage:F1}%");
            }
        }

        [TearDown]
        public void TearDown()
        {
            phonemizer?.Dispose();
        }
    }
}