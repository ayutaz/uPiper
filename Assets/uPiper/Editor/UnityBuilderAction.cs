using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace UnityBuilderAction
{
    public static class BuildScript
    {
        private static readonly string[] Scenes = FindEnabledEditorScenes();

        public static void Build()
        {
            // Get build target from environment or command line
            var buildTarget = GetBuildTarget();
            var namedBuildTarget = GetNamedBuildTarget(buildTarget);

            // Get scripting backend from custom parameters
            var scriptingBackend = GetScriptingBackend();

            // Log build configuration
            Debug.Log($"=== uPiper Build Configuration ===");
            Debug.Log($"Target: {buildTarget}");
            Debug.Log($"Scripting Backend: {scriptingBackend}");
            Debug.Log($"Scenes: {string.Join(", ", Scenes)}");

            // Configure player settings based on scripting backend
            if (scriptingBackend.Contains("IL2CPP"))
            {
                Debug.Log("Configuring IL2CPP build...");
                PlayerSettings.SetScriptingBackend(namedBuildTarget, ScriptingImplementation.IL2CPP);

                // Configure IL2CPP settings
                PlayerSettings.SetIl2CppCompilerConfiguration(namedBuildTarget, Il2CppCompilerConfiguration.Release);
                PlayerSettings.SetIl2CppCodeGeneration(namedBuildTarget, Il2CppCodeGeneration.OptimizeSpeed);

                // Platform-specific IL2CPP optimizations
                if (buildTarget == BuildTarget.WebGL)
                {
                    PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
                    PlayerSettings.WebGL.dataCaching = false;
                }

                // Platform specific IL2CPP settings
                ConfigurePlatformSpecificIL2CPP(buildTarget, namedBuildTarget);
            }
            else
            {
                Debug.Log("Configuring Mono build...");
                PlayerSettings.SetScriptingBackend(namedBuildTarget, ScriptingImplementation.Mono2x);
            }

            // Get build location
            var buildLocation = GetBuildLocation(buildTarget, scriptingBackend);

            Debug.Log($"Building for {buildTarget} with {scriptingBackend} backend...");
            Debug.Log($"Build location: {buildLocation}");

            // Configure build options
            var buildOptions = BuildOptions.None;
            if (Debug.isDebugBuild)
            {
                buildOptions |= BuildOptions.Development;
            }

            // Perform build
            var buildReport = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = buildLocation,
                target = buildTarget,
                options = buildOptions
            });

            // Check build result
            if (buildReport.summary.result != BuildResult.Succeeded)
            {
                throw new Exception($"Build failed with {buildReport.summary.totalErrors} errors");
            }

            Debug.Log($"Build succeeded: {buildLocation}");
        }

        private static void ConfigurePlatformSpecificIL2CPP(BuildTarget target, NamedBuildTarget namedTarget)
        {
            switch (target)
            {
                case BuildTarget.Android:
                    // Android IL2CPP specific settings
                    PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
                    break;

                case BuildTarget.iOS:
                    // iOS IL2CPP specific settings
                    PlayerSettings.iOS.scriptCallOptimization = ScriptCallOptimizationLevel.FastButNoExceptions;
                    break;

                case BuildTarget.StandaloneWindows64:
                case BuildTarget.StandaloneLinux64:
                case BuildTarget.StandaloneOSX:
                    // Desktop IL2CPP settings
                    PlayerSettings.SetApiCompatibilityLevel(namedTarget, ApiCompatibilityLevel.NET_Standard_2_1);
                    break;

                case BuildTarget.WebGL:
                    // WebGL specific IL2CPP settings
                    PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
                    PlayerSettings.WebGL.memorySize = 512;
                    break;
            }
        }

        private static string GetBuildLocation(BuildTarget target, string scriptingBackend)
        {
            // Try to get from command line arguments first
            var buildPath = GetArgument(Environment.GetCommandLineArgs(), "-customBuildPath", "");
            bool isFromCommandLine = !string.IsNullOrEmpty(buildPath);

            // If not found in arguments, try environment variable (Unity Builder v4 compatibility)
            if (!isFromCommandLine)
            {
                buildPath = Environment.GetEnvironmentVariable("BUILD_PATH");
                if (!string.IsNullOrEmpty(buildPath))
                {
                    Debug.Log($"Using BUILD_PATH from environment variable: {buildPath}");
                }
            }

            // If still not found, throw exception as in original
            if (string.IsNullOrEmpty(buildPath))
            {
                throw new Exception("customBuildPath not specified");
            }

            // Get build name - first try command line, then environment variables
            var buildName = GetArgument(Environment.GetCommandLineArgs(), "-customBuildName", "");
            if (string.IsNullOrEmpty(buildName))
            {
                // Try BUILD_FILE first (Unity Builder might set this)
                buildName = Environment.GetEnvironmentVariable("BUILD_FILE");
                if (!string.IsNullOrEmpty(buildName))
                {
                    buildName = Path.GetFileNameWithoutExtension(buildName);
                }
                else
                {
                    // Fallback to BUILD_NAME
                    buildName = Environment.GetEnvironmentVariable("BUILD_NAME");
                }
            }
            
            // Default if still not found
            if (string.IsNullOrEmpty(buildName))
            {
                buildName = "uPiper";
            }

            // Append scripting backend to build name if not already included
            if (!buildName.Contains(scriptingBackend))
            {
                buildName = $"{buildName}-{scriptingBackend}";
            }

            // If from command line, buildPath already contains the base file name
            // We need to append the build name with scripting backend
            if (isFromCommandLine)
            {
                // buildPath is like: /path/to/build/StandaloneWindows64/uPiper.exe
                // or /path/to/build/StandaloneLinux64/uPiper
                // We need to create: /path/to/build/StandaloneWindows64/uPiper.exe/uPiper-Mono2x.exe
                
                // For all platforms, we append directory and then the full name with extension
                return target switch
                {
                    BuildTarget.StandaloneWindows64 => $"{buildPath}/{buildName}.exe",
                    BuildTarget.StandaloneOSX => $"{buildPath}/{buildName}.app",
                    BuildTarget.StandaloneLinux64 => $"{buildPath}/{buildName}",
                    BuildTarget.Android => $"{buildPath}/{buildName}.apk",
                    BuildTarget.iOS => $"{buildPath}/{buildName}",
                    BuildTarget.WebGL => $"{buildPath}/{buildName}",
                    _ => throw new Exception($"Unsupported build target: {target}"),
                };
            }
            else
            {
                // From environment variable, construct full path
                return target switch
                {
                    BuildTarget.StandaloneWindows64 => $"{buildPath}/{buildName}.exe",
                    BuildTarget.StandaloneOSX => $"{buildPath}/{buildName}.app",
                    BuildTarget.StandaloneLinux64 => $"{buildPath}/{buildName}",
                    BuildTarget.Android => $"{buildPath}/{buildName}.apk",
                    BuildTarget.iOS => $"{buildPath}/{buildName}",
                    BuildTarget.WebGL => $"{buildPath}/{buildName}",
                    _ => throw new Exception($"Unsupported build target: {target}"),
                };
            }
        }

        private static string GetArgument(string[] args, string name, string defaultValue = "")
        {
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i] == name && i + 1 < args.Length)
                {
                    return args[i + 1];
                }
            }
            return defaultValue;
        }

        private static BuildTarget GetBuildTarget()
        {
            var targetName = Environment.GetEnvironmentVariable("BUILD_TARGET");
            if (string.IsNullOrEmpty(targetName))
            {
                targetName = GetArgument(Environment.GetCommandLineArgs(), "-buildTarget", "StandaloneWindows64");
            }

            if (Enum.TryParse<BuildTarget>(targetName, out var target))
            {
                return target;
            }

            Debug.LogWarning($"Unknown build target: {targetName}, defaulting to StandaloneWindows64");
            return BuildTarget.StandaloneWindows64;
        }

        private static NamedBuildTarget GetNamedBuildTarget(BuildTarget target)
        {
            return target switch
            {
                BuildTarget.StandaloneWindows64 => NamedBuildTarget.Standalone,
                BuildTarget.StandaloneLinux64 => NamedBuildTarget.Standalone,
                BuildTarget.StandaloneOSX => NamedBuildTarget.Standalone,
                BuildTarget.Android => NamedBuildTarget.Android,
                BuildTarget.iOS => NamedBuildTarget.iOS,
                BuildTarget.WebGL => NamedBuildTarget.WebGL,
                _ => NamedBuildTarget.Standalone,
            };
        }

        private static string GetScriptingBackend()
        {
            var customParams = Environment.GetEnvironmentVariable("CUSTOM_PARAMETERS");
            if (string.IsNullOrEmpty(customParams))
            {
                customParams = string.Join(" ", Environment.GetCommandLineArgs());
            }

            if (customParams.Contains("-scriptingBackend IL2CPP", StringComparison.OrdinalIgnoreCase))
            {
                return "IL2CPP";
            }

            return "Mono2x";
        }

        private static string[] FindEnabledEditorScenes()
        {
            return EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
        }
    }
}