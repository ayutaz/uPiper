using NUnit.Framework;
using uPiper.Core.Phonemizers.Text;

namespace uPiper.Tests.Runtime.Core.Phonemizers
{
    public class TextNormalizerTest
    {
        private TextNormalizer _normalizer;

        [SetUp]
        public void Setup()
        {
            _normalizer = new TextNormalizer();
        }

        #region Common Normalization Tests

        [Test]
        public void Normalize_EmptyString_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, _normalizer.Normalize(string.Empty, "en"));
            Assert.AreEqual(string.Empty, _normalizer.Normalize(null, "en"));
        }

        [Test]
        public void Normalize_MultipleSpaces_CollapsedToSingle()
        {
            var input = "Hello    world    test";
            var expected = "hello world test";

            Assert.AreEqual(expected, _normalizer.Normalize(input, "en"));
        }

        [Test]
        public void Normalize_LineBreaks_ConvertedToSpaces()
        {
            var input = "Hello\nworld\r\ntest";
            var expected = "hello world test";

            Assert.AreEqual(expected, _normalizer.Normalize(input, "en"));
        }

        [Test]
        public void Normalize_ControlCharacters_Removed()
        {
            var input = "Hello\x00world\x1Ftest";
            var expected = "hello world test";

            Assert.AreEqual(expected, _normalizer.Normalize(input, "en"));
        }

        [Test]
        public void Normalize_LeadingTrailingSpaces_Trimmed()
        {
            var input = "  Hello world  ";
            var expected = "hello world";

            Assert.AreEqual(expected, _normalizer.Normalize(input, "en"));
        }

        #endregion

        #region Japanese Normalization Tests

        [Test]
        public void Normalize_Japanese_FullWidthNumbers_ConvertedToHalfWidth()
        {
            var input = "０１２３４５６７８９";
            var expected = "0123456789";

            Assert.AreEqual(expected, _normalizer.Normalize(input, "ja"));
        }

        [Test]
        public void Normalize_Japanese_FullWidthUppercase_ConvertedToHalfWidth()
        {
            var input = "ＡＢＣＤＥＦＧＨＩＪＫＬＭＮＯＰＱＲＳＴＵＶＷＸＹＺ";
            var expected = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

            Assert.AreEqual(expected, _normalizer.Normalize(input, "ja"));
        }

        [Test]
        public void Normalize_Japanese_FullWidthLowercase_ConvertedToHalfWidth()
        {
            var input = "ａｂｃｄｅｆｇｈｉｊｋｌｍｎｏｐｑｒｓｔｕｖｗｘｙｚ";
            var expected = "abcdefghijklmnopqrstuvwxyz";

            Assert.AreEqual(expected, _normalizer.Normalize(input, "ja"));
        }

        [Test]
        public void Normalize_Japanese_FullWidthSpace_ConvertedToHalfWidth()
        {
            var input = "こんにちは　世界";
            var expected = "こんにちは 世界";

            Assert.AreEqual(expected, _normalizer.Normalize(input, "ja"));
        }

        [Test]
        public void Normalize_Japanese_MixedText()
        {
            var input = "今日は２０２４年１月１５日です。";
            var expected = "今日は2024年1月15日です。";

            Assert.AreEqual(expected, _normalizer.Normalize(input, "ja"));
        }

        [Test]
        public void Normalize_Japanese_PreservesHiraganaKatakanaKanji()
        {
            var input = "ひらがな カタカナ 漢字";
            var expected = "ひらがな カタカナ 漢字";

            Assert.AreEqual(expected, _normalizer.Normalize(input, "ja"));
        }

        #endregion

        #region English Normalization Tests

        [Test]
        public void Normalize_English_Lowercase()
        {
            var input = "HELLO WORLD";
            var expected = "hello world";

            Assert.AreEqual(expected, _normalizer.Normalize(input, "en"));
        }

        [Test]
        public void Normalize_English_Contractions_Expanded()
        {
            Assert.AreEqual("it is", _normalizer.Normalize("it's", "en"));
            Assert.AreEqual("we are", _normalizer.Normalize("we're", "en"));
            Assert.AreEqual("i have", _normalizer.Normalize("i've", "en"));
            Assert.AreEqual("they will", _normalizer.Normalize("they'll", "en"));
            Assert.AreEqual("he would", _normalizer.Normalize("he'd", "en"));
            Assert.AreEqual("can not", _normalizer.Normalize("can't", "en"));
        }

        [Test]
        public void Normalize_English_PossessiveApostrophes_Removed()
        {
            var input = "John's book";
            var expected = "john is book";

            Assert.AreEqual(expected, _normalizer.Normalize(input, "en"));
        }

        [Test]
        public void Normalize_English_ComplexSentence()
        {
            var input = "I can't believe it's John's birthday!";
            var expected = "i can not believe it is john is birthday!";

            Assert.AreEqual(expected, _normalizer.Normalize(input, "en"));
        }

        #endregion

        #region Chinese Normalization Tests

        [Test]
        public void Normalize_Chinese_FullWidthPunctuation_ConvertedToHalfWidth()
        {
            Assert.AreEqual(".", _normalizer.Normalize("。", "zh"));
            Assert.AreEqual(",", _normalizer.Normalize("，", "zh"));
            Assert.AreEqual("?", _normalizer.Normalize("？", "zh"));
            Assert.AreEqual("!", _normalizer.Normalize("！", "zh"));
            Assert.AreEqual(",", _normalizer.Normalize("、", "zh"));
        }

        [Test]
        public void Normalize_Chinese_MixedPunctuation()
        {
            var input = "你好，世界！今天天气怎么样？";
            var expected = "你好,世界!今天天气怎么样?";

            Assert.AreEqual(expected, _normalizer.Normalize(input, "zh"));
        }

        #endregion

        #region NeedsNormalization Tests

        [Test]
        public void NeedsNormalization_EmptyString_ReturnsFalse()
        {
            Assert.IsFalse(_normalizer.NeedsNormalization(string.Empty, "en"));
            Assert.IsFalse(_normalizer.NeedsNormalization(null, "en"));
        }

        [Test]
        public void NeedsNormalization_MultipleSpaces_ReturnsTrue()
        {
            Assert.IsTrue(_normalizer.NeedsNormalization("Hello  world", "en"));
        }

        [Test]
        public void NeedsNormalization_LineBreaks_ReturnsTrue()
        {
            Assert.IsTrue(_normalizer.NeedsNormalization("Hello\nworld", "en"));
        }

        [Test]
        public void NeedsNormalization_ControlCharacters_ReturnsTrue()
        {
            Assert.IsTrue(_normalizer.NeedsNormalization("Hello\x00world", "en"));
        }

        [Test]
        public void NeedsNormalization_Japanese_FullWidth_ReturnsTrue()
        {
            Assert.IsTrue(_normalizer.NeedsNormalization("０１２３", "ja"));
            Assert.IsTrue(_normalizer.NeedsNormalization("ＡＢＣ", "ja"));
            Assert.IsTrue(_normalizer.NeedsNormalization("　", "ja"));
        }

        [Test]
        public void NeedsNormalization_English_Uppercase_ReturnsTrue()
        {
            Assert.IsTrue(_normalizer.NeedsNormalization("HELLO", "en"));
        }

        [Test]
        public void NeedsNormalization_English_Contractions_ReturnsTrue()
        {
            Assert.IsTrue(_normalizer.NeedsNormalization("can't", "en"));
        }

        [Test]
        public void NeedsNormalization_NormalText_ReturnsFalse()
        {
            Assert.IsFalse(_normalizer.NeedsNormalization("hello world", "en"));
            Assert.IsFalse(_normalizer.NeedsNormalization("こんにちは", "ja"));
        }

        #endregion

        #region Language Support Tests

        [Test]
        public void Normalize_UnsupportedLanguage_AppliesCommonNormalization()
        {
            var input = "Hello    world\ntest";
            var expected = "hello world test";

            Assert.AreEqual(expected, _normalizer.Normalize(input, "unknown"));
            Assert.AreEqual(expected, _normalizer.Normalize(input, null));
        }

        [Test]
        public void Normalize_Korean_AppliesCommonNormalization()
        {
            // Korean normalization is basic in current implementation
            var input = "안녕하세요    세계";
            var expected = "안녕하세요 세계";

            Assert.AreEqual(expected, _normalizer.Normalize(input, "ko"));
        }

        #endregion
    }
}