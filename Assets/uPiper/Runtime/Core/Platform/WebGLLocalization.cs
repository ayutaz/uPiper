using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace uPiper.Core.Platform
{
    /// <summary>
    /// WebGL環境でのブラウザ言語検出とUIメッセージのローカリゼーション。
    /// </summary>
    internal static class WebGLLocalization
    {
        private static readonly Dictionary<string, string> OverlayMessages = new()
        {
            { "ja", "音声合成の準備ができました\nクリックして開始" },
            { "en", "Text-to-speech is ready\nClick to start" },
            { "zh", "语音合成已准备就绪\n点击开始" },
            { "es", "La síntesis de voz está lista\nHaz clic para comenzar" },
            { "fr", "La synthèse vocale est prête\nCliquez pour commencer" },
            { "pt", "A síntese de voz está pronta\nClique para iniciar" },
            { "ko", "음성 합성이 준비되었습니다\n클릭하여 시작" },
        };

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern string WebGL_GetBrowserLanguage();
#endif

        /// <summary>
        /// ブラウザの言語設定を取得する。
        /// WebGL以外の環境では "en" を返す。
        /// </summary>
        internal static string GetBrowserLanguage()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                var lang = WebGL_GetBrowserLanguage();
                return string.IsNullOrEmpty(lang) ? "en" : lang;
            }
            catch
            {
                return "en";
            }
#else
            return "en";
#endif
        }

        /// <summary>
        /// ブラウザ言語に対応するオーバーレイメッセージを取得する。
        /// </summary>
        /// <param name="browserLanguage">navigator.language の値（例: "ja-JP", "en-US"）</param>
        /// <returns>ローカライズされたメッセージ。未対応言語は英語フォールバック</returns>
        internal static string GetOverlayMessage(string browserLanguage = null)
        {
            var lang = browserLanguage ?? GetBrowserLanguage();

            // Try exact match first (e.g., "ja" will match directly)
            if (OverlayMessages.TryGetValue(lang, out var message))
                return message;

            // Try primary language subtag (e.g., "ja-JP" → "ja")
            var hyphenIndex = lang.IndexOf('-');
            if (hyphenIndex > 0)
            {
                var primary = lang[..hyphenIndex];
                if (OverlayMessages.TryGetValue(primary, out message))
                    return message;
            }

            // Fallback to English
            return OverlayMessages["en"];
        }
    }
}