using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace uPiper.Samples.BasicTTSDemo.Editor
{
    /// <summary>
    /// Creates a basic TTS demo scene
    /// </summary>
    public static class CreateBasicTTSDemoScene
    {
        [MenuItem("uPiper/Samples/Create Basic TTS Demo Scene")]
        public static void CreateScene()
        {
            // Create new scene
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects,
                UnityEditor.SceneManagement.NewSceneMode.Single
            );

            // Create Canvas
            var canvasGO = new GameObject("Canvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var canvasScaler = canvasGO.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920, 1080);
            canvasGO.AddComponent<GraphicRaycaster>();

            // Create EventSystem
            var eventSystemGO = new GameObject("EventSystem");
            eventSystemGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemGO.AddComponent<InputSystemUIInputModule>();

            // Create background panel
            var panelGO = new GameObject("Panel");
            panelGO.transform.SetParent(canvasGO.transform, false);
            var panelImage = panelGO.AddComponent<Image>();
            panelImage.color = new Color(0.15f, 0.15f, 0.15f, 1f);
            var panelRect = panelGO.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            // Create title
            var titleGO = new GameObject("Title");
            titleGO.transform.SetParent(panelGO.transform, false);
            var titleText = titleGO.AddComponent<TextMeshProUGUI>();
            titleText.text = "uPiper - Basic TTS Demo";
            titleText.fontSize = 36;
            titleText.color = Color.white;
            titleText.alignment = TextAlignmentOptions.Center;
            var titleRect = titleGO.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.2f, 0.8f);
            titleRect.anchorMax = new Vector2(0.8f, 0.9f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            // Create input field label
            var inputLabelGO = new GameObject("InputLabel");
            inputLabelGO.transform.SetParent(panelGO.transform, false);
            var inputLabel = inputLabelGO.AddComponent<TextMeshProUGUI>();
            inputLabel.text = "Japanese Text:";
            inputLabel.fontSize = 20;
            inputLabel.color = Color.white;
            inputLabel.alignment = TextAlignmentOptions.MidlineRight;
            var inputLabelRect = inputLabelGO.GetComponent<RectTransform>();
            inputLabelRect.anchorMin = new Vector2(0.1f, 0.55f);
            inputLabelRect.anchorMax = new Vector2(0.25f, 0.65f);
            inputLabelRect.offsetMin = Vector2.zero;
            inputLabelRect.offsetMax = Vector2.zero;

            // Create input field
            var inputFieldGO = new GameObject("InputField");
            inputFieldGO.transform.SetParent(panelGO.transform, false);
            var inputFieldImage = inputFieldGO.AddComponent<Image>();
            inputFieldImage.color = Color.white;
            var inputField = inputFieldGO.AddComponent<TMP_InputField>();
            var inputFieldRect = inputFieldGO.GetComponent<RectTransform>();
            inputFieldRect.anchorMin = new Vector2(0.3f, 0.45f);
            inputFieldRect.anchorMax = new Vector2(0.9f, 0.65f);
            inputFieldRect.offsetMin = Vector2.zero;
            inputFieldRect.offsetMax = Vector2.zero;

            // Create text area
            var textAreaGO = new GameObject("Text Area");
            textAreaGO.transform.SetParent(inputFieldGO.transform, false);
            var textAreaRect = textAreaGO.AddComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(10, 5);
            textAreaRect.offsetMax = new Vector2(-10, -5);

            // Create placeholder
            var placeholderGO = new GameObject("Placeholder");
            placeholderGO.transform.SetParent(textAreaGO.transform, false);
            var placeholderText = placeholderGO.AddComponent<TextMeshProUGUI>();
            placeholderText.text = "日本語のテキストを入力してください...";
            placeholderText.fontSize = 18;
            placeholderText.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            var placeholderRect = placeholderGO.GetComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = Vector2.zero;
            placeholderRect.offsetMax = Vector2.zero;

            // Create text component
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(textAreaGO.transform, false);
            var text = textGO.AddComponent<TextMeshProUGUI>();
            text.fontSize = 18;
            text.color = Color.black;
            text.richText = false;
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            // Configure input field
            inputField.targetGraphic = inputFieldImage;
            inputField.textViewport = textAreaRect;
            inputField.textComponent = text;
            inputField.placeholder = placeholderText;
            inputField.lineType = TMP_InputField.LineType.MultiLineNewline;

            // Create generate button
            var generateButtonGO = new GameObject("GenerateButton");
            generateButtonGO.transform.SetParent(panelGO.transform, false);
            var generateButton = generateButtonGO.AddComponent<Button>();
            var generateButtonImage = generateButtonGO.AddComponent<Image>();
            generateButtonImage.color = new Color(0.2f, 0.6f, 1f);
            generateButton.targetGraphic = generateButtonImage;
            var generateButtonRect = generateButtonGO.GetComponent<RectTransform>();
            generateButtonRect.anchorMin = new Vector2(0.35f, 0.3f);
            generateButtonRect.anchorMax = new Vector2(0.65f, 0.4f);
            generateButtonRect.offsetMin = Vector2.zero;
            generateButtonRect.offsetMax = Vector2.zero;

            // Create button text
            var buttonTextGO = new GameObject("Text");
            buttonTextGO.transform.SetParent(generateButtonGO.transform, false);
            var buttonText = buttonTextGO.AddComponent<TextMeshProUGUI>();
            buttonText.text = "Generate Speech";
            buttonText.fontSize = 24;
            buttonText.color = Color.white;
            buttonText.alignment = TextAlignmentOptions.Center;
            var buttonTextRect = buttonTextGO.GetComponent<RectTransform>();
            buttonTextRect.anchorMin = Vector2.zero;
            buttonTextRect.anchorMax = Vector2.one;
            buttonTextRect.offsetMin = Vector2.zero;
            buttonTextRect.offsetMax = Vector2.zero;

            // Create status text
            var statusTextGO = new GameObject("StatusText");
            statusTextGO.transform.SetParent(panelGO.transform, false);
            var statusText = statusTextGO.AddComponent<TextMeshProUGUI>();
            statusText.text = "Ready";
            statusText.fontSize = 18;
            statusText.color = Color.green;
            statusText.alignment = TextAlignmentOptions.Center;
            var statusTextRect = statusTextGO.GetComponent<RectTransform>();
            statusTextRect.anchorMin = new Vector2(0.2f, 0.15f);
            statusTextRect.anchorMax = new Vector2(0.8f, 0.25f);
            statusTextRect.offsetMin = Vector2.zero;
            statusTextRect.offsetMax = Vector2.zero;

            // Create demo controller
            var controllerGO = new GameObject("BasicTTSDemo");
            var audioSource = controllerGO.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            var demo = controllerGO.AddComponent<BasicTTSDemo>();

            // Set references
            var serializedObject = new SerializedObject(demo);
            serializedObject.FindProperty("_inputField").objectReferenceValue = inputField;
            serializedObject.FindProperty("_generateButton").objectReferenceValue = generateButton;
            serializedObject.FindProperty("_statusText").objectReferenceValue = statusText;
            serializedObject.FindProperty("_audioSource").objectReferenceValue = audioSource;
            serializedObject.ApplyModifiedProperties();

            // Mark scene as dirty
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log("Basic TTS Demo scene created. Don't forget to save the scene!");
        }
    }
}