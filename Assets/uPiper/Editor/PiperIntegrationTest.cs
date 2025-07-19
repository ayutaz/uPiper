using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Unity.InferenceEngine;
using UnityEditor;
using UnityEngine;
using uPiper.Core;
using uPiper.Core.AudioGeneration;
using uPiper.Core.Logging;
using uPiper.Core.Phonemizers.Implementations;

namespace uPiper.Editor
{
    /// <summary>
    /// Phase 1.9 統合テスト - 実際のONNXモデルを使用した音声生成
    /// </summary>
    public class PiperIntegrationTest : EditorWindow
    {
        private PiperTTS _tts;
        private string _inputText = "こんにちは";
        private bool _isProcessing;
        private string _status = "準備完了";
        private AudioClip _lastGeneratedClip;

        [MenuItem("uPiper/Demo/Phase 1.9 Integration Test")]
        public static void ShowWindow()
        {
            GetWindow<PiperIntegrationTest>("Piper Integration Test");
        }

        private async void OnEnable()
        {
            await InitializeTTS();
        }

        private void OnDisable()
        {
            _tts?.Dispose();
            _tts = null;

            if (_lastGeneratedClip != null)
            {
                DestroyImmediate(_lastGeneratedClip);
            }
        }

        private async Task InitializeTTS()
        {
            try
            {
                _status = "初期化中...";

                // 基本設定で初期化
                var config = new PiperConfig
                {
                    DefaultLanguage = "ja",
                    EnablePhonemeCache = true,
                    EnableDebugLogging = true
                };

                // PiperTTSを作成
                _tts = new PiperTTS(config);

                await _tts.InitializeAsync();

                // モデルとコンフィグをロード
                var modelAsset = Resources.Load<ModelAsset>("Models/ja_JP-test-medium");
                if (modelAsset == null)
                {
                    throw new Exception("モデルが見つかりません");
                }

                var jsonAsset = Resources.Load<TextAsset>("Models/ja_JP-test-medium.onnx");
                var voiceConfig = ParseVoiceConfig(jsonAsset.text, "ja_JP-test-medium");

                // InferenceEngineで初期化
                await _tts.InitializeWithInferenceAsync(modelAsset, voiceConfig);

                _status = "初期化完了";
            }
            catch (Exception ex)
            {
                _status = $"初期化エラー: {ex.Message}";
                PiperLogger.LogError($"TTS初期化エラー: {ex}");
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Piper TTS 統合テスト", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // ステータス
            EditorGUILayout.LabelField("ステータス:", _status);
            EditorGUILayout.Space();

            // テキスト入力
            EditorGUILayout.LabelField("テキスト:");
            _inputText = EditorGUILayout.TextArea(_inputText, GUILayout.Height(60));

            EditorGUILayout.Space();

            // 生成ボタン
            EditorGUI.BeginDisabledGroup(_isProcessing || _tts == null || !_tts.IsInitialized);
            if (GUILayout.Button("音声生成 (OpenJTalk + InferenceEngine)", GUILayout.Height(30)))
            {
                _ = GenerateAudioAsync();
            }
            EditorGUI.EndDisabledGroup();

            // AudioClip再生
            if (_lastGeneratedClip != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("生成された音声:");

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("再生"))
                {
                    PlayClip(_lastGeneratedClip);
                }
                if (GUILayout.Button("停止"))
                {
                    StopAllClips();
                }
                GUILayout.EndHorizontal();

                EditorGUILayout.LabelField($"長さ: {_lastGeneratedClip.length:F2}秒");
                EditorGUILayout.LabelField($"サンプルレート: {_lastGeneratedClip.frequency}Hz");
            }
        }

        private async Task GenerateAudioAsync()
        {
            _isProcessing = true;
            _status = "処理中...";
            Repaint();

            try
            {
                // 音素化情報をログ
                _status = "音素化中...";
                Repaint();

                var phonemeResult = await _tts.GetPhonemesAsync(_inputText);
                PiperLogger.LogInfo($"音素化結果: {string.Join(" ", phonemeResult.Phonemes)}");

                // InferenceEngineを使った音声生成
                _status = "音声生成中...";
                Repaint();

                if (_lastGeneratedClip != null)
                {
                    DestroyImmediate(_lastGeneratedClip);
                }

                _lastGeneratedClip = await _tts.GenerateAudioWithInferenceAsync(_inputText, System.Threading.CancellationToken.None);

                _status = $"生成完了！ ({_lastGeneratedClip.length:F2}秒)";
            }
            catch (Exception ex)
            {
                _status = $"エラー: {ex.Message}";
                PiperLogger.LogError($"音声生成エラー: {ex}");
            }
            finally
            {
                _isProcessing = false;
                Repaint();
            }
        }

        private PiperVoiceConfig ParseVoiceConfig(string json, string voiceId)
        {
            var jsonObj = JObject.Parse(json);
            var config = new PiperVoiceConfig
            {
                VoiceId = voiceId,
                DisplayName = voiceId,
                Language = jsonObj["language"]?["code"]?.ToString() ?? "ja",
                SampleRate = jsonObj["audio"]?["sample_rate"]?.ToObject<int>() ?? 22050,
                ModelPath = $"Models/{voiceId}",
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

        private void PlayClip(AudioClip clip)
        {
            if (clip == null) return;

            var go = new GameObject("TempAudioSource");
            go.hideFlags = HideFlags.HideAndDontSave;
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.Play();

            // 再生終了後に自動削除
            EditorApplication.delayCall += () =>
            {
                EditorApplication.delayCall += () =>
                {
                    if (source != null && !source.isPlaying)
                    {
                        DestroyImmediate(go);
                    }
                };
            };
        }

        private void StopAllClips()
        {
            var sources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
            foreach (var source in sources)
            {
                if (source.gameObject.name == "TempAudioSource")
                {
                    DestroyImmediate(source.gameObject);
                }
            }
        }
    }
}