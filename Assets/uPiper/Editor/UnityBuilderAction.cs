using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace UnityBuilderAction
{
    /// <summary>
    /// Build script for Unity Builder GitHub Action with IL2CPP support
    /// </summary>
    public static class BuildScript
    {
        private static string[] GetScenes()
        {
            return EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
        }

        public static void Build()
        {
            // Get build parameters from command line
            var args = Environment.GetCommandLineArgs();
            var scriptingBackend = GetArgument(args, "-scriptingBackend", "Mono2x");

            // Parse target platform
            var buildTarget = EditorUserBuildSettings.activeBuildTarget;
            var buildTargetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);
            var namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);

            // Configure scripting backend
            if (scriptingBackend.ToLower() == "il2cpp")
            {
                Debug.Log("Configuring IL2CPP build...");
                PlayerSettings.SetScriptingBackend(namedBuildTarget, ScriptingImplementation.IL2CPP);

                // Configure IL2CPP specific settings
                PlayerSettings.SetIl2CppCompilerConfiguration(namedBuildTarget, Il2CppCompilerConfiguration.Release);
                PlayerSettings.SetManagedStrippingLevel(namedBuildTarget, ManagedStrippingLevel.Low);
                PlayerSettings.gcIncremental = true;

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
                scenes = GetScenes(),
                locationPathName = buildLocation,
                target = buildTarget,
                options = buildOptions
            });

            // Check build result
            if (buildReport.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"Build succeeded: {buildReport.summary.totalSize} bytes");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"Build failed with {buildReport.summary.totalErrors} errors");
                EditorApplication.Exit(1);
            }
        }

        private static void ConfigurePlatformSpecificIL2CPP(BuildTarget target, NamedBuildTarget namedTarget)
        {
            switch (target)
            {
                case BuildTarget.Android:
                    PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
                    PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;
                    break;

                case BuildTarget.iOS:
                    PlayerSettings.SetArchitecture(namedTarget, 2); // Universal
                    PlayerSettings.iOS.targetOSVersionString = "11.0";
                    break;

                case BuildTarget.WebGL:
                    PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
                    PlayerSettings.WebGL.memorySize = 512;
                    break;
            }
        }

        private static string GetBuildLocation(BuildTarget target, string scriptingBackend)
        {
            var buildPath = GetArgument(Environment.GetCommandLineArgs(), "-customBuildPath", "");
            if (string.IsNullOrEmpty(buildPath))
            {
                throw new Exception("customBuildPath not specified");
            }

            var buildName = GetArgument(Environment.GetCommandLineArgs(), "-customBuildName", "uPiper");

            // Append scripting backend to build name if not already included
            if (!buildName.Contains(scriptingBackend))
            {
                buildName = $"{buildName}-{scriptingBackend}";
            }

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
    }
}