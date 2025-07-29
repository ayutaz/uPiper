using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace uPiper.Editor
{
    /// <summary>
    /// IL2CPP build settings configuration for uPiper
    /// </summary>
    public static class IL2CPPBuildSettings
    {
        [MenuItem("uPiper/Build/Configure IL2CPP Settings", false, 210)]
        public static void ConfigureIL2CPPSettings()
        {
            Debug.Log("Configuring IL2CPP settings for uPiper...");

            // Get NamedBuildTarget from current build target group
            var targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            var namedTarget = NamedBuildTarget.FromBuildTargetGroup(targetGroup);

            // Set scripting backend to IL2CPP
            PlayerSettings.SetScriptingBackend(namedTarget, ScriptingImplementation.IL2CPP);

            // Set API compatibility level to .NET Standard 2.1
            PlayerSettings.SetApiCompatibilityLevel(namedTarget, ApiCompatibilityLevel.NET_Standard_2_0);

            // Configure stripping level
            PlayerSettings.SetManagedStrippingLevel(namedTarget, ManagedStrippingLevel.Low);

            // Set IL2CPP compiler configuration
            PlayerSettings.SetIl2CppCompilerConfiguration(namedTarget, Il2CppCompilerConfiguration.Release);

            // Enable incremental GC for better performance
            PlayerSettings.gcIncremental = true;

            // Platform-specific settings
            ConfigurePlatformSpecificSettings();

            Debug.Log("IL2CPP settings configured successfully!");
            Debug.Log($"Scripting Backend: IL2CPP");
            Debug.Log($"API Compatibility: .NET Standard 2.0");
            Debug.Log($"Stripping Level: Low");
            Debug.Log($"Compiler Configuration: Release");
        }

        private static void ConfigurePlatformSpecificSettings()
        {
            var targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;

            switch (targetGroup)
            {
                case BuildTargetGroup.Android:
                    ConfigureAndroidSettings();
                    break;
                case BuildTargetGroup.iOS:
                    ConfigureiOSSettings();
                    break;
                case BuildTargetGroup.WebGL:
                    ConfigureWebGLSettings();
                    break;
                case BuildTargetGroup.Standalone:
                    ConfigureStandaloneSettings();
                    break;
            }
        }

        private static void ConfigureAndroidSettings()
        {
            // Target architectures
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;

            // Minimum API level for IL2CPP
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;

            Debug.Log("Android IL2CPP settings configured:");
            Debug.Log($"Target Architectures: ARM64, ARMv7");
            Debug.Log($"Minimum API Level: 21");
        }

        private static void ConfigureiOSSettings()
        {
            // Target architectures
            var namedTarget = NamedBuildTarget.FromBuildTargetGroup(BuildTargetGroup.iOS);
            PlayerSettings.SetArchitecture(namedTarget, 2); // Universal architecture

            // Minimum iOS version
            PlayerSettings.iOS.targetOSVersionString = "11.0";

            Debug.Log("iOS IL2CPP settings configured:");
            Debug.Log($"Architecture: Universal");
            Debug.Log($"Minimum iOS Version: 11.0");
        }

        private static void ConfigureWebGLSettings()
        {
            // WebGL specific settings
            PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
            PlayerSettings.WebGL.memorySize = 512; // MB

            Debug.Log("WebGL IL2CPP settings configured:");
            Debug.Log($"Linker Target: WebAssembly");
            Debug.Log($"Memory Size: 512 MB");
        }

        private static void ConfigureStandaloneSettings()
        {
            // Windows/Mac/Linux settings
            var architecture = BuildTarget.StandaloneWindows64;
            if (Application.platform == RuntimePlatform.OSXEditor)
            {
                architecture = BuildTarget.StandaloneOSX;
            }
            else if (Application.platform == RuntimePlatform.LinuxEditor)
            {
                architecture = BuildTarget.StandaloneLinux64;
            }

            Debug.Log($"Standalone IL2CPP settings configured for: {architecture}");
        }

        [MenuItem("uPiper/Build/Verify IL2CPP Configuration", false, 211)]
        public static void VerifyIL2CPPConfiguration()
        {
            var targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            var namedTarget = NamedBuildTarget.FromBuildTargetGroup(targetGroup);

            Debug.Log("=== IL2CPP Configuration Verification ===");
            Debug.Log($"Target Platform: {targetGroup}");
            Debug.Log($"Scripting Backend: {PlayerSettings.GetScriptingBackend(namedTarget)}");
            Debug.Log($"API Compatibility: {PlayerSettings.GetApiCompatibilityLevel(namedTarget)}");
            Debug.Log($"Stripping Level: {PlayerSettings.GetManagedStrippingLevel(namedTarget)}");
            Debug.Log($"IL2CPP Compiler: {PlayerSettings.GetIl2CppCompilerConfiguration(namedTarget)}");

            // Check if link.xml exists
            var linkXmlPath = Path.Combine(Application.dataPath, "uPiper", "link.xml");
            if (File.Exists(linkXmlPath))
            {
                Debug.Log("✓ link.xml found");
            }
            else
            {
                Debug.LogWarning("✗ link.xml not found!");
            }

            // Check native libraries
            CheckNativeLibraries();
        }

        private static void CheckNativeLibraries()
        {
            var nativeLibPaths = new[]
            {
                Path.Combine(Application.dataPath, "uPiper", "Plugins", "Windows", "x86_64", "openjtalk_wrapper.dll"),
                Path.Combine(Application.dataPath, "uPiper", "Plugins", "Linux", "x86_64", "libopenjtalk_wrapper.so"),
                Path.Combine(Application.dataPath, "uPiper", "Plugins", "macOS", "openjtalk_wrapper.dylib"),
                Path.Combine(Application.dataPath, "uPiper", "Plugins", "Android", "arm64-v8a", "libopenjtalk_wrapper.so"),
                Path.Combine(Application.dataPath, "uPiper", "Plugins", "Android", "armeabi-v7a", "libopenjtalk_wrapper.so"),
                Path.Combine(Application.dataPath, "uPiper", "Plugins", "iOS", "libopenjtalk_wrapper.a")
            };

            Debug.Log("Native Library Check:");
            foreach (var path in nativeLibPaths)
            {
                if (File.Exists(path))
                {
                    Debug.Log($"✓ {Path.GetFileName(path)} found at {Path.GetDirectoryName(path)}");
                }
                else
                {
                    var dir = Path.GetDirectoryName(path);
                    if (!Directory.Exists(dir))
                    {
                        Debug.Log($"- {Path.GetFileName(path)} (directory not present: {dir})");
                    }
                    else
                    {
                        Debug.LogWarning($"✗ {Path.GetFileName(path)} missing from {dir}");
                    }
                }
            }
        }
    }
}