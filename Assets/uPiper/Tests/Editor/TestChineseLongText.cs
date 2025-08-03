using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core.Phonemizers.Backend;
using uPiper.Core.Phonemizers.Backend.Chinese;

namespace uPiper.Tests.Editor
{
    public class TestChineseLongText
    {
        private ChinesePhonemizer phonemizer;

        [SetUp]
        public void SetUp()
        {
            phonemizer = new ChinesePhonemizer();
            var initTask = phonemizer.InitializeAsync();
            initTask.Wait();
        }

        [Test]
        public void TestLongChineseText()
        {
            var testTexts = new[]
            {
                "我是一个人工智能助手。",
                "今天天气很好，我们一起去公园玩吧。",
                "中国是一个历史悠久的国家，有着灿烂的文化。",
                "人工智能技术正在改变世界，让生活变得更加便利。",
                "学习中文需要付出努力，但是很有意义。",
                "春节是中国最重要的传统节日，家人会团聚在一起。",
                "北京是中国的首都，有许多著名的历史古迹。",
                "互联网改变了人们的生活方式，让信息传播更加快速。",
                "中国菜有很多种类，每个地区都有自己的特色。",
                "教育对于个人发展和社会进步都非常重要。"
            };

            foreach (var text in testTexts)
            {
                Debug.Log($"\n=== Testing: {text} ===");

                var result = phonemizer.PhonemizeAsync(text, "zh").Result;

                Assert.IsNotNull(result);
                Assert.Greater(result.Phonemes.Length, 0);

                Debug.Log($"Phonemes ({result.Phonemes.Length}): {string.Join(" ", result.Phonemes)}");

                // Log detailed phoneme breakdown
                Debug.Log("Detailed breakdown:");
                for (int i = 0; i < result.Phonemes.Length; i++)
                {
                    var phoneme = result.Phonemes[i];
                    Debug.Log($"  [{i}] {phoneme}");
                }
            }
        }

        [Test]
        public void TestMixedChineseEnglishText()
        {
            var testTexts = new[]
            {
                "我喜欢用Unity开发游戏。",
                "AI技术在2024年取得了重大突破。",
                "iPhone和Android是两大主流手机系统。",
                "COVID-19疫情改变了世界。",
                "Python是一种流行的编程语言。"
            };

            foreach (var text in testTexts)
            {
                Debug.Log($"\n=== Testing mixed: {text} ===");

                var result = phonemizer.PhonemizeAsync(text, "zh").Result;

                Assert.IsNotNull(result);
                Assert.Greater(result.Phonemes.Length, 0);

                Debug.Log($"Phonemes: {string.Join(" ", result.Phonemes)}");
            }
        }

        [Test]
        public void CompareShortVsLongText()
        {
            var shortTexts = new[] { "你", "好", "你好", "中国" };
            var longTexts = new[]
            {
                "你好世界",
                "你好，我是助手",
                "中国人民共和国",
                "中国是一个大国"
            };

            Debug.Log("\n=== Short texts ===");
            foreach (var text in shortTexts)
            {
                var result = phonemizer.PhonemizeAsync(text, "zh").Result;
                Debug.Log($"{text}: {string.Join(" ", result.Phonemes)}");
            }

            Debug.Log("\n=== Long texts ===");
            foreach (var text in longTexts)
            {
                var result = phonemizer.PhonemizeAsync(text, "zh").Result;
                Debug.Log($"{text}: {string.Join(" ", result.Phonemes)}");
            }
        }

        [TearDown]
        public void TearDown()
        {
            phonemizer?.Dispose();
        }
    }
}