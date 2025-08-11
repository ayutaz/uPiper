using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.IO;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace uPiper.Editor
{
    /// <summary>
    /// WebGLビルド後にOpenJTalkファイルを自動的にコピーする
    /// </summary>
    public class WebGLPostBuildProcessor : IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            // WebGLビルドの場合のみ処理
            if (report.summary.platform != BuildTarget.WebGL)
            {
                return;
            }

            Debug.Log("[uPiper] WebGL build detected. Copying OpenJTalk files...");

            string buildPath = report.summary.outputPath;
            string streamingAssetsSource = Path.Combine(Application.dataPath, "StreamingAssets");
            string streamingAssetsTarget = Path.Combine(buildPath, "StreamingAssets");

            // StreamingAssetsディレクトリを作成
            if (!Directory.Exists(streamingAssetsTarget))
            {
                Directory.CreateDirectory(streamingAssetsTarget);
                Debug.Log($"[uPiper] Created directory: {streamingAssetsTarget}");
            }

            // コピーするファイルのリスト
            string[] filesToCopy = new string[]
            {
                "openjtalk-unity.js",
                "openjtalk-unity.wasm",
                "openjtalk-unity.data",  // オリジナルファイル（ローカルテスト用）
                "openjtalk-unity.data.part000",  // GitHub Pages用分割ファイル1
                "openjtalk-unity.data.part001",  // GitHub Pages用分割ファイル2
                "openjtalk-unity.data.manifest.json",  // 分割ファイルのマニフェスト
                "openjtalk-webgl-integration.js",
                "openjtalk-unity-wrapper.js",
                "onnx-runtime-wrapper.js",
                "github-pages-adapter.js",
                "ja_JP-test-medium.onnx",
                "ja_JP-test-medium.onnx.json"
            };

            int copiedCount = 0;
            long totalSize = 0;

            foreach (string fileName in filesToCopy)
            {
                string sourcePath = Path.Combine(streamingAssetsSource, fileName);
                string targetPath = Path.Combine(streamingAssetsTarget, fileName);

                if (File.Exists(sourcePath))
                {
                    try
                    {
                        File.Copy(sourcePath, targetPath, true);
                        FileInfo fileInfo = new FileInfo(targetPath);
                        totalSize += fileInfo.Length;
                        copiedCount++;

                        // ファイルサイズを表示
                        string sizeStr = GetFileSizeString(fileInfo.Length);
                        Debug.Log($"[uPiper] Copied: {fileName} ({sizeStr})");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[uPiper] Failed to copy {fileName}: {e.Message}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[uPiper] File not found: {sourcePath}");
                }
            }

            // index.htmlにスクリプトタグを追加
            string indexPath = Path.Combine(buildPath, "index.html");
            if (File.Exists(indexPath))
            {
                try
                {
                    string htmlContent = File.ReadAllText(indexPath);
                    
                    // GitHub Pagesアダプターを最初に追加
                    if (!htmlContent.Contains("github-pages-adapter.js"))
                    {
                        string adapterScript = "  <script src=\"StreamingAssets/github-pages-adapter.js\"></script>\n</head>";
                        htmlContent = htmlContent.Replace("</head>", adapterScript);
                    }
                    
                    // ONNX Runtime WebとOpenJTalkスクリプトを追加
                    if (!htmlContent.Contains("onnxruntime-web"))
                    {
                        // </body>タグの前にスクリプトを追加
                        string scriptsToAdd = @"
  <!-- ONNX Runtime Web -->
  <script src=""https://cdn.jsdelivr.net/npm/onnxruntime-web@1.17.1/dist/ort.min.js""></script>
  
  <!-- OpenJTalk WebGL Integration -->
  <script src=""StreamingAssets/openjtalk-unity.js""></script>
  <script src=""StreamingAssets/openjtalk-unity-wrapper.js""></script>
  <script src=""StreamingAssets/onnx-runtime-wrapper.js""></script>
  <!-- End OpenJTalk -->
</body>";
                        
                        htmlContent = htmlContent.Replace("</body>", scriptsToAdd);
                    }
                    
                    File.WriteAllText(indexPath, htmlContent);
                    Debug.Log("[uPiper] Updated index.html with required scripts");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[uPiper] Failed to update index.html: {e.Message}");
                }
            }

            // 完了メッセージ
            string totalSizeStr = GetFileSizeString(totalSize);
            Debug.Log($"[uPiper] ✅ WebGL build post-processing complete!");
            Debug.Log($"[uPiper] Copied {copiedCount} files, Total size: {totalSizeStr}");
            Debug.Log($"[uPiper] OpenJTalk is ready for WebGL deployment!");
            
            // Note: ファイル分割は事前に実行済み（openjtalk-unity.data.part000, part001）
            // GitHub Pagesデプロイ時はgithub-pages-adapter.jsが自動的に分割ファイルを再構築
            
            // ビルド完了後の手順を表示
            Debug.Log("[uPiper] Next steps:");
            Debug.Log("[uPiper] 1. Start local server: python -m http.server 8080");
            Debug.Log("[uPiper] 2. Open browser: http://localhost:8080");
            Debug.Log("[uPiper] 3. For GitHub Pages: git push and enable Pages in repository settings");
            Debug.Log("[uPiper] Note: Large files are pre-split for GitHub Pages compatibility");
        }

        private string GetFileSizeString(long bytes)
        {
            if (bytes >= 1024 * 1024)
            {
                return $"{bytes / (1024.0 * 1024.0):F2} MB";
            }
            else if (bytes >= 1024)
            {
                return $"{bytes / 1024.0:F2} KB";
            }
            else
            {
                return $"{bytes} bytes";
            }
        }
    }
}