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
            OpenJTalkPhonemizer phonemizer = null;
            
            try
            {
                UpdateStatus("Creating OpenJTalkPhonemizer...");
                phonemizer = new OpenJTalkPhonemizer();
                UpdateStatus("✓ OpenJTalkPhonemizer created successfully");
            }
            catch (System.Exception e)
            {
                UpdateStatus($"✗ Error creating phonemizer: {e.Message}");
                Debug.LogError($"[AndroidOpenJTalkTest] Creation error: {e}");
            }
            
            if (phonemizer != null)
            {
                yield return new WaitForSeconds(0.5f);
                
                // Test 3: Try to phonemize simple text
                try
                {
                    UpdateStatus("Testing phonemization...");
                    string testText = "こんにちは";
                    UpdateStatus($"Input text: {testText}");
                    
                    var result = phonemizer.Phonemize(testText);
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
                catch (System.Exception e)
                {
                    UpdateStatus($"✗ Phonemization error: {e.Message}");
                    UpdateStatus("Checking library availability...");
                    
                    // Additional debug info
                    #if UNITY_ANDROID && !UNITY_EDITOR
                    UpdateStatus("Dictionary path: " + uPiper.Core.Platform.AndroidPathResolver.GetOpenJTalkDictionaryPath());
                    #endif
                }
                
                phonemizer.Dispose();
            }

            // Test 5: Test PiperTTS
            yield return new WaitForSeconds(1f);
            UpdateStatus("\nTesting PiperTTS...");
            
            PiperTTS piperTTS = null;
            
            try
            {
                var config = new PiperConfig();
                piperTTS = new PiperTTS(config);
                UpdateStatus("PiperTTS instance created");
            }
            catch (System.Exception e)
            {
                UpdateStatus($"✗ PiperTTS creation error: {e.Message}");
                Debug.LogError($"[AndroidOpenJTalkTest] PiperTTS creation error: {e}");
            }
            
            if (piperTTS != null)
            {
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
                    try
                    {
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
                    catch (System.Exception e)
                    {
                        UpdateStatus($"✗ Audio generation error: {e.Message}");
                        Debug.LogError($"[AndroidOpenJTalkTest] Audio generation error: {e}");
                    }
                }
                else
                {
                    UpdateStatus("✗ PiperTTS initialization timeout");
                }
                
                piperTTS.Dispose();
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