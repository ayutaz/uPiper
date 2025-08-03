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

            // Initialize phonemizer without Task.Run to avoid threading issues
            var initTask = phonemizer.InitializeAsync();

            while (!initTask.IsCompleted)
            {
                yield return null;
            }

            if (initTask.IsFaulted)
            {
                throw initTask.Exception?.GetBaseException() ?? new System.Exception("Phonemizer init failed");
            }

            if (!initTask.Result)
            {
                throw new System.Exception("Phonemizer initialization returned false");
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
            // Use cached fallback dictionary
            var dictionary = ChineseDictionaryTestCache.GetDictionary();

            // Test Common characters
            TestCategoryCharacters("Common", "的一是了我不人在他有这个上们来到时大地为", dictionary);

            // Test Numbers
            TestCategoryCharacters("Numbers", "零一二三四五六七八九十百千万亿", dictionary);

            // Test Technical terms
            TestCategoryCharacters("Technical", "电脑网络软件硬件数据算法程序代码系统", dictionary);

            // Test Daily life
            TestCategoryCharacters("Daily", "吃饭睡觉工作学习生活家庭朋友", dictionary);

            // Test Geography
            TestCategoryCharacters("Geography", "中国美国日本英国法国德国俄罗斯", dictionary);

            // Test Culture
            TestCategoryCharacters("Culture", "文化历史艺术音乐电影文学诗歌", dictionary);
        }

        private void TestCategoryCharacters(string category, string chars, ChinesePinyinDictionary dictionary)
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

        [TearDown]
        public void TearDown()
        {
            phonemizer?.Dispose();
        }
    }
}