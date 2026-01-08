using System.Collections.Generic;

namespace uPiper.Demo
{
    /// <summary>
    /// Static class containing test phrases and model configuration for the demo.
    /// </summary>
    public static class DemoTestData
    {
        #region Model Configuration

        /// <summary>
        /// Available model names.
        /// </summary>
        public static readonly string[] ModelNames = { "tsukuyomi-chan" };

        /// <summary>
        /// Model name to language code mapping.
        /// </summary>
        public static readonly Dictionary<string, string> ModelLanguages = new()
        {
            { "tsukuyomi-chan", "ja" }
        };

        #endregion

        #region Japanese Test Phrases

        /// <summary>
        /// Japanese test phrases for the demo UI.
        /// </summary>
        public static List<string> JapaneseTestPhrases => new()
        {
            "\u81ea\u7531\u5165\u529b",  // 自由入力 (Custom input)
            "\u3053\u3093\u306b\u3061\u306f",  // こんにちは
            "\u3053\u3093\u306b\u3061\u306f\u3001\u4e16\u754c\uff01",  // こんにちは、世界！
            "\u3042\u308a\u304c\u3068\u3046\u3054\u3056\u3044\u307e\u3059",  // ありがとうございます
            "\u65e5\u672c\u306e\u65e5\u672c\u6a4b\u306e\u4e0a\u3067\u7b94\u3092\u4f7f\u3063\u3066\u3054\u98ef\u3092\u98df\u3079\u308b",  // 日本の日本橋の上で箸を使ってご飯を食べる
            "\u79c1\u306f\u6771\u4eac\u306b\u4f4f\u3093\u3067\u3044\u307e\u3059",  // 私は東京に住んでいます
            "\u4eca\u65e5\u306f\u3044\u3044\u5929\u6c17\u3067\u3059\u306d",  // 今日はいい天気ですね
            "\u97f3\u58f0\u5408\u6210\u306e\u30c6\u30b9\u30c8\u3067\u3059",  // 音声合成のテストです
            "\u30e6\u30cb\u30c6\u30a3\u3067\u65e5\u672c\u8a9e\u97f3\u58f0\u5408\u6210\u304c\u3067\u304d\u307e\u3057\u305f",  // ユニティで日本語音声合成ができました
            "\u304a\u306f\u3088\u3046\u3054\u3056\u3044\u307e\u3059\u3001\u4eca\u65e5\u3082\u4e00\u65e5\u9811\u5f35\u308a\u307e\u3057\u3087\u3046",  // おはようございます、今日も一日頑張りましょう
            "\u3059\u307f\u307e\u305b\u3093\u3001\u3061\u3087\u3063\u3068\u304a\u805e\u304d\u3057\u305f\u3044\u3053\u3068\u304c\u3042\u308a\u307e\u3059",  // すみません、ちょっとお聞きしたいことがあります
            // アルファベット・英単語を含むテスト (カスタム辞書で発音変換)
            "Docker\u3068GitHub\u3092\u4f7f\u3063\u305f\u958b\u767a",  // DockerとGitHubを使った開発
            "Python\u3067AI\u30e2\u30c7\u30eb\u3092\u4f5c\u6210\u3059\u308b",  // PythonでAIモデルを作成する
            "AWS\u3068Azure\u306e\u30af\u30e9\u30a6\u30c9\u6bd4\u8f03",  // AWSとAzureのクラウド比較
            "ChatGPT\u3068Claude\u306e\u9055\u3044",  // ChatGPTとClaudeの違い
            "Unity\u3067VITS\u30e2\u30c7\u30eb\u3092\u5b9f\u884c"  // UnityでVITSモデルを実行
        };

        /// <summary>
        /// Default Japanese text for the input field.
        /// </summary>
        public static string DefaultJapaneseText => "\u3053\u3093\u306b\u3061\u306f";  // こんにちは

        #endregion

        #region English Test Phrases

        /// <summary>
        /// English test phrases for the demo UI.
        /// </summary>
        public static List<string> EnglishTestPhrases => new()
        {
            "Custom Input",
            "Hello world",
            "Welcome to Unity",
            "This is a test of the text to speech system",
            "The quick brown fox jumps over the lazy dog",
            "How are you doing today?",
            "Unity Inference Engine is amazing",
            "Can you hear me clearly?",
            "Let's test the voice synthesis"
        };

        /// <summary>
        /// Default English text for the input field.
        /// </summary>
        public static string DefaultEnglishText => "Hello world";

        #endregion

        #region Helper Methods

        /// <summary>
        /// Get the model name for a given dropdown index.
        /// </summary>
        public static string GetModelName(int index)
        {
            return index >= 0 && index < ModelNames.Length ? ModelNames[index] : ModelNames[0];
        }

        /// <summary>
        /// Get the language code for a given model name.
        /// </summary>
        public static string GetLanguage(string modelName)
        {
            return ModelLanguages.TryGetValue(modelName, out var language) ? language : "ja";
        }

        /// <summary>
        /// Get the test phrases for a given language.
        /// </summary>
        public static List<string> GetTestPhrases(string language)
        {
            return language == "ja" ? JapaneseTestPhrases : EnglishTestPhrases;
        }

        /// <summary>
        /// Get the default text for a given language.
        /// </summary>
        public static string GetDefaultText(string language)
        {
            return language == "ja" ? DefaultJapaneseText : DefaultEnglishText;
        }

        #endregion
    }
}
