using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;
using uPiper.Core;
using uPiper.Core.AudioGeneration;
using uPiper.Core.Logging;
using Unity.InferenceEngine;
using TMPro;

namespace uPiper.Demo
{
    /// <summary>
    /// Phase 1.9 - Unity.InferenceEngineを使用したPiper TTSデモ（シーン用）
    /// </summary>
    public class InferenceEngineDemo : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_InputField _inputField;
        [SerializeField] private Button _generateButton;
        [SerializeField] private TextMeshProUGUI _statusText;
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private TMP_Dropdown _modelDropdown;
        
        [Header("Settings")]
        [SerializeField] private string _defaultJapaneseText = "こんにちは、世界！";
        [SerializeField] private string _defaultEnglishText = "Hello, world!";
        
        private InferenceAudioGenerator _generator;
        private PhonemeEncoder _encoder;
        private AudioClipBuilder _audioBuilder;
        private PiperVoiceConfig _currentConfig;
        private bool _isGenerating;
        
        private readonly Dictionary<string, string> _modelLanguages = new Dictionary<string, string>
        {
            { "ja_JP-test-medium", "ja" },
            { "test_voice", "en" }
        };

        private void Start()
        {
            _generator = new InferenceAudioGenerator();
            _audioBuilder = new AudioClipBuilder();
            
            SetupUI();
            SetStatus("準備完了");
        }

        private void OnDestroy()
        {
            _generator?.Dispose();
        }

        private void SetupUI()
        {
            // モデル選択ドロップダウンの設定
            if (_modelDropdown != null)
            {
                _modelDropdown.ClearOptions();
                _modelDropdown.AddOptions(new List<string> { "ja_JP-test-medium", "test_voice" });
                _modelDropdown.onValueChanged.AddListener(OnModelChanged);
            }
            
            // 生成ボタンの設定
            if (_generateButton != null)
            {
                _generateButton.onClick.AddListener(() => _ = GenerateAudioAsync());
            }
            
            // 初期テキストの設定
            if (_inputField != null)
            {
                _inputField.text = _defaultJapaneseText;
            }
        }

        private void OnModelChanged(int index)
        {
            var modelName = index == 0 ? "ja_JP-test-medium" : "test_voice";
            var isJapanese = _modelLanguages[modelName] == "ja";
            
            if (_inputField != null)
            {
                _inputField.text = isJapanese ? _defaultJapaneseText : _defaultEnglishText;
            }
        }

        private async Task GenerateAudioAsync()
        {
            if (_isGenerating) return;
            if (string.IsNullOrWhiteSpace(_inputField?.text)) return;
            
            _isGenerating = true;
            SetStatus("処理中...");
            
            try
            {
                // モデル名を取得
                var modelName = _modelDropdown?.value == 0 ? "ja_JP-test-medium" : "test_voice";
                
                // モデルをロード
                SetStatus("モデルをロード中...");
                var modelAsset = Resources.Load<ModelAsset>($"Models/{modelName}");
                if (modelAsset == null)
                {
                    throw new Exception($"モデルが見つかりません: {modelName}");
                }
                
                // JSONコンフィグをロード
                var jsonAsset = Resources.Load<TextAsset>($"Models/{modelName}.onnx");
                if (jsonAsset == null)
                {
                    throw new Exception($"設定ファイルが見つかりません: {modelName}.onnx.json");
                }
                
                var config = ParseConfig(jsonAsset.text, modelName);
                _encoder = new PhonemeEncoder(config);
                
                // ジェネレーターを初期化
                SetStatus("ジェネレーターを初期化中...");
                await _generator.InitializeAsync(modelAsset, config);
                
                // 音素に変換
                SetStatus("音素に変換中...");
                var phonemes = ConvertToPhonemes(_inputField.text, _modelLanguages[modelName]);
                PiperLogger.LogDebug($"Phonemes: {string.Join(" ", phonemes)}");
                
                // 音素をIDに変換
                var phonemeIds = _encoder.Encode(phonemes);
                PiperLogger.LogDebug($"Phoneme IDs: {string.Join(", ", phonemeIds)}");
                
                // 音声生成
                SetStatus("音声を生成中...");
                var audioData = await _generator.GenerateAudioAsync(phonemeIds);
                
                // AudioClipを作成
                SetStatus("AudioClipを作成中...");
                var audioClip = _audioBuilder.BuildAudioClip(
                    audioData, 
                    config.SampleRate, 
                    $"Generated_{DateTime.Now:HHmmss}"
                );
                
                // 再生
                if (_audioSource != null && audioClip != null)
                {
                    _audioSource.clip = audioClip;
                    _audioSource.Play();
                }
                
                SetStatus($"生成完了！ ({audioClip.length:F2}秒)");
            }
            catch (Exception ex)
            {
                SetStatus($"エラー: {ex.Message}");
                PiperLogger.LogError($"音声生成エラー: {ex}");
            }
            finally
            {
                _isGenerating = false;
                if (_generateButton != null)
                {
                    _generateButton.interactable = true;
                }
            }
        }

        private PiperVoiceConfig ParseConfig(string json, string modelName)
        {
            var jsonObj = JObject.Parse(json);
            var config = new PiperVoiceConfig
            {
                VoiceId = modelName,
                DisplayName = modelName,
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

        private string[] ConvertToPhonemes(string text, string language)
        {
            // 簡易的な音素変換（実際にはOpenJTalkを使用すべき）
            if (language == "ja")
            {
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
                // 英語の簡易音素変換
                return text.ToLower()
                    .Replace(",", " ,")
                    .Replace(".", " .")
                    .Replace("!", " !")
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries);
            }
        }

        private void SetStatus(string status)
        {
            if (_statusText != null)
            {
                _statusText.text = status;
            }
            
            if (_generateButton != null)
            {
                _generateButton.interactable = !_isGenerating;
            }
        }
    }
}