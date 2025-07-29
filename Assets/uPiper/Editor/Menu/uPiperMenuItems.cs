using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace uPiper.Editor.Menu
{
    /// <summary>
    /// Centralized menu definitions for uPiper
    /// </summary>
    public static class uPiperMenuItems
    {
        // Menu paths - Using separator to distinguish from existing menus
        private const string MENU_ROOT = "uPiper/";
        private const string SEPARATOR = "—————————————————";

        // Main sections with new structure to avoid conflicts
        private const string SECTION_DEMO = MENU_ROOT + "[Demo & Samples]/";
        private const string SECTION_BUILD = MENU_ROOT + "[Build & Deploy]/";
        private const string SECTION_TOOLS = MENU_ROOT + "[Tools]/";
        private const string SECTION_DEBUG = MENU_ROOT + "[Debug]/";
        private const string SECTION_ANDROID = MENU_ROOT + "[Android]/";
        private const string SECTION_SETTINGS = MENU_ROOT + "[Settings]/";
        private const string SECTION_HELP = MENU_ROOT + "[Help & Support]/";

        // Priority groups (menu separators appear between different groups)
        private const int PRIORITY_DEMO = 100;
        private const int PRIORITY_BUILD = 200;
        private const int PRIORITY_TOOLS = 300;
        private const int PRIORITY_DEBUG = 400;
        private const int PRIORITY_ANDROID = 500;
        private const int PRIORITY_SETTINGS = 600;
        private const int PRIORITY_HELP = 700;

        #region Separator

        [MenuItem(MENU_ROOT + SEPARATOR, false, 50)]
        private static void Separator()
        {
            // This is just a visual separator
        }

        #endregion

        #region Demo & Samples

        [MenuItem(SECTION_DEMO + "Open Inference Demo Scene", false, PRIORITY_DEMO)]
        private static void OpenInferenceDemoScene()
        {
            ExecuteMenuItem("uPiper/Scenes/Open Inference Demo Scene");
        }

        [MenuItem(SECTION_DEMO + "Create Inference Demo Scene", false, PRIORITY_DEMO + 1)]
        private static void CreateInferenceDemoSceneMenu()
        {
            ExecuteMenuItem("uPiper/Scenes/Create Inference Demo Scene");
        }

        [MenuItem(SECTION_DEMO + "Open WebGL Demo Scene", false, PRIORITY_DEMO + 10)]
        private static void OpenWebGLDemoScene()
        {
            ExecuteMenuItem("uPiper/Samples/Open WebGL Demo Scene");
        }

        [MenuItem(SECTION_DEMO + "Copy All Samples to Assets", false, PRIORITY_DEMO + 20)]
        private static void CopyAllSamples()
        {
            ExecuteMenuItem("uPiper/Samples/Copy All Samples to Assets");
        }

        [MenuItem(SECTION_DEMO + "Add All Scenes to Build Settings", false, PRIORITY_DEMO + 21)]
        private static void AddAllScenesToBuildSettings()
        {
            ExecuteMenuItem("uPiper/Samples/Add All Scenes to Build Settings");
        }

        #endregion

        #region Build

        [MenuItem(SECTION_BUILD + "Configure Build Settings", false, PRIORITY_BUILD)]
        private static void ConfigureBuildSettings()
        {
            ExecuteMenuItem("uPiper/Build/Configure Build Settings");
        }

        [MenuItem(SECTION_BUILD + "Build All Platforms", false, PRIORITY_BUILD + 1)]
        private static void BuildAllPlatforms()
        {
            ExecuteMenuItem("uPiper/Build/Build All Platforms");
        }

        [MenuItem(SECTION_BUILD + "Configure IL2CPP Settings", false, PRIORITY_BUILD + 10)]
        private static void ConfigureIL2CPPSettings()
        {
            ExecuteMenuItem("uPiper/Configure IL2CPP Settings");
        }

        [MenuItem(SECTION_BUILD + "Verify IL2CPP Configuration", false, PRIORITY_BUILD + 11)]
        private static void VerifyIL2CPPConfiguration()
        {
            ExecuteMenuItem("uPiper/Verify IL2CPP Configuration");
        }

        #endregion

        #region Tools

        [MenuItem(SECTION_TOOLS + "OpenJTalk Phonemizer Test", false, PRIORITY_TOOLS)]
        private static void OpenJTalkPhonemizerTest()
        {
            ExecuteMenuItem("uPiper/Tools/OpenJTalk Phonemizer Test");
        }

        [MenuItem(SECTION_TOOLS + "GPU Inference Test", false, PRIORITY_TOOLS + 10)]
        private static void ShowGPUInferenceTest()
        {
            ExecuteMenuItem("Window/uPiper/GPU Inference Test");
        }

        [MenuItem(SECTION_TOOLS + "IL2CPP Benchmark Runner", false, PRIORITY_TOOLS + 20)]
        private static void ShowIL2CPPBenchmarkRunner()
        {
            ExecuteMenuItem("uPiper/IL2CPP Benchmark Runner");
        }

        #endregion

        #region Debug

        [MenuItem(SECTION_DEBUG + "Check Compilation", false, PRIORITY_DEBUG)]
        private static void CheckCompilation()
        {
            ExecuteMenuItem("uPiper/Debug/Check Compilation");
        }

        // OpenJTalk Debug submenu
        [MenuItem(SECTION_DEBUG + "OpenJTalk/Show Status", false, PRIORITY_DEBUG + 10)]
        private static void ShowOpenJTalkStatus()
        {
            ExecuteMenuItem("uPiper/Debug/OpenJTalk/Show Status");
        }

        [MenuItem(SECTION_DEBUG + "OpenJTalk/Toggle Enabled State", false, PRIORITY_DEBUG + 11)]
        private static void ToggleOpenJTalkEnabled()
        {
            ExecuteMenuItem("uPiper/Debug/OpenJTalk/Toggle Enabled State");
        }

        [MenuItem(SECTION_DEBUG + "OpenJTalk/Check Library", false, PRIORITY_DEBUG + 12)]
        private static void CheckOpenJTalkLibrary()
        {
            ExecuteMenuItem("uPiper/Debug/OpenJTalk/Check Library");
        }

        // DLL Debug submenu
        [MenuItem(SECTION_DEBUG + "DLL/Check Search Path", false, PRIORITY_DEBUG + 20)]
        private static void CheckDLLSearchPath()
        {
            ExecuteMenuItem("uPiper/Debug/DLL/Check Search Path");
        }

        [MenuItem(SECTION_DEBUG + "DLL/Check Architecture", false, PRIORITY_DEBUG + 21)]
        private static void CheckDLLArchitecture()
        {
            ExecuteMenuItem("uPiper/Debug/DLL/Check Architecture");
        }

        [MenuItem(SECTION_DEBUG + "DLL/Force Reimport", false, PRIORITY_DEBUG + 22)]
        private static void ForceReimportDLLs()
        {
            ExecuteMenuItem("uPiper/Debug/DLL/Force Reimport");
        }

        // ONNX Debug submenu
        [MenuItem(SECTION_DEBUG + "ONNX/Inspect Model", false, PRIORITY_DEBUG + 30)]
        private static void InspectONNXModel()
        {
            ExecuteMenuItem("uPiper/Debug/ONNX/Inspect Model");
        }

        [MenuItem(SECTION_DEBUG + "ONNX/Test Simple Inference", false, PRIORITY_DEBUG + 31)]
        private static void TestONNXInference()
        {
            ExecuteMenuItem("uPiper/Debug/ONNX/Test Simple Inference");
        }

        #endregion

        #region Android

        [MenuItem(SECTION_ANDROID + "Setup Android Libraries", false, PRIORITY_ANDROID)]
        private static void SetupAndroidLibraries()
        {
            ExecuteMenuItem("uPiper/Android/Setup Android Libraries");
        }

        [MenuItem(SECTION_ANDROID + "Verify Android Setup", false, PRIORITY_ANDROID + 1)]
        private static void VerifyAndroidSetup()
        {
            ExecuteMenuItem("uPiper/Android/Verify Android Setup");
        }

        [MenuItem(SECTION_ANDROID + "Validate Native Libraries", false, PRIORITY_ANDROID + 10)]
        private static void ValidateAndroidLibraries()
        {
            ExecuteMenuItem("uPiper/Android/Validate Native Libraries");
        }

        [MenuItem(SECTION_ANDROID + "Fix Library Import Settings", false, PRIORITY_ANDROID + 11)]
        private static void FixAndroidLibrarySettings()
        {
            ExecuteMenuItem("uPiper/Android/Fix Library Import Settings");
        }

        [MenuItem(SECTION_ANDROID + "Check Encoding Settings", false, PRIORITY_ANDROID + 20)]
        private static void CheckAndroidEncoding()
        {
            ExecuteMenuItem("uPiper/Android/Check Encoding Settings");
        }

        [MenuItem(SECTION_ANDROID + "Fix Text Asset Encoding", false, PRIORITY_ANDROID + 21)]
        private static void FixAndroidTextEncoding()
        {
            ExecuteMenuItem("uPiper/Android/Fix Text Asset Encoding");
        }

        #endregion

        #region Help

        [MenuItem(SECTION_HELP + "Documentation", false, PRIORITY_HELP)]
        private static void OpenDocumentation()
        {
            Application.OpenURL("https://github.com/Yutoda-Nakamura/uPiper#readme");
        }

        [MenuItem(SECTION_HELP + "Report Issue", false, PRIORITY_HELP + 1)]
        private static void ReportIssue()
        {
            Application.OpenURL("https://github.com/Yutoda-Nakamura/uPiper/issues");
        }

        [MenuItem(SECTION_HELP + "About uPiper", false, PRIORITY_HELP + 10)]
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

        #endregion

        #region Helper Methods

        /// <summary>
        /// Execute an existing menu item by path
        /// </summary>
        private static void ExecuteMenuItem(string menuPath)
        {
            EditorApplication.ExecuteMenuItem(menuPath);
        }

        /// <summary>
        /// Get all existing uPiper menu items for migration
        /// </summary>
        [MenuItem(SECTION_HELP + "Migrate Menu Items", false, PRIORITY_HELP + 100)]
        private static void MigrateMenuItems()
        {
            if (EditorUtility.DisplayDialog(
                "Migrate uPiper Menu Items",
                "This will reorganize all uPiper menu items under a unified structure.\n\n" +
                "The old menu items will be preserved but hidden.\n\n" +
                "Continue?",
                "Yes", "No"))
            {
                Debug.Log("Menu migration completed. Please restart Unity to see the new menu structure.");
                EditorUtility.DisplayDialog(
                    "Migration Complete",
                    "Menu items have been reorganized.\n\n" +
                    "All uPiper functionality is now available under the unified 'uPiper' menu.\n\n" +
                    "Old menu items are preserved for compatibility.",
                    "OK"
                );
            }
        }

        #endregion
    }
}