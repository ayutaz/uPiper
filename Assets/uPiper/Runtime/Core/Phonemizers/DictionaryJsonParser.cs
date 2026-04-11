using System.Text.RegularExpressions;

namespace uPiper.Core.Phonemizers
{
    /// <summary>
    /// piper-plus互換辞書JSONの共有パースユーティリティ。
    /// CustomDictionary（Runtime）と DictionaryJsonEditor（Editor）の両方から使用。
    /// </summary>
    internal static class DictionaryJsonParser
    {
        /// <summary>
        /// 辞書エントリ解析用の正規表現パターン。
        /// "key": { "pronunciation": "value", "priority": N } または "key": "value" 形式に対応。
        /// </summary>
        internal static readonly Regex EntryPattern = new(
            @"""([^""]+)""\s*:\s*(?:\{\s*""pronunciation""\s*:\s*""([^""]*)""\s*" +
            @"(?:,\s*""priority""\s*:\s*(\d+))?\s*\}|""([^""]*)"")",
            RegexOptions.Singleline | RegexOptions.Compiled);

        /// <summary>
        /// JSON文字列から "entries" セクションの内容を抽出する（括弧のバランスを考慮）。
        /// </summary>
        /// <param name="json">辞書JSON文字列</param>
        /// <returns>entries オブジェクト内の文字列。見つからない場合は null。</returns>
        internal static string ExtractEntriesSection(string json)
        {
            // "entries" : { の位置を見つける
            var entriesIndex = json.IndexOf("\"entries\"", System.StringComparison.Ordinal);
            if (entriesIndex < 0) return null;

            // 開始括弧 { を見つける
            var openBraceIndex = json.IndexOf('{', entriesIndex);
            if (openBraceIndex < 0) return null;

            // 対応する閉じ括弧 } を見つける（括弧のネストを考慮）
            var braceCount = 1;
            var currentIndex = openBraceIndex + 1;

            while (currentIndex < json.Length && braceCount > 0)
            {
                var c = json[currentIndex];
                if (c == '{')
                    braceCount++;
                else if (c == '}')
                    braceCount--;
                currentIndex++;
            }

            if (braceCount != 0) return null;

            // 括弧の中身を返す（開始括弧の次から閉じ括弧の前まで）
            return json.Substring(openBraceIndex + 1, currentIndex - openBraceIndex - 2);
        }
    }
}