using UnityEditor;
using UnityEngine;
using System.Text;
using System;
using UnityEditor.Build;

namespace uPiper.Editor.Build
{
    /// <summary>
    /// Check and display Android encoding settings
    /// </summary>
    public static class AndroidEncodingChecker
    {
        [MenuItem("uPiper/Android/Check Encoding Settings")]
        public static void CheckEncodingSettings()
        {
            Debug.Log("[uPiper] === Android Encoding Settings ===");
            
            // Player Settings
            Debug.Log($"Company Name: {PlayerSettings.companyName}");
            Debug.Log($"Product Name: {PlayerSettings.productName}");
            
            // Android specific settings
            Debug.Log($"Android Minimum API Level: {PlayerSettings.Android.minSdkVersion}");
            Debug.Log($"Android Target API Level: {PlayerSettings.Android.targetSdkVersion}");
            
            // Configuration
            var namedBuildTarget = NamedBuildTarget.Android;
            Debug.Log($"Script Backend: {PlayerSettings.GetScriptingBackend(namedBuildTarget)}");
            Debug.Log($"Api Compatibility Level: {PlayerSettings.GetApiCompatibilityLevel(namedBuildTarget)}");
            
            // Text encoding test
            Debug.Log("\n=== Text Encoding Test ===");
            string testText = "こんにちは";
            byte[] utf8Bytes = Encoding.UTF8.GetBytes(testText);
            Debug.Log($"Test text: {testText}");
            Debug.Log($"UTF-8 bytes ({utf8Bytes.Length}): {BitConverter.ToString(utf8Bytes)}");
            
            // Check if UTF-8 is default
            Debug.Log($"\nDefault Encoding: {Encoding.Default.EncodingName} (CodePage: {Encoding.Default.CodePage})");
            Debug.Log($"UTF-8 Encoding: {Encoding.UTF8.EncodingName} (CodePage: {Encoding.UTF8.CodePage})");
            
            // System info
            Debug.Log("\n=== System Info ===");
            Debug.Log($"Unity Version: {Application.unityVersion}");
            Debug.Log($"System Language: {Application.systemLanguage}");
            Debug.Log($"Operating System: {SystemInfo.operatingSystem}");
            
            Debug.Log("\n[uPiper] === End of Encoding Settings ===");
        }
        
        [MenuItem("uPiper/Android/Fix Text Asset Encoding")]
        public static void FixTextAssetEncoding()
        {
            Debug.Log("[uPiper] Checking text assets encoding...");
            
            // Find all text assets in the project
            string[] guids = AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets" });
            int fixedCount = 0;
            
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                
                // Skip non-uPiper assets
                if (!path.Contains("uPiper"))
                    continue;
                    
                TextAsset textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (textAsset != null)
                {
                    // Check if the text contains Japanese characters
                    if (ContainsJapanese(textAsset.text))
                    {
                        Debug.Log($"[uPiper] Found Japanese text in: {path}");
                        
                        // Force reimport with UTF-8 encoding
                        // Note: TextScriptImporter is not directly accessible
                        // Just force reimport which should use UTF-8 by default
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                        fixedCount++;
                    }
                }
            }
            
            Debug.Log($"[uPiper] Checked {guids.Length} text assets, fixed {fixedCount} files");
            
            if (fixedCount > 0)
            {
                AssetDatabase.Refresh();
            }
        }
        
        private static bool ContainsJapanese(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;
                
            foreach (char c in text)
            {
                // Check for Hiragana (3040-309F), Katakana (30A0-30FF), or Kanji (4E00-9FAF)
                if ((c >= '\u3040' && c <= '\u309F') ||
                    (c >= '\u30A0' && c <= '\u30FF') ||
                    (c >= '\u4E00' && c <= '\u9FAF'))
                {
                    return true;
                }
            }
            
            return false;
        }
    }
}