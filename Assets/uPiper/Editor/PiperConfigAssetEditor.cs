using UnityEditor;
using UnityEngine;
using uPiper.Core;

namespace uPiper.Editor
{
    [CustomEditor(typeof(PiperConfigAsset))]
    public sealed class PiperConfigAssetEditor : UnityEditor.Editor
    {
        private SerializedProperty _configProperty;
        private string _validationError;
        private bool _isValid;

        private void OnEnable()
        {
            _configProperty = serializedObject.FindProperty("_config");
            ValidateConfig();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("uPiper Configuration Asset", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // バリデーション状態
            if (_isValid)
            {
                EditorGUILayout.HelpBox("設定は有効です。", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"バリデーションエラー:\n{_validationError}", MessageType.Error);
            }

            EditorGUILayout.Space(4);

            // PiperConfig の Inspector 描画
            EditorGUILayout.PropertyField(
                _configProperty, new GUIContent("Configuration"), true);

            if (serializedObject.ApplyModifiedProperties())
            {
                ValidateConfig();
            }

            EditorGUILayout.Space(8);

            // ボタン
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("バリデーション実行"))
            {
                ValidateConfig();
            }

            if (GUILayout.Button("デフォルトにリセット"))
            {
                if (EditorUtility.DisplayDialog(
                    "設定のリセット",
                    "すべての設定をデフォルト値にリセットしますか？",
                    "リセット",
                    "キャンセル"))
                {
                    ResetToDefault();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void ValidateConfig()
        {
            var asset = (PiperConfigAsset)target;
            try
            {
                asset.ToValidated();
                _isValid = true;
                _validationError = null;
            }
            catch (PiperException ex)
            {
                _isValid = false;
                _validationError = ex.Message;
            }
        }

        private void ResetToDefault()
        {
            Undo.RecordObject(target, "Reset PiperConfigAsset to Default");

            var defaultConfig = PiperConfig.CreateDefault();

            // General Settings
            _configProperty.FindPropertyRelative("EnableDebugLogging").boolValue =
                defaultConfig.EnableDebugLogging;
            _configProperty.FindPropertyRelative("DefaultLanguage").stringValue =
                defaultConfig.DefaultLanguage;
            _configProperty.FindPropertyRelative("AutoDetectLanguage").boolValue =
                defaultConfig.AutoDetectLanguage;

            // Fallback Settings
            _configProperty.FindPropertyRelative("FallbackLanguage").stringValue =
                defaultConfig.FallbackLanguage;

            // Multilingual Settings
            var supportedLangsProp =
                _configProperty.FindPropertyRelative("SupportedLanguages");
            supportedLangsProp.ClearArray();
            for (var i = 0; i < defaultConfig.SupportedLanguages.Count; i++)
            {
                supportedLangsProp.InsertArrayElementAtIndex(i);
                supportedLangsProp.GetArrayElementAtIndex(i).stringValue =
                    defaultConfig.SupportedLanguages[i];
            }

            _configProperty.FindPropertyRelative("MixedLanguageMode").enumValueIndex =
                (int)defaultConfig.MixedLanguageMode;

            // Performance Settings
            _configProperty.FindPropertyRelative("MaxCacheSizeMB").intValue =
                defaultConfig.MaxCacheSizeMB;
            _configProperty.FindPropertyRelative("EnablePhonemeCache").boolValue =
                defaultConfig.EnablePhonemeCache;
            _configProperty.FindPropertyRelative("WorkerThreads").intValue =
                defaultConfig.WorkerThreads;
            _configProperty.FindPropertyRelative("Backend").enumValueIndex =
                (int)defaultConfig.Backend;

            // Sentence Silence Settings
            _configProperty.FindPropertyRelative("EnablePhonemeSilence").boolValue =
                defaultConfig.EnablePhonemeSilence;
            _configProperty.FindPropertyRelative("PhonemeSilenceSpec").stringValue =
                defaultConfig.PhonemeSilenceSpec;

            // Audio Settings
            _configProperty.FindPropertyRelative("SampleRate").intValue =
                defaultConfig.SampleRate;
            _configProperty.FindPropertyRelative("NormalizeAudio").boolValue =
                defaultConfig.NormalizeAudio;
            _configProperty.FindPropertyRelative("TargetRMSLevel").floatValue =
                defaultConfig.TargetRMSLevel;

            // Advanced Settings
            _configProperty.FindPropertyRelative("EnableWarmup").boolValue =
                defaultConfig.EnableWarmup;
            _configProperty.FindPropertyRelative("WarmupIterations").intValue =
                defaultConfig.WarmupIterations;
            _configProperty.FindPropertyRelative("TimeoutMs").intValue =
                defaultConfig.TimeoutMs;
            _configProperty.FindPropertyRelative("EnableMultiThreadedInference").boolValue =
                defaultConfig.EnableMultiThreadedInference;
            _configProperty.FindPropertyRelative("InferenceBatchSize").intValue =
                defaultConfig.InferenceBatchSize;

            // GPU Settings
            var gpuSettingsProp = _configProperty.FindPropertyRelative("GPUSettings");
            gpuSettingsProp.FindPropertyRelative("MaxMemoryMB").intValue =
                defaultConfig.GPUSettings.MaxMemoryMB;

            _configProperty.FindPropertyRelative("AllowFallbackToCPU").boolValue =
                defaultConfig.AllowFallbackToCPU;

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            ValidateConfig();
        }
    }
}