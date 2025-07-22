using System;
using System.Linq;
using UnityEditor;
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
            
            // Configure scripting backend
            if (scriptingBackend.ToLower() == "il2cpp")
            {
                Debug.Log("Configuring IL2CPP build...");
                PlayerSettings.SetScriptingBackend(buildTargetGroup, ScriptingImplementation.IL2CPP);
                
                // Configure IL2CPP specific settings
                PlayerSettings.SetIl2CppCompilerConfiguration(buildTargetGroup, Il2CppCompilerConfiguration.Release);
                PlayerSettings.SetManagedStrippingLevel(buildTargetGroup, ManagedStrippingLevel.Low);
                PlayerSettings.gcIncremental = true;
                
                // Platform specific IL2CPP settings
                ConfigurePlatformSpecificIL2CPP(buildTarget, buildTargetGroup);
            }
            else
            {
                Debug.Log("Configuring Mono build...");
                PlayerSettings.SetScriptingBackend(buildTargetGroup, ScriptingImplementation.Mono2x);
            }
            
            // Get build location
            var buildLocation = GetBuildLocation(buildTarget);
            
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
        
        private static void ConfigurePlatformSpecificIL2CPP(BuildTarget target, BuildTargetGroup targetGroup)
        {
            switch (target)
            {
                case BuildTarget.Android:
                    PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
                    PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel21;
                    break;
                    
                case BuildTarget.iOS:
                    PlayerSettings.SetArchitecture(targetGroup, 2); // Universal
                    PlayerSettings.iOS.targetOSVersionString = "11.0";
                    break;
                    
                case BuildTarget.WebGL:
                    PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
                    PlayerSettings.WebGL.memorySize = 512;
                    break;
            }
        }
        
        private static string GetBuildLocation(BuildTarget target)
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
            
            switch (target)
            {
                case BuildTarget.StandaloneWindows64:
                    return $"{buildPath}/{buildName}.exe";
                case BuildTarget.StandaloneOSX:
                    return $"{buildPath}/{buildName}.app";
                case BuildTarget.StandaloneLinux64:
                    return $"{buildPath}/{buildName}";
                case BuildTarget.Android:
                    return $"{buildPath}/{buildName}.apk";
                case BuildTarget.iOS:
                    return $"{buildPath}/{buildName}";
                case BuildTarget.WebGL:
                    return $"{buildPath}/{buildName}";
                default:
                    throw new Exception($"Unsupported build target: {target}");
            }
        }
        
        private static string GetArgument(string[] args, string name, string defaultValue = "")
        {
            for (int i = 0; i < args.Length; i++)
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