using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using uPiper.Core;
using uPiper.Core.Phonemizers.Implementations;

namespace uPiper.Samples.AndroidDemo
{
    /// <summary>
    /// Test script to verify OpenJTalk functionality on Android
    /// </summary>
    public class AndroidOpenJTalkTest : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Text statusText;
        [SerializeField] private Button testButton;
        [SerializeField] private AudioSource audioSource;

        private void Start()
        {
            if (testButton != null)
            {
                testButton.onClick.AddListener(RunTest);
            }

            UpdateStatus("Ready to test OpenJTalk");
        }

        private void RunTest()
        {
            StartCoroutine(TestOpenJTalk());
        }

        private IEnumerator TestOpenJTalk()
        {
            UpdateStatus("Testing OpenJTalk on Android...");
            yield return null;

            // Test 1: Check if we're on Android
            UpdateStatus($"Platform: {Application.platform}");
            UpdateStatus($"Is Android: {Application.platform == RuntimePlatform.Android}");
            yield return new WaitForSeconds(0.5f);

            // Test 2: Try to create OpenJTalkPhonemizer
            try
            {
                UpdateStatus("Creating OpenJTalkPhonemizer...");
                var phonemizer = new OpenJTalkPhonemizer();
                UpdateStatus("✓ OpenJTalkPhonemizer created successfully");
                
                // Test 3: Check if initialized
                yield return new WaitForSeconds(0.5f);
                UpdateStatus($"IsInitialized: {phonemizer.IsInitialized}");
                
                // Test 4: Try to phonemize simple text
                if (phonemizer.IsInitialized)
                {
                    UpdateStatus("Testing phonemization...");
                    string testText = "こんにちは";
                    UpdateStatus($"Input text: {testText}");
                    
                    var result = phonemizer.PhonemizeText(testText);
                    if (result != null)
                    {
                        UpdateStatus($"✓ Phonemes: {string.Join(" ", result.Phonemes)}");
                        UpdateStatus($"Phoneme count: {result.Phonemes.Length}");
                    }
                    else
                    {
                        UpdateStatus("✗ Phonemization returned null");
                    }
                }
                else
                {
                    UpdateStatus("✗ OpenJTalkPhonemizer not initialized");
                    UpdateStatus("Checking library availability...");
                    
                    // Additional debug info
                    #if UNITY_ANDROID && !UNITY_EDITOR
                    UpdateStatus("Dictionary path: " + uPiper.Core.Platform.AndroidPathResolver.GetOpenJTalkDictionaryPath());
                    #endif
                }
                
                phonemizer.Dispose();
            }
            catch (System.Exception e)
            {
                UpdateStatus($"✗ Error: {e.Message}");
                Debug.LogError($"[AndroidOpenJTalkTest] Full error: {e}");
            }

            // Test 5: Test PiperTTS
            yield return new WaitForSeconds(1f);
            UpdateStatus("\nTesting PiperTTS...");
            
            try
            {
                var config = new PiperConfig();
                var piperTTS = new PiperTTS(config);
                
                UpdateStatus("Waiting for PiperTTS initialization...");
                float timeout = 5f;
                float elapsed = 0f;
                
                while (!piperTTS.IsInitialized && elapsed < timeout)
                {
                    yield return new WaitForSeconds(0.1f);
                    elapsed += 0.1f;
                }
                
                if (piperTTS.IsInitialized)
                {
                    UpdateStatus("✓ PiperTTS initialized");
                    
                    // Try to generate audio
                    string testText = "テスト";
                    UpdateStatus($"Generating audio for: {testText}");
                    
                    var audioClip = piperTTS.GenerateAudio(testText);
                    if (audioClip != null)
                    {
                        UpdateStatus($"✓ Audio generated: {audioClip.length}s");
                        
                        if (audioSource != null)
                        {
                            audioSource.clip = audioClip;
                            audioSource.Play();
                        }
                    }
                    else
                    {
                        UpdateStatus("✗ Failed to generate audio");
                    }
                }
                else
                {
                    UpdateStatus("✗ PiperTTS initialization timeout");
                }
                
                piperTTS.Dispose();
            }
            catch (System.Exception e)
            {
                UpdateStatus($"✗ PiperTTS Error: {e.Message}");
                Debug.LogError($"[AndroidOpenJTalkTest] PiperTTS error: {e}");
            }

            UpdateStatus("\nTest completed!");
        }

        private void UpdateStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text += "\n" + message;
            }
            Debug.Log($"[AndroidOpenJTalkTest] {message}");
        }

        private void OnDestroy()
        {
            if (testButton != null)
            {
                testButton.onClick.RemoveListener(RunTest);
            }
        }
    }
}