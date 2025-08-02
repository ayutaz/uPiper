using System;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using uPiper.Core.Phonemizers.Backend;
using uPiper.Core.Phonemizers.Backend.Chinese;
using Debug = UnityEngine.Debug;

namespace uPiper.Editor
{
    /// <summary>
    /// Editor window for testing and debugging Chinese dictionary (Phase 2)
    /// </summary>
    public class ChineseDictionaryDebugWindow : EditorWindow
    {
        private ChineseDictionaryLoader loader;
        private ChinesePinyinDictionary dictionary;
        
        private string testText = "你好世界！这是一个测试。人工智能和机器学习。";
        private string characterCount = "N/A";
        private string phraseCount = "N/A";
        private string ipaCount = "N/A";
        private string memoryUsage = "N/A";
        private string loadTime = "N/A";
        private bool isLoading = false;
        private bool isLoaded = false;
        
        private Vector2 scrollPosition;
        private string lookupResult = "";
        private string performanceLog = "";
        
        [MenuItem("uPiper/Debug/Chinese Dictionary Debug (Phase 2)")]
        public static void ShowWindow()
        {
            var window = GetWindow<ChineseDictionaryDebugWindow>("Chinese Dictionary Debug");
            window.minSize = new Vector2(600, 400);
        }
        
        private void OnEnable()
        {
            loader = new ChineseDictionaryLoader();
        }
        
        private void OnGUI()
        {
            GUILayout.Label("Chinese Dictionary Debug Tool (Phase 2)", EditorStyles.boldLabel);
            GUILayout.Space(10);
            
            // Load Dictionary Section
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Dictionary Loading", EditorStyles.boldLabel);
            
            EditorGUI.BeginDisabledGroup(isLoading || isLoaded);
            if (GUILayout.Button("Load Expanded Dictionary", GUILayout.Height(30)))
            {
                LoadDictionaryAsync();
            }
            EditorGUI.EndDisabledGroup();
            
            if (isLoading)
            {
                EditorGUILayout.HelpBox("Loading dictionary...", MessageType.Info);
            }
            
            EditorGUILayout.EndVertical();
            
            // Statistics Section
            if (isLoaded)
            {
                GUILayout.Space(10);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label("Dictionary Statistics", EditorStyles.boldLabel);
                
                EditorGUILayout.LabelField("Characters:", characterCount);
                EditorGUILayout.LabelField("Phrases:", phraseCount);
                EditorGUILayout.LabelField("IPA Mappings:", ipaCount);
                EditorGUILayout.LabelField("Memory Usage:", memoryUsage);
                EditorGUILayout.LabelField("Load Time:", loadTime);
                
                EditorGUILayout.EndVertical();
                
                // Test Section
                GUILayout.Space(10);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label("Dictionary Testing", EditorStyles.boldLabel);
                
                EditorGUILayout.LabelField("Test Text:");
                testText = EditorGUILayout.TextArea(testText, GUILayout.Height(50));
                
                if (GUILayout.Button("Test Phonemization"))
                {
                    TestPhonemization();
                }
                
                if (GUILayout.Button("Test Performance (1000 iterations)"))
                {
                    TestPerformance();
                }
                
                if (GUILayout.Button("Test Memory Pressure"))
                {
                    TestMemoryPressure();
                }
                
                EditorGUILayout.EndVertical();
                
                // Results Section
                if (!string.IsNullOrEmpty(lookupResult))
                {
                    GUILayout.Space(10);
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    GUILayout.Label("Results", EditorStyles.boldLabel);
                    
                    scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));
                    EditorGUILayout.TextArea(lookupResult, GUILayout.ExpandHeight(true));
                    EditorGUILayout.EndScrollView();
                    
                    EditorGUILayout.EndVertical();
                }
            }
            
            // Performance Log
            if (!string.IsNullOrEmpty(performanceLog))
            {
                GUILayout.Space(10);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label("Performance Log", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(performanceLog, GUILayout.Height(100));
                EditorGUILayout.EndVertical();
            }
        }
        
        private async void LoadDictionaryAsync()
        {
            isLoading = true;
            performanceLog = "";
            
            try
            {
                // Measure memory before loading
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                var memoryBefore = GC.GetTotalMemory(false);
                
                // Measure load time
                var stopwatch = Stopwatch.StartNew();
                
                await Task.Run(async () =>
                {
                    dictionary = await loader.LoadAsync();
                });
                
                stopwatch.Stop();
                loadTime = $"{stopwatch.ElapsedMilliseconds} ms";
                
                // Measure memory after loading
                var memoryAfter = GC.GetTotalMemory(false);
                var memoryUsedBytes = memoryAfter - memoryBefore;
                memoryUsage = FormatBytes(memoryUsedBytes);
                
                // Update statistics
                characterCount = dictionary.CharacterCount.ToString("N0");
                phraseCount = dictionary.PhraseCount.ToString("N0");
                ipaCount = dictionary.IPACount.ToString("N0");
                
                isLoaded = true;
                
                Debug.Log($"[ChineseDictionaryDebug] Dictionary loaded successfully:");
                Debug.Log($"  - Characters: {characterCount}");
                Debug.Log($"  - Phrases: {phraseCount}");
                Debug.Log($"  - IPA Mappings: {ipaCount}");
                Debug.Log($"  - Memory Usage: {memoryUsage}");
                Debug.Log($"  - Load Time: {loadTime}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ChineseDictionaryDebug] Failed to load dictionary: {ex.Message}");
                EditorUtility.DisplayDialog("Error", $"Failed to load dictionary: {ex.Message}", "OK");
            }
            finally
            {
                isLoading = false;
            }
        }
        
        private void TestPhonemization()
        {
            if (dictionary == null) return;
            
            lookupResult = "=== Phonemization Test ===\n\n";
            
            var normalizer = new ChineseTextNormalizer();
            var converter = new PinyinConverter(dictionary);
            var ipaConverter = new PinyinToIPAConverter(dictionary);
            
            // Normalize text
            var normalized = normalizer.Normalize(testText);
            lookupResult += $"Normalized: {normalized}\n\n";
            
            // Convert to pinyin
            var pinyin = converter.GetPinyin(normalized);
            lookupResult += $"Pinyin: {string.Join(" ", pinyin)}\n\n";
            
            // Convert to IPA
            var ipa = ipaConverter.ConvertMultipleToIPA(pinyin);
            lookupResult += $"IPA: {string.Join(" ", ipa)}\n\n";
            
            // Test individual characters
            lookupResult += "Character Details:\n";
            foreach (char c in testText)
            {
                if (ChineseTextNormalizer.IsChinese(c))
                {
                    if (dictionary.TryGetCharacterPinyin(c, out var pinyinOptions))
                    {
                        lookupResult += $"  {c}: {string.Join(", ", pinyinOptions)}\n";
                    }
                    else
                    {
                        lookupResult += $"  {c}: [NOT FOUND]\n";
                    }
                }
            }
        }
        
        private void TestPerformance()
        {
            if (dictionary == null) return;
            
            performanceLog = "=== Performance Test (1000 iterations) ===\n";
            
            var normalizer = new ChineseTextNormalizer();
            var converter = new PinyinConverter(dictionary);
            var ipaConverter = new PinyinToIPAConverter(dictionary);
            
            // Warm up
            for (int i = 0; i < 10; i++)
            {
                var normalized = normalizer.Normalize(testText);
                var pinyin = converter.GetPinyin(normalized);
                var ipa = ipaConverter.ConvertMultipleToIPA(pinyin);
            }
            
            // Actual test
            var stopwatch = Stopwatch.StartNew();
            
            for (int i = 0; i < 1000; i++)
            {
                var normalized = normalizer.Normalize(testText);
                var pinyin = converter.GetPinyin(normalized);
                var ipa = ipaConverter.ConvertMultipleToIPA(pinyin);
            }
            
            stopwatch.Stop();
            
            var avgTime = stopwatch.ElapsedMilliseconds / 1000.0;
            var charsPerSecond = (testText.Length * 1000) / stopwatch.ElapsedMilliseconds;
            
            performanceLog += $"Total time: {stopwatch.ElapsedMilliseconds} ms\n";
            performanceLog += $"Average per iteration: {avgTime:F2} ms\n";
            performanceLog += $"Characters per second: {charsPerSecond:N0}\n";
            performanceLog += $"Text length: {testText.Length} characters\n";
            
            Debug.Log($"[ChineseDictionaryDebug] Performance test completed: {avgTime:F2} ms per iteration");
        }
        
        private void TestMemoryPressure()
        {
            if (dictionary == null) return;
            
            performanceLog = "=== Memory Pressure Test ===\n";
            
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var memoryStart = GC.GetTotalMemory(false);
            
            // Create many phonemizer instances
            var phonemizers = new uPiper.Core.Phonemizers.Backend.ChinesePhonemizer[100];
            for (int i = 0; i < phonemizers.Length; i++)
            {
                phonemizers[i] = new uPiper.Core.Phonemizers.Backend.ChinesePhonemizer();
            }
            
            var memoryAfterCreate = GC.GetTotalMemory(false);
            
            // Process text multiple times
            var normalizer = new ChineseTextNormalizer();
            var converter = new PinyinConverter(dictionary);
            
            for (int i = 0; i < 100; i++)
            {
                var normalized = normalizer.Normalize(testText);
                var pinyin = converter.GetPinyin(normalized);
            }
            
            var memoryAfterProcess = GC.GetTotalMemory(false);
            
            performanceLog += $"Memory at start: {FormatBytes(memoryStart)}\n";
            performanceLog += $"After creating 100 instances: {FormatBytes(memoryAfterCreate)}\n";
            performanceLog += $"After processing 100 times: {FormatBytes(memoryAfterProcess)}\n";
            performanceLog += $"Total increase: {FormatBytes(memoryAfterProcess - memoryStart)}\n";
            
            // Cleanup
            phonemizers = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var memoryAfterCleanup = GC.GetTotalMemory(false);
            performanceLog += $"After cleanup: {FormatBytes(memoryAfterCleanup)}\n";
        }
        
        private string FormatBytes(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:F2} KB";
            return $"{bytes / (1024.0 * 1024.0):F2} MB";
        }
    }
}