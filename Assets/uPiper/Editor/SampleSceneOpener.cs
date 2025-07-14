using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

namespace uPiper.Editor
{
    /// <summary>
    /// Samples~ディレクトリ内のシーンを開くためのメニュー
    /// </summary>
    public static class SampleSceneOpener
    {
        private const string WEBGL_DEMO_SCENE_PATH = "Assets/uPiper/Samples~/WebGLDemo/WebGLDemoScene.unity";
        
        [MenuItem("uPiper/Samples/Open WebGL Demo Scene")]
        public static void OpenWebGLDemoScene()
        {
            if (File.Exists(WEBGL_DEMO_SCENE_PATH))
            {
                // 現在のシーンに変更がある場合は保存を促す
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(WEBGL_DEMO_SCENE_PATH);
                    Debug.Log($"[uPiper] Opened WebGL Demo Scene from: {WEBGL_DEMO_SCENE_PATH}");
                }
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "Scene Not Found",
                    $"WebGL Demo Scene not found at:\n{WEBGL_DEMO_SCENE_PATH}\n\n" +
                    "The scene file may not exist yet.",
                    "OK"
                );
            }
        }
        
        [MenuItem("uPiper/Samples/Copy Samples to Assets")]
        public static void CopySamplesToAssets()
        {
            string sourcePath = "Assets/uPiper/Samples~";
            string targetPath = "Assets/Samples/uPiper";
            
            if (!Directory.Exists(sourcePath))
            {
                EditorUtility.DisplayDialog(
                    "Samples Not Found",
                    $"Samples directory not found at:\n{sourcePath}",
                    "OK"
                );
                return;
            }
            
            // ターゲットディレクトリを作成
            if (!Directory.Exists(targetPath))
            {
                Directory.CreateDirectory(targetPath);
            }
            
            // WebGLDemoをコピー
            string sourceDemo = Path.Combine(sourcePath, "WebGLDemo");
            string targetDemo = Path.Combine(targetPath, "WebGLDemo");
            
            if (Directory.Exists(sourceDemo))
            {
                CopyDirectory(sourceDemo, targetDemo);
                
                // メタファイルをリフレッシュ
                AssetDatabase.Refresh();
                
                EditorUtility.DisplayDialog(
                    "Samples Copied",
                    $"Samples have been copied to:\n{targetPath}\n\n" +
                    "You can now access them from the Project window.",
                    "OK"
                );
                
                // コピーしたシーンを開く
                string copiedScenePath = Path.Combine(targetDemo, "WebGLDemoScene.unity");
                if (File.Exists(copiedScenePath))
                {
                    EditorSceneManager.OpenScene(copiedScenePath);
                }
            }
        }
        
        [MenuItem("uPiper/Samples/Add WebGL Demo to Build Settings")]
        public static void AddWebGLDemoToBuildSettings()
        {
            var currentScenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(
                EditorBuildSettings.scenes
            );
            
            // すでに追加されているかチェック
            bool alreadyAdded = false;
            foreach (var scene in currentScenes)
            {
                if (scene.path == WEBGL_DEMO_SCENE_PATH || 
                    scene.path.EndsWith("WebGLDemoScene.unity"))
                {
                    alreadyAdded = true;
                    break;
                }
            }
            
            if (!alreadyAdded)
            {
                // Samples~内のシーンを追加
                if (File.Exists(WEBGL_DEMO_SCENE_PATH))
                {
                    currentScenes.Add(new EditorBuildSettingsScene(WEBGL_DEMO_SCENE_PATH, true));
                }
                
                // コピーされたシーンも探す
                string copiedPath = "Assets/Samples/uPiper/WebGLDemo/WebGLDemoScene.unity";
                if (File.Exists(copiedPath))
                {
                    currentScenes.Add(new EditorBuildSettingsScene(copiedPath, true));
                }
                
                EditorBuildSettings.scenes = currentScenes.ToArray();
                
                EditorUtility.DisplayDialog(
                    "Build Settings Updated",
                    "WebGL Demo Scene has been added to Build Settings.",
                    "OK"
                );
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "Already Added",
                    "WebGL Demo Scene is already in Build Settings.",
                    "OK"
                );
            }
        }
        
        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            // ディレクトリを作成
            Directory.CreateDirectory(targetDir);
            
            // ファイルをコピー
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(file);
                // .metaファイルはUnityが自動生成するのでスキップ
                if (!fileName.EndsWith(".meta"))
                {
                    string destFile = Path.Combine(targetDir, fileName);
                    File.Copy(file, destFile, true);
                }
            }
            
            // サブディレクトリを再帰的にコピー
            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string dirName = Path.GetFileName(subDir);
                string destDir = Path.Combine(targetDir, dirName);
                CopyDirectory(subDir, destDir);
            }
        }
    }
}