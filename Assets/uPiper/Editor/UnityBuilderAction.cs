using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
#if UNITY_2022_1_OR_NEWER
using UnityEditor.Build;
#endif

namespace UnityBuilderAction
{
    public static class BuildScript
    {
        private static readonly string[] Scenes = FindEnabledEditorScenes();

        public static void Build()
        {
            // Get build target from environment or command line
            var buildTarget = GetBuildTarget();
#if UNITY_2022_1_OR_NEWER
            var namedBuildTarget = GetNamedBuildTarget(buildTarget);
#else
            var targetGroup = GetBuildTargetGroup(buildTarget);
#endif

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
#if UNITY_2022_1_OR_NEWER
                PlayerSettings.SetScriptingBackend(namedBuildTarget, ScriptingImplementation.IL2CPP);

                // Configure IL2CPP settings
                PlayerSettings.SetIl2CppCompilerConfiguration(namedBuildTarget, Il2CppCompilerConfiguration.Release);
                PlayerSettings.SetIl2CppCodeGeneration(namedBuildTarget, Il2CppCodeGeneration.OptimizeSpeed);
#else
                PlayerSettings.SetScriptingBackend(targetGroup, ScriptingImplementation.IL2CPP);

                // Configure IL2CPP settings
                PlayerSettings.SetIl2CppCompilerConfiguration(targetGroup, Il2CppCompilerConfiguration.Release);
                PlayerSettings.SetIl2CppCodeGeneration(targetGroup, Il2CppCodeGeneration.OptimizeSpeed);
#endif

                // Configure IL2CPP settings
                if (buildTarget == BuildTarget.WebGL)
                {
                    PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
                    PlayerSettings.WebGL.dataCaching = false;
                }

                // Platform specific IL2CPP settings
#if UNITY_2022_1_OR_NEWER
                ConfigurePlatformSpecificIL2CPP(buildTarget, namedBuildTarget);
#else
                ConfigurePlatformSpecificIL2CPP(buildTarget, targetGroup);
#endif
            }
            else
            {
                Debug.Log("Configuring Mono build...");
#if UNITY_2022_1_OR_NEWER
                PlayerSettings.SetScriptingBackend(namedBuildTarget, ScriptingImplementation.Mono2x);
#else
                PlayerSettings.SetScriptingBackend(targetGroup, ScriptingImplementation.Mono2x);
#endif
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

#if UNITY_2022_1_OR_NEWER
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
                    PlayerSettings.SetApiCompatibilityLevel(namedTarget, ApiCompatibilityLevel.NET_Standard);
                    break;

                case BuildTarget.WebGL:
                    // WebGL specific IL2CPP settings
                    PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
                    PlayerSettings.WebGL.memorySize = 512;
                    break;
            }
        }
#else
        private static void ConfigurePlatformSpecificIL2CPP(BuildTarget target, BuildTargetGroup targetGroup)
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
                    PlayerSettings.SetApiCompatibilityLevel(targetGroup, ApiCompatibilityLevel.NET_Standard);
                    break;

                case BuildTarget.WebGL:
                    // WebGL specific IL2CPP settings
                    PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
                    PlayerSettings.WebGL.memorySize = 512;
                    break;
            }
        }
#endif

        private static string GetBuildLocation(BuildTarget target, string scriptingBackend)
        {
            // Unity Builder v4 passes the full path including filename via -customBuildPath
            // Example: -customBuildPath /github/workspace/build/StandaloneWindows64/uPiper.exe
            var buildPath = GetArgument(Environment.GetCommandLineArgs(), "-customBuildPath", "");

            if (string.IsNullOrEmpty(buildPath))
            {
                throw new Exception("customBuildPath not specified");
            }

            Debug.Log($"Build path from Unity Builder: {buildPath}");

            // Unity Builder already includes the filename in the path, just use it directly
            return buildPath;
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

#if UNITY_2022_1_OR_NEWER
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
#else
        private static BuildTargetGroup GetBuildTargetGroup(BuildTarget target)
        {
            return target switch
            {
                BuildTarget.StandaloneWindows64 => BuildTargetGroup.Standalone,
                BuildTarget.StandaloneLinux64 => BuildTargetGroup.Standalone,
                BuildTarget.StandaloneOSX => BuildTargetGroup.Standalone,
                BuildTarget.Android => BuildTargetGroup.Android,
                BuildTarget.iOS => BuildTargetGroup.iOS,
                BuildTarget.WebGL => BuildTargetGroup.WebGL,
                _ => BuildTargetGroup.Standalone,
            };
        }
#endif

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