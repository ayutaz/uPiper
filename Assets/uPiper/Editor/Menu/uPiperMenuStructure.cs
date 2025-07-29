using UnityEditor;

namespace uPiper.Editor.Menu
{
    /// <summary>
    /// Defines the menu structure and separators for uPiper
    /// </summary>
    public static class uPiperMenuStructure
    {
        // Priority ranges for each section (with gaps for future items)
        public const int PRIORITY_DEMO = 100;      // 100-199
        public const int PRIORITY_BUILD = 200;     // 200-299
        public const int PRIORITY_TOOLS = 300;     // 300-399
        public const int PRIORITY_DEBUG = 400;     // 400-499
        public const int PRIORITY_ANDROID = 500;   // 500-599
        public const int PRIORITY_HELP = 600;      // 600-699

        // Menu separators - Unity requires unique paths for each separator
        [MenuItem("uPiper/ ", false, 99)]
        private static void Separator1() { }

        [MenuItem("uPiper/  ", false, 199)]
        private static void Separator2() { }

        [MenuItem("uPiper/   ", false, 299)]
        private static void Separator3() { }

        [MenuItem("uPiper/    ", false, 399)]
        private static void Separator4() { }

        [MenuItem("uPiper/     ", false, 499)]
        private static void Separator5() { }

        [MenuItem("uPiper/      ", false, 599)]
        private static void Separator6() { }

        // Help menu items
        [MenuItem("uPiper/Documentation", false, PRIORITY_HELP)]
        private static void OpenDocumentation()
        {
            UnityEngine.Application.OpenURL("https://github.com/ayutaz/uPiper#readme");
        }

        [MenuItem("uPiper/Report Issue", false, PRIORITY_HELP + 1)]
        private static void ReportIssue()
        {
            UnityEngine.Application.OpenURL("https://github.com/ayutaz/uPiper/issues");
        }

        [MenuItem("uPiper/About uPiper", false, PRIORITY_HELP + 10)]
        private static void ShowAbout()
        {
            EditorUtility.DisplayDialog(
                "About uPiper",
                "uPiper - Unity Piper TTS Plugin\n\n" +
                "Version: 1.10.0\n" +
                "License: MIT\n\n" +
                "High-quality text-to-speech synthesis using Piper TTS and Unity AI Inference Engine.",
                "OK"
            );
        }
    }
}