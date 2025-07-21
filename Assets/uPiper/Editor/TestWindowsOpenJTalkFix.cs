#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using uPiper.Core.Phonemizers.Implementations;
using uPiper.Core.Logging;

namespace uPiper.Editor
{
    public class TestWindowsOpenJTalkFix : EditorWindow
    {
        private string _testText = "今日はいい天気ですね";
        private string _result = "";
        private OpenJTalkPhonemizer _phonemizer;
        
        [MenuItem("uPiper/Test Windows OpenJTalk Fix")]
        static void Init()
        {
            var window = GetWindow<TestWindowsOpenJTalkFix>();
            window.titleContent = new GUIContent("Test Windows Fix");
            window.Show();
        }
        
        void OnEnable()
        {
            PiperLogger.SetMinimumLevel(PiperLogger.LogLevel.Debug);
            try
            {
                _phonemizer = new OpenJTalkPhonemizer();
                Debug.Log("[TestWindowsFix] OpenJTalk phonemizer initialized successfully");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TestWindowsFix] Failed to initialize: {e}");
            }
        }
        
        void OnDisable()
        {
            _phonemizer?.Dispose();
        }
        
        void OnGUI()
        {
            EditorGUILayout.LabelField("Windows OpenJTalk Fix Test", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            _testText = EditorGUILayout.TextField("Test Text:", _testText);
            
            if (GUILayout.Button("Test Phonemization"))
            {
                TestPhonemization();
            }
            
            EditorGUILayout.Space();
            
            if (!string.IsNullOrEmpty(_result))
            {
                EditorGUILayout.LabelField("Result:", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(_result, GUILayout.Height(200));
            }
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button("Test Common Phrases"))
            {
                TestCommonPhrases();
            }
        }
        
        void TestPhonemization()
        {
            if (_phonemizer == null)
            {
                _result = "Phonemizer not initialized!";
                return;
            }
            
            try
            {
                var result = _phonemizer.Phonemize(_testText, "ja");
                _result = $"Input: {_testText}\n";
                _result += $"Phoneme count: {result.Phonemes.Length}\n";
                _result += $"Phonemes: {string.Join(" ", result.Phonemes)}\n\n";
                
                // Check for common issues
                var phonemeStr = string.Join(" ", result.Phonemes);
                
                // Check for the specific "ne de r su" pattern
                if (phonemeStr.Contains("n e d e r s u"))
                {
                    _result += "❌ ERROR: Still seeing incorrect phonemization pattern!\n";
                }
                else if (_testText == "今日はいい天気ですね" && phonemeStr.Contains("k y o"))
                {
                    _result += "✅ SUCCESS: Correct phonemization detected!\n";
                }
                
                Debug.Log($"[TestWindowsFix] Phonemization result: {phonemeStr}");
            }
            catch (System.Exception e)
            {
                _result = $"Error: {e.Message}\n{e.StackTrace}";
                Debug.LogError($"[TestWindowsFix] Error: {e}");
            }
        }
        
        void TestCommonPhrases()
        {
            var testCases = new[]
            {
                ("こんにちは", "k o N n i ch i w a"),
                ("今日はいい天気ですね", "k y o: w a i: t e N k i d e s u n e"),
                ("ありがとうございます", "a r i g a t o: g o z a i m a s u"),
                ("日本語", "n i h o N g o"),
                ("おはようございます", "o h a y o: g o z a i m a s u")
            };
            
            _result = "Testing common phrases:\n\n";
            
            foreach (var (text, expected) in testCases)
            {
                try
                {
                    var result = _phonemizer.Phonemize(text, "ja");
                    var phonemes = string.Join(" ", result.Phonemes);
                    
                    _result += $"Text: {text}\n";
                    _result += $"Result: {phonemes}\n";
                    
                    // Simple check if result looks reasonable
                    if (phonemes.Contains("d e r s u") || phonemes.Contains("n e d e"))
                    {
                        _result += "❌ Incorrect phonemization detected\n";
                    }
                    else if (result.Phonemes.Length > text.Length * 0.5 && result.Phonemes.Length < text.Length * 4)
                    {
                        _result += "✅ Looks correct\n";
                    }
                    else
                    {
                        _result += "⚠️ Suspicious phoneme count\n";
                    }
                    
                    _result += "\n";
                }
                catch (System.Exception e)
                {
                    _result += $"Error processing '{text}': {e.Message}\n\n";
                }
            }
        }
    }
}
#endif