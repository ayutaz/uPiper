using TMPro;
using UnityEngine;
using UnityEngine.UI;
using uPiper.Core.Logging;

namespace uPiper.Samples.WebGLDemo
{
    /// <summary>
    /// WebGLデモシーンの自動セットアップ
    /// </summary>
    [ExecuteInEditMode]
    public class WebGLDemoSceneSetup : MonoBehaviour
    {
        private void Awake()
        {
            // エディタでの実行時のみセットアップ
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                SetupScene();
            }
#endif
        }

        [ContextMenu("Setup WebGL Demo Scene")]
        public void SetupScene()
        {
            PiperLogger.LogInfo("[WebGLDemoSceneSetup] Setting up demo scene...");

            // Canvas を作成
            GameObject canvasObj = GameObject.Find("Canvas");
            if (canvasObj == null)
            {
                canvasObj = new GameObject("Canvas");
                Canvas canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // EventSystem を作成
            if (GameObject.Find("EventSystem") == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // UI Panel を作成
            GameObject panelObj = GameObject.Find("DemoPanel");
            if (panelObj == null)
            {
                panelObj = new GameObject("DemoPanel");
                panelObj.transform.SetParent(canvasObj.transform, false);

                RectTransform panelRect = panelObj.AddComponent<RectTransform>();
                panelRect.anchorMin = Vector2.zero;
                panelRect.anchorMax = Vector2.one;
                panelRect.sizeDelta = Vector2.zero;
                panelRect.anchoredPosition = Vector2.zero;

                Image panelImage = panelObj.AddComponent<Image>();
                panelImage.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
            }

            // Title Text
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(panelObj.transform, false);
            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "uPiper WebGL Demo";
            titleText.fontSize = 36;
            titleText.alignment = TextAlignmentOptions.Center;

            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.8f);
            titleRect.anchorMax = new Vector2(0.5f, 0.9f);
            titleRect.sizeDelta = new Vector2(600, 60);
            titleRect.anchoredPosition = Vector2.zero;

            // Input Field
            GameObject inputObj = new GameObject("InputField");
            inputObj.transform.SetParent(panelObj.transform, false);
            TMP_InputField inputField = inputObj.AddComponent<TMP_InputField>();

            RectTransform inputRect = inputObj.GetComponent<RectTransform>();
            inputRect.anchorMin = new Vector2(0.5f, 0.5f);
            inputRect.anchorMax = new Vector2(0.5f, 0.5f);
            inputRect.sizeDelta = new Vector2(600, 50);
            inputRect.anchoredPosition = new Vector2(0, 50);

            // Input Field Background
            GameObject inputBg = new GameObject("Background");
            inputBg.transform.SetParent(inputObj.transform, false);
            Image bgImage = inputBg.AddComponent<Image>();
            bgImage.color = Color.white;
            RectTransform bgRect = inputBg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            // Text Area
            GameObject textArea = new GameObject("Text Area");
            textArea.transform.SetParent(inputObj.transform, false);
            RectTransform textAreaRect = textArea.AddComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.sizeDelta = new Vector2(-20, -10);
            textAreaRect.anchoredPosition = Vector2.zero;

            // Text Component
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(textArea.transform, false);
            TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = "こんにちは、uPiper WebGLデモへようこそ！";
            text.fontSize = 16;
            text.color = Color.black;

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            // Configure InputField
            inputField.targetGraphic = bgImage;
            inputField.textViewport = textAreaRect;
            inputField.textComponent = text;

            // Generate Button
            GameObject buttonObj = new GameObject("GenerateButton");
            buttonObj.transform.SetParent(panelObj.transform, false);
            Button button = buttonObj.AddComponent<Button>();
            Image buttonImage = buttonObj.AddComponent<Image>();
            buttonImage.color = new Color(0.3f, 0.6f, 0.9f);

            RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.sizeDelta = new Vector2(200, 50);
            buttonRect.anchoredPosition = new Vector2(0, -50);

            // Button Text
            GameObject buttonTextObj = new GameObject("Text");
            buttonTextObj.transform.SetParent(buttonObj.transform, false);
            TextMeshProUGUI buttonText = buttonTextObj.AddComponent<TextMeshProUGUI>();
            buttonText.text = "音声生成";
            buttonText.fontSize = 20;
            buttonText.alignment = TextAlignmentOptions.Center;
            buttonText.color = Color.white;

            RectTransform buttonTextRect = buttonTextObj.GetComponent<RectTransform>();
            buttonTextRect.anchorMin = Vector2.zero;
            buttonTextRect.anchorMax = Vector2.one;
            buttonTextRect.sizeDelta = Vector2.zero;

            // Status Text
            GameObject statusObj = new GameObject("StatusText");
            statusObj.transform.SetParent(panelObj.transform, false);
            TextMeshProUGUI statusText = statusObj.AddComponent<TextMeshProUGUI>();
            statusText.text = "状態: 準備完了";
            statusText.fontSize = 16;
            statusText.alignment = TextAlignmentOptions.Center;

            RectTransform statusRect = statusObj.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0.5f, 0.3f);
            statusRect.anchorMax = new Vector2(0.5f, 0.3f);
            statusRect.sizeDelta = new Vector2(400, 30);
            statusRect.anchoredPosition = Vector2.zero;

            // Progress Slider
            GameObject sliderObj = new GameObject("ProgressSlider");
            sliderObj.transform.SetParent(panelObj.transform, false);
            Slider slider = sliderObj.AddComponent<Slider>();

            RectTransform sliderRect = sliderObj.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0.5f, 0.2f);
            sliderRect.anchorMax = new Vector2(0.5f, 0.2f);
            sliderRect.sizeDelta = new Vector2(400, 20);
            sliderRect.anchoredPosition = Vector2.zero;

            // Slider Background
            GameObject sliderBg = new GameObject("Background");
            sliderBg.transform.SetParent(sliderObj.transform, false);
            Image sliderBgImage = sliderBg.AddComponent<Image>();
            sliderBgImage.color = new Color(0.5f, 0.5f, 0.5f);
            RectTransform sliderBgRect = sliderBg.GetComponent<RectTransform>();
            sliderBgRect.anchorMin = Vector2.zero;
            sliderBgRect.anchorMax = Vector2.one;
            sliderBgRect.sizeDelta = Vector2.zero;

            // Slider Fill Area
            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderObj.transform, false);
            RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.sizeDelta = new Vector2(-10, 0);
            fillAreaRect.anchoredPosition = Vector2.zero;

            // Slider Fill
            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            Image fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color(0.3f, 0.8f, 0.3f);
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0, 1);
            fillRect.sizeDelta = new Vector2(10, 0);

            // Configure Slider
            slider.fillRect = fillRect;
            slider.targetGraphic = sliderBgImage;

            // WebGLDemoUI Component
            WebGLDemoUI demoUI = panelObj.GetComponent<WebGLDemoUI>();
            if (demoUI == null)
            {
                demoUI = panelObj.AddComponent<WebGLDemoUI>();
            }

            // AudioSource
            AudioSource audioSource = panelObj.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = panelObj.AddComponent<AudioSource>();
            }

            // Assign references
            demoUI.textInput = inputField;
            demoUI.generateButton = button;
            demoUI.statusText = statusText;
            demoUI.progressSlider = slider;
            demoUI.audioSource = audioSource;

            PiperLogger.LogInfo("[WebGLDemoSceneSetup] Demo scene setup completed!");

            // エディタでの実行時はこのコンポーネントを削除
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(this);
            }
#endif
        }
    }
}