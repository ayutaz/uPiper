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

        // Note: Unity menu separators are problematic with caching
        // Using priority values with gaps instead of visual separators

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

        // About menu removed - version information available in package.json
    }
}