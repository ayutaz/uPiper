using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using uPiper.Core.Phonemizers;

namespace uPiper.Editor.DictionaryManager
{
    /// <summary>
    /// Editor専用の辞書JSONファイル読み書きヘルパー。
    /// Newtonsoft.Jsonを使わず手動パース（CustomDictionaryの既存パターンに合わせる）。
    /// </summary>
    internal static class DictionaryJsonEditor
    {
        /// <summary>
        /// 辞書ファイルの最大サイズ（10MB）
        /// </summary>
        private const int MaxDictFileSize = 10 * 1024 * 1024;

        /// <summary>
        /// 全エントリ読み込み（コメント行スキップ）
        /// </summary>
        public static Dictionary<string, (string pronunciation, int priority)> ReadEntries(string filePath)
        {
            ValidateFilePath(filePath);

            var result = new Dictionary<string, (string pronunciation, int priority)>();

            if (!File.Exists(filePath))
                return result;

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > MaxDictFileSize)
            {
                throw new ArgumentException(
                    $"Dictionary file too large: {fileInfo.Length} bytes exceeds {MaxDictFileSize} byte limit");
            }

            var json = File.ReadAllText(filePath, Encoding.UTF8);
            var entriesContent = DictionaryJsonParser.ExtractEntriesSection(json);
            if (string.IsNullOrEmpty(entriesContent))
                return result;

            foreach (Match match in DictionaryJsonParser.EntryPattern.Matches(entriesContent))
            {
                var word = match.Groups[1].Value;

                // コメントキーをスキップ
                if (word.StartsWith("//"))
                    continue;

                string pronunciation;
                var priority = 5;

                if (!string.IsNullOrEmpty(match.Groups[2].Value))
                {
                    pronunciation = match.Groups[2].Value;
                    if (!string.IsNullOrEmpty(match.Groups[3].Value))
                    {
                        int.TryParse(match.Groups[3].Value, out priority);
                    }
                }
                else
                {
                    pronunciation = match.Groups[4].Value;
                }

                result[word] = (pronunciation, priority);
            }

            return result;
        }

        /// <summary>
        /// エントリ追加/更新
        /// </summary>
        public static void UpsertEntry(string filePath, string word, string pronunciation, int priority)
        {
            ValidateFilePath(filePath);

            var entries = File.Exists(filePath) ? ReadEntries(filePath) : new Dictionary<string, (string, int)>();
            entries[word] = (pronunciation, priority);
            ExportToJson(filePath, entries);
        }

        /// <summary>
        /// エントリ削除
        /// </summary>
        public static bool RemoveEntry(string filePath, string word)
        {
            ValidateFilePath(filePath);

            if (!File.Exists(filePath))
                return false;

            var entries = ReadEntries(filePath);
            if (!entries.Remove(word))
                return false;

            ExportToJson(filePath, entries);
            return true;
        }

        /// <summary>
        /// piper-plus互換JSON出力
        /// </summary>
        public static void ExportToJson(
            string filePath,
            IReadOnlyDictionary<string, (string pronunciation, int priority)> entries)
        {
            ValidateFilePath(filePath);

            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"version\": \"2.0\",");
            sb.AppendLine("  \"entries\": {");

            var isFirst = true;
            foreach (var kvp in entries)
            {
                if (!isFirst)
                    sb.AppendLine(",");
                isFirst = false;

                var escapedWord = EscapeJsonString(kvp.Key);
                var escapedPronunciation = EscapeJsonString(kvp.Value.pronunciation);
                sb.Append($"    \"{escapedWord}\": {{\"pronunciation\": \"{escapedPronunciation}\", " +
                          $"\"priority\": {kvp.Value.priority}}}");
            }

            if (!isFirst)
                sb.AppendLine();

            sb.AppendLine("  }");
            sb.Append("}");

            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(false));
        }

        /// <summary>
        /// ファイルパスバリデーション（パストラバーサル拒否）
        /// </summary>
        private static void ValidateFilePath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path must not be null or empty.", nameof(filePath));

            var separators = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
            var segments = filePath.Split(separators, StringSplitOptions.RemoveEmptyEntries);
            if (Array.Exists(segments, s => s == ".."))
            {
                throw new ArgumentException(
                    $"File path contains path traversal pattern: {filePath}",
                    nameof(filePath));
            }
        }

        /// <summary>
        /// JSON文字列エスケープ
        /// </summary>
        private static string EscapeJsonString(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            var sb = new StringBuilder(value.Length);
            foreach (var c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }

            return sb.ToString();
        }
    }
}