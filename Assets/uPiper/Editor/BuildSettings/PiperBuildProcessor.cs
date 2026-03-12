using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
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

            // すべてのプラットフォームで必要なStreamingAssetsをセットアップ
            SetupStreamingAssets();

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
                case BuildTarget.Android:
                    ConfigureAndroidBuild();
                    break;
                case BuildTarget.iOS:
                    ConfigureIOSBuild();
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

            // iOSの場合、UnityはXcodeプロジェクトを生成するだけなので、BuildResult.Unknownは正常
            if (report.summary.platform == BuildTarget.iOS && report.summary.result == BuildResult.Unknown)
            {
                PiperLogger.LogInfo("[PiperBuildProcessor] iOS Xcode project generated successfully");
            }
            // ビルドが成功した場合はログを出さない（CI環境でのエラー表示を避けるため）
            else if (report.summary.result != BuildResult.Succeeded)
            {
                PiperLogger.LogWarning($"[PiperBuildProcessor] Build result: {report.summary.result}");
            }

            // ビルド後のクリーンアップ（開発環境のみ）
            CleanupStreamingAssets();
        }

        private void ConfigureWebGLBuild()
        {
            PiperLogger.LogInfo("[PiperBuildProcessor] Configuring WebGL build settings");

            // WebGL固有の設定
            // GitHub PagesはContent-Encoding: gzipヘッダーを付与できないため、
            // CI環境では圧縮を無効にする（ローカルビルドではGzipを使用）
            var isCi = !string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("GITHUB_WORKFLOW"));
            PlayerSettings.WebGL.compressionFormat = isCi
                ? WebGLCompressionFormat.Disabled
                : WebGLCompressionFormat.Gzip;
            // Unity 6ではデフォルトテンプレートを使用
            PlayerSettings.WebGL.template = "APPLICATION:Default";

            // メモリサイズの設定（ONNXモデルのため大きめに）
            PlayerSettings.WebGL.memorySize = 512;

            // Unity 6ではWebAssembly算術例外は常に無視される
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

        private void ConfigureAndroidBuild()
        {
            PiperLogger.LogInfo("[PiperBuildProcessor] Configuring Android build settings");

            // Android固有の設定
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Android, ApiCompatibilityLevel.NET_Standard);

            // 最小APIレベルの設定（Android 7.0以上）
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;

            // ターゲットAPIレベルの設定
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        }

        private void ConfigureIOSBuild()
        {
            PiperLogger.LogInfo("[PiperBuildProcessor] Configuring iOS build settings");

            // iOS固有の設定
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.iOS, ApiCompatibilityLevel.NET_Standard);

            // 最小iOSバージョン（iOS 11.0以上）
            PlayerSettings.iOS.targetOSVersionString = "11.0";

            // アーキテクチャ設定（ARM64）
            PlayerSettings.SetArchitecture(NamedBuildTarget.iOS, 1); // 1 = ARM64

            // Bundle Identifierの確認と設定（Apple推奨のreverse DNS形式）
            if (string.IsNullOrEmpty(PlayerSettings.applicationIdentifier) ||
                PlayerSettings.applicationIdentifier == "com.DefaultCompany.uPiper" ||
                PlayerSettings.applicationIdentifier == "com.DefaultCompany.ProjectName")
            {
                PlayerSettings.applicationIdentifier = "com.ayutaz.uPiper";
                PiperLogger.LogInfo("[PiperBuildProcessor] Set Bundle Identifier to: com.ayutaz.uPiper");
            }

            // iOS向けの追加設定
            PlayerSettings.insecureHttpOption = InsecureHttpOption.NotAllowed; // HTTPSのみ許可
            PlayerSettings.iOS.requiresPersistentWiFi = false; // Wi-Fi必須ではない
            PlayerSettings.iOS.appInBackgroundBehavior = iOSAppInBackgroundBehavior.Suspend;

            // カメラ使用権限（必要ない場合はNone）
            PlayerSettings.iOS.cameraUsageDescription = "";

            // マイク使用権限（音声入力を使用する場合）
            PlayerSettings.iOS.microphoneUsageDescription = "This app uses the microphone for voice input features.";
        }

        private void SetupStreamingAssets()
        {
            PiperLogger.LogInfo("[PiperBuildProcessor] Setting up StreamingAssets for build");

            // StreamingAssetsディレクトリの作成
            var streamingAssetsPath = "Assets/StreamingAssets/uPiper";
            Directory.CreateDirectory(Path.Combine(streamingAssetsPath, "Phonemizers"));

            // CMU辞書のコピー
            var cmuSource = "Assets/uPiper/Samples~/CMU Pronouncing Dictionary/cmudict-0.7b.txt";
            var cmuTarget = Path.Combine(streamingAssetsPath, "Phonemizers", "cmudict-0.7b.txt");

            if (File.Exists(cmuSource))
            {
                File.Copy(cmuSource, cmuTarget, true);
                PiperLogger.LogInfo($"[PiperBuildProcessor] Copied CMU dictionary to StreamingAssets");
            }
            else
            {
                PiperLogger.LogWarning($"[PiperBuildProcessor] CMU dictionary not found at: {cmuSource}");
            }

            // StreamingAssets.metaファイルの生成を確実にする
            AssetDatabase.Refresh();
        }

        private void CleanupStreamingAssets()
        {
#if UPIPER_DEVELOPMENT
            // 開発環境でのみ、ビルド後にStreamingAssetsをクリーンアップ
            PiperLogger.LogInfo("[PiperBuildProcessor] Cleaning up temporary StreamingAssets");

            var streamingAssetsPath = "Assets/StreamingAssets/uPiper";
            if (Directory.Exists(streamingAssetsPath))
            {
                try
                {
                    Directory.Delete(streamingAssetsPath, true);

                    // StreamingAssetsフォルダが空の場合は削除
                    var parentPath = "Assets/StreamingAssets";
                    if (Directory.Exists(parentPath) && Directory.GetFileSystemEntries(parentPath).Length == 0)
                    {
                        Directory.Delete(parentPath);
                    }

                    PiperLogger.LogInfo("[PiperBuildProcessor] Temporary StreamingAssets cleaned up");
                }
                catch (Exception ex)
                {
                    PiperLogger.LogWarning($"[PiperBuildProcessor] Failed to cleanup StreamingAssets: {ex.Message}");
                }
            }
#endif
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
#if UPIPER_DEVELOPMENT
        [MenuItem("uPiper/Build/Configure Build Settings")]
#endif
        public static void ConfigureBuildSettings()
        {
            // プロダクト名の設定
            PlayerSettings.productName = "uPiper Demo";
            PlayerSettings.companyName = "ayutaz";

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

#if UPIPER_DEVELOPMENT
        [MenuItem("uPiper/Build/Build All Platforms")]
#endif
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
            BuildForPlatform(BuildTarget.iOS, Path.Combine(outputPath, "iOS"));
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