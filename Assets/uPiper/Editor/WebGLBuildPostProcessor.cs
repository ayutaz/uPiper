using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace uPiper.Editor
{
    /// <summary>
    /// WebGLビルド後処理
    /// ビルド完了後に必要なJavaScriptファイルを自動的にコピーする
    /// </summary>
    public class WebGLBuildPostProcessor
    {
        [PostProcessBuild(1)]
        public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.WebGL)
            {
                return;
            }

            Debug.Log($"[uPiper] WebGL build post-processing started for: {pathToBuiltProject}");

            // StreamingAssetsのJavaScriptファイルをビルド先にコピー
            CopyStreamingAssetsFiles(pathToBuiltProject);
        }

        private static void CopyStreamingAssetsFiles(string buildPath)
        {
            // StreamingAssetsフォルダのパス
            string sourceStreamingAssets = Path.Combine(Application.dataPath, "StreamingAssets");
            string targetStreamingAssets = Path.Combine(buildPath, "StreamingAssets");

            // WebGL用のJavaScriptファイル
            string[] filesToCopy = new[]
            {
                "openjtalk-unity-wrapper.js",
                "openjtalk-unity.js",
                "openjtalk-unity.wasm",
                "openjtalk-unity.data",
                "onnx-runtime-wrapper.js",
                "ort-wasm-simd.wasm",
                "ort-wasm-simd.js"
            };

            foreach (string fileName in filesToCopy)
            {
                string sourceFile = Path.Combine(sourceStreamingAssets, fileName);
                string targetFile = Path.Combine(targetStreamingAssets, fileName);

                if (File.Exists(sourceFile))
                {
                    // ターゲットディレクトリが存在しない場合は作成
                    string targetDir = Path.GetDirectoryName(targetFile);
                    if (!Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }

                    // ファイルをコピー（既存のファイルは上書き）
                    File.Copy(sourceFile, targetFile, true);
                    Debug.Log($"[uPiper] Copied: {fileName} to build");
                }
                else
                {
                    Debug.LogWarning($"[uPiper] File not found: {sourceFile}");
                }
            }

            // ONNXモデルファイルもコピー（.onnxと.onnx.json）
            CopyONNXModels(sourceStreamingAssets, targetStreamingAssets);

            Debug.Log("[uPiper] WebGL build post-processing completed");
        }

        private static void CopyONNXModels(string sourceDir, string targetDir)
        {
            // ONNXモデルファイルを検索してコピー
            string[] onnxFiles = Directory.GetFiles(sourceDir, "*.onnx", SearchOption.TopDirectoryOnly);
            string[] onnxJsonFiles = Directory.GetFiles(sourceDir, "*.onnx.json", SearchOption.TopDirectoryOnly);

            foreach (string sourceFile in onnxFiles)
            {
                string fileName = Path.GetFileName(sourceFile);
                string targetFile = Path.Combine(targetDir, fileName);
                File.Copy(sourceFile, targetFile, true);
                Debug.Log($"[uPiper] Copied ONNX model: {fileName}");
            }

            foreach (string sourceFile in onnxJsonFiles)
            {
                string fileName = Path.GetFileName(sourceFile);
                string targetFile = Path.Combine(targetDir, fileName);
                File.Copy(sourceFile, targetFile, true);
                Debug.Log($"[uPiper] Copied ONNX config: {fileName}");
            }
        }
    }
}