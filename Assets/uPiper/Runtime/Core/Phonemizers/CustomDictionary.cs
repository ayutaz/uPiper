using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using uPiper.Core.Logging;
using uPiper.Core.Platform;
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
        /// 辞書ファイルの最大サイズ（10MB）
        /// </summary>
        internal const int MaxDictFileSize = 10 * 1024 * 1024; // 10 MB

        /// <summary>
        /// 辞書エントリの優先度レベル定数。
        /// </summary>
        public static class DictionaryPriority
        {
            /// <summary>低優先度（フォールバック）</summary>
            public const int Low = 3;
            /// <summary>標準優先度</summary>
            public const int Default = 5;
            /// <summary>高優先度</summary>
            public const int High = 7;
            /// <summary>オーバーライド（辞書間の上書き用）</summary>
            public const int Override = 9;
            /// <summary>最高優先度（常に適用）</summary>
            public const int Always = 10;
        }

        /// <summary>
        /// 辞書エントリ
        /// </summary>
        public class DictionaryEntry
        {
            public string Pronunciation { get; set; }
            public int Priority { get; set; } = DictionaryPriority.Default;
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
        /// デフォルト辞書を非同期で読み込む（WebGL対応）
        /// WebGLではディレクトリ列挙ができないため、既知の辞書ファイル名リストを使用する
        /// </summary>
        public async Task LoadDefaultDictionariesAsync(CancellationToken cancellationToken = default)
        {
            var dictionaryFiles = new[]
            {
                "additional_tech_dict.json",
                "default_common_dict.json",
                "default_tech_dict.json",
                "user_custom_dict.json"
            };

            foreach (var fileName in dictionaryFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relativePath = $"uPiper/Dictionaries/{fileName}";
                try
                {
                    var json = await WebGLStreamingAssetsLoader.LoadTextAsync(relativePath, cancellationToken);
                    LoadFromJson(json, fileName);
                    PiperLogger.LogInfo($"[CustomDictionary] Loaded: {fileName}");
                }
                catch (Exception e)
                {
                    PiperLogger.LogWarning($"[CustomDictionary] Failed to load {fileName}: {e.Message}");
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
            // パストラバーサル拒否（セグメント単位で ".." を検出、ファイル名に ".." を含む正当なパスを誤検知しない）
            var separators = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
            var segments = filePath.Split(separators, StringSplitOptions.RemoveEmptyEntries);
            if (Array.Exists(segments, s => s == ".."))
            {
                throw new ArgumentException(
                    $"Dictionary file path contains path traversal pattern: {filePath}",
                    nameof(filePath));
            }

            // ファイル存在・サイズチェック（FileInfo で統一）
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
            {
                throw new FileNotFoundException($"Dictionary file not found: {filePath}");
            }

            if (fileInfo.Length > MaxDictFileSize)
            {
                throw new ArgumentException(
                    $"Dictionary file too large: {fileInfo.Length} bytes exceeds {MaxDictFileSize} byte limit");
            }

            var json = File.ReadAllText(filePath);
            LoadFromJson(json, Path.GetFileName(filePath));
        }

        /// <summary>
        /// JSON文字列から辞書を読み込む
        /// </summary>
        /// <param name="json">辞書JSON文字列</param>
        /// <param name="sourceFileName">ログ出力用のソースファイル名（任意）</param>
        public void LoadFromJson(string json, string sourceFileName = null)
        {
            var source = sourceFileName ?? "(inline)";
            try
            {
                var data = JsonUtility.FromJson<DictionaryJsonWrapper>(json);
                if (data == null)
                {
                    // JsonUtilityが失敗した場合、手動パース
                    ParseJsonManually(json, source);
                    return;
                }
            }
            catch (Exception ex)
            {
                PiperLogger.LogWarning(
                    $"[CustomDictionary] JsonUtility parse failed for '{source}', " +
                    $"falling back to manual parse: {ex.Message}");
            }

            // JsonUtilityはDictionary非対応のため手動パース
            ParseJsonManually(json, source);
        }

        /// <summary>
        /// JSONを手動でパース（Unity JsonUtilityの制限を回避）
        /// </summary>
        /// <param name="json">辞書JSON文字列</param>
        /// <param name="source">ログ出力用のソース識別名</param>
        private void ParseJsonManually(string json, string source)
        {
            try
            {
                // "entries" セクションを抽出（括弧のバランスを考慮）
                var entriesContent = DictionaryJsonParser.ExtractEntriesSection(json);
                if (string.IsNullOrEmpty(entriesContent))
                {
                    PiperLogger.LogWarning(
                        $"[CustomDictionary] No 'entries' section found in '{source}'");
                    return;
                }

                var successCount = 0;
                var failCount = 0;

                // 各エントリを解析
                foreach (Match match in DictionaryJsonParser.EntryPattern.Matches(entriesContent))
                {
                    try
                    {
                        var word = match.Groups[1].Value;

                        // コメント行をスキップ
                        if (word.StartsWith("//")) continue;

                        string pronunciation;
                        var priority = DictionaryPriority.Default;

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
                        successCount++;
                    }
                    catch (Exception entryEx)
                    {
                        failCount++;
                        PiperLogger.LogWarning(
                            $"[CustomDictionary] Failed to parse entry in '{source}': {entryEx.Message}");
                    }
                }

                if (failCount > 0)
                {
                    PiperLogger.LogError(
                        $"[CustomDictionary] Partially loaded '{source}': " +
                        $"{successCount} succeeded, {failCount} failed");
                }
            }
            catch (Exception ex)
            {
                PiperLogger.LogError(
                    $"[CustomDictionary] Failed to parse dictionary '{source}': {ex.Message}");
            }
        }

        /// <summary>
        /// エントリを追加
        /// </summary>
        private void AddEntry(string word, DictionaryEntry entry)
        {
            // 大文字小文字が混在している場合は区別する
            if (word != word.ToLower() && word != word.ToUpper())
            {
                if (_caseSensitiveEntries.TryGetValue(word, out var existingCs))
                {
                    if (entry.Priority < existingCs.Priority)
                    {
                        PiperLogger.LogInfo(
                            $"[CustomDictionary] Skipping '{word}': existing entry has higher priority " +
                            $"({existingCs.Priority} > {entry.Priority})");
                        return;
                    }

                    PiperLogger.LogWarning(
                        $"[CustomDictionary] Overwriting '{word}': '{existingCs.Pronunciation}' " +
                        $"(priority={existingCs.Priority}) → '{entry.Pronunciation}' " +
                        $"(priority={entry.Priority})");
                }

                _caseSensitiveEntries[word] = entry;
            }
            else
            {
                var normalizedWord = word.ToLower();

                // 既存エントリとの優先度比較
                if (_entries.TryGetValue(normalizedWord, out var existing))
                {
                    if (entry.Priority < existing.Priority)
                    {
                        PiperLogger.LogInfo(
                            $"[CustomDictionary] Skipping '{word}': existing entry has higher priority " +
                            $"({existing.Priority} > {entry.Priority})");
                        return;
                    }

                    PiperLogger.LogWarning(
                        $"[CustomDictionary] Overwriting '{word}': '{existing.Pronunciation}' " +
                        $"(priority={existing.Priority}) → '{entry.Pronunciation}' " +
                        $"(priority={entry.Priority})");
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
        /// 置換詳細情報
        /// </summary>
        public readonly struct ReplacementDetail
        {
            public string OriginalWord { get; init; }
            public string Pronunciation { get; init; }
            public int Priority { get; init; }
            public int Position { get; init; }
        }

        /// <summary>
        /// テキストに辞書を適用し、置換詳細を返す（Editor プレビュー用）
        /// </summary>
        public (string resultText, IReadOnlyList<ReplacementDetail> replacements) ApplyToTextWithDetails(string text)
        {
            if (string.IsNullOrEmpty(text))
                return (text, Array.Empty<ReplacementDetail>());

            var replacements = new List<ReplacementDetail>();

            // まず大文字小文字を区別するエントリを処理（長い順）
            foreach (var kvp in _caseSensitiveEntries.OrderByDescending(x => x.Key.Length))
            {
                var pattern = GetWordPattern(kvp.Key, caseSensitive: true);
                foreach (Match m in pattern.Matches(text))
                {
                    replacements.Add(new ReplacementDetail
                    {
                        OriginalWord = m.Value,
                        Pronunciation = kvp.Value.Pronunciation,
                        Priority = kvp.Value.Priority,
                        Position = m.Index
                    });
                }

                text = pattern.Replace(text, kvp.Value.Pronunciation);
            }

            // 次に大文字小文字を区別しないエントリを処理（長い順）
            foreach (var kvp in _entries.OrderByDescending(x => x.Key.Length))
            {
                var pattern = GetWordPattern(kvp.Key, caseSensitive: false);
                foreach (Match m in pattern.Matches(text))
                {
                    replacements.Add(new ReplacementDetail
                    {
                        OriginalWord = m.Value,
                        Pronunciation = kvp.Value.Pronunciation,
                        Priority = kvp.Value.Priority,
                        Position = m.Index
                    });
                }

                text = pattern.Replace(text, kvp.Value.Pronunciation);
            }

            return (text, replacements);
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
        public void AddWord(string word, string pronunciation, int priority = DictionaryPriority.Default)
        {
            AddEntry(word, new DictionaryEntry { Pronunciation = pronunciation, Priority = priority });
            _patternCache.Clear(); // キャッシュをクリア
        }

        /// <summary>
        /// 複数エントリを一括追加する。パターンキャッシュの再構築は最後に1回だけ実行。
        /// </summary>
        public void AddWords(IEnumerable<(string word, string pronunciation, int priority)> entries)
        {
            var count = 0;
            foreach (var (word, pronunciation, priority) in entries)
            {
                AddEntry(word, new DictionaryEntry { Pronunciation = pronunciation, Priority = priority });
                count++;
            }

            if (count > 0)
            {
                _patternCache.Clear();
            }

            PiperLogger.LogInfo($"[CustomDictionary] Batch added {count} entries");
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