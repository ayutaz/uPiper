using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace uPiper.Editor
{
    public static class GenerateBasicTTSScene
    {
        [MenuItem("uPiper/Samples/Generate BasicTTSDemo Scene")]
        public static void GenerateAndSaveScene()
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
            titleText.text = "uPiper TTS Demo";
            titleText.fontSize = 48;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = Color.white;
            var titleRect = titleGO.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.9f);
            titleRect.anchorMax = new Vector2(0.5f, 0.95f);
            titleRect.offsetMin = new Vector2(-400, -30);
            titleRect.offsetMax = new Vector2(400, 30);

            // Create model dropdown
            var modelDropdownGO = new GameObject("ModelDropdown");
            modelDropdownGO.transform.SetParent(panelGO.transform, false);
            var modelDropdown = modelDropdownGO.AddComponent<TMP_Dropdown>();
            modelDropdown.options.Clear();
            modelDropdown.options.Add(new TMP_Dropdown.OptionData("ja_JP-test-medium"));
            modelDropdown.options.Add(new TMP_Dropdown.OptionData("en_US-test-voice"));
            var modelDropdownRect = modelDropdownGO.GetComponent<RectTransform>();
            modelDropdownRect.anchorMin = new Vector2(0.5f, 0.8f);
            modelDropdownRect.anchorMax = new Vector2(0.5f, 0.85f);
            modelDropdownRect.offsetMin = new Vector2(-200, -20);
            modelDropdownRect.offsetMax = new Vector2(200, 20);

            // Create preset phrase dropdown
            var phraseDropdownGO = new GameObject("PhraseDropdown");
            phraseDropdownGO.transform.SetParent(panelGO.transform, false);
            var phraseDropdown = phraseDropdownGO.AddComponent<TMP_Dropdown>();
            phraseDropdown.options.Clear();
            phraseDropdown.options.Add(new TMP_Dropdown.OptionData("Select a preset phrase..."));
            phraseDropdown.options.Add(new TMP_Dropdown.OptionData("こんにちは、これはテストです"));
            phraseDropdown.options.Add(new TMP_Dropdown.OptionData("今日はいい天気ですね"));
            phraseDropdown.options.Add(new TMP_Dropdown.OptionData("Hello, this is a test"));
            var phraseDropdownRect = phraseDropdownGO.GetComponent<RectTransform>();
            phraseDropdownRect.anchorMin = new Vector2(0.5f, 0.7f);
            phraseDropdownRect.anchorMax = new Vector2(0.5f, 0.75f);
            phraseDropdownRect.offsetMin = new Vector2(-200, -20);
            phraseDropdownRect.offsetMax = new Vector2(200, 20);

            // Create input field with text area
            var inputFieldGO = new GameObject("InputField");
            inputFieldGO.transform.SetParent(panelGO.transform, false);
            
            // Add the background image for the input field
            var inputFieldImage = inputFieldGO.AddComponent<Image>();
            inputFieldImage.color = new Color(1f, 1f, 1f, 0.1f);
            
            var inputField = inputFieldGO.AddComponent<TMP_InputField>();
            
            // Create the text area for the input field
            var textAreaGO = new GameObject("Text Area");
            textAreaGO.transform.SetParent(inputFieldGO.transform, false);
            var textAreaRect = textAreaGO.AddComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(10, 6);
            textAreaRect.offsetMax = new Vector2(-10, -7);
            
            // Create placeholder
            var placeholderGO = new GameObject("Placeholder");
            placeholderGO.transform.SetParent(textAreaGO.transform, false);
            var placeholderText = placeholderGO.AddComponent<TextMeshProUGUI>();
            placeholderText.text = "Enter text here...";
            placeholderText.color = new Color(1f, 1f, 1f, 0.5f);
            var placeholderRect = placeholderGO.GetComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = Vector2.zero;
            placeholderRect.offsetMax = Vector2.zero;
            
            // Create text component
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(textAreaGO.transform, false);
            var textComponent = textGO.AddComponent<TextMeshProUGUI>();
            textComponent.text = "こんにちは、これはuPiperのテストです。";
            textComponent.color = Color.white;
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            
            // Configure input field
            inputField.textViewport = textAreaRect;
            inputField.textComponent = textComponent;
            inputField.placeholder = placeholderText;
            inputField.text = "こんにちは、これはuPiperのテストです。";
            inputField.lineType = TMP_InputField.LineType.MultiLineNewline;
            
            var inputFieldRect = inputFieldGO.GetComponent<RectTransform>();
            inputFieldRect.anchorMin = new Vector2(0.5f, 0.4f);
            inputFieldRect.anchorMax = new Vector2(0.5f, 0.6f);
            inputFieldRect.offsetMin = new Vector2(-400, -80);
            inputFieldRect.offsetMax = new Vector2(400, 80);

            // Create generate button
            var generateButtonGO = new GameObject("GenerateButton");
            generateButtonGO.transform.SetParent(panelGO.transform, false);
            var generateButton = generateButtonGO.AddComponent<Button>();
            var generateButtonImage = generateButtonGO.AddComponent<Image>();
            generateButtonImage.color = new Color(0.2f, 0.7f, 0.3f, 1f);
            var generateButtonText = new GameObject("Text");
            generateButtonText.transform.SetParent(generateButtonGO.transform, false);
            var genText = generateButtonText.AddComponent<TextMeshProUGUI>();
            genText.text = "Generate Speech";
            genText.fontSize = 24;
            genText.alignment = TextAlignmentOptions.Center;
            genText.color = Color.white;
            var genTextRect = generateButtonText.GetComponent<RectTransform>();
            genTextRect.anchorMin = Vector2.zero;
            genTextRect.anchorMax = Vector2.one;
            genTextRect.offsetMin = Vector2.zero;
            genTextRect.offsetMax = Vector2.zero;
            var generateButtonRect = generateButtonGO.GetComponent<RectTransform>();
            generateButtonRect.anchorMin = new Vector2(0.5f, 0.25f);
            generateButtonRect.anchorMax = new Vector2(0.5f, 0.35f);
            generateButtonRect.offsetMin = new Vector2(-150, -30);
            generateButtonRect.offsetMax = new Vector2(150, 30);

            // Create status text
            var statusTextGO = new GameObject("StatusText");
            statusTextGO.transform.SetParent(panelGO.transform, false);
            var statusText = statusTextGO.AddComponent<TextMeshProUGUI>();
            statusText.text = "Ready";
            statusText.fontSize = 20;
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.color = Color.yellow;
            var statusTextRect = statusTextGO.GetComponent<RectTransform>();
            statusTextRect.anchorMin = new Vector2(0.5f, 0.1f);
            statusTextRect.anchorMax = new Vector2(0.5f, 0.2f);
            statusTextRect.offsetMin = new Vector2(-400, -30);
            statusTextRect.offsetMax = new Vector2(400, 30);

            // Create demo controller GameObject
            var controllerGO = new GameObject("BasicTTSDemo");
            var audioSource = controllerGO.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            
            // Note: The BasicTTSDemo component will need to be added manually after import
            // or via dynamic type loading if the sample is already imported

            // Save scene
            string scenePath = "Assets/uPiper/Samples~/BasicTTSDemo/BasicTTSDemo.unity";
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, scenePath);
            
            Debug.Log($"BasicTTSDemo scene generated and saved to: {scenePath}");
            Debug.Log("Note: Please add the BasicTTSDemo component to the BasicTTSDemo GameObject and connect the UI references.");
            
            // Open the saved scene
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
        }
    }
}