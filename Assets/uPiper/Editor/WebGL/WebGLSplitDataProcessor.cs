#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace uPiper.Editor.WebGL
{
    /// <summary>
    /// WebGLビルド後に大容量ファイルを分割してGitHub Pagesデプロイ可能にする。
    /// GitHub Pagesの100MBファイルサイズ制限に対応するため、
    /// 90MBチャンクに分割し、split-file-loader.jsで透過的に再結合する。
    /// </summary>
    public static class WebGLSplitDataProcessor
    {
        private const long MaxFileSize = 90L * 1024 * 1024; // 90MB per chunk
        private const long SplitThreshold = 100L * 1024 * 1024; // 100MB threshold
        private const string LogPrefix = "[WebGLSplitDataProcessor]";

        [PostProcessBuild(999)]
        public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.WebGL) return;

            Debug.Log($"{LogPrefix} Processing WebGL build for GitHub Pages deployment...");

            SplitLargeFiles(pathToBuiltProject);
            InjectSplitLoaderScript(pathToBuiltProject);
            ModifyIndexHtml(pathToBuiltProject);

            Debug.Log($"{LogPrefix} WebGL build processing complete!");
        }

        /// <summary>
        /// Build/ ディレクトリ内の大容量ファイルを検出・分割する
        /// </summary>
        private static void SplitLargeFiles(string buildPath)
        {
            var buildDir = Path.Combine(buildPath, "Build");
            if (!Directory.Exists(buildDir))
            {
                Debug.LogWarning($"{LogPrefix} Build directory not found: {buildDir}");
                return;
            }

            var files = Directory.GetFiles(buildDir);
            foreach (var filePath in files)
            {
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length <= SplitThreshold) continue;

                Debug.Log($"{LogPrefix} File exceeds {SplitThreshold / 1024 / 1024}MB, splitting: " +
                          $"{fileInfo.Name} ({fileInfo.Length / 1024 / 1024}MB)");
                SplitFile(filePath);
            }
        }

        /// <summary>
        /// ファイルを90MBチャンクに分割する。
        /// チャンク名は .partaa, .partab, ... の形式。
        /// </summary>
        private static void SplitFile(string filePath)
        {
            var fileBytes = File.ReadAllBytes(filePath);
            var chunks = (int)((fileBytes.Length + MaxFileSize - 1) / MaxFileSize);

            for (var i = 0; i < chunks; i++)
            {
                var offset = i * MaxFileSize;
                var length = (int)Math.Min(MaxFileSize, fileBytes.Length - offset);

                var suffix = $"{(char)('a' + i / 26)}{(char)('a' + i % 26)}";
                var chunkPath = $"{filePath}.part{suffix}";

                using (var fs = new FileStream(chunkPath, FileMode.Create))
                {
                    fs.Write(fileBytes, (int)offset, length);
                }

                Debug.Log($"{LogPrefix} Created chunk: {Path.GetFileName(chunkPath)} ({length / 1024 / 1024}MB)");
            }

            // 分割メタデータファイルを作成（ローダーがチャンク数を知るため）
            var metadataPath = $"{filePath}.split-meta";
            var metadata = $"{{\"originalFile\":\"{Path.GetFileName(filePath)}\",\"chunks\":{chunks},\"chunkSize\":{MaxFileSize}}}";
            File.WriteAllText(metadataPath, metadata);
            Debug.Log($"{LogPrefix} Created split metadata: {Path.GetFileName(metadataPath)}");
        }

        /// <summary>
        /// split-file-loader.js と github-pages-adapter.js をビルド出力にコピーする
        /// </summary>
        private static void InjectSplitLoaderScript(string buildPath)
        {
            CopyEditorAsset("split-file-loader.js", buildPath);
            CopyEditorAsset("github-pages-adapter.js", buildPath);
        }

        private static void CopyEditorAsset(string fileName, string buildPath)
        {
            // Editor/WebGL/ ディレクトリからJSファイルを検索
            var guids = AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(fileName));
            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!assetPath.EndsWith(fileName)) continue;

                var fullSourcePath = Path.GetFullPath(assetPath);
                var destPath = Path.Combine(buildPath, fileName);
                File.Copy(fullSourcePath, destPath, true);
                Debug.Log($"{LogPrefix} Copied {fileName} to build output");
                return;
            }

            Debug.LogWarning($"{LogPrefix} {fileName} not found in Editor assets");
        }

        /// <summary>
        /// index.html に split-file-loader.js と github-pages-adapter.js のスクリプトタグを注入する
        /// </summary>
        private static void ModifyIndexHtml(string buildPath)
        {
            var indexPath = Path.Combine(buildPath, "index.html");
            if (!File.Exists(indexPath))
            {
                Debug.LogWarning($"{LogPrefix} index.html not found: {indexPath}");
                return;
            }

            var content = File.ReadAllText(indexPath);

            if (content.Contains("split-file-loader.js"))
            {
                Debug.Log($"{LogPrefix} index.html already contains split loader");
                return;
            }

            // <head> タグの直後にスクリプトを注入
            const string headTag = "<head>";
            var insertionPoint = content.IndexOf(headTag, StringComparison.Ordinal);
            if (insertionPoint < 0)
            {
                Debug.LogWarning($"{LogPrefix} <head> tag not found in index.html");
                return;
            }

            var scriptTags = @"
    <!-- uPiper: GitHub Pages deployment support -->
    <script src=""github-pages-adapter.js""></script>
    <script src=""split-file-loader.js""></script>";

            content = content.Insert(insertionPoint + headTag.Length, scriptTags);

            File.WriteAllText(indexPath, content);
            Debug.Log($"{LogPrefix} Modified index.html to include split loader scripts");
        }
    }
}
#endif