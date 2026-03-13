using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using uPiper.Core.Logging;

namespace uPiper.Core.Platform
{
    /// <summary>
    /// Interaction gate UI for WebGL AudioContext activation.
    /// Browsers require a user click/tap before AudioContext can be started.
    /// This component displays a fullscreen overlay prompting user interaction,
    /// then resumes the AudioContext and hides the overlay.
    /// </summary>
    public class WebGLInteractionGate : MonoBehaviour
    {
        private GameObject _overlayPanel;
        private bool _isCompleted;

        /// <summary>
        /// Fired when the user has clicked/tapped and AudioContext has been resumed.
        /// </summary>
        public event Action OnInteractionCompleted;

        /// <summary>
        /// Whether the user interaction has been completed.
        /// </summary>
        public bool IsCompleted
        {
            get => _isCompleted;
            private set => _isCompleted = value;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void WebGL_ResumeAudioContext();

        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern int WebGL_IsAudioContextResumed();
#endif

        /// <summary>
        /// Creates a WebGLInteractionGate, waits for user interaction, then destroys the gate object.
        /// On non-WebGL platforms, completes immediately.
        /// </summary>
        public static async Task WaitForInteractionAsync(CancellationToken cancellationToken = default)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var gateObject = new GameObject("WebGLInteractionGate");
            var gate = gateObject.AddComponent<WebGLInteractionGate>();

            try
            {
                while (!gate.IsCompleted)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Yield();
                }
            }
            finally
            {
                if (gateObject != null) Destroy(gateObject);
            }
#else
            await Task.CompletedTask;
#endif
        }

        private void Start()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (WebGL_IsAudioContextResumed() == 1)
            {
                PiperLogger.LogDebug("[WebGLInteractionGate] AudioContext already running, skipping gate");
                CompleteInteraction();
                return;
            }

            CreateOverlayUI();
#else
            CompleteInteraction();
#endif
        }

        private void Update()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (IsCompleted) return;

            if (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
            {
                ResumeAudioContext();
            }
#endif
        }

        private void ResumeAudioContext()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                WebGL_ResumeAudioContext();
                PiperLogger.LogInfo("[WebGLInteractionGate] AudioContext resume requested");
            }
            catch (Exception ex)
            {
                PiperLogger.LogError($"[WebGLInteractionGate] Failed to resume AudioContext: {ex.Message}");
            }
#endif

            if (_overlayPanel != null)
            {
                _overlayPanel.SetActive(false);
            }

            CompleteInteraction();
        }

        private void CompleteInteraction()
        {
            if (IsCompleted) return;

            IsCompleted = true;
            PiperLogger.LogInfo("[WebGLInteractionGate] User interaction completed");
            OnInteractionCompleted?.Invoke();
        }

        private void CreateOverlayUI()
        {
            // Canvas
            var canvasObject = new GameObject("InteractionGateCanvas");
            canvasObject.transform.SetParent(transform);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();

            // Background panel
            _overlayPanel = new GameObject("OverlayPanel");
            _overlayPanel.transform.SetParent(canvasObject.transform, false);
            var panelRect = _overlayPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var panelImage = _overlayPanel.AddComponent<Image>();
            panelImage.color = new Color(0, 0, 0, 0.7f);

            // Text
            var textObject = new GameObject("MessageText");
            textObject.transform.SetParent(_overlayPanel.transform, false);
            var textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.1f, 0.3f);
            textRect.anchorMax = new Vector2(0.9f, 0.7f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var tmpText = textObject.AddComponent<TextMeshProUGUI>();
            tmpText.text = "音声合成の準備ができました\nクリックして開始";
            tmpText.fontSize = 36;
            tmpText.color = Color.white;
            tmpText.alignment = TextAlignmentOptions.Center;
            tmpText.font = TMP_Settings.defaultFontAsset;
        }

        private void OnDestroy()
        {
            OnInteractionCompleted = null;
        }
    }
}