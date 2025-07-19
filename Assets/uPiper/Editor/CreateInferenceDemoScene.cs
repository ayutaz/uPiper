using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using uPiper.Demo;

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
            var dropdownImage = dropdownGO.AddComponent<Image>();
            dropdownImage.color = new Color(0.3f, 0.3f, 0.3f);
            var dropdown = dropdownGO.AddComponent<TMP_Dropdown>();
            var dropdownRect = dropdownGO.GetComponent<RectTransform>();
            dropdownRect.anchorMin = new Vector2(0.3f, 0.7f);
            dropdownRect.anchorMax = new Vector2(0.9f, 0.8f);
            dropdownRect.offsetMin = Vector2.zero;
            dropdownRect.offsetMax = Vector2.zero;

            // Dropdown Template を作成
            var templateGO = new GameObject("Template", typeof(RectTransform));
            templateGO.transform.SetParent(dropdownGO.transform, false);
            var templateImage = templateGO.AddComponent<Image>();
            templateImage.color = Color.white;
            var templateRect = templateGO.GetComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0, 0);
            templateRect.anchorMax = new Vector2(1, 0);
            templateRect.pivot = new Vector2(0.5f, 1);
            templateRect.anchoredPosition = new Vector2(0, 2);
            templateRect.sizeDelta = new Vector2(0, 150);

            // Viewport
            var viewportGO = new GameObject("Viewport", typeof(RectTransform));
            viewportGO.transform.SetParent(templateGO.transform, false);
            viewportGO.AddComponent<Image>();
            viewportGO.AddComponent<Mask>().showMaskGraphic = false;
            var viewportRect = viewportGO.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = new Vector2(-18, 0);
            viewportRect.pivot = new Vector2(0, 1);

            // Content
            var contentGO = new GameObject("Content", typeof(RectTransform));
            contentGO.transform.SetParent(viewportGO.transform, false);
            var contentRect = contentGO.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0, 28);

            // Item
            var itemGO = new GameObject("Item", typeof(RectTransform));
            itemGO.transform.SetParent(contentGO.transform, false);
            var itemToggle = itemGO.AddComponent<Toggle>();
            var itemRect = itemGO.GetComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0, 0.5f);
            itemRect.anchorMax = new Vector2(1, 0.5f);
            itemRect.sizeDelta = new Vector2(0, 20);

            // Item Background
            var itemBgGO = new GameObject("Item Background", typeof(RectTransform));
            itemBgGO.transform.SetParent(itemGO.transform, false);
            var itemBgImage = itemBgGO.AddComponent<Image>();
            itemBgImage.color = Color.white;
            var itemBgRect = itemBgGO.GetComponent<RectTransform>();
            itemBgRect.anchorMin = Vector2.zero;
            itemBgRect.anchorMax = Vector2.one;
            itemBgRect.sizeDelta = Vector2.zero;

            // Item Checkmark
            var itemCheckGO = new GameObject("Item Checkmark", typeof(RectTransform));
            itemCheckGO.transform.SetParent(itemGO.transform, false);
            var itemCheckImage = itemCheckGO.AddComponent<Image>();
            itemCheckImage.color = Color.black;
            var itemCheckRect = itemCheckGO.GetComponent<RectTransform>();
            itemCheckRect.anchorMin = new Vector2(0, 0.5f);
            itemCheckRect.anchorMax = new Vector2(0, 0.5f);
            itemCheckRect.sizeDelta = new Vector2(20, 20);
            itemCheckRect.anchoredPosition = new Vector2(10, 0);

            // Item Label
            var itemLabelGO = new GameObject("Item Label", typeof(RectTransform));
            itemLabelGO.transform.SetParent(itemGO.transform, false);
            var itemLabel = itemLabelGO.AddComponent<TextMeshProUGUI>();
            itemLabel.text = "Option";
            itemLabel.fontSize = 14;
            itemLabel.color = Color.black;
            var itemLabelRect = itemLabelGO.GetComponent<RectTransform>();
            itemLabelRect.anchorMin = Vector2.zero;
            itemLabelRect.anchorMax = Vector2.one;
            itemLabelRect.offsetMin = new Vector2(20, 1);
            itemLabelRect.offsetMax = new Vector2(-10, -2);

            // Toggle の設定
            itemToggle.targetGraphic = itemBgImage;
            itemToggle.graphic = itemCheckImage;
            itemToggle.isOn = true;

            // Scrollbar
            var scrollbarGO = new GameObject("Scrollbar", typeof(RectTransform));
            scrollbarGO.transform.SetParent(templateGO.transform, false);
            var scrollbarImage = scrollbarGO.AddComponent<Image>();
            scrollbarImage.color = new Color(0.5f, 0.5f, 0.5f);
            var scrollbar = scrollbarGO.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.TopToBottom;
            var scrollbarRect = scrollbarGO.GetComponent<RectTransform>();
            scrollbarRect.anchorMin = new Vector2(1, 0);
            scrollbarRect.anchorMax = new Vector2(1, 1);
            scrollbarRect.pivot = Vector2.one;
            scrollbarRect.sizeDelta = new Vector2(20, 0);
            scrollbarRect.anchoredPosition = new Vector2(0, 0);

            // Scrollbar Handle
            var handleGO = new GameObject("Sliding Area", typeof(RectTransform));
            handleGO.transform.SetParent(scrollbarGO.transform, false);
            var handleRect = handleGO.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0, 0);
            handleRect.anchorMax = new Vector2(1, 1);
            handleRect.sizeDelta = new Vector2(-20, -20);

            var handleAreaGO = new GameObject("Handle", typeof(RectTransform));
            handleAreaGO.transform.SetParent(handleGO.transform, false);
            var handleImage = handleAreaGO.AddComponent<Image>();
            handleImage.color = new Color(0.7f, 0.7f, 0.7f);
            var handleAreaRect = handleAreaGO.GetComponent<RectTransform>();
            handleAreaRect.anchorMin = new Vector2(0, 0);
            handleAreaRect.anchorMax = new Vector2(1, 1);
            handleAreaRect.sizeDelta = new Vector2(20, 20);

            scrollbar.handleRect = handleAreaRect;
            scrollbar.targetGraphic = handleImage;

            // ScrollRect
            var scrollRect = templateGO.AddComponent<ScrollRect>();
            scrollRect.content = contentRect;
            scrollRect.viewport = viewportRect;
            scrollRect.horizontal = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            scrollRect.verticalScrollbarSpacing = -3;

            // Dropdown Label
            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(dropdownGO.transform, false);
            var labelText = labelGO.AddComponent<TextMeshProUGUI>();
            labelText.text = "ja_JP-test-medium";
            labelText.fontSize = 14;
            labelText.color = Color.white;
            labelText.alignment = TextAlignmentOptions.MidlineLeft;
            var labelRect = labelGO.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10, 2);
            labelRect.offsetMax = new Vector2(-25, -2);

            // Arrow
            var arrowGO = new GameObject("Arrow", typeof(RectTransform));
            arrowGO.transform.SetParent(dropdownGO.transform, false);
            var arrowText = arrowGO.AddComponent<TextMeshProUGUI>();
            arrowText.text = "▼";
            arrowText.fontSize = 14;
            arrowText.color = Color.white;
            var arrowRect = arrowGO.GetComponent<RectTransform>();
            arrowRect.anchorMin = new Vector2(1, 0.5f);
            arrowRect.anchorMax = new Vector2(1, 0.5f);
            arrowRect.sizeDelta = new Vector2(20, 20);
            arrowRect.pivot = new Vector2(0.5f, 0.5f);
            arrowRect.anchoredPosition = new Vector2(-15, 0);

            // Dropdown の設定
            dropdown.template = templateRect;
            dropdown.captionText = labelText;
            dropdown.itemText = itemLabel;
            dropdown.targetGraphic = dropdownImage;

            // オプションを設定
            dropdown.ClearOptions();
            dropdown.AddOptions(new System.Collections.Generic.List<string> { "ja_JP-test-medium", "test_voice" });

            // Template を非表示
            templateGO.SetActive(false);

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
            statusRect.anchorMin = new Vector2(0.1f, 0.1f);
            statusRect.anchorMax = new Vector2(0.9f, 0.15f);
            statusRect.offsetMin = Vector2.zero;
            statusRect.offsetMax = Vector2.zero;
            
            // 音素詳細テキスト
            var phonemeDetailsGO = new GameObject("PhonemeDetailsText");
            phonemeDetailsGO.transform.SetParent(panelGO.transform, false);
            var phonemeDetailsText = phonemeDetailsGO.AddComponent<TextMeshProUGUI>();
            phonemeDetailsText.text = "";
            phonemeDetailsText.fontSize = 12;
            phonemeDetailsText.color = new Color(0.8f, 0.8f, 0.8f);
            phonemeDetailsText.alignment = TextAlignmentOptions.MidlineLeft;
            var phonemeDetailsRect = phonemeDetailsGO.GetComponent<RectTransform>();
            phonemeDetailsRect.anchorMin = new Vector2(0.1f, 0.02f);
            phonemeDetailsRect.anchorMax = new Vector2(0.9f, 0.08f);
            phonemeDetailsRect.offsetMin = Vector2.zero;
            phonemeDetailsRect.offsetMax = Vector2.zero;

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
            serializedObject.FindProperty("_phonemeDetailsText").objectReferenceValue = phonemeDetailsText;
            serializedObject.ApplyModifiedProperties();

            // シーンを保存
            System.IO.Directory.CreateDirectory("Assets/uPiper/Scenes");
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log($"デモシーンを作成しました: {ScenePath}");
            EditorUtility.DisplayDialog("完了",
                $"Phase 1.10 デモシーンを作成しました。\n{ScenePath}\n\nOpenJTalk統合による正確な日本語音素変換が利用できます。\nPlayモードで実行してください。",
                "OK");
        }
    }
}