#if UNITY_EDITOR && UNITY_EDITOR_WIN
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core;
using uPiper.Core.Logging;
using uPiper.Core.Phonemizers.Implementations;

namespace uPiper.Tests.Editor
{
    /// <summary>
    /// Windows環境での長文・漢字テスト
    /// </summary>
    public class WindowsLongTextTest
    {
        private OpenJTalkPhonemizer _phonemizer;

        [SetUp]
        public void Setup()
        {
            // デバッグログを有効化
            PiperLogger.SetMinimumLevel(PiperLogger.LogLevel.Debug);
        }

        [TearDown]
        public void TearDown()
        {
            _phonemizer?.Dispose();
        }

        [Test]
        public void TestLongJapaneseText()
        {
            try
            {
                _phonemizer = new OpenJTalkPhonemizer();

                // テストケース1: 短い文
                var shortText = "こんにちは";
                Debug.Log($"\n=== Testing short text: {shortText} ===");
                var shortResult = _phonemizer.Phonemize(shortText, "ja");
                Debug.Log($"Phonemes: {string.Join(" ", shortResult.Phonemes)}");
                Assert.IsNotNull(shortResult.Phonemes);
                Assert.Greater(shortResult.Phonemes.Length, 0);

                // テストケース2: 漢字を含む文
                var kanjiText = "日本語の音声合成";
                Debug.Log($"\n=== Testing kanji text: {kanjiText} ===");
                var kanjiResult = _phonemizer.Phonemize(kanjiText, "ja");
                Debug.Log($"Phonemes: {string.Join(" ", kanjiResult.Phonemes)}");
                Assert.IsNotNull(kanjiResult.Phonemes);
                Assert.Greater(kanjiResult.Phonemes.Length, 0);

                // テストケース3: 長文
                var longText = "人工知能による音声合成技術は、近年急速に発展しています。特に深層学習を用いた手法により、より自然で人間らしい音声を生成することが可能になりました。";
                Debug.Log($"\n=== Testing long text: {longText} ===");
                var longResult = _phonemizer.Phonemize(longText, "ja");
                Debug.Log($"Phonemes ({longResult.Phonemes.Length} total): {string.Join(" ", longResult.Phonemes)}");
                Assert.IsNotNull(longResult.Phonemes);
                Assert.Greater(longResult.Phonemes.Length, 20); // 長文なので多くのフォネームが期待される

                // テストケース4: 問題が報告されていた「日本橋」
                var nihonbashiText = "日本橋";
                Debug.Log($"\n=== Testing problematic text: {nihonbashiText} ===");
                var nihonbashiResult = _phonemizer.Phonemize(nihonbashiText, "ja");
                Debug.Log($"Phonemes: {string.Join(" ", nihonbashiResult.Phonemes)}");

                // 重複チェック
                var phonemeString = string.Join(" ", nihonbashiResult.Phonemes);
                Assert.IsFalse(phonemeString.Contains("n i h o N b a s i pau n i h o N b a s i"),
                    "Detected duplicate phoneme pattern!");

                // テストケース5: 繰り返しの多い文
                var repetitiveText = "ててて";
                Debug.Log($"\n=== Testing repetitive text: {repetitiveText} ===");
                var repetitiveResult = _phonemizer.Phonemize(repetitiveText, "ja");
                Debug.Log($"Phonemes: {string.Join(" ", repetitiveResult.Phonemes)}");

                // 異常な繰り返しがないかチェック
                var phonemeCount = repetitiveResult.Phonemes.Length;
                Assert.Less(phonemeCount, 20, "Too many phonemes for short repetitive text!");

                Debug.Log("\n=== All tests completed successfully ===");
            }
            catch (PiperInitializationException e)
            {
                Debug.LogWarning($"OpenJTalk initialization failed: {e.Message}");
                Debug.LogWarning("This is expected if native library is not built.");
                Assert.Inconclusive("Native library not available");
            }
        }

        [Test]
        public void TestWindowsSpecificIssues()
        {
            try
            {
                _phonemizer = new OpenJTalkPhonemizer();

                // Windows環境で問題が発生していた具体的なケース
                var testCases = new[]
                {
                    ("東京都", "とうきょうと"),
                    ("大阪府", "おおさかふ"),
                    ("新幹線", "しんかんせん"),
                    ("人工知能", "じんこうちのう"),
                    ("音声合成", "おんせいごうせい")
                };

                foreach (var (kanji, hiragana) in testCases)
                {
                    Debug.Log($"\n=== Testing: {kanji} ({hiragana}) ===");
                    var result = _phonemizer.Phonemize(kanji, "ja");
                    var phonemes = string.Join(" ", result.Phonemes);
                    Debug.Log($"Phonemes: {phonemes}");

                    // 基本的なチェック
                    Assert.IsNotNull(result.Phonemes);
                    Assert.Greater(result.Phonemes.Length, 0);

                    // 重複パターンのチェック
                    for (var i = 0; i < result.Phonemes.Length - 3; i++)
                    {
                        var segment = string.Join(" ", result.Phonemes.Skip(i).Take(3));
                        var nextSegment = string.Join(" ", result.Phonemes.Skip(i + 3).Take(3));
                        if (segment == nextSegment && segment.Length > 5)
                        {
                            Assert.Fail($"Detected repeated pattern: {segment}");
                        }
                    }
                }
            }
            catch (PiperInitializationException)
            {
                Assert.Inconclusive("Native library not available");
            }
        }
    }
}
#endif