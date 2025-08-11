#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace uPiper.Editor
{
    /// <summary>
    /// WebGLビルド後に大容量ファイルを分割してGitHub Pagesデプロイ可能にする
    /// </summary>
    public static class WebGLSplitDataProcessor
    {
        private const long MAX_FILE_SIZE = 90 * 1024 * 1024; // 90MB per chunk
        
        [PostProcessBuild(999)]
        public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.WebGL) return;
            
            Debug.Log("[WebGLSplitDataProcessor] Processing WebGL build for GitHub Pages deployment...");
            
            // Process data file
            ProcessDataFile(pathToBuiltProject);
            
            // Add split loader
            AddSplitLoader(pathToBuiltProject);
            
            // Modify index.html
            ModifyIndexHtml(pathToBuiltProject);
            
            Debug.Log("[WebGLSplitDataProcessor] WebGL build processing complete!");
        }
        
        private static void ProcessDataFile(string buildPath)
        {
            string dataPath = Path.Combine(buildPath, "Build", "Web.data.gz");
            
            if (!File.Exists(dataPath))
            {
                Debug.LogWarning($"[WebGLSplitDataProcessor] Data file not found: {dataPath}");
                return;
            }
            
            FileInfo fileInfo = new FileInfo(dataPath);
            long fileSize = fileInfo.Length;
            
            Debug.Log($"[WebGLSplitDataProcessor] Data file size: {fileSize / 1024 / 1024}MB");
            
            if (fileSize > 100 * 1024 * 1024) // > 100MB
            {
                Debug.Log("[WebGLSplitDataProcessor] File exceeds 100MB, splitting...");
                SplitFile(dataPath);
            }
        }
        
        private static void SplitFile(string filePath)
        {
            byte[] fileBytes = File.ReadAllBytes(filePath);
            int chunks = (int)((fileBytes.Length + MAX_FILE_SIZE - 1) / MAX_FILE_SIZE);
            
            for (int i = 0; i < chunks; i++)
            {
                long offset = i * MAX_FILE_SIZE;
                long length = System.Math.Min(MAX_FILE_SIZE, fileBytes.Length - offset);
                
                string chunkPath = $"{filePath}.part{(char)('a' + (i / 26))}{(char)('a' + (i % 26))}";
                
                using (FileStream fs = new FileStream(chunkPath, FileMode.Create))
                {
                    fs.Write(fileBytes, (int)offset, (int)length);
                }
                
                Debug.Log($"[WebGLSplitDataProcessor] Created chunk: {Path.GetFileName(chunkPath)} ({length / 1024 / 1024}MB)");
            }
        }
        
        private static void AddSplitLoader(string buildPath)
        {
            string loaderPath = Path.Combine(buildPath, "split-loader.js");
            string loaderContent = @"// Split file loader for GitHub Pages deployment
(function() {
    console.log('[SplitLoader] Initializing split data loader...');
    
    // Store original fetch
    const originalFetch = window.fetch;
    
    // Override fetch to intercept Unity data file requests
    window.fetch = async function(...args) {
        const url = args[0];
        
        // Check if this is the Unity data file request
        if (typeof url === 'string' && url.includes('Web.data.gz') && !url.includes('.part')) {
            console.log('[SplitLoader] Intercepted Unity data request:', url);
            
            // Check if split files exist
            try {
                // Try to load the first part to check if splits exist
                const testResponse = await originalFetch('Build/Web.data.gz.partaa');
                if (!testResponse.ok) {
                    // Split files don't exist, use original file
                    console.log('[SplitLoader] Split files not found, using original');
                    return originalFetch.apply(this, args);
                }
            } catch (e) {
                // Split files don't exist, use original file
                console.log('[SplitLoader] Split files not found, using original');
                return originalFetch.apply(this, args);
            }
            
            // Load split files
            const parts = [
                'Build/Web.data.gz.partaa',
                'Build/Web.data.gz.partab'
            ];
            
            const chunks = [];
            
            for (const part of parts) {
                console.log(`[SplitLoader] Loading ${part}...`);
                const response = await originalFetch(part);
                if (!response.ok) {
                    console.error(`[SplitLoader] Failed to load ${part}: ${response.status}`);
                    // Fall back to original file
                    return originalFetch.apply(this, args);
                }
                const arrayBuffer = await response.arrayBuffer();
                chunks.push(new Uint8Array(arrayBuffer));
                console.log(`[SplitLoader] Loaded ${part}: ${arrayBuffer.byteLength} bytes`);
            }
            
            // Combine chunks
            const totalLength = chunks.reduce((sum, chunk) => sum + chunk.length, 0);
            const combined = new Uint8Array(totalLength);
            let offset = 0;
            
            for (const chunk of chunks) {
                combined.set(chunk, offset);
                offset += chunk.length;
            }
            
            console.log(`[SplitLoader] Combined ${totalLength} bytes (${(totalLength / 1024 / 1024).toFixed(2)} MB)`);
            
            // Create a Response object that mimics the original file
            const blob = new Blob([combined], { type: 'application/gzip' });
            const response = new Response(blob, {
                status: 200,
                statusText: 'OK',
                headers: {
                    'Content-Type': 'application/gzip',
                    'Content-Length': totalLength.toString()
                }
            });
            
            console.log('[SplitLoader] Returning combined data as response');
            return response;
        }
        
        // For all other requests, use original fetch
        return originalFetch.apply(this, args);
    };
    
    console.log('[SplitLoader] Fetch interceptor installed');
})();";
            
            File.WriteAllText(loaderPath, loaderContent);
            Debug.Log($"[WebGLSplitDataProcessor] Created split loader: {loaderPath}");
        }
        
        private static void ModifyIndexHtml(string buildPath)
        {
            string indexPath = Path.Combine(buildPath, "index.html");
            
            if (!File.Exists(indexPath))
            {
                Debug.LogWarning($"[WebGLSplitDataProcessor] index.html not found: {indexPath}");
                return;
            }
            
            string content = File.ReadAllText(indexPath);
            
            // Check if already modified
            if (content.Contains("split-loader.js"))
            {
                Debug.Log("[WebGLSplitDataProcessor] index.html already contains split loader");
                return;
            }
            
            // Add split loader script before Unity loader
            string searchFor = "var buildUrl = \"Build\";";
            string replacement = @"// GitHub Pages deployment support
      var isProduction = window.location.hostname.includes('github.io') || 
                        (window.location.protocol === 'https:' && !window.location.hostname.includes('localhost'));
      
      if (isProduction) {
        console.log('[Unity] Production environment detected, loading split data loader...');
        var splitScript = document.createElement('script');
        splitScript.src = 'split-loader.js';
        document.head.appendChild(splitScript);
      }
      
      var buildUrl = ""Build"";";
            
            content = content.Replace(searchFor, replacement);
            
            File.WriteAllText(indexPath, content);
            Debug.Log("[WebGLSplitDataProcessor] Modified index.html to include split loader");
        }
    }
}
#endif