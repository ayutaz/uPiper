using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

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
            // 現在のシーンに変更がある場合は保存を促す
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            // Tempディレクトリにコピーして開く（差分を避けるため）
            string tempPath = "Assets/Temp/WebGLDemoScene_Temp.unity";

            if (File.Exists(WEBGL_DEMO_SCENE_PATH))
            {
                // Tempディレクトリを作成
                string tempDir = Path.GetDirectoryName(tempPath);
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);

                    // Temp.metaファイルを作成
                    string metaPath = tempDir + ".meta";
                    File.WriteAllText(metaPath,
                        "fileFormatVersion: 2\n" +
                        "guid: " + System.Guid.NewGuid().ToString("N") + "\n" +
                        "folderAsset: yes\n" +
                        "DefaultImporter:\n" +
                        "  externalObjects: {}\n" +
                        "  userData: \n" +
                        "  assetBundleName: \n" +
                        "  assetBundleVariant: \n");
                }

                // シーンをコピー
                File.Copy(WEBGL_DEMO_SCENE_PATH, tempPath, true);

                // Tempフォルダを.gitignoreに追加されていることを確認
                AddToGitIgnore("Assets/Temp/");

                // メタファイルをリフレッシュ
                AssetDatabase.Refresh();

                // コピーしたシーンを開く
                EditorSceneManager.OpenScene(tempPath);
                Debug.Log($"[uPiper] Opened WebGL Demo Scene (temporary copy)");

                // シーンにセットアップコンポーネントを追加
                SetupDemoScene();

                // Build Settingsへの追加は手動で行うように案内
                if (!IsSceneInBuildSettings(WEBGL_DEMO_SCENE_PATH))
                {
                    EditorUtility.DisplayDialog(
                        "Build Settings",
                        "WebGL Demo Scene is not in Build Settings.\n\n" +
                        "To build with this scene:\n" +
                        "1. File > Build Settings\n" +
                        "2. Add Open Scenes\n" +
                        "3. Or use 'uPiper > Build > Configure Build Settings'",
                        "OK"
                    );
                }
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "Scene Not Found",
                    $"WebGL Demo Scene not found at:\n{WEBGL_DEMO_SCENE_PATH}",
                    "OK"
                );
            }
        }

        private static void AddToGitIgnore(string path)
        {
            string gitignorePath = ".gitignore";
            if (File.Exists(gitignorePath))
            {
                string content = File.ReadAllText(gitignorePath);
                if (!content.Contains(path))
                {
                    File.AppendAllText(gitignorePath, $"\n# Temporary demo scenes\n{path}\n");
                    Debug.Log($"[uPiper] Added {path} to .gitignore");
                }
            }
        }

        private static void SetupDemoScene()
        {
            Debug.Log("[uPiper] Setting up WebGL demo scene UI...");

            // Canvas を作成
            GameObject canvasObj = GameObject.Find("Canvas");
            if (canvasObj == null)
            {
                canvasObj = new GameObject("Canvas");
                var canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }

            // EventSystem を作成
            if (GameObject.Find("EventSystem") == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // Demo Info Panel を作成
            GameObject infoPanel = new GameObject("DemoInfoPanel");
            infoPanel.transform.SetParent(canvasObj.transform, false);

            var rectTransform = infoPanel.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = new Vector2(600, 400);
            rectTransform.anchoredPosition = Vector2.zero;

            var image = infoPanel.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

            // Info Text (TextMeshProを使用)
            GameObject textObj = new GameObject("InfoText");
            textObj.transform.SetParent(infoPanel.transform, false);

            // TextMeshProUGUIを動的に作成
            var textType = System.Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
            if (textType != null)
            {
                var text = textObj.AddComponent(textType);

                // リフレクションでプロパティを設定
                var textProp = textType.GetProperty("text");
                textProp?.SetValue(text, "uPiper WebGL Demo\n\n" +
                           "このシーンはWebGLデモ用のシーンです。\n\n" +
                           "WebGLはUnity.InferenceEngineがサポートしていないため、\n" +
                           "現在は UI レイアウトのプレビューのみ表示されています。");

                var fontSizeProp = textType.GetProperty("fontSize");
                fontSizeProp?.SetValue(text, 24f);

                var alignmentProp = textType.GetProperty("alignment");
                if (alignmentProp != null)
                {
                    // TextAlignmentOptions.Center の値を設定
                    var alignmentType = System.Type.GetType("TMPro.TextAlignmentOptions, Unity.TextMeshPro");
                    if (alignmentType != null)
                    {
                        var centerValue = System.Enum.Parse(alignmentType, "Center");
                        alignmentProp.SetValue(text, centerValue);
                    }
                }

                var colorProp = textType.GetProperty("color");
                colorProp?.SetValue(text, Color.white);
            }
            else
            {
                // TextMeshProが利用できない場合はLegacy Textを使用
                var text = textObj.AddComponent<UnityEngine.UI.Text>();
                text.text = "uPiper WebGL Demo\n\n" +
                           "このシーンはWebGLデモ用のシーンです。\n\n" +
                           "WebGLはUnity.InferenceEngineがサポートしていないため、\n" +
                           "現在は UI レイアウトのプレビューのみ表示されています。";
                text.fontSize = 24;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = Color.white;

                Debug.LogWarning("[uPiper] TextMeshPro not found. Using legacy Text component.");
            }

            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = new Vector2(-40, -40);
            textRect.anchoredPosition = Vector2.zero;

            Debug.Log("[uPiper] Demo scene UI setup completed");
        }

        private static bool IsSceneInBuildSettings(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes;
            return System.Array.Exists(scenes, s => s.path == scenePath);
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            // すでに追加されているかチェック
            bool alreadyAdded = scenes.Exists(s => s.path == scenePath);

            if (!alreadyAdded)
            {
                scenes.Add(new EditorBuildSettingsScene(scenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
                Debug.Log($"[uPiper] Added {scenePath} to Build Settings");
            }
        }

        [MenuItem("uPiper/Samples/Copy All Samples to Assets")]
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

        [MenuItem("uPiper/Samples/Add All Scenes to Build Settings")]
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