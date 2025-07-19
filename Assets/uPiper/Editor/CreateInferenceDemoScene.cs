using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using uPiper.Demo;
using TMPro;
using UnityEngine.InputSystem.UI;

namespace uPiper.Editor
{
    /// <summary>
    /// Phase 1.9 デモシーンを作成するエディタスクリプト
    /// </summary>
    public static class CreateInferenceDemoScene
    {
        private const string ScenePath = "Assets/uPiper/Scenes/InferenceEngineDemo.unity";
        
        [MenuItem("uPiper/Demo/Open Inference Demo Scene")]
        public static void OpenDemoScene()
        {
            // シーンが存在しない場合は作成
            if (!System.IO.File.Exists(ScenePath))
            {
                CreateDemoScene();
                return;
            }
            
            // シーンを開く
            if (UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath);
                Debug.Log("Inference Demo シーンを開きました。Playモードで実行してください。");
            }
        }
        
        [MenuItem("uPiper/Demo/Create Inference Demo Scene")]
        public static void CreateDemoScene()
        {
            // 新しいシーンを作成
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects,
                UnityEditor.SceneManagement.NewSceneMode.Single
            );
            
            // Canvas を作成
            var canvasGO = new GameObject("Canvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
            
            // EventSystem を作成 (Input System対応)
            var eventSystemGO = new GameObject("EventSystem");
            eventSystemGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemGO.AddComponent<InputSystemUIInputModule>();
            
            // UI パネルを作成
            var panelGO = new GameObject("Panel");
            panelGO.transform.SetParent(canvasGO.transform, false);
            var panelImage = panelGO.AddComponent<Image>();
            panelImage.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
            var panelRect = panelGO.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.1f, 0.1f);
            panelRect.anchorMax = new Vector2(0.9f, 0.9f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            
            // タイトル
            var titleGO = new GameObject("Title");
            titleGO.transform.SetParent(panelGO.transform, false);
            var titleText = titleGO.AddComponent<TextMeshProUGUI>();
            titleText.text = "Piper TTS - Unity.InferenceEngine Demo";
            titleText.fontSize = 24;
            titleText.color = Color.white;
            titleText.alignment = TextAlignmentOptions.Center;
            // フォントはTMP Settingsのデフォルトを使用（プロジェクト側で日本語フォントを設定）
            var titleRect = titleGO.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.9f);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;
            
            // モデル選択ドロップダウン
            var modelLabelGO = new GameObject("ModelLabel");
            modelLabelGO.transform.SetParent(panelGO.transform, false);
            var modelLabel = modelLabelGO.AddComponent<TextMeshProUGUI>();
            modelLabel.text = "Model:";
            modelLabel.fontSize = 16;
            modelLabel.color = Color.white;
            var modelLabelRect = modelLabelGO.GetComponent<RectTransform>();
            modelLabelRect.anchorMin = new Vector2(0.1f, 0.7f);
            modelLabelRect.anchorMax = new Vector2(0.3f, 0.8f);
            modelLabelRect.offsetMin = Vector2.zero;
            modelLabelRect.offsetMax = Vector2.zero;
            
            var dropdownGO = new GameObject("ModelDropdown");
            dropdownGO.transform.SetParent(panelGO.transform, false);
            var dropdown = dropdownGO.AddComponent<TMP_Dropdown>();
            var dropdownRect = dropdownGO.GetComponent<RectTransform>();
            dropdownRect.anchorMin = new Vector2(0.3f, 0.7f);
            dropdownRect.anchorMax = new Vector2(0.9f, 0.8f);
            dropdownRect.offsetMin = Vector2.zero;
            dropdownRect.offsetMax = Vector2.zero;
            
            // 入力フィールド
            var inputLabelGO = new GameObject("InputLabel");
            inputLabelGO.transform.SetParent(panelGO.transform, false);
            var inputLabel = inputLabelGO.AddComponent<TextMeshProUGUI>();
            inputLabel.text = "Text:";
            inputLabel.fontSize = 16;
            inputLabel.color = Color.white;
            var inputLabelRect = inputLabelGO.GetComponent<RectTransform>();
            inputLabelRect.anchorMin = new Vector2(0.1f, 0.5f);
            inputLabelRect.anchorMax = new Vector2(0.3f, 0.6f);
            inputLabelRect.offsetMin = Vector2.zero;
            inputLabelRect.offsetMax = Vector2.zero;
            
            var inputGO = new GameObject("InputField", typeof(RectTransform));
            inputGO.transform.SetParent(panelGO.transform, false);
            var inputImage = inputGO.AddComponent<Image>();
            inputImage.color = Color.white;
            var inputField = inputGO.AddComponent<TMP_InputField>();
            var inputRect = inputGO.GetComponent<RectTransform>();
            inputRect.anchorMin = new Vector2(0.3f, 0.4f);
            inputRect.anchorMax = new Vector2(0.9f, 0.6f);
            inputRect.offsetMin = Vector2.zero;
            inputRect.offsetMax = Vector2.zero;
            
            // Text Area を作成
            var textAreaGO = new GameObject("Text Area", typeof(RectTransform));
            textAreaGO.transform.SetParent(inputGO.transform, false);
            var textAreaRect = textAreaGO.GetComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(10, 5);
            textAreaRect.offsetMax = new Vector2(-10, -5);
            
            // Placeholder
            var placeholderGO = new GameObject("Placeholder", typeof(RectTransform));
            placeholderGO.transform.SetParent(textAreaGO.transform, false);
            var placeholderText = placeholderGO.AddComponent<TextMeshProUGUI>();
            placeholderText.text = "Enter text...";
            placeholderText.fontSize = 14;
            placeholderText.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            var placeholderRect = placeholderGO.GetComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = Vector2.zero;
            placeholderRect.offsetMax = Vector2.zero;
            
            // Text
            var inputTextGO = new GameObject("Text", typeof(RectTransform));
            inputTextGO.transform.SetParent(textAreaGO.transform, false);
            var inputText = inputTextGO.AddComponent<TextMeshProUGUI>();
            inputText.fontSize = 14;
            inputText.color = Color.black;
            inputText.richText = false;
            var inputTextRect = inputTextGO.GetComponent<RectTransform>();
            inputTextRect.anchorMin = Vector2.zero;
            inputTextRect.anchorMax = Vector2.one;
            inputTextRect.offsetMin = Vector2.zero;
            inputTextRect.offsetMax = Vector2.zero;
            
            // InputField の設定
            inputField.targetGraphic = inputImage;
            inputField.textViewport = textAreaRect;
            inputField.textComponent = inputText;
            inputField.placeholder = placeholderText;
            
            // 生成ボタン
            var buttonGO = new GameObject("GenerateButton");
            buttonGO.transform.SetParent(panelGO.transform, false);
            var button = buttonGO.AddComponent<Button>();
            var buttonImage = buttonGO.AddComponent<Image>();
            buttonImage.color = new Color(0.3f, 0.6f, 0.9f);
            button.targetGraphic = buttonImage;
            var buttonRect = buttonGO.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.3f, 0.2f);
            buttonRect.anchorMax = new Vector2(0.7f, 0.3f);
            buttonRect.offsetMin = Vector2.zero;
            buttonRect.offsetMax = Vector2.zero;
            
            var buttonTextGO = new GameObject("Text");
            buttonTextGO.transform.SetParent(buttonGO.transform, false);
            var buttonText = buttonTextGO.AddComponent<TextMeshProUGUI>();
            buttonText.text = "Generate";
            buttonText.fontSize = 18;
            buttonText.color = Color.white;
            buttonText.alignment = TextAlignmentOptions.Center;
            var buttonTextRect = buttonTextGO.GetComponent<RectTransform>();
            buttonTextRect.anchorMin = Vector2.zero;
            buttonTextRect.anchorMax = Vector2.one;
            buttonTextRect.offsetMin = Vector2.zero;
            buttonTextRect.offsetMax = Vector2.zero;
            
            // ステータステキスト
            var statusGO = new GameObject("StatusText");
            statusGO.transform.SetParent(panelGO.transform, false);
            var statusText = statusGO.AddComponent<TextMeshProUGUI>();
            statusText.text = "Ready";
            statusText.fontSize = 14;
            statusText.color = Color.yellow;
            statusText.alignment = TextAlignmentOptions.Center;
            var statusRect = statusGO.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0.1f, 0.05f);
            statusRect.anchorMax = new Vector2(0.9f, 0.15f);
            statusRect.offsetMin = Vector2.zero;
            statusRect.offsetMax = Vector2.zero;
            
            // デモコントローラーを作成
            var controllerGO = new GameObject("InferenceEngineDemo");
            var audioSource = controllerGO.AddComponent<AudioSource>();
            var demo = controllerGO.AddComponent<uPiper.Demo.InferenceEngineDemo>();
            
            // UI参照を設定
            var serializedObject = new SerializedObject(demo);
            serializedObject.FindProperty("_inputField").objectReferenceValue = inputField;
            serializedObject.FindProperty("_generateButton").objectReferenceValue = button;
            serializedObject.FindProperty("_statusText").objectReferenceValue = statusText;
            serializedObject.FindProperty("_audioSource").objectReferenceValue = audioSource;
            serializedObject.FindProperty("_modelDropdown").objectReferenceValue = dropdown;
            serializedObject.ApplyModifiedProperties();
            
            // シーンを保存
            System.IO.Directory.CreateDirectory("Assets/uPiper/Scenes");
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, ScenePath);
            
            Debug.Log($"デモシーンを作成しました: {ScenePath}");
            EditorUtility.DisplayDialog("完了", 
                $"デモシーンを作成しました。\n{ScenePath}\n\nPlayモードで実行してください。", 
                "OK");
        }
    }
}