using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using uPiper.Core;
#if !UNITY_WEBGL
using uPiper.Core.Phonemizers.Implementations;
#endif

namespace uPiper.Samples.AndroidDemo
{
    /// <summary>
    /// Simplified TTS test for Android debugging
    /// </summary>
    public class SimpleAndroidTTSTest : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private readonly Button testButton;
        [SerializeField] private readonly Text logText;
        [SerializeField] private readonly AudioSource audioSource;

        private string logContent = "";

        private void Start()
        {
            testButton?.onClick.AddListener(RunSimpleTest);

            AddLog("=== Simple Android TTS Test ===");
            AddLog($"Platform: {Application.platform}");
            AddLog($"System Language: {Application.systemLanguage}");

            // Test basic text
            var testText = "こんにちは";
            AddLog($"Test text: {testText}");
            AddLog($"Text length: {testText.Length}");

            // Check if text contains expected characters
            var hasHiragana = false;
            foreach (var c in testText)
            {
                if (c >= '\u3040' && c <= '\u309F')
                {
                    hasHiragana = true;
                    break;
                }
            }
            AddLog($"Contains Hiragana: {hasHiragana}");
        }

        private void RunSimpleTest()
        {
            StartCoroutine(TestCoroutine());
        }

        private IEnumerator TestCoroutine()
        {
            AddLog("\n--- Starting Test ---");

            // Step 1: Test dictionary path
            AddLog("\n1. Testing dictionary path...");

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                string dictPath = uPiper.Core.Platform.AndroidPathResolver.GetOpenJTalkDictionaryPath();
                AddLog($"Dictionary path: {dictPath}");
                
                if (System.IO.Directory.Exists(dictPath))
                {
                    AddLog("✓ Dictionary directory exists");
                    
                    // Check for specific files
                    string[] requiredFiles = { "char.bin", "sys.dic", "unk.dic" };
                    foreach (var file in requiredFiles)
                    {
                        string filePath = System.IO.Path.Combine(dictPath, file);
                        if (System.IO.File.Exists(filePath))
                        {
                            var fileInfo = new System.IO.FileInfo(filePath);
                            AddLog($"✓ {file}: {fileInfo.Length} bytes");
                        }
                        else
                        {
                            AddLog($"✗ {file}: NOT FOUND");
                        }
                    }
                }
                else
                {
                    AddLog("✗ Dictionary directory does not exist");
                }
            }
            catch (System.Exception e)
            {
                AddLog($"✗ Error checking dictionary: {e.Message}");
            }
#else
            AddLog("Dictionary check skipped (not on Android device)");
#endif

            yield return new WaitForSeconds(1f);

            // Step 2: Test OpenJTalk initialization
            AddLog("\n2. Testing OpenJTalk...");

#if !UNITY_WEBGL
            OpenJTalkPhonemizer phonemizer = null;
            try
            {
                phonemizer = new OpenJTalkPhonemizer();
                AddLog("✓ OpenJTalkPhonemizer created");

                // Test phonemization
                var testText = "テスト";
                AddLog($"Testing phonemization of: {testText}");

                var result = phonemizer.Phonemize(testText);
                if (result != null && result.Phonemes != null)
                {
                    AddLog($"✓ Phonemes: {string.Join(" ", result.Phonemes)}");
                }
                else
                {
                    AddLog("✗ Phonemization failed");
                }
            }
            catch (System.Exception e)
            {
                AddLog($"✗ OpenJTalk error: {e.Message}");
            }
            finally
            {
                phonemizer?.Dispose();
            }
#else
            AddLog("OpenJTalk is not available in WebGL builds");
#endif

            yield return new WaitForSeconds(1f);

            // Step 3: Test PiperTTS
            AddLog("\n3. Testing PiperTTS...");

            PiperTTS piperTTS = null;
            var initSuccess = false;

            try
            {
                var config = new PiperConfig();
                piperTTS = new PiperTTS(config);
                AddLog("✓ PiperTTS created");
                initSuccess = true;
            }
            catch (System.Exception e)
            {
                AddLog($"✗ PiperTTS creation error: {e.Message}");
                AddLog($"Stack trace: {e.StackTrace}");
            }

            if (initSuccess && piperTTS != null)
            {
                // Wait for initialization
                var timeout = 5f;
                var elapsed = 0f;
                while (!piperTTS.IsInitialized && elapsed < timeout)
                {
                    yield return new WaitForSeconds(0.1f);
                    elapsed += 0.1f;
                }

                try
                {
                    if (piperTTS.IsInitialized)
                    {
                        AddLog("✓ PiperTTS initialized");

                        // Generate audio
                        var testText = "こんにちは";
                        AddLog($"Generating audio for: {testText}");

                        var audioClip = piperTTS.GenerateAudio(testText);
                        if (audioClip != null)
                        {
                            AddLog($"✓ Audio generated: {audioClip.length:F2}s, {audioClip.samples} samples");

                            if (audioSource != null)
                            {
                                audioSource.clip = audioClip;
                                audioSource.Play();
                                AddLog("✓ Playing audio...");
                            }
                        }
                        else
                        {
                            AddLog("✗ Audio generation failed");
                        }
                    }
                    else
                    {
                        AddLog("✗ PiperTTS initialization timeout");
                    }
                }
                catch (System.Exception e)
                {
                    AddLog($"✗ PiperTTS usage error: {e.Message}");
                    AddLog($"Stack trace: {e.StackTrace}");
                }
                finally
                {
                    piperTTS?.Dispose();
                }
            }

            AddLog("\n--- Test Complete ---");
        }

        private void AddLog(string message)
        {
            logContent += message + "\n";
            Debug.Log($"[SimpleAndroidTTSTest] {message}");

            if (logText != null)
            {
                logText.text = logContent;
            }
        }

        private void OnDestroy()
        {
            testButton?.onClick.RemoveListener(RunSimpleTest);
        }
    }
}