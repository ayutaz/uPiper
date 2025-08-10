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
                "openjtalk-unity.data",
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
                    
                    // OpenJTalkスクリプトを追加
                    if (!htmlContent.Contains("openjtalk-unity.js"))
                    {
                        // </body>タグの前にスクリプトを追加
                        string scriptsToAdd = @"
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
            
            // GitHub Pages用の処理
            ProcessLargeFilesForGitHubPages(buildPath);
            
            // ビルド完了後の手順を表示
            Debug.Log("[uPiper] Next steps:");
            Debug.Log("[uPiper] 1. Start local server: python -m http.server 8080");
            Debug.Log("[uPiper] 2. Open browser: http://localhost:8080");
            Debug.Log("[uPiper] 3. For GitHub Pages: git push and enable Pages in repository settings");
        }
        
        private void ProcessLargeFilesForGitHubPages(string buildPath)
        {
            Debug.Log("[uPiper] Processing large files for GitHub Pages (100MB limit)...");
            
            string pythonScript = Path.Combine(Application.dataPath, "..", "split-large-files.py");
            
            if (!File.Exists(pythonScript))
            {
                Debug.LogWarning($"[uPiper] split-large-files.py not found. Large files may not work on GitHub Pages.");
                return;
            }
            
            try
            {
                // Pythonスクリプトを実行してファイルを分割
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{pythonScript}\" process \"{buildPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                using (Process process = Process.Start(psi))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    
                    if (!string.IsNullOrEmpty(output))
                    {
                        Debug.Log($"[uPiper] File splitting output:\n{output}");
                    }
                    
                    if (!string.IsNullOrEmpty(error))
                    {
                        Debug.LogError($"[uPiper] File splitting error:\n{error}");
                    }
                    
                    if (process.ExitCode == 0)
                    {
                        Debug.Log("[uPiper] ✅ Large files processed for GitHub Pages deployment");
                    }
                    else
                    {
                        Debug.LogWarning("[uPiper] File splitting failed. You may need to manually split files > 100MB");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[uPiper] Could not split large files: {ex.Message}");
                Debug.Log("[uPiper] Install Python and run: python split-large-files.py process Build/Web");
            }
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