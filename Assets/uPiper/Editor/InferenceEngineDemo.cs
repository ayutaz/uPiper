using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using uPiper.Core;
using uPiper.Core.AudioGeneration;
using uPiper.Core.Logging;
#if UNITY_AI_INTERFACE_2_2_OR_NEWER
using Unity.InferenceEngine;
#endif

namespace uPiper.Editor
{
    /// <summary>
    /// Unity.InferenceEngineを使用したPiper TTSデモ
    /// </summary>
    public class InferenceEngineDemo : EditorWindow
    {
#if UNITY_AI_INTERFACE_2_2_OR_NEWER
        private string _inputText = "こんにちは、世界！";
        private string _selectedModel = "ja_JP-test-medium";
        private AudioClip _generatedClip;
        private bool _isGenerating;
        private string _status = "準備完了";
        
        private InferenceAudioGenerator _generator;
        private PhonemeEncoder _encoder;
        private AudioClipBuilder _audioBuilder;
        private PiperVoiceConfig _currentConfig;
#endif

        [MenuItem("Window/uPiper/Inference Engine Demo")]
        public static void ShowWindow()
        {
            GetWindow<InferenceEngineDemo>("Piper TTS Demo");
        }

#if UNITY_AI_INTERFACE_2_2_OR_NEWER

        private void OnEnable()
        {
            _generator = new InferenceAudioGenerator();
            _audioBuilder = new AudioClipBuilder();
        }

        private void OnDisable()
        {
            _generator?.Dispose();
            _generator = null;
            
            if (_generatedClip != null)
            {
                DestroyImmediate(_generatedClip);
                _generatedClip = null;
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Unity.InferenceEngine Piper TTS Demo", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // モデル選択
            EditorGUILayout.LabelField("モデル選択:");
            var models = new[] { "ja_JP-test-medium", "test_voice" };
            var selectedIndex = Array.IndexOf(models, _selectedModel);
            var newIndex = EditorGUILayout.Popup(selectedIndex, models);
            if (newIndex != selectedIndex && newIndex >= 0)
            {
                _selectedModel = models[newIndex];
                _inputText = _selectedModel == "ja_JP-test-medium" ? "こんにちは、世界！" : "Hello, world!";
            }

            // テキスト入力
            EditorGUILayout.LabelField("テキスト:");
            _inputText = EditorGUILayout.TextArea(_inputText, GUILayout.Height(60));

            EditorGUILayout.Space();

            // 生成ボタン
            EditorGUI.BeginDisabledGroup(_isGenerating);
            if (GUILayout.Button("音声生成", GUILayout.Height(30)))
            {
                _ = GenerateAudioAsync();
            }
            EditorGUI.EndDisabledGroup();

            // ステータス表示
            EditorGUILayout.LabelField("ステータス:", _status);

            // 再生コントロール
            if (_generatedClip != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("生成された音声:");
                
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("再生"))
                {
                    PlayAudioClip(_generatedClip);
                }
                if (GUILayout.Button("停止"))
                {
                    StopAudioClip();
                }
                GUILayout.EndHorizontal();

                // クリップ情報
                EditorGUILayout.LabelField($"長さ: {_generatedClip.length:F2}秒");
                EditorGUILayout.LabelField($"サンプルレート: {_generatedClip.frequency}Hz");
            }
        }

        private async Task GenerateAudioAsync()
        {
            _isGenerating = true;
            _status = "処理中...";
            Repaint();

            try
            {
                // モデルとコンフィグをロード
                _status = "モデルをロード中...";
                Repaint();
                
                var modelPath = $"Models/{_selectedModel}";
                var modelAsset = Resources.Load<ModelAsset>(modelPath);
                if (modelAsset == null)
                {
                    throw new Exception($"モデルが見つかりません: {modelPath}");
                }

                // JSONコンフィグをロード
                var jsonPath = $"Models/{_selectedModel}.onnx.json";
                var jsonAsset = Resources.Load<TextAsset>(jsonPath);
                if (jsonAsset == null)
                {
                    throw new Exception($"設定ファイルが見つかりません: {jsonPath}");
                }

                var config = ParseConfig(jsonAsset.text);
                
                // エンコーダーを初期化
                _encoder = new PhonemeEncoder(config);

                // ジェネレーターを初期化
                _status = "ジェネレーターを初期化中...";
                Repaint();
                
                await _generator.InitializeAsync(modelAsset, config);

                // 音素に変換（簡易実装）
                _status = "音素に変換中...";
                Repaint();
                
                var phonemes = ConvertToPhonemes(_inputText);
                PiperLogger.LogDebug($"Phonemes: {string.Join(" ", phonemes)}");

                // 音素をIDに変換
                var phonemeIds = _encoder.Encode(phonemes);
                PiperLogger.LogDebug($"Phoneme IDs: {string.Join(", ", phonemeIds)}");

                // 音声生成
                _status = "音声を生成中...";
                Repaint();
                
                var audioData = await _generator.GenerateAudioAsync(phonemeIds);
                
                // AudioClipを作成
                _status = "AudioClipを作成中...";
                Repaint();
                
                if (_generatedClip != null)
                {
                    DestroyImmediate(_generatedClip);
                }
                
                _generatedClip = _audioBuilder.BuildAudioClip(
                    audioData, 
                    config.SampleRate, 
                    $"Generated_{DateTime.Now:HHmmss}"
                );

                _status = "生成完了！";
            }
            catch (Exception ex)
            {
                _status = $"エラー: {ex.Message}";
                PiperLogger.LogError($"音声生成エラー: {ex}");
            }
            finally
            {
                _isGenerating = false;
                Repaint();
            }
        }

        private PiperVoiceConfig ParseConfig(string json)
        {
            var jsonObj = JObject.Parse(json);
            var config = new PiperVoiceConfig
            {
                VoiceId = _selectedModel,
                DisplayName = _selectedModel,
                Language = jsonObj["language"]?["code"]?.ToString() ?? "ja",
                SampleRate = jsonObj["audio"]?["sample_rate"]?.ToObject<int>() ?? 22050,
                PhonemeIdMap = new Dictionary<string, int>()
            };

            // phoneme_id_mapをパース
            var phonemeIdMap = jsonObj["phoneme_id_map"] as JObject;
            if (phonemeIdMap != null)
            {
                foreach (var kvp in phonemeIdMap)
                {
                    var idArray = kvp.Value as JArray;
                    if (idArray != null && idArray.Count > 0)
                    {
                        config.PhonemeIdMap[kvp.Key] = idArray[0].ToObject<int>();
                    }
                }
            }

            return config;
        }

        private string[] ConvertToPhonemes(string text)
        {
            // 簡易的な音素変換（実際にはOpenJTalkを使用すべき）
            if (_selectedModel == "ja_JP-test-medium")
            {
                // 日本語の簡易音素変換
                var phonemeMap = new Dictionary<string, string[]>
                {
                    { "こ", new[] { "k", "o" } },
                    { "ん", new[] { "N" } },
                    { "に", new[] { "n", "i" } },
                    { "ち", new[] { "t", "i" } },
                    { "は", new[] { "h", "a" } },
                    { "、", new[] { "?" } },
                    { "世", new[] { "s", "e" } },
                    { "界", new[] { "k", "a", "i" } },
                    { "！", new[] { "!" } }
                };

                var phonemes = new List<string>();
                foreach (char c in text)
                {
                    var key = c.ToString();
                    if (phonemeMap.TryGetValue(key, out var ph))
                    {
                        phonemes.AddRange(ph);
                    }
                }
                return phonemes.ToArray();
            }
            else
            {
                // 英語の簡易音素変換（文字をそのまま使用）
                return text.ToLower()
                    .Replace(",", " ,")
                    .Replace(".", " .")
                    .Replace("!", " !")
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries);
            }
        }

        private void PlayAudioClip(AudioClip clip)
        {
            if (clip == null) return;

            // EditorでAudioClipを再生
            var audioSource = CreateAudioSource();
            audioSource.clip = clip;
            audioSource.Play();

            EditorApplication.delayCall += () =>
            {
                if (audioSource != null && !audioSource.isPlaying)
                {
                    DestroyImmediate(audioSource.gameObject);
                }
            };
        }

        private void StopAudioClip()
        {
            var audioSources = FindObjectsOfType<AudioSource>();
            foreach (var source in audioSources)
            {
                if (source.clip == _generatedClip)
                {
                    source.Stop();
                    DestroyImmediate(source.gameObject);
                }
            }
        }

        private AudioSource CreateAudioSource()
        {
            var go = new GameObject("TempAudioSource");
            go.hideFlags = HideFlags.HideAndDontSave;
            return go.AddComponent<AudioSource>();
        }
#else // !UNITY_AI_INTERFACE_2_2_OR_NEWER
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Unity.InferenceEngine Not Available", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            EditorGUILayout.HelpBox(
                "Unity.InferenceEngine package is not available in this Unity version.\n\n" +
                "Please ensure you have:\n" +
                "1. Unity 6000.0 or newer\n" +
                "2. com.unity.ai.inference package installed (version 2.2.0 or newer)\n" +
                "3. Project properly configured\n\n" +
                "Current Unity version: " + Application.unityVersion,
                MessageType.Warning
            );
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button("Open Package Manager"))
            {
                UnityEditor.PackageManager.UI.Window.Open("com.unity.ai.inference");
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Phase 1.9 features require Unity.InferenceEngine for ONNX model support.");
        }
#endif // UNITY_AI_INTERFACE_2_2_OR_NEWER
    }
}