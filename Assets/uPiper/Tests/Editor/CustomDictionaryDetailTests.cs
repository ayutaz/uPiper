using System.Linq;
using NUnit.Framework;
using uPiper.Core.Phonemizers;

namespace uPiper.Tests.Editor
{
    [TestFixture]
    public class CustomDictionaryDetailTests
    {
        [Test]
        public void ApplyToTextWithDetails_SingleReplacement_ReturnsDetail()
        {
            var dict = new CustomDictionary(loadDefaults: false);
            dict.AddWord("Docker", "ドッカー", priority: 9);

            var (resultText, replacements) = dict.ApplyToTextWithDetails("Dockerを使う");

            Assert.AreEqual("ドッカーを使う", resultText);
            Assert.AreEqual(1, replacements.Count);
            Assert.AreEqual("Docker", replacements[0].OriginalWord);
            Assert.AreEqual("ドッカー", replacements[0].Pronunciation);
            Assert.AreEqual(9, replacements[0].Priority);
            Assert.AreEqual(0, replacements[0].Position);
        }

        [Test]
        public void ApplyToTextWithDetails_MultipleReplacements_ReturnsAllDetails()
        {
            var dict = new CustomDictionary(loadDefaults: false);
            dict.AddWord("Docker", "ドッカー", priority: 9);
            dict.AddWord("GitHub", "ギットハブ", priority: 8);

            var (resultText, replacements) = dict.ApplyToTextWithDetails("DockerとGitHubを使った開発");

            Assert.AreEqual("ドッカーとギットハブを使った開発", resultText);
            Assert.AreEqual(2, replacements.Count);

            var dockerDetail = replacements.FirstOrDefault(r => r.OriginalWord == "Docker");
            var gitHubDetail = replacements.FirstOrDefault(r => r.OriginalWord == "GitHub");

            Assert.IsNotNull(dockerDetail.OriginalWord);
            Assert.AreEqual("ドッカー", dockerDetail.Pronunciation);
            Assert.IsNotNull(gitHubDetail.OriginalWord);
            Assert.AreEqual("ギットハブ", gitHubDetail.Pronunciation);
        }

        [Test]
        public void ApplyToTextWithDetails_NoReplacement_ReturnsEmptyList()
        {
            var dict = new CustomDictionary(loadDefaults: false);
            dict.AddWord("Docker", "ドッカー", priority: 9);

            var (resultText, replacements) = dict.ApplyToTextWithDetails("こんにちは");

            Assert.AreEqual("こんにちは", resultText);
            Assert.AreEqual(0, replacements.Count);
        }

        [Test]
        public void ApplyToTextWithDetails_ResultText_MatchesApplyToText()
        {
            var dict = new CustomDictionary(loadDefaults: false);
            dict.AddWord("Docker", "ドッカー", priority: 9);
            dict.AddWord("GitHub", "ギットハブ", priority: 8);
            dict.AddWord("API", "エーピーアイ", priority: 7);

            var input = "DockerとGitHubのAPIを使った開発";
            var expected = dict.ApplyToText(input);
            var (resultText, _) = dict.ApplyToTextWithDetails(input);

            Assert.AreEqual(expected, resultText);
        }

        [Test]
        public void ApplyToTextWithDetails_NullInput_ReturnsNullAndEmpty()
        {
            var dict = new CustomDictionary(loadDefaults: false);

            var (resultText, replacements) = dict.ApplyToTextWithDetails(null);

            Assert.IsNull(resultText);
            Assert.AreEqual(0, replacements.Count);
        }

        [Test]
        public void ApplyToTextWithDetails_EmptyInput_ReturnsEmptyAndEmpty()
        {
            var dict = new CustomDictionary(loadDefaults: false);

            var (resultText, replacements) = dict.ApplyToTextWithDetails("");

            Assert.AreEqual("", resultText);
            Assert.AreEqual(0, replacements.Count);
        }
    }
}