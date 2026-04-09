using NUnit.Framework;
using uPiper.Core.AudioGeneration;

namespace uPiper.Tests.Editor.AudioGeneration
{
    /// <summary>
    /// PhonemeEncoder.FlattenProsody の直接ユニットテスト。
    /// internal static メソッドのため InternalsVisibleTo 経由でアクセスする。
    /// </summary>
    [TestFixture]
    public class FlattenProsodyTests
    {
        [Test]
        public void FlattenProsody_NormalArrays_ReturnsInterleavedStride3()
        {
            // Arrange: 3音素分のA1/A2/A3
            var a1 = new[] { 1, 2, 3 };
            var a2 = new[] { 4, 5, 6 };
            var a3 = new[] { 7, 8, 9 };

            // Act
            var flat = PhonemeEncoder.FlattenProsody(a1, a2, a3, 3);

            // Assert: stride=3 で [a1_0, a2_0, a3_0, a1_1, a2_1, a3_1, a1_2, a2_2, a3_2]
            Assert.AreEqual(9, flat.Length, "Length should be phonemeCount * 3");
            Assert.AreEqual(1, flat[0], "flat[0] = a1[0]");
            Assert.AreEqual(4, flat[1], "flat[1] = a2[0]");
            Assert.AreEqual(7, flat[2], "flat[2] = a3[0]");
            Assert.AreEqual(2, flat[3], "flat[3] = a1[1]");
            Assert.AreEqual(5, flat[4], "flat[4] = a2[1]");
            Assert.AreEqual(8, flat[5], "flat[5] = a3[1]");
            Assert.AreEqual(3, flat[6], "flat[6] = a1[2]");
            Assert.AreEqual(6, flat[7], "flat[7] = a2[2]");
            Assert.AreEqual(9, flat[8], "flat[8] = a3[2]");
        }

        [Test]
        public void FlattenProsody_EmptyArrays_ReturnsEmptyArray()
        {
            // Arrange: 全て空、phonemeCount=0
            var a1 = new int[0];
            var a2 = new int[0];
            var a3 = new int[0];

            // Act
            var flat = PhonemeEncoder.FlattenProsody(a1, a2, a3, 0);

            // Assert
            Assert.IsNotNull(flat);
            Assert.AreEqual(0, flat.Length, "Empty arrays with phonemeCount=0 should return empty");
        }

        [Test]
        public void FlattenProsody_ShorterArrays_ZeroFillsMissingElements()
        {
            // Arrange: a1 has 2 elements but phonemeCount=3 — third element should be 0
            var a1 = new[] { 10, 20 };
            var a2 = new[] { 30 };
            var a3 = new[] { 40, 50, 60 };

            // Act
            var flat = PhonemeEncoder.FlattenProsody(a1, a2, a3, 3);

            // Assert
            Assert.AreEqual(9, flat.Length);
            // Index 0: a1[0]=10, a2[0]=30, a3[0]=40
            Assert.AreEqual(10, flat[0]);
            Assert.AreEqual(30, flat[1]);
            Assert.AreEqual(40, flat[2]);
            // Index 1: a1[1]=20, a2[1]=0 (out of bounds), a3[1]=50
            Assert.AreEqual(20, flat[3]);
            Assert.AreEqual(0, flat[4], "a2[1] out of bounds should be 0");
            Assert.AreEqual(50, flat[5]);
            // Index 2: a1[2]=0 (out of bounds), a2[2]=0, a3[2]=60
            Assert.AreEqual(0, flat[6], "a1[2] out of bounds should be 0");
            Assert.AreEqual(0, flat[7], "a2[2] out of bounds should be 0");
            Assert.AreEqual(60, flat[8]);
        }

        [Test]
        public void FlattenProsody_PhonemeCountZero_ReturnsEmptyArray()
        {
            // Arrange: non-empty arrays but phonemeCount=0
            var a1 = new[] { 1, 2, 3 };
            var a2 = new[] { 4, 5, 6 };
            var a3 = new[] { 7, 8, 9 };

            // Act
            var flat = PhonemeEncoder.FlattenProsody(a1, a2, a3, 0);

            // Assert
            Assert.IsNotNull(flat);
            Assert.AreEqual(0, flat.Length,
                "phonemeCount=0 should return empty array regardless of input lengths");
        }
    }
}