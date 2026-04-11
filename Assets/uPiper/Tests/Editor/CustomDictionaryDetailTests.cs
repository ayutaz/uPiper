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

            Assert.That(resultText, Is.EqualTo("ドッカーを使う"),
                "Result text should have Docker replaced with pronunciation");
            Assert.That(replacements.Count, Is.EqualTo(1),
                "Should have exactly one replacement");
            Assert.That(replacements[0].OriginalWord, Is.EqualTo("Docker"),
                "OriginalWord should be 'Docker'");
            Assert.That(replacements[0].Pronunciation, Is.EqualTo("ドッカー"),
                "Pronunciation should be 'ドッカー'");
            Assert.That(replacements[0].Priority, Is.EqualTo(9),
                "Priority should be 9");
            Assert.That(replacements[0].Position, Is.EqualTo(0),
                "Position should be 0 for first match");
        }

        [Test]
        public void ApplyToTextWithDetails_MultipleReplacements_ReturnsAllDetails()
        {
            var dict = new CustomDictionary(loadDefaults: false);
            dict.AddWord("Docker", "ドッカー", priority: 9);
            dict.AddWord("GitHub", "ギットハブ", priority: 8);

            var (resultText, replacements) = dict.ApplyToTextWithDetails("DockerとGitHubを使った開発");

            Assert.That(resultText, Is.EqualTo("ドッカーとギットハブを使った開発"),
                "Result text should have both words replaced");
            Assert.That(replacements.Count, Is.EqualTo(2),
                "Should have two replacements");

            var dockerDetail = replacements.FirstOrDefault(r => r.OriginalWord == "Docker");
            var gitHubDetail = replacements.FirstOrDefault(r => r.OriginalWord == "GitHub");

            Assert.That(dockerDetail.OriginalWord, Is.Not.Null,
                "Docker replacement should be found");
            Assert.That(dockerDetail.Pronunciation, Is.EqualTo("ドッカー"),
                "Docker pronunciation should be 'ドッカー'");
            Assert.That(gitHubDetail.OriginalWord, Is.Not.Null,
                "GitHub replacement should be found");
            Assert.That(gitHubDetail.Pronunciation, Is.EqualTo("ギットハブ"),
                "GitHub pronunciation should be 'ギットハブ'");
        }

        [Test]
        public void ApplyToTextWithDetails_NoReplacement_ReturnsEmptyList()
        {
            var dict = new CustomDictionary(loadDefaults: false);
            dict.AddWord("Docker", "ドッカー", priority: 9);

            var (resultText, replacements) = dict.ApplyToTextWithDetails("こんにちは");

            Assert.That(resultText, Is.EqualTo("こんにちは"),
                "Text without dictionary words should remain unchanged");
            Assert.That(replacements.Count, Is.EqualTo(0),
                "Replacements should be empty when no words matched");
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

            Assert.That(resultText, Is.EqualTo(expected),
                "ApplyToTextWithDetails result should match ApplyToText result");
        }

        [Test]
        public void ApplyToTextWithDetails_NullInput_ReturnsNullAndEmpty()
        {
            var dict = new CustomDictionary(loadDefaults: false);

            var (resultText, replacements) = dict.ApplyToTextWithDetails(null);

            Assert.That(resultText, Is.Null,
                "Null input should return null result text");
            Assert.That(replacements.Count, Is.EqualTo(0),
                "Null input should return empty replacements");
        }

        [Test]
        public void ApplyToTextWithDetails_EmptyInput_ReturnsEmptyAndEmpty()
        {
            var dict = new CustomDictionary(loadDefaults: false);

            var (resultText, replacements) = dict.ApplyToTextWithDetails("");

            Assert.That(resultText, Is.EqualTo(""),
                "Empty input should return empty result text");
            Assert.That(replacements.Count, Is.EqualTo(0),
                "Empty input should return empty replacements");
        }
    }
}