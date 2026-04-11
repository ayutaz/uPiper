using System.Collections.Generic;

namespace uPiper.Tests.Editor.TestHelpers
{
    /// <summary>
    /// テスト用PhonemeIdMapファクトリ。
    /// </summary>
    internal static class TestPhonemeIdMapFactory
    {
        /// <summary>特殊トークン(_,^,$)のみ。3エントリ。</summary>
        internal static Dictionary<string, int[]> CreateMinimal()
        {
            return new Dictionary<string, int[]>
            {
                { "_", new[] { 0 } },
                { "^", new[] { 1 } },
                { "$", new[] { 2 } },
            };
        }

        /// <summary>特殊トークン+基本英字音素。約20エントリ。</summary>
        internal static Dictionary<string, int[]> CreateValid()
        {
            return new Dictionary<string, int[]>
            {
                { "_", new[] { 0 } },
                { "^", new[] { 1 } },
                { "$", new[] { 2 } },
                { "a", new[] { 3 } },
                { "b", new[] { 4 } },
                { "c", new[] { 5 } },
                { "d", new[] { 6 } },
                { "e", new[] { 7 } },
                { "f", new[] { 8 } },
                { "g", new[] { 9 } },
                { "h", new[] { 10 } },
                { "i", new[] { 11 } },
                { "j", new[] { 12 } },
                { "k", new[] { 13 } },
                { "l", new[] { 14 } },
                { "m", new[] { 15 } },
                { "n", new[] { 16 } },
                { "o", new[] { 17 } },
                { "p", new[] { 18 } },
                { "r", new[] { 19 } },
                { "s", new[] { 20 } },
                { "t", new[] { 21 } },
            };
        }

        /// <summary>60エントリ。バリデーションの count >= 10 チェック通過用。</summary>
        internal static Dictionary<string, int[]> CreateFull()
        {
            var map = CreateValid();
            for (var i = map.Count; i < 60; i++)
            {
                map[$"p{i}"] = new[] { i };
            }
            return map;
        }
    }
}