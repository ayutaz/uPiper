using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using uPiper.Core.Logging;
using UnityEngine;

namespace uPiper.Core.Phonemizers
{
    /// <summary>
    /// カスタム辞書クラス
    /// 技術用語や固有名詞の読みを管理し、テキスト前処理を行う
    /// piper-plusのPython実装と互換性のあるJSON形式をサポート
    /// </summary>
    public class CustomDictionary
    {
        /// <summary>
        /// 辞書エントリ
        /// </summary>
        public class DictionaryEntry
        {
            public string Pronunciation { get; set; }
            public int Priority { get; set; } = 5;
        }

        // 大文字小文字を区別しないエントリ（正規化済み）
        private readonly Dictionary<string, DictionaryEntry> _entries = new();

        // 大文字小文字を区別するエントリ
        private readonly Dictionary<string, DictionaryEntry> _caseSensitiveEntries = new();

        // コンパイル済み正規表現パターンのキャッシュ
        private readonly Dictionary<string, Regex> _patternCache = new();

        // StreamingAssetsのデフォルト辞書ディレクトリ
        private static string DefaultDictionaryPath =>
            Path.Combine(Application.streamingAssetsPath, "uPiper", "Dictionaries");

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="loadDefaults">デフォルト辞書を読み込むかどうか</param>
        public CustomDictionary(bool loadDefaults = true)
        {
            PiperLogger.LogInfo("[CustomDictionary] Initializing...");
            if (loadDefaults)
            {
                LoadDefaultDictionaries();
            }

            var stats = GetStats();
            PiperLogger.LogInfo($"[CustomDictionary] Initialized with {stats.TotalEntries} entries");
        }

        /// <summary>
        /// デフォルト辞書を読み込む
        /// StreamingAssets/uPiper/Dictionaries/ 内の全JSONファイルを自動読み込み
        /// </summary>
        public void LoadDefaultDictionaries()
        {
            var dictPath = DefaultDictionaryPath;
            PiperLogger.LogInfo($"[CustomDictionary] Dictionary path: {dictPath}");
            if (!Directory.Exists(dictPath))
            {
                PiperLogger.LogWarning($"[CustomDictionary] Dictionary directory not found: {dictPath}");
                return;
            }

            // 全ての.jsonファイルをファイル名順に読み込む
            var jsonFiles = Directory.GetFiles(dictPath, "*.json")
                .OrderBy(f => Path.GetFileName(f))
                .ToArray();

            foreach (var filePath in jsonFiles)
            {
                try
                {
                    LoadDictionaryFromPath(filePath);
                    PiperLogger.LogInfo($"[CustomDictionary] Loaded: {Path.GetFileName(filePath)}");
                }
                catch (Exception e)
                {
                    PiperLogger.LogWarning($"[CustomDictionary] Failed to load {filePath}: {e.Message}");
                }
            }

            var stats = GetStats();
            PiperLogger.LogInfo($"[CustomDictionary] Total entries: {stats.TotalEntries} " +
                               $"(case-insensitive: {stats.CaseInsensitiveEntries}, " +
                               $"case-sensitive: {stats.CaseSensitiveEntries})");
        }

        /// <summary>
        /// ファイルパスから辞書を読み込む
        /// </summary>
        public void LoadDictionaryFromPath(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Dictionary file not found: {filePath}");
            }

            var json = File.ReadAllText(filePath);
            LoadFromJson(json);
        }

        /// <summary>
        /// JSON文字列から辞書を読み込む
        /// </summary>
        public void LoadFromJson(string json)
        {
            var data = JsonUtility.FromJson<DictionaryJsonWrapper>(json);
            if (data == null)
            {
                // JsonUtilityが失敗した場合、手動パース
                ParseJsonManually(json);
                return;
            }

            // JsonUtilityはDictionary非対応のため手動パース
            ParseJsonManually(json);
        }

        /// <summary>
        /// JSONを手動でパース（Unity JsonUtilityの制限を回避）
        /// </summary>
        private void ParseJsonManually(string json)
        {
            // "entries" セクションを抽出（括弧のバランスを考慮）
            var entriesContent = ExtractEntriesSection(json);
            if (string.IsNullOrEmpty(entriesContent)) return;

            // 各エントリを解析
            // "key": { "pronunciation": "value", "priority": 9 } または "key": "value"
            var entryPattern = new Regex(
                @"""([^""]+)""\s*:\s*(?:\{\s*""pronunciation""\s*:\s*""([^""]+)""(?:\s*,\s*""priority""\s*:\s*(\d+))?\s*\}|""([^""]+)"")",
                RegexOptions.Singleline);

            foreach (Match match in entryPattern.Matches(entriesContent))
            {
                var word = match.Groups[1].Value;

                // コメント行をスキップ
                if (word.StartsWith("//")) continue;

                string pronunciation;
                var priority = 5;

                if (!string.IsNullOrEmpty(match.Groups[2].Value))
                {
                    // 詳細形式: { "pronunciation": "...", "priority": N }
                    pronunciation = match.Groups[2].Value;
                    if (!string.IsNullOrEmpty(match.Groups[3].Value))
                    {
                        int.TryParse(match.Groups[3].Value, out priority);
                    }
                }
                else
                {
                    // 簡易形式: "value"
                    pronunciation = match.Groups[4].Value;
                }

                AddEntry(word, new DictionaryEntry { Pronunciation = pronunciation, Priority = priority });
            }
        }

        /// <summary>
        /// JSONからentriesセクションの内容を抽出（括弧のバランスを考慮）
        /// </summary>
        private string ExtractEntriesSection(string json)
        {
            // "entries" : { の位置を見つける
            var entriesIndex = json.IndexOf("\"entries\"", StringComparison.Ordinal);
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

        /// <summary>
        /// エントリを追加
        /// </summary>
        private void AddEntry(string word, DictionaryEntry entry)
        {
            // 大文字小文字が混在している場合は区別する
            if (word != word.ToLower() && word != word.ToUpper())
            {
                _caseSensitiveEntries[word] = entry;
            }
            else
            {
                var normalizedWord = word.ToLower();

                // 既存エントリとの優先度比較
                if (_entries.TryGetValue(normalizedWord, out var existing))
                {
                    if (entry.Priority <= existing.Priority)
                    {
                        return; // 既存の方が優先度が高い
                    }
                }

                _entries[normalizedWord] = entry;
            }
        }

        /// <summary>
        /// テキストに辞書を適用して単語を置換
        /// </summary>
        /// <param name="text">入力テキスト</param>
        /// <returns>置換後のテキスト</returns>
        public string ApplyToText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var originalText = text;

            // まず大文字小文字を区別するエントリを処理（長い順）
            foreach (var kvp in _caseSensitiveEntries.OrderByDescending(x => x.Key.Length))
            {
                var pattern = GetWordPattern(kvp.Key, caseSensitive: true);
                text = pattern.Replace(text, kvp.Value.Pronunciation);
            }

            // 次に大文字小文字を区別しないエントリを処理（長い順）
            foreach (var kvp in _entries.OrderByDescending(x => x.Key.Length))
            {
                var pattern = GetWordPattern(kvp.Key, caseSensitive: false);
                text = pattern.Replace(text, kvp.Value.Pronunciation);
            }

            if (text != originalText)
            {
                PiperLogger.LogInfo($"[CustomDictionary] Text replaced: '{originalText}' -> '{text}'");
            }

            return text;
        }

        /// <summary>
        /// 単語の正規表現パターンを取得（キャッシュ付き）
        /// </summary>
        private Regex GetWordPattern(string word, bool caseSensitive)
        {
            var cacheKey = $"{word}_{caseSensitive}";
            if (_patternCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            var escapedWord = Regex.Escape(word);

            // 日本語を含むかチェック（ひらがな、カタカナ、漢字）
            var hasJapanese = word.Length > 0 && IsJapaneseChar(word[0]);
            if (!hasJapanese && word.Length > 1)
            {
                hasJapanese = word.Skip(1).Any(IsJapaneseChar);
            }

            string patternStr;
            if (hasJapanese)
            {
                // 日本語を含む場合はそのまま置換
                patternStr = escapedWord;
            }
            else
            {
                // 英語の場合: 前後が英数字でないことを確認
                // (?<![a-zA-Z0-9]) = 前が英数字でない
                // (?![a-zA-Z0-9]) = 後が英数字でない
                patternStr = @"(?<![a-zA-Z0-9])" + escapedWord + @"(?![a-zA-Z0-9])";
            }

            var options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
            var regex = new Regex(patternStr, options);
            _patternCache[cacheKey] = regex;

            return regex;
        }

        /// <summary>
        /// 単語の読みを取得
        /// </summary>
        public string GetPronunciation(string word)
        {
            // まず大文字小文字を区別してチェック
            if (_caseSensitiveEntries.TryGetValue(word, out var csEntry))
            {
                return csEntry.Pronunciation;
            }

            // 次に正規化してチェック
            if (_entries.TryGetValue(word.ToLower(), out var entry))
            {
                return entry.Pronunciation;
            }

            return null;
        }

        /// <summary>
        /// 単語を動的に追加
        /// </summary>
        public void AddWord(string word, string pronunciation, int priority = 5)
        {
            AddEntry(word, new DictionaryEntry { Pronunciation = pronunciation, Priority = priority });
            _patternCache.Clear(); // キャッシュをクリア
        }

        /// <summary>
        /// 単語を削除
        /// </summary>
        public bool RemoveWord(string word)
        {
            var removed = false;

            if (_caseSensitiveEntries.Remove(word))
            {
                removed = true;
            }

            if (_entries.Remove(word.ToLower()))
            {
                removed = true;
            }

            if (removed)
            {
                _patternCache.Clear();
            }

            return removed;
        }

        /// <summary>
        /// 統計情報
        /// </summary>
        public class DictionaryStats
        {
            public int TotalEntries { get; set; }
            public int CaseInsensitiveEntries { get; set; }
            public int CaseSensitiveEntries { get; set; }
        }

        /// <summary>
        /// 辞書の統計情報を取得
        /// </summary>
        public DictionaryStats GetStats()
        {
            return new DictionaryStats
            {
                TotalEntries = _entries.Count + _caseSensitiveEntries.Count,
                CaseInsensitiveEntries = _entries.Count,
                CaseSensitiveEntries = _caseSensitiveEntries.Count
            };
        }

        /// <summary>
        /// 日本語文字かどうかを判定
        /// ひらがな、カタカナ、CJK統合漢字をチェック
        /// </summary>
        private static bool IsJapaneseChar(char c)
        {
            // ひらがな (U+3040-U+309F), カタカナ (U+30A0-U+30FF), 漢字 (U+4E00-U+9FAF)
            return (c >= 0x3040 && c <= 0x309F) ||  // ひらがな
                   (c >= 0x30A0 && c <= 0x30FF) ||  // カタカナ
                   (c >= 0x4E00 && c <= 0x9FAF);    // 漢字 (CJK統合漢字)
        }

        /// <summary>
        /// パターンキャッシュをクリア
        /// </summary>
        public void ClearPatternCache()
        {
            _patternCache.Clear();
        }

        // JsonUtility用のラッパークラス（実際には使用しない）
        [Serializable]
        private class DictionaryJsonWrapper
        {
            public string version;
            public string description;
        }
    }
}