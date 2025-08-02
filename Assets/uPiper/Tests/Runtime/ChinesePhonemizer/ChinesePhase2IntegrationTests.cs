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