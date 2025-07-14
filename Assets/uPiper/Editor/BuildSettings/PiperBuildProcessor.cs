using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.IO;
using System.Linq;
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
            PiperLogger.LogInfo($"[PiperBuildProcessor] Build completed for {report.summary.platform}");
            PiperLogger.LogInfo($"[PiperBuildProcessor] Build size: {report.summary.totalSize / (1024 * 1024)}MB");
            PiperLogger.LogInfo($"[PiperBuildProcessor] Build time: {report.summary.totalTime.TotalSeconds:F2} seconds");
            
            if (report.summary.result != BuildResult.Succeeded)
            {
                PiperLogger.LogError($"[PiperBuildProcessor] Build failed: {report.summary.result}");
            }
        }

        private void ConfigureWebGLBuild()
        {
            PiperLogger.LogInfo("[PiperBuildProcessor] Configuring WebGL build settings");
            
            // WebGL固有の設定
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.template = "PROJECT:Minimal";
            
            // メモリサイズの設定（ONNXモデルのため大きめに）
            PlayerSettings.WebGL.memorySize = 512;
            
            // WebAssemblyストリーミングを有効化
            #if UNITY_2021_2_OR_NEWER
            PlayerSettings.WebGL.wasmArithmeticExceptions = WebGLWasmArithmeticExceptions.Throw;
            #endif
        }

        private void ConfigureWindowsBuild()
        {
            PiperLogger.LogInfo("[PiperBuildProcessor] Configuring Windows build settings");
            
            // Windows固有の設定
            PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Standalone, ApiCompatibilityLevel.NET_Standard);
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
            PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Standalone, ApiCompatibilityLevel.NET_Standard);
        }

        private void ValidateONNXModels()
        {
            string resourcesPath = "Assets/uPiper/Resources";
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
                string jsonPath = file + ".json";
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
            
            string outputPath = EditorUtility.SaveFolderPanel("ビルド出力先を選択", "", "");
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
    }
}