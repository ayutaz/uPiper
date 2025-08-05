using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace uPiper.Editor.CI
{
    /// <summary>
    /// CI/CD専用のWebGLビルダー
    /// </summary>
    public static class WebGLBuilder
    {
        /// <summary>
        /// CI/CD用のWebGLビルドメソッド
        /// </summary>
        public static void Build()
        {
            Debug.Log("[WebGLBuilder] Starting WebGL build for CI/CD");
            
            // コマンドライン引数から設定を取得
            var args = System.Environment.GetCommandLineArgs();
            string buildPath = "build/WebGL";
            string buildName = "uPiperWebGL";
            
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-customBuildPath" && i + 1 < args.Length)
                {
                    buildPath = args[i + 1];
                }
                else if (args[i] == "-customBuildName" && i + 1 < args.Length)
                {
                    buildName = args[i + 1];
                }
            }
            
            Debug.Log($"[WebGLBuilder] Build path: {buildPath}");
            Debug.Log($"[WebGLBuilder] Build name: {buildName}");
            
            // ビルドプレイヤーオプションの設定
            var buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = GetEnabledScenes(),
                locationPathName = buildPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };
            
            // プラットフォーム固有の設定
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Asm;
            PlayerSettings.WebGL.memorySize = 1024;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.FullWithStacktrace;
            PlayerSettings.WebGL.template = "PROJECT:uPiper";
            
            Debug.Log($"[WebGLBuilder] Building {buildPlayerOptions.scenes.Length} scenes");
            
            // ビルド実行
            var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            var summary = report.summary;
            
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[WebGLBuilder] Build succeeded: {summary.totalSize} bytes");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"[WebGLBuilder] Build failed: {summary.result}");
                foreach (var step in report.steps)
                {
                    foreach (var message in step.messages)
                    {
                        if (message.type == LogType.Error || message.type == LogType.Exception)
                        {
                            Debug.LogError($"[WebGLBuilder] {message.content}");
                        }
                    }
                }
                EditorApplication.Exit(1);
            }
        }
        
        private static string[] GetEnabledScenes()
        {
            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
                
            if (scenes.Length == 0)
            {
                Debug.LogWarning("[WebGLBuilder] No scenes enabled in build settings, using default scene");
                scenes = new[] { "Assets/uPiper/Samples/InferenceEngineDemo/InferenceEngineDemo.unity" };
            }
            
            return scenes;
        }
    }
}