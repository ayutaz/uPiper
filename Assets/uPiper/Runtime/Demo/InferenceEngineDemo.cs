using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TMPro;
using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.UI;
using uPiper.Core;
using uPiper.Core.AudioGeneration;
using uPiper.Core.Logging;

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
        [SerializeField] private string _defaultJapaneseText = "こんにちは";
        [SerializeField] private string _defaultEnglishText = "Hello world";

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
            if (_isGenerating)
            {
                PiperLogger.LogDebug("Already generating, skipping request");
                return;
            }

            if (string.IsNullOrWhiteSpace(_inputField?.text))
            {
                PiperLogger.LogWarning("Input text is empty");
                return;
            }

            _isGenerating = true;
            SetStatus("処理中...");
            PiperLogger.LogInfo($"Starting audio generation for text: {_inputField.text}");

            try
            {
                // モデル名を取得
                var modelName = _modelDropdown?.value == 0 ? "ja_JP-test-medium" : "test_voice";
                PiperLogger.LogDebug($"Selected model: {modelName}");

                // モデルをロード
                SetStatus("モデルをロード中...");
                PiperLogger.LogDebug($"Loading model asset: Models/{modelName}");
                var modelAsset = Resources.Load<ModelAsset>($"Models/{modelName}");
                if (modelAsset == null)
                {
                    throw new Exception($"モデルが見つかりません: {modelName}");
                }
                PiperLogger.LogDebug($"Model asset loaded successfully");

                // JSONコンフィグをロード
                PiperLogger.LogDebug($"Loading config: Models/{modelName}.onnx");
                var jsonAsset = Resources.Load<TextAsset>($"Models/{modelName}.onnx");
                if (jsonAsset == null)
                {
                    throw new Exception($"設定ファイルが見つかりません: {modelName}.onnx.json");
                }
                PiperLogger.LogDebug($"Config loaded, parsing JSON ({jsonAsset.text.Length} chars)");

                var config = ParseConfig(jsonAsset.text, modelName);
                _encoder = new PhonemeEncoder(config);
                PiperLogger.LogDebug($"PhonemeEncoder created with {config.PhonemeIdMap.Count} phonemes");

                // デバッグ用：いくつかの音素マッピングを表示
                int count = 0;
                foreach (var kvp in config.PhonemeIdMap)
                {
                    if (count++ < 10) // 最初の10個だけ表示
                    {
                        PiperLogger.LogDebug($"  Phoneme '{kvp.Key}' -> ID {kvp.Value}");
                    }
                }

                // ジェネレーターを初期化
                SetStatus("ジェネレーターを初期化中...");
                PiperLogger.LogDebug("Initializing InferenceAudioGenerator...");
                await _generator.InitializeAsync(modelAsset, config);
                PiperLogger.LogDebug("Generator initialized successfully");

                // 音素に変換
                SetStatus("音素に変換中...");
                var phonemes = ConvertToPhonemes(_inputField.text, _modelLanguages[modelName]);
                PiperLogger.LogInfo($"Phonemes ({phonemes.Length}): {string.Join(" ", phonemes)}");

                // 音素変換の詳細をログ出力
                PiperLogger.LogDebug($"Input text: '{_inputField.text}'");
                for (int i = 0; i < phonemes.Length; i++)
                {
                    PiperLogger.LogDebug($"  Phoneme[{i}]: '{phonemes[i]}'");
                }

                // 音素をIDに変換
                var phonemeIds = _encoder.Encode(phonemes);
                PiperLogger.LogInfo($"Phoneme IDs ({phonemeIds.Length}): {string.Join(", ", phonemeIds)}");

                // 音声生成
                SetStatus("音声を生成中...");
                PiperLogger.LogDebug("Calling GenerateAudioAsync...");
                var audioData = await _generator.GenerateAudioAsync(phonemeIds);
                PiperLogger.LogInfo($"Audio generated: {audioData.Length} samples");

                // 音声データの最大値を確認
                var maxVal = audioData.Max(x => Math.Abs(x));
                PiperLogger.LogInfo($"Original audio max amplitude: {maxVal:F4}");

                // 音声データが既に小さい値の場合は増幅、大きい値の場合は正規化
                float[] processedAudio;
                if (maxVal < 0.1f)
                {
                    // 音声が小さすぎる場合は増幅
                    SetStatus("音声データを増幅中...");
                    PiperLogger.LogDebug($"Amplifying audio data (max: {maxVal:F4} is too small)");
                    float amplificationFactor = 0.5f / maxVal; // 最大値を0.5にする
                    processedAudio = audioData.Select(x => x * amplificationFactor).ToArray();
                    PiperLogger.LogInfo($"Amplified audio by factor {amplificationFactor:F2}");
                }
                else if (maxVal > 1.0f)
                {
                    // 音声データを正規化
                    SetStatus("音声データを正規化中...");
                    PiperLogger.LogDebug("Normalizing audio data...");
                    processedAudio = _audioBuilder.NormalizeAudio(audioData, 0.95f);
                }
                else
                {
                    // 既に適切な範囲
                    processedAudio = audioData;
                    PiperLogger.LogDebug("Audio data is already in proper range");
                }

                // AudioClipを作成
                SetStatus("AudioClipを作成中...");
                PiperLogger.LogDebug($"Building AudioClip (sample rate: {config.SampleRate})");
                var audioClip = _audioBuilder.BuildAudioClip(
                    processedAudio,
                    config.SampleRate,
                    $"Generated_{DateTime.Now:HHmmss}"
                );
                PiperLogger.LogDebug($"AudioClip created: {audioClip.length:F2} seconds");

                // 再生
                if (_audioSource != null && audioClip != null)
                {
                    _audioSource.clip = audioClip;
                    _audioSource.Play();
                    PiperLogger.LogInfo("Audio playback started");
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
                PiperLogger.LogDebug("Audio generation process completed");
            }
        }

        private PiperVoiceConfig ParseConfig(string json, string modelName)
        {
            PiperLogger.LogDebug("[ParseConfig] Starting JSON parsing");
            var jsonObj = JObject.Parse(json);

            var config = new PiperVoiceConfig
            {
                VoiceId = modelName,
                DisplayName = modelName,
                Language = jsonObj["language"]?["code"]?.ToString() ?? "ja",
                SampleRate = jsonObj["audio"]?["sample_rate"]?.ToObject<int>() ?? 22050,
                PhonemeIdMap = new Dictionary<string, int>()
            };

            PiperLogger.LogDebug($"[ParseConfig] Language: {config.Language}, SampleRate: {config.SampleRate}");

            // phoneme_id_mapをパース
            var phonemeIdMap = jsonObj["phoneme_id_map"] as JObject;
            if (phonemeIdMap != null)
            {
                PiperLogger.LogDebug($"[ParseConfig] Found phoneme_id_map with {phonemeIdMap.Count} entries");

                foreach (var kvp in phonemeIdMap)
                {
                    var idArray = kvp.Value as JArray;
                    if (idArray != null && idArray.Count > 0)
                    {
                        config.PhonemeIdMap[kvp.Key] = idArray[0].ToObject<int>();
                    }
                }

                PiperLogger.LogDebug($"[ParseConfig] Parsed {config.PhonemeIdMap.Count} phoneme mappings");
            }
            else
            {
                PiperLogger.LogWarning("[ParseConfig] No phoneme_id_map found in JSON");
            }

            return config;
        }

        private string[] ConvertToPhonemes(string text, string language)
        {
            // TODO: Replace with proper phonemizer integration (OpenJTalk for Japanese, espeak-ng for other languages)
            // This is a simplified demo implementation for Phase 1.9
            if (language == "ja")
            {
                // Simplified Japanese phoneme mapping for demo purposes
                // Phase 1.10 will integrate OpenJTalkPhonemizer for accurate conversion
                var phonemeMap = new Dictionary<string, string[]>
                {
                    { "こ", new[] { "k", "o" } },
                    { "ん", new[] { "N" } },
                    { "に", new[] { "n", "i" } },
                    { "ち", new[] { "ch", "i" } },  // "ち" is "chi" - ch will be mapped to PUA
                    { "は", new[] { "w", "a" } },    // "は" as particle is pronounced "wa"
                    { "、", new[] { "_" } },  // pause (using pad token)
                    { "世", new[] { "s", "e" } },
                    { "界", new[] { "k", "a", "i" } },
                    { "！", new[] { "_" } }  // pause (using pad token)
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
                    .Replace(",", " _")
                    .Replace(".", " _")
                    .Replace("!", " _")
                    .Replace("?", " _")
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