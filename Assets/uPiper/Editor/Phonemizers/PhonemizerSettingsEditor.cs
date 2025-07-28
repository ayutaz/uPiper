using UnityEngine;
using UnityEditor;
using uPiper.Phonemizers.Configuration;
using uPiper.Core.Phonemizers.Unity;
using System.Linq;

namespace uPiper.Editor.Phonemizers
{
    /// <summary>
    /// Custom editor for PhonemizerSettings
    /// </summary>
    [CustomEditor(typeof(PhonemizerSettings))]
    public class PhonemizerSettingsEditor : UnityEditor.Editor
    {
        private SerializedProperty enablePhonemizerService;
        private SerializedProperty defaultLanguage;
        private SerializedProperty languageSettings;
        
        private bool showPerformanceSettings = true;
        private bool showMobileSettings = true;
        private bool showDataManagement = true;
        private bool showErrorHandling = true;
        private bool showDebugSettings = true;

        private void OnEnable()
        {
            enablePhonemizerService = serializedObject.FindProperty("enablePhonemizerService");
            defaultLanguage = serializedObject.FindProperty("defaultLanguage");
            languageSettings = serializedObject.FindProperty("languageSettings");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Header
            EditorGUILayout.LabelField("uPiper Phonemizer Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // General Settings
            EditorGUILayout.PropertyField(enablePhonemizerService);
            EditorGUILayout.PropertyField(defaultLanguage);
            
            EditorGUILayout.Space();
            
            // Language Settings
            EditorGUILayout.LabelField("Language Configuration", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            
            for (int i = 0; i < languageSettings.arraySize; i++)
            {
                var langProp = languageSettings.GetArrayElementAtIndex(i);
                var langCode = langProp.FindPropertyRelative("languageCode").stringValue;
                var displayName = langProp.FindPropertyRelative("displayName").stringValue;
                
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                var foldout = EditorGUILayout.Foldout(
                    langProp.isExpanded, 
                    $"{displayName} ({langCode})", 
                    true
                );
                langProp.isExpanded = foldout;
                
                if (foldout)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(langProp, GUIContent.none, true);
                    
                    if (GUILayout.Button("Remove Language", GUILayout.Width(120)))
                    {
                        languageSettings.DeleteArrayElementAtIndex(i);
                        break;
                    }
                    EditorGUI.indentLevel--;
                }
                
                EditorGUILayout.EndVertical();
            }
            
            if (GUILayout.Button("Add Language", GUILayout.Width(120)))
            {
                languageSettings.InsertArrayElementAtIndex(languageSettings.arraySize);
            }
            
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            // Collapsible sections
            showPerformanceSettings = EditorGUILayout.Foldout(showPerformanceSettings, "Performance Settings", true);
            if (showPerformanceSettings)
            {
                EditorGUI.indentLevel++;
                DrawPerformanceSettings();
                EditorGUI.indentLevel--;
            }

            showMobileSettings = EditorGUILayout.Foldout(showMobileSettings, "Mobile Optimization", true);
            if (showMobileSettings)
            {
                EditorGUI.indentLevel++;
                DrawMobileSettings();
                EditorGUI.indentLevel--;
            }

            showDataManagement = EditorGUILayout.Foldout(showDataManagement, "Data Management", true);
            if (showDataManagement)
            {
                EditorGUI.indentLevel++;
                DrawDataManagementSettings();
                EditorGUI.indentLevel--;
            }

            showErrorHandling = EditorGUILayout.Foldout(showErrorHandling, "Error Handling", true);
            if (showErrorHandling)
            {
                EditorGUI.indentLevel++;
                DrawErrorHandlingSettings();
                EditorGUI.indentLevel--;
            }

            showDebugSettings = EditorGUILayout.Foldout(showDebugSettings, "Debug Settings", true);
            if (showDebugSettings)
            {
                EditorGUI.indentLevel++;
                DrawDebugSettings();
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
            
            EditorGUILayout.Space();
            DrawActionButtons();
        }

        private void DrawPerformanceSettings()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxConcurrentOperations"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("cacheSize"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("cacheMemoryLimitMB"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("enableBatchProcessing"));
            
            if (serializedObject.FindProperty("enableBatchProcessing").boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("batchSize"));
                EditorGUI.indentLevel--;
            }
        }

        private void DrawMobileSettings()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("enableMobileOptimization"));
            
            if (serializedObject.FindProperty("enableMobileOptimization").boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("reduceCacheOnLowMemory"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("pauseOnApplicationPause"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("mobileMaxConcurrentOperations"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("mobileCacheMemoryLimitMB"));
                EditorGUI.indentLevel--;
            }
        }

        private void DrawDataManagementSettings()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("dataPath"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("autoDownloadEssentialData"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("downloadOverCellular"));
            
            EditorGUILayout.Space();
            
            if (Application.isPlaying)
            {
                if (GUILayout.Button("Check Data Status"))
                {
                    CheckDataStatus();
                }
            }
        }

        private void DrawErrorHandlingSettings()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxRetries"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("retryDelay"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("enableFallbackPhonemeizer"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("circuitBreakerThreshold"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("circuitBreakerResetTime"));
        }

        private void DrawDebugSettings()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("enableDebugLogging"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("logPhonemeOutput"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("measurePerformance"));
        }

        private void DrawActionButtons()
        {
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Create Default Settings"))
            {
                CreateDefaultSettings();
            }
            
            if (Application.isPlaying)
            {
                if (GUILayout.Button("Test Phonemization"))
                {
                    TestPhonemizerWindow.ShowWindow();
                }
                
                if (GUILayout.Button("View Cache Statistics"))
                {
                    ShowCacheStatistics();
                }
            }
            
            EditorGUILayout.EndHorizontal();
        }

        private void CreateDefaultSettings()
        {
            var settings = PhonemizerSettings.CreateDefault();
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Phonemizer Settings",
                "PhonemizerSettings",
                "asset",
                "Save phonemizer settings asset"
            );
            
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.CreateAsset(settings, path);
                AssetDatabase.SaveAssets();
                Selection.activeObject = settings;
            }
        }

        private void CheckDataStatus()
        {
            var service = UnityPhonemizerService.Instance;
            if (service != null)
            {
                var languages = service.GetAvailableLanguages();
                foreach (var lang in languages)
                {
                    bool available = service.IsLanguageDataAvailable(lang);
                    Debug.Log($"Language {lang}: {(available ? "Available" : "Not Downloaded")}");
                }
            }
        }

        private void ShowCacheStatistics()
        {
            var service = UnityPhonemizerService.Instance;
            if (service != null)
            {
                var stats = service.GetCacheStatistics();
                Debug.Log($"Cache Statistics - Count: {stats.count}, Memory: {stats.memoryBytes / 1024f / 1024f:F2} MB, Hit Rate: {stats.hitRate:P}");
            }
        }
    }

    /// <summary>
    /// Test window for phonemization
    /// </summary>
    public class TestPhonemizerWindow : EditorWindow
    {
        private string testText = "Hello, this is a test.";
        private string selectedLanguage = "en-US";
        private string result = "";
        
        public static void ShowWindow()
        {
            var window = GetWindow<TestPhonemizerWindow>("Test Phonemizer");
            window.minSize = new Vector2(400, 300);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Test Phonemization", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            selectedLanguage = EditorGUILayout.TextField("Language", selectedLanguage);
            EditorGUILayout.LabelField("Test Text:");
            testText = EditorGUILayout.TextArea(testText, GUILayout.Height(60));
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button("Test Phonemization"))
            {
                TestPhonemizerService();
            }
            
            EditorGUILayout.Space();
            
            if (!string.IsNullOrEmpty(result))
            {
                EditorGUILayout.LabelField("Result:", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(result, GUILayout.Height(100));
            }
        }

        private void TestPhonemizerService()
        {
            var service = UnityPhonemizerService.Instance;
            if (service != null)
            {
                result = "Processing...";
                service.PhonemizeAsync(testText, selectedLanguage, 
                    (phonemeResult) =>
                    {
                        result = string.Join(" ", phonemeResult.Phonemes);
                        Repaint();
                    },
                    (error) =>
                    {
                        result = $"Error: {error.Message}";
                        Repaint();
                    }
                );
            }
            else
            {
                result = "Phonemizer service not available";
            }
        }
    }
}