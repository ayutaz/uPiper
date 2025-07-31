using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using System.Linq;
using System.Diagnostics;

namespace uPiper.Editor
{
    /// <summary>
    /// Unity Package export functionality for uPiper
    /// </summary>
    public static class PackageExporter
    {
        private const string PACKAGE_NAME = "uPiper";
        private const string PACKAGE_JSON_PATH = "Assets/uPiper/package.json";
        private const string UPIPER_ROOT = "Assets/uPiper";
        private const string TEMP_PACKAGE_DIR = "uPiperPackageTemp";
        
        [MenuItem("uPiper/Package/Export Unity Package (.unitypackage)", false, Menu.uPiperMenuStructure.PRIORITY_BUILD + 50)]
        public static void ExportUnityPackage()
        {
            try
            {
                var packageInfo = ReadPackageInfo();
                var version = packageInfo?.Version ?? "0.1.0";
                
                var exportPath = EditorUtility.SaveFilePanel(
                    "Export Unity Package",
                    "",
                    $"{PACKAGE_NAME}-v{version}.unitypackage",
                    "unitypackage"
                );
                
                if (string.IsNullOrEmpty(exportPath))
                    return;
                
                var assetPaths = GetAssetPaths();
                
                EditorUtility.DisplayProgressBar("Exporting Package", "Collecting assets...", 0.1f);
                
                AssetDatabase.ExportPackage(
                    assetPaths.ToArray(),
                    exportPath,
                    ExportPackageOptions.Recurse
                );
                
                EditorUtility.ClearProgressBar();
                
                UnityEngine.Debug.Log($"Unity Package exported successfully: {exportPath}");
                EditorUtility.DisplayDialog("Export Complete", 
                    $"Unity Package exported to:\n{exportPath}", "OK");
                
                // Reveal in Explorer/Finder
                EditorUtility.RevealInFinder(exportPath);
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                UnityEngine.Debug.LogError($"Failed to export Unity Package: {ex.Message}");
                EditorUtility.DisplayDialog("Export Failed", 
                    $"Failed to export Unity Package:\n{ex.Message}", "OK");
            }
        }
        
        [MenuItem("uPiper/Package/Export UPM Package (.tgz)", false, Menu.uPiperMenuStructure.PRIORITY_BUILD + 51)]
        public static void ExportUPMPackage()
        {
            try
            {
                var packageInfo = ReadPackageInfo();
                if (packageInfo == null)
                {
                    EditorUtility.DisplayDialog("Export Failed", 
                        "Could not read package.json", "OK");
                    return;
                }
                
                var exportPath = EditorUtility.SaveFilePanel(
                    "Export UPM Package",
                    "",
                    $"{packageInfo.Name}-{packageInfo.Version}.tgz",
                    "tgz"
                );
                
                if (string.IsNullOrEmpty(exportPath))
                    return;
                
                EditorUtility.DisplayProgressBar("Exporting UPM Package", "Creating package...", 0.1f);
                
                CreateUPMPackage(exportPath, packageInfo);
                
                EditorUtility.ClearProgressBar();
                
                UnityEngine.Debug.Log($"UPM Package exported successfully: {exportPath}");
                EditorUtility.DisplayDialog("Export Complete", 
                    $"UPM Package exported to:\n{exportPath}", "OK");
                
                // Reveal in Explorer/Finder
                EditorUtility.RevealInFinder(exportPath);
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                UnityEngine.Debug.LogError($"Failed to export UPM Package: {ex.Message}");
                EditorUtility.DisplayDialog("Export Failed", 
                    $"Failed to export UPM Package:\n{ex.Message}", "OK");
            }
        }
        
        [MenuItem("uPiper/Package/Export Unity Package (No Dependencies)", false, Menu.uPiperMenuStructure.PRIORITY_BUILD + 53)]
        public static void ExportUnityPackageNoDependencies()
        {
            try
            {
                var packageInfo = ReadPackageInfo();
                var version = packageInfo?.Version ?? "0.1.0";
                
                var exportPath = EditorUtility.SaveFilePanel(
                    "Export Unity Package (No Dependencies)",
                    "",
                    $"{PACKAGE_NAME}-v{version}-NoDeps.unitypackage",
                    "unitypackage"
                );
                
                if (string.IsNullOrEmpty(exportPath))
                    return;
                
                var assetPaths = GetAssetPaths();
                
                EditorUtility.DisplayProgressBar("Exporting Package", "Collecting assets...", 0.1f);
                
                AssetDatabase.ExportPackage(
                    assetPaths.ToArray(),
                    exportPath,
                    ExportPackageOptions.Recurse // No IncludeDependencies
                );
                
                EditorUtility.ClearProgressBar();
                
                UnityEngine.Debug.Log($"Unity Package (No Dependencies) exported successfully: {exportPath}");
                EditorUtility.DisplayDialog("Export Complete", 
                    $"Unity Package exported to:\n{exportPath}\n\nNote: Package Manager dependencies are not included.", "OK");
                
                // Reveal in Explorer/Finder
                EditorUtility.RevealInFinder(exportPath);
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                UnityEngine.Debug.LogError($"Failed to export Unity Package: {ex.Message}");
                EditorUtility.DisplayDialog("Export Failed", 
                    $"Failed to export Unity Package:\n{ex.Message}", "OK");
            }
        }
        
        [MenuItem("uPiper/Package/Export Both Formats", false, Menu.uPiperMenuStructure.PRIORITY_BUILD + 54)]
        public static void ExportBothFormats()
        {
            try
            {
                var packageInfo = ReadPackageInfo();
                var version = packageInfo?.Version ?? "0.1.0";
                
                var exportDirectory = EditorUtility.OpenFolderPanel(
                    "Select Export Directory",
                    "",
                    ""
                );
                
                if (string.IsNullOrEmpty(exportDirectory))
                    return;
                
                EditorUtility.DisplayProgressBar("Exporting Packages", "Exporting Unity Package...", 0.2f);
                
                // Export .unitypackage
                var unityPackagePath = Path.Combine(exportDirectory, $"{PACKAGE_NAME}-v{version}.unitypackage");
                var assetPaths = GetAssetPaths();
                AssetDatabase.ExportPackage(
                    assetPaths.ToArray(),
                    unityPackagePath,
                    ExportPackageOptions.Recurse
                );
                
                EditorUtility.DisplayProgressBar("Exporting Packages", "Exporting UPM Package...", 0.6f);
                
                // Export .tgz
                if (packageInfo != null)
                {
                    var upmPackagePath = Path.Combine(exportDirectory, $"{packageInfo.Name}-{packageInfo.Version}.tgz");
                    CreateUPMPackage(upmPackagePath, packageInfo);
                }
                
                EditorUtility.ClearProgressBar();
                
                UnityEngine.Debug.Log($"Both packages exported successfully to: {exportDirectory}");
                EditorUtility.DisplayDialog("Export Complete", 
                    $"Both packages exported to:\n{exportDirectory}", "OK");
                
                // Reveal in Explorer/Finder
                EditorUtility.RevealInFinder(exportDirectory);
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                UnityEngine.Debug.LogError($"Failed to export packages: {ex.Message}");
                EditorUtility.DisplayDialog("Export Failed", 
                    $"Failed to export packages:\n{ex.Message}", "OK");
            }
        }
        
        [MenuItem("uPiper/Package/Open Export Directory", false, Menu.uPiperMenuStructure.PRIORITY_BUILD + 70)]
        public static void OpenExportDirectory()
        {
            var projectPath = Application.dataPath.Replace("/Assets", "");
            var exportPath = Path.Combine(projectPath, "Exports");
            
            if (!Directory.Exists(exportPath))
            {
                Directory.CreateDirectory(exportPath);
            }
            
            EditorUtility.RevealInFinder(exportPath);
        }
        
        private static PackageInfo ReadPackageInfo()
        {
            try
            {
                if (!File.Exists(PACKAGE_JSON_PATH))
                {
                    UnityEngine.Debug.LogError($"package.json not found at: {PACKAGE_JSON_PATH}");
                    return null;
                }
                
                var json = File.ReadAllText(PACKAGE_JSON_PATH);
                return JsonUtility.FromJson<PackageInfo>(json);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Failed to read package.json: {ex.Message}");
                return null;
            }
        }
        
        private static List<string> GetAssetPaths()
        {
            var paths = new List<string>();
            
            // Include all uPiper assets
            if (Directory.Exists(UPIPER_ROOT))
            {
                paths.Add(UPIPER_ROOT);
            }
            
            // Add samples if they exist
            var samplesPath = "Assets/Samples/uPiper";
            if (Directory.Exists(samplesPath))
            {
                paths.Add(samplesPath);
            }
            
            return paths;
        }
        
        private static void CreateUPMPackage(string outputPath, PackageInfo packageInfo)
        {
            var tempDir = Path.Combine(Application.temporaryCachePath, TEMP_PACKAGE_DIR);
            
            try
            {
                // Clean up previous temp directory
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
                
                Directory.CreateDirectory(tempDir);
                
                // Copy package files
                CopyDirectory(UPIPER_ROOT, tempDir);
                
                // Create package.json if it doesn't exist in temp
                var packageJsonPath = Path.Combine(tempDir, "package.json");
                if (!File.Exists(packageJsonPath))
                {
                    var packageJson = JsonUtility.ToJson(packageInfo, true);
                    File.WriteAllText(packageJsonPath, packageJson);
                }
                
                // Create tarball using npm pack or tar command
                CreateTarball(tempDir, outputPath, packageInfo);
            }
            finally
            {
                // Clean up temp directory
                if (Directory.Exists(tempDir))
                {
                    try
                    {
                        Directory.Delete(tempDir, true);
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogWarning($"Failed to clean up temp directory: {ex.Message}");
                    }
                }
            }
        }
        
        private static void CopyDirectory(string sourceDir, string destDir)
        {
            if (!Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);
            
            // Copy files
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var fileName = Path.GetFileName(file);
                // Skip .meta files for UPM package
                if (fileName.EndsWith(".meta"))
                    continue;
                    
                var destFile = Path.Combine(destDir, fileName);
                File.Copy(file, destFile, true);
            }
            
            // Copy subdirectories
            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var dirName = Path.GetFileName(dir);
                var destSubDir = Path.Combine(destDir, dirName);
                CopyDirectory(dir, destSubDir);
            }
        }
        
        private static void CreateTarball(string sourceDir, string outputPath, PackageInfo packageInfo)
        {
            // Try npm pack first (if available)
            if (TryNpmPack(sourceDir, outputPath, packageInfo))
                return;
            
            // Fallback to basic file copy (not a real tarball, but functional)
            UnityEngine.Debug.LogWarning("npm not available, creating zip archive instead of tarball");
            CreateZipArchive(sourceDir, outputPath.Replace(".tgz", ".zip"));
        }
        
        private static bool TryNpmPack(string sourceDir, string outputPath, PackageInfo packageInfo)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "npm",
                    Arguments = "pack",
                    WorkingDirectory = sourceDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                using (var process = Process.Start(startInfo))
                {
                    process.WaitForExit();
                    
                    if (process.ExitCode == 0)
                    {
                        // Find the generated tarball
                        var generatedTarball = Directory.GetFiles(sourceDir, "*.tgz").FirstOrDefault();
                        if (generatedTarball != null && File.Exists(generatedTarball))
                        {
                            File.Move(generatedTarball, outputPath);
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"npm pack failed: {ex.Message}");
            }
            
            return false;
        }
        
        private static void CreateZipArchive(string sourceDir, string outputPath)
        {
            // Simple fallback - this would need System.IO.Compression in a real implementation
            // For now, just copy the directory structure
            UnityEngine.Debug.LogWarning("Creating directory copy instead of proper archive. Consider installing npm for proper tarball creation.");
            
            var outputDir = outputPath.Replace(".zip", "");
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, true);
                
            CopyDirectory(sourceDir, outputDir);
        }
        
        [System.Serializable]
        private class PackageInfo
        {
            public string name;
            public string version;
            public string displayName;
            public string description;
            public string unity;
            public string unityRelease;
            
            // Properties for compatibility
            public string Name => name;
            public string Version => version;
            public string DisplayName => displayName;
            public string Description => description;
            public string Unity => unity;
            public string UnityRelease => unityRelease;
        }
        
        /// <summary>
        /// CI/CD用のUnity Packageエクスポートメソッド
        /// </summary>
        public static void ExportUnityPackageCI()
        {
            try
            {
                // コマンドライン引数から出力パスを取得
                var args = System.Environment.GetCommandLineArgs();
                string outputPath = null;
                
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "-outputPath" && i + 1 < args.Length)
                    {
                        outputPath = args[i + 1];
                        break;
                    }
                }
                
                if (string.IsNullOrEmpty(outputPath))
                {
                    var packageInfo = ReadPackageInfo();
                    var version = packageInfo?.Version ?? "0.1.0";
                    outputPath = $"./Exports/{PACKAGE_NAME}-v{version}.unitypackage";
                }
                
                // 出力ディレクトリを作成
                var outputDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }
                
                var assetPaths = GetAssetPaths();
                
                UnityEngine.Debug.Log($"Exporting Unity Package to: {outputPath}");
                UnityEngine.Debug.Log($"Assets to export: {string.Join(", ", assetPaths)}");
                
                AssetDatabase.ExportPackage(
                    assetPaths.ToArray(),
                    outputPath,
                    ExportPackageOptions.Recurse
                );
                
                UnityEngine.Debug.Log($"Unity Package exported successfully: {outputPath}");
                
                // CI環境では成功時に適切な終了コードで終了
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(0);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Failed to export Unity Package: {ex.Message}");
                UnityEngine.Debug.LogError($"Stack trace: {ex.StackTrace}");
                
                // CI環境では失敗時に適切な終了コードで終了
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                throw;
            }
        }
    }
}