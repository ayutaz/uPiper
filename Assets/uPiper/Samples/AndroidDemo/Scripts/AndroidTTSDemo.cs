using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using uPiper.Core;
using uPiper.Core.Phonemizers;

#if UNITY_2021_2_OR_NEWER
using TMPro;
#endif

namespace uPiper.Samples.AndroidDemo
{
    /// <summary>
    /// Android-specific TTS demo showcasing uPiper on mobile devices
    /// </summary>
    public class AndroidTTSDemo : MonoBehaviour
    {
        [Header("UI References")]
#if UNITY_2021_2_OR_NEWER
        [SerializeField] private TMP_InputField inputField;
#else
        [SerializeField] private InputField inputField;
#endif
        [SerializeField] private Button speakButton;
#if UNITY_2021_2_OR_NEWER
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI speedText;
#else
        [SerializeField] private Text statusText;
        [SerializeField] private Text speedText;
#endif
        [SerializeField] private Slider speedSlider;
        [SerializeField] private AudioSource audioSource;

        [Header("Test Sentences")]
        [SerializeField]
        private string[] testSentences = new string[]
        {
            "こんにちは、Androidでのテストです。",
            "本日は晴天なり。",
            "音声合成のテストを行っています。",
            "Unity上でPiper TTSが動作しています。"
        };

        private PiperTTS piperTTS;
        private bool isInitialized = false;
        private bool isSpeaking = false;

        private void Start()
        {
            StartCoroutine(InitializeAsync());

            // Setup UI
            if (speakButton != null)
            {
                speakButton.onClick.AddListener(OnSpeakButtonClicked);
                speakButton.interactable = false;
            }

            if (speedSlider != null)
            {
                speedSlider.onValueChanged.AddListener(OnSpeedChanged);
                speedSlider.value = 1.0f;
            }

            // Set default text
            if (inputField != null && testSentences.Length > 0)
            {
                inputField.text = testSentences[0];
            }

            UpdateStatus("Initializing...");
        }

        private IEnumerator InitializeAsync()
        {
            yield return new WaitForSeconds(0.5f); // Small delay for UI initialization

            UpdateStatus("Initializing PiperTTS...");

            try
            {
                // Create PiperTTS instance
                var config = new PiperConfig();
                piperTTS = new PiperTTS(config);

                if (piperTTS == null)
                {
                    UpdateStatus("Failed to create PiperTTS instance");
                    yield break;
                }
            }
            catch (System.Exception e)
            {
                UpdateStatus($"Initialization error: {e.Message}");
                Debug.LogError($"[AndroidTTSDemo] Initialization error: {e}");
                yield break;
            }

            // Wait for initialization
            float timeout = 10f;
            float elapsed = 0f;

            while (!piperTTS.IsInitialized && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            if (piperTTS.IsInitialized)
            {
                isInitialized = true;
                UpdateStatus("Ready to speak!");

                if (speakButton != null)
                    speakButton.interactable = true;

                // Log system info
                LogSystemInfo();
            }
            else
            {
                UpdateStatus("Initialization timeout");
                Debug.LogError("[AndroidTTSDemo] PiperTTS initialization timeout");
            }
        }

        private void OnSpeakButtonClicked()
        {
            if (!isInitialized || isSpeaking)
                return;

            string text = inputField != null ? inputField.text : "テストです。";
            if (string.IsNullOrWhiteSpace(text))
            {
                UpdateStatus("Please enter text to speak");
                return;
            }

            StartCoroutine(SpeakAsync(text));
        }

        private IEnumerator SpeakAsync(string text)
        {
            isSpeaking = true;
            UpdateStatus("Generating speech...");

            if (speakButton != null)
                speakButton.interactable = false;

            float startTime = Time.realtimeSinceStartup;

            AudioClip audioClip = null;
            System.Exception error = null;

            try
            {
                // Generate audio clip
                audioClip = piperTTS.GenerateAudio(text);
            }
            catch (System.Exception e)
            {
                error = e;
            }

            if (error != null)
            {
                UpdateStatus($"Error: {error.Message}");
                Debug.LogError($"[AndroidTTSDemo] Speech generation error: {error}");
            }
            else if (audioClip != null)
            {
                float generationTime = Time.realtimeSinceStartup - startTime;
                UpdateStatus($"Generated in {generationTime:F2}s");

                // Play audio
                if (audioSource != null)
                {
                    audioSource.clip = audioClip;
                    audioSource.Play();

                    // Wait for playback to complete
                    yield return new WaitForSeconds(audioClip.length);
                }

                UpdateStatus("Ready to speak!");
            }
            else
            {
                UpdateStatus("Failed to generate audio");
            }

            isSpeaking = false;
            if (speakButton != null)
                speakButton.interactable = true;
        }

        private void OnSpeedChanged(float value)
        {
            if (speedText != null)
            {
                speedText.text = $"Speed: {value:F1}x";
            }

            // Note: Speech rate adjustment would need to be implemented
            // through voice configuration or audio post-processing
            Debug.Log($"[AndroidTTSDemo] Speed changed to: {value}x (not implemented yet)");
        }

        private void UpdateStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
            Debug.Log($"[AndroidTTSDemo] {message}");
        }

        private void LogSystemInfo()
        {
            Debug.Log("[AndroidTTSDemo] === System Info ===");
            Debug.Log($"Platform: {Application.platform}");
            Debug.Log($"Device Model: {SystemInfo.deviceModel}");
            Debug.Log($"Device Type: {SystemInfo.deviceType}");
            Debug.Log($"Operating System: {SystemInfo.operatingSystem}");
            Debug.Log($"System Memory: {SystemInfo.systemMemorySize}MB");
            Debug.Log($"Processor: {SystemInfo.processorType}");
            Debug.Log($"Processor Count: {SystemInfo.processorCount}");
            Debug.Log($"Graphics Device: {SystemInfo.graphicsDeviceName}");
            Debug.Log($"Graphics Memory: {SystemInfo.graphicsMemorySize}MB");
            Debug.Log("[AndroidTTSDemo] ==================");
        }

        public void OnTestButtonClicked(int index)
        {
            if (inputField != null && index >= 0 && index < testSentences.Length)
            {
                inputField.text = testSentences[index];
            }
        }

        private void OnDestroy()
        {
            if (speakButton != null)
            {
                speakButton.onClick.RemoveListener(OnSpeakButtonClicked);
            }

            if (speedSlider != null)
            {
                speedSlider.onValueChanged.RemoveListener(OnSpeedChanged);
            }
        }
    }
}