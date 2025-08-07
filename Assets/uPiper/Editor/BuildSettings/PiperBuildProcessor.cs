using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using uPiper.Core.Logging;

namespace uPiper.Editor.BuildSettings
{
    /// <summary>
    /// uPiper用のビルド前後処理
    /// </summary>
    public class PiperBuildProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            PiperLogger.LogInfo($"[PiperBuildProcessor] Starting build for {report.summary.platform}");

            // ビルドプラットフォームごとの設定
            switch (report.summary.platform)
            {
                case BuildTarget.WebGL:
                    ConfigureWebGLBuild();
                    break;
                case BuildTarget.StandaloneWindows64:
                    ConfigureWindowsBuild();
                    break;
                case BuildTarget.StandaloneOSX:
                    ConfigureMacOSBuild();
                    break;
                case BuildTarget.StandaloneLinux64:
                    ConfigureLinuxBuild();
                    break;
            }

            // ONNXモデルファイルの確認
            ValidateONNXModels();
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            // CI環境でのログ出力はDebugレベルにして、エラーログを避ける
            PiperLogger.LogDebug($"[PiperBuildProcessor] Build completed for {report.summary.platform}");
            PiperLogger.LogDebug($"[PiperBuildProcessor] Build size: {report.summary.totalSize / (1024 * 1024)}MB");
            PiperLogger.LogDebug($"[PiperBuildProcessor] Build time: {report.summary.totalTime.TotalSeconds:F2} seconds");

            // ビルドが成功した場合はログを出さない（CI環境でのエラー表示を避けるため）
            if (report.summary.result != BuildResult.Succeeded)
            {
                PiperLogger.LogWarning($"[PiperBuildProcessor] Build result: {report.summary.result}");
            }
        }

        private void ConfigureWebGLBuild()
        {
            PiperLogger.LogInfo("[PiperBuildProcessor] Configuring WebGL build settings");

            // WebGL固有の設定
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            
            // Use default Unity WebGL template for better compatibility
            PlayerSettings.WebGL.template = "APPLICATION:Default";
            PiperLogger.LogInfo("[PiperBuildProcessor] Using default WebGL template");

            // メモリサイズの設定（ONNXモデルのため大きめに）
            // Increased from 512MB to 1GB for better performance
            PlayerSettings.WebGL.memorySize = 1024;

            // Unity 6ではWebAssembly算術例外は常に無視される
            
            // Enable WebAssembly streaming instantiation for faster loading
            PlayerSettings.WebGL.decompressionFallback = true;
            
            // Set linker target to Wasm (Asm.js is deprecated)
            PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
        }

        private void ConfigureWindowsBuild()
        {
            PiperLogger.LogInfo("[PiperBuildProcessor] Configuring Windows build settings");

            // Windows固有の設定
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
        }

        private void ConfigureMacOSBuild()
        {
            PiperLogger.LogInfo("[PiperBuildProcessor] Configuring macOS build settings");

            // macOS固有の設定
            PlayerSettings.macOS.buildNumber = PlayerSettings.bundleVersion;
        }

        private void ConfigureLinuxBuild()
        {
            PiperLogger.LogInfo("[PiperBuildProcessor] Configuring Linux build settings");

            // Linux固有の設定
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
        }

        private void ValidateONNXModels()
        {
            var resourcesPath = "Assets/uPiper/Resources";
            if (!Directory.Exists(resourcesPath))
            {
                PiperLogger.LogWarning("[PiperBuildProcessor] Resources directory not found");
                return;
            }

            var onnxFiles = Directory.GetFiles(resourcesPath, "*.onnx", SearchOption.AllDirectories);
            PiperLogger.LogInfo($"[PiperBuildProcessor] Found {onnxFiles.Length} ONNX model files");

            foreach (var file in onnxFiles)
            {
                var fileInfo = new FileInfo(file);
                PiperLogger.LogInfo($"[PiperBuildProcessor] - {fileInfo.Name} ({fileInfo.Length / (1024 * 1024)}MB)");

                // 対応するJSONファイルの確認
                var jsonPath = file + ".json";
                if (!File.Exists(jsonPath))
                {
                    PiperLogger.LogWarning($"[PiperBuildProcessor] Missing JSON config for {fileInfo.Name}");
                }
            }
        }
    }

    /// <summary>
    /// ビルド設定メニュー
    /// </summary>
    public static class PiperBuildMenu
    {
        [MenuItem("uPiper/Build/Configure Build Settings")]
        public static void ConfigureBuildSettings()
        {
            // プロダクト名の設定
            PlayerSettings.productName = "uPiper Demo";
            PlayerSettings.companyName = "com.yousan";

            // 全般的な設定
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;

            // 各プラットフォームのシーンを設定
            var scenes = new[]
            {
                "Assets/uPiper/Samples~/WebGLDemo/WebGLDemoScene.unity",
                "Assets/Scenes/SampleScene.unity"
            };

            var validScenes = scenes.Where(File.Exists).ToArray();
            EditorBuildSettings.scenes = validScenes.Select(path => new EditorBuildSettingsScene(path, true)).ToArray();

            PiperLogger.LogInfo($"[PiperBuildMenu] Configured {validScenes.Length} scenes for build");

            EditorUtility.DisplayDialog("uPiper", "ビルド設定が完了しました", "OK");
        }

        [MenuItem("uPiper/Build/Build All Platforms")]
        public static void BuildAllPlatforms()
        {
            if (!EditorUtility.DisplayDialog("uPiper", "全プラットフォームのビルドを開始しますか？", "はい", "いいえ"))
                return;

            var outputPath = EditorUtility.SaveFolderPanel("ビルド出力先を選択", "", "");
            if (string.IsNullOrEmpty(outputPath))
                return;

            // 各プラットフォームでビルド
            BuildForPlatform(BuildTarget.StandaloneWindows64, Path.Combine(outputPath, "Windows", "uPiper.exe"));
            BuildForPlatform(BuildTarget.StandaloneOSX, Path.Combine(outputPath, "macOS", "uPiper.app"));
            BuildForPlatform(BuildTarget.StandaloneLinux64, Path.Combine(outputPath, "Linux", "uPiper"));
            BuildForPlatform(BuildTarget.WebGL, Path.Combine(outputPath, "WebGL"));
        }

        private static void BuildForPlatform(BuildTarget target, string outputPath)
        {
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = outputPath,
                target = target,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
            {
                PiperLogger.LogInfo($"[PiperBuildMenu] {target} build succeeded");
            }
            else
            {
                PiperLogger.LogError($"[PiperBuildMenu] {target} build failed");
            }
        }

        /// <summary>
        /// CI/CD用のWebGLビルドメソッド（Unity Builder互換）
        /// </summary>
        [MenuItem("uPiper/Build/Build WebGL")]
        public static void BuildWebGL()
        {
            var buildPath = GetBuildPath();
            var buildName = GetBuildName();
            var fullPath = Path.Combine(buildPath, "WebGL", buildName);

            PiperLogger.LogInfo($"[PiperBuildMenu] Starting WebGL build to: {fullPath}");

            // Configure WebGL-specific settings
            ConfigureWebGLBuildSettings();

            // Get enabled scenes or use InferenceEngineDemo scene
            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                // Try to find InferenceEngineDemo scene
                string demoScenePath = "Assets/uPiper/Scenes/InferenceEngineDemo.unity";
                if (!File.Exists(demoScenePath))
                {
                    // Alternative path - WebGL specific demo
                    demoScenePath = "Assets/uPiper/Samples~/WebGLDemo/WebGLDemoScene.unity";
                }
                
                if (File.Exists(demoScenePath))
                {
                    PiperLogger.LogWarning($"[PiperBuildMenu] No scenes enabled, using demo scene: {demoScenePath}");
                    scenes = new[] { demoScenePath };
                }
                else
                {
                    PiperLogger.LogError("[PiperBuildMenu] No scenes are enabled in build settings and demo scene not found");
                    EditorApplication.Exit(1);
                    return;
                }
            }

            var buildOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = fullPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            // Add development build option if specified
            if (GetCommandLineArg("-developmentBuild") == "true")
            {
                buildOptions.options |= BuildOptions.Development;
            }

            var report = BuildPipeline.BuildPlayer(buildOptions);

            if (report.summary.result == BuildResult.Succeeded)
            {
                PiperLogger.LogInfo($"[PiperBuildMenu] WebGL build succeeded");
                PiperLogger.LogInfo($"[PiperBuildMenu] Build size: {report.summary.totalSize / (1024 * 1024)}MB");
                PiperLogger.LogInfo($"[PiperBuildMenu] Build time: {report.summary.totalTime.TotalSeconds:F2} seconds");
            }
            else
            {
                PiperLogger.LogError($"[PiperBuildMenu] WebGL build failed: {report.summary.result}");
                foreach (var step in report.steps)
                {
                    foreach (var message in step.messages)
                    {
                        if (message.type == LogType.Error || message.type == LogType.Exception)
                        {
                            PiperLogger.LogError($"[PiperBuildMenu] {message.content}");
                        }
                    }
                }
                EditorApplication.Exit(1);
            }
        }

        private static void ConfigureWebGLBuildSettings()
        {
            // Use default Unity WebGL template for better compatibility
            PlayerSettings.WebGL.template = "APPLICATION:Default";
            PiperLogger.LogInfo("[PiperBuildMenu] Using default WebGL template");

            // Configure compression
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            
            // Configure memory
            PlayerSettings.WebGL.memorySize = 1024; // 1GB for ONNX models
            
            // Configure other settings
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
        }

        private static string GetBuildPath()
        {
            var buildPath = GetCommandLineArg("-customBuildPath");
            if (string.IsNullOrEmpty(buildPath))
            {
                buildPath = "build";
            }
            return buildPath;
        }

        private static string GetBuildName()
        {
            var buildName = GetCommandLineArg("-customBuildName");
            if (string.IsNullOrEmpty(buildName))
            {
                buildName = "uPiperWebGL";
            }
            return buildName;
        }

        private static string GetCommandLineArg(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == name && args.Length > i + 1)
                {
                    return args[i + 1];
                }
            }
            return null;
        }
        
        /// <summary>
        /// CI/CD用のシンプルなWebGLビルドメソッド（Unity Builder用）
        /// </summary>
        public static void PerformWebGLBuild()
        {
            BuildWebGL();
        }
    }
}