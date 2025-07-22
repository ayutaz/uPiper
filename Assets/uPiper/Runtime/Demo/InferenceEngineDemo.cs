using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using uPiper.Core.Phonemizers;
#if !UNITY_WEBGL
using uPiper.Core.Phonemizers.Implementations;
#endif

namespace uPiper.Demo
{
    /// <summary>
    /// Phase 1.10 - Unity.InferenceEngineを使用したPiper TTSデモ（OpenJTalk統合版）
    /// 
    /// ARCHITECTURE OVERVIEW
    /// ====================
    /// This demo implements neural text-to-speech using the following pipeline:
    /// 
    /// 1. Text Input (Japanese/English)
    ///    ↓
    /// 2. Phonemization
    ///    - Japanese: OpenJTalk (MeCab + dictionary) → phonemes
    ///    - English: Simple word splitting (eSpeak-NG in future phases)
    ///    ↓
    /// 3. Phoneme Encoding
    ///    - Multi-char phonemes → PUA characters (e.g., "ky" → "\ue006")
    ///    - Phoneme strings → ID arrays for model input
    ///    ↓
    /// 4. Neural Synthesis (VITS model via Unity.InferenceEngine)
    ///    - Input: phoneme IDs
    ///    - Duration Predictor: automatically estimates phoneme timing
    ///    - Decoder: generates audio waveform
    ///    ↓
    /// 5. Audio Output (Unity AudioSource)
    /// 
    /// IMPORTANT: Phoneme Timing Design
    /// ================================
    /// OpenJTalk provides fixed 50ms durations for all phonemes.
    /// This is intentional because:
    /// - VITS models have built-in Duration Predictor
    /// - The model re-estimates timing during inference
    /// - Precise input timing is not required for neural TTS
    /// 
    /// For details, see comments in openjtalk_full_wrapper.c
    /// </summary>
    public class InferenceEngineDemo : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_InputField _inputField;
        [SerializeField] private Button _generateButton;
        [SerializeField] private TextMeshProUGUI _statusText;
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private TMP_Dropdown _modelDropdown;
        [SerializeField] private TMP_Dropdown _phraseDropdown;
        [SerializeField] private TextMeshProUGUI _phonemeDetailsText;

        [Header("Settings")]
        [SerializeField] private string _defaultJapaneseText = "";  // Will be set in Start()
        [SerializeField] private string _defaultEnglishText = "Hello world";

        private InferenceAudioGenerator _generator;
        private PhonemeEncoder _encoder;
        private AudioClipBuilder _audioBuilder;
        private PiperVoiceConfig _currentConfig;
        private bool _isGenerating;
#if !UNITY_WEBGL
        private ITextPhonemizer _phonemizer;
#endif

        private readonly Dictionary<string, string> _modelLanguages = new Dictionary<string, string>
        {
            { "ja_JP-test-medium", "ja" },
            { "test_voice", "en" }
        };

        // テスト用の定型文 - will be initialized in Start() to avoid encoding issues
        private List<string> _japaneseTestPhrases;

        private readonly List<string> _englishTestPhrases = new List<string>
        {
            "Custom Input",  // Custom input option
            "Hello world",
            "Welcome to Unity",
            "This is a test of the text to speech system",
            "The quick brown fox jumps over the lazy dog",
            "How are you doing today?",
            "Unity Inference Engine is amazing",
            "Can you hear me clearly?",
            "Let's test the voice synthesis"
        };

        private void Start()
        {
            // Set default Japanese text dynamically to avoid encoding issues
            _defaultJapaneseText = new string(new char[] { 'こ', 'ん', 'に', 'ち', 'は' });
            
            // Initialize Japanese test phrases dynamically
            _japaneseTestPhrases = new List<string>
            {
                "自由入力",  // Custom input option
                new string(new char[] { 'こ', 'ん', 'に', 'ち', 'は' }),
                new string(new char[] { 'こ', 'ん', 'に', 'ち', 'は', '、', '世', '界', '！' }),
                new string(new char[] { 'あ', 'り', 'が', 'と', 'う', 'ご', 'ざ', 'い', 'ま', 'す' }),
                new string(new char[] { '日', '本', 'の', '日', '本', '橋', 'の', '上', 'で', '箸', 'を', '使', 'っ', 'て', 'ご', '飯', 'を', '食', 'べ', 'る' }),
                new string(new char[] { '私', 'は', '東', '京', 'に', '住', 'ん', 'で', 'い', 'ま', 'す' }),
                new string(new char[] { '今', '日', 'は', 'い', 'い', '天', '気', 'で', 'す', 'ね' }),
                new string(new char[] { '音', '声', '合', '成', 'の', 'テ', 'ス', 'ト', 'で', 'す' }),
                new string(new char[] { 'ユ', 'ニ', 'テ', 'ィ', 'で', '日', '本', '語', '音', '声', '合', '成', 'が', 'で', 'き', 'ま', 'し', 'た' }),
                new string(new char[] { 'お', 'は', 'よ', 'う', 'ご', 'ざ', 'い', 'ま', 'す', '、', '今', '日', 'も', '一', '日', '頑', '張', 'り', 'ま', 'し', 'ょ', 'う' }),
                new string(new char[] { 'す', 'み', 'ま', 'せ', 'ん', '、', 'ち', 'ょ', 'っ', 'と', 'お', '聞', 'き', 'し', 'た', 'い', 'こ', 'と', 'が', 'あ', 'り', 'ま', 'す' })
            };
            
            _generator = new InferenceAudioGenerator();
            _audioBuilder = new AudioClipBuilder();

            // Debug OpenJTalk library loading on non-WebGL platforms in builds
#if !UNITY_WEBGL && !UNITY_EDITOR
            PiperLogger.LogInfo("[InferenceEngineDemo] Running OpenJTalk debug helper...");
            OpenJTalkDebugHelper.DebugLibraryLoading();
            
            // Additional Android debugging
            #if UNITY_ANDROID
            DebugAndroidSetup();
            #endif
#endif

#if !UNITY_WEBGL
            // Initialize OpenJTalk phonemizer for Japanese
            try
            {
                var openJTalk = new OpenJTalkPhonemizer();
                _phonemizer = new TextPhonemizerAdapter(openJTalk);
                PiperLogger.LogInfo("[InferenceEngineDemo] OpenJTalk phonemizer initialized successfully");
            }
            catch (Exception ex)
            {
                PiperLogger.LogError($"[InferenceEngineDemo] Failed to initialize OpenJTalk: {ex.Message}");
                PiperLogger.LogError("[InferenceEngineDemo] Japanese text-to-speech will not be available.");
                PiperLogger.LogError("[InferenceEngineDemo] To enable Japanese TTS, please build the OpenJTalk native library:");
                PiperLogger.LogError("[InferenceEngineDemo]   1. Navigate to NativePlugins/OpenJTalk/");
                PiperLogger.LogError("[InferenceEngineDemo]   2. Run ./build.sh (macOS/Linux) or build.bat (Windows)");
                _phonemizer = null;
            }
#endif

            SetupUI();
            SetStatus("準備完了");
        }

        private void OnDestroy()
        {
            _generator?.Dispose();
#if !UNITY_WEBGL
            if (_phonemizer is TextPhonemizerAdapter adapter)
            {
                // Dispose the underlying OpenJTalkPhonemizer
                var field = adapter.GetType().GetField("_phonemizer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field?.GetValue(adapter) is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
#endif
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

            // フレーズ選択ドロップダウンの設定
            if (_phraseDropdown != null)
            {
                _phraseDropdown.ClearOptions();
                _phraseDropdown.AddOptions(_japaneseTestPhrases);
                _phraseDropdown.onValueChanged.AddListener(OnPhraseChanged);
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

            // フレーズドロップダウンを更新
            if (_phraseDropdown != null)
            {
                _phraseDropdown.ClearOptions();
                _phraseDropdown.AddOptions(isJapanese ? _japaneseTestPhrases : _englishTestPhrases);
                _phraseDropdown.value = 1; // デフォルトフレーズを選択
            }

            if (_inputField != null)
            {
                _inputField.text = isJapanese ? _defaultJapaneseText : _defaultEnglishText;
            }
        }

        private void OnPhraseChanged(int index)
        {
            if (_phraseDropdown == null || _inputField == null)
                return;

            var isJapanese = _modelDropdown?.value == 0;
            var phrases = isJapanese ? _japaneseTestPhrases : _englishTestPhrases;

            if (index > 0 && index < phrases.Count)
            {
                // 定型文を選択
                _inputField.text = phrases[index];
                _inputField.interactable = false; // 定型文選択時は編集不可
            }
            else
            {
                // 自由入力を選択
                _inputField.interactable = true; // 編集可能にする
                if (string.IsNullOrEmpty(_inputField.text) || phrases.Contains(_inputField.text))
                {
                    // 空または定型文の場合はデフォルトテキストを設定
                    _inputField.text = isJapanese ? _defaultJapaneseText : _defaultEnglishText;
                }
                _inputField.Select(); // フォーカスを設定
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
            
            // Debug text encoding
            string inputText = _inputField.text;
            PiperLogger.LogInfo($"Starting audio generation for text: {inputText}");
            
            #if UNITY_ANDROID
            // Additional encoding debug on Android
            byte[] utf8Bytes = System.Text.Encoding.UTF8.GetBytes(inputText);
            PiperLogger.LogInfo($"[Android] Text length: {inputText.Length} chars, UTF-8 bytes: {utf8Bytes.Length}");
            PiperLogger.LogInfo($"[Android] First few bytes: {string.Join(" ", utf8Bytes.Take(20).Select(b => b.ToString("X2")))}");
            
            // Try to detect if the text is correctly encoded by checking UTF-8 bytes
            string testPhrase = new string(new char[] { 'こ', 'ん', 'に', 'ち', 'は' });
            if (inputText == testPhrase)
            {
                PiperLogger.LogInfo("[Android] Input matches expected 'こんにちは' - encoding is correct");
            }
            else if (inputText.Contains("縺"))
            {
                PiperLogger.LogWarning("[Android] Detected mojibake (文字化け) - text encoding issue!");
                PiperLogger.LogWarning("[Android] The text field may be corrupted. Please use the dropdown to select a test phrase.");
            }
            #endif

            // Start overall timing
            var totalStopwatch = Stopwatch.StartNew();
            var timings = new Dictionary<string, long>();

            try
            {
                // モデル名を取得
                var modelName = _modelDropdown?.value == 0 ? "ja_JP-test-medium" : "test_voice";
                PiperLogger.LogDebug($"Selected model: {modelName}");

                // モデルをロード
                SetStatus("モデルをロード中...");
                var loadStopwatch = Stopwatch.StartNew();
                PiperLogger.LogDebug($"Loading model asset: Models/{modelName}");
                var modelAsset = Resources.Load<ModelAsset>($"Models/{modelName}");
                if (modelAsset == null)
                {
                    throw new Exception($"モデルが見つかりません: {modelName}");
                }
                PiperLogger.LogDebug($"Model asset loaded successfully");
                timings["ModelLoad"] = loadStopwatch.ElapsedMilliseconds;

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

                // デバッグ用：日本語音素に関連するマッピングを表示
                string[] importantPhonemes = { "ch", "ts", "sh", "k", "o", "n", "i", "h", "a", "w", "N", "_", "^", "$" };
                foreach (var phoneme in importantPhonemes)
                {
                    if (config.PhonemeIdMap.TryGetValue(phoneme, out var id))
                    {
                        PiperLogger.LogDebug($"  Important phoneme '{phoneme}' -> ID {id}");
                    }
                    else
                    {
                        PiperLogger.LogDebug($"  Important phoneme '{phoneme}' -> NOT FOUND in model");
                    }
                }

                // ジェネレーターを初期化
                SetStatus("ジェネレーターを初期化中...");
                PiperLogger.LogDebug("Initializing InferenceAudioGenerator...");
                await _generator.InitializeAsync(modelAsset, config);
                PiperLogger.LogDebug("Generator initialized successfully");

                // 音素に変換
                SetStatus("音素に変換中...");
                var phonemeStopwatch = Stopwatch.StartNew();
                string[] phonemes;
                var language = _modelLanguages[modelName];

#if !UNITY_WEBGL
                // Use OpenJTalk for Japanese if available
                if (language == "ja" && _phonemizer != null)
                {
                    PiperLogger.LogDebug("[InferenceEngineDemo] Using OpenJTalk phonemizer for Japanese text");
                    PiperLogger.LogInfo($"[InferenceEngineDemo] Input text: '{_inputField.text}'");

                    var openJTalkStopwatch = Stopwatch.StartNew();
                    var phonemeResult = await _phonemizer.PhonemizeAsync(_inputField.text, language);
                    timings["OpenJTalk"] = openJTalkStopwatch.ElapsedMilliseconds;
                    var openJTalkPhonemes = phonemeResult.Phonemes;

                    PiperLogger.LogInfo($"[OpenJTalk] Raw phonemes ({openJTalkPhonemes.Length}): {string.Join(" ", openJTalkPhonemes)}");

                    // Convert OpenJTalk phonemes to Piper phonemes
                    phonemes = OpenJTalkToPiperMapping.ConvertToPiperPhonemes(openJTalkPhonemes);
                    PiperLogger.LogInfo($"[OpenJTalk] Converted to Piper phonemes ({phonemes.Length}): {string.Join(" ", phonemes)}");

                    // Show phoneme details in UI
                    if (_phonemeDetailsText != null)
                    {
                        // Special handling for こんにちは to debug
                        if (_inputField.text == konnichiwa)
                        {
                            _phonemeDetailsText.text = $"[DEBUG こんにちは]\nOpenJTalk: {string.Join(" ", openJTalkPhonemes)}\nPiper: {string.Join(" ", phonemes)}\n" +
                                $"Expected: k o n n i ch i w a\nCheck if 'ch i' sounds like 'ch u'";
                        }
                        else
                        {
                            _phonemeDetailsText.text = $"OpenJTalk: {string.Join(" ", openJTalkPhonemes)}\nPiper: {string.Join(" ", phonemes)}";
                        }
                    }

                    // Log detailed phoneme information
                    if (phonemeResult.Durations != null && phonemeResult.Durations.Length > 0)
                    {
                        PiperLogger.LogDebug($"[OpenJTalk] Total duration: {phonemeResult.Durations.Sum():F3}s");
                    }
                }
                else if (language == "ja")
                {
                    // OpenJTalk is required for Japanese
                    var errorMsg = "OpenJTalk is required for Japanese text but is not available.\n" +
                                  "To enable Japanese TTS:\n" +
                                  "1. Navigate to NativePlugins/OpenJTalk/\n" +
                                  "2. Run ./build.sh (macOS/Linux) or build.bat (Windows)\n" +
                                  "3. Restart Unity Editor";
                    
                    #if UNITY_ANDROID && !UNITY_EDITOR
                    // Add Android-specific error info
                    errorMsg += "\n\nOn Android:\n" +
                                "- Check if libopenjtalk_wrapper.so is in Plugins/Android/libs/{ABI}/\n" +
                                "- Check if dictionary files are in StreamingAssets\n" +
                                "- Dictionary will be extracted to persistent data path on first run";
                    #endif
                    
                    throw new Exception(errorMsg);
                }
                else
                {
                    // TEMPORARY: Basic English phonemization
                    // This is a placeholder implementation for Phase 1.10.
                    // Phase 2 will integrate eSpeak-NG for proper English phonemization.
                    // Current approach: simple word-based splitting (not real phonemes)
                    phonemes = _inputField.text.ToLower()
                        .Replace(",", " _")
                        .Replace(".", " _")
                        .Replace("!", " _")
                        .Replace("?", " _")
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    PiperLogger.LogInfo($"Basic English phonemes ({phonemes.Length}): {string.Join(" ", phonemes)}");
                }
#else
                // WebGL is not supported for Japanese
                if (language == "ja")
                {
                    throw new Exception("Japanese text-to-speech is not supported on WebGL platform. OpenJTalk native library is required.");
                }
                else
                {
                    // For non-Japanese languages, use basic phoneme splitting
                    phonemes = _inputField.text.ToLower()
                        .Replace(",", " _")
                        .Replace(".", " _")
                        .Replace("!", " _")
                        .Replace("?", " _")
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    PiperLogger.LogInfo($"English phonemes ({phonemes.Length}): {string.Join(" ", phonemes)}");
                }
#endif

                // 音素変換の詳細をログ出力
                PiperLogger.LogDebug($"Input text: '{_inputField.text}'");

                // 「こんにちは」の場合、特に詳しくログ
                string konnichiwa = new string(new char[] { 'こ', 'ん', 'に', 'ち', 'は' });
                if (_inputField.text == konnichiwa)
                {
                    PiperLogger.LogInfo("=== Special debug for 'こんにちは' ===");
                    for (int i = 0; i < phonemes.Length; i++)
                    {
                        PiperLogger.LogInfo($"  Phoneme[{i}]: '{phonemes[i]}' (length: {phonemes[i].Length})");
                        if (phonemes[i] == "ch" || phonemes[i] == "t" || phonemes[i] == "ty" || phonemes[i] == "i")
                        {
                            PiperLogger.LogInfo($"    -> This is the 'chi' sound component");
                        }
                    }
                }

                timings["Phonemization"] = phonemeStopwatch.ElapsedMilliseconds;

                // 音素をIDに変換
                var encodeStopwatch = Stopwatch.StartNew();
                var phonemeIds = _encoder.Encode(phonemes);
                PiperLogger.LogInfo($"Phoneme IDs ({phonemeIds.Length}): {string.Join(", ", phonemeIds)}");
                timings["Encoding"] = encodeStopwatch.ElapsedMilliseconds;

                // Log phoneme to ID mapping for debugging
                var phonemeIdPairs = new List<string>();
                for (int i = 0; i < Math.Min(phonemes.Length, phonemeIds.Length); i++)
                {
                    phonemeIdPairs.Add($"'{phonemes[i]}'={phonemeIds[i]}");
                }
                PiperLogger.LogDebug($"Phoneme->ID mapping: {string.Join(", ", phonemeIdPairs)}");

                // 音声生成
                SetStatus("音声を生成中...");
                PiperLogger.LogDebug("Calling GenerateAudioAsync...");
                var synthesisStopwatch = Stopwatch.StartNew();
                var audioData = await _generator.GenerateAudioAsync(phonemeIds);
                timings["Synthesis"] = synthesisStopwatch.ElapsedMilliseconds;
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

                // Calculate total time
                totalStopwatch.Stop();
                timings["Total"] = totalStopwatch.ElapsedMilliseconds;

                // Log performance metrics
                PiperLogger.LogInfo("=== Performance Metrics ===");
                PiperLogger.LogInfo($"Text length: {_inputField.text.Length} characters");
                foreach (var timing in timings)
                {
                    PiperLogger.LogInfo($"{timing.Key}: {timing.Value}ms");
                }

                // Check if we meet the <100ms requirement
                var processingTime = timings["Total"];
                var meetsRequirement = processingTime < 100;
                PiperLogger.LogInfo($"Performance requirement (<100ms): {(meetsRequirement ? "PASSED" : "FAILED")} ({processingTime}ms)");

                // Update status with timing info
                SetStatus($"生成完了！ ({audioClip.length:F2}秒) - 処理時間: {processingTime}ms");

                // Show timing details in phoneme details text
                if (_phonemeDetailsText != null)
                {
                    var existingText = _phonemeDetailsText.text;
                    _phonemeDetailsText.text = $"{existingText}\n\n[Performance]\nTotal: {processingTime}ms";
                    if (timings.ContainsKey("OpenJTalk"))
                    {
                        _phonemeDetailsText.text += $"\nOpenJTalk: {timings["OpenJTalk"]}ms";
                    }
                    _phonemeDetailsText.text += $"\nSynthesis: {timings["Synthesis"]}ms";
                    _phonemeDetailsText.text += $"\nRequirement: {(meetsRequirement ? "✓ PASSED" : "✗ FAILED")}";
                }
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

            // Clear phoneme details when starting new generation
            if (_isGenerating && _phonemeDetailsText != null)
            {
                _phonemeDetailsText.text = "";
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void DebugAndroidSetup()
        {
            PiperLogger.LogInfo("[Android Debug] === Android Setup Debug ===");
            
            // Check platform
            PiperLogger.LogInfo($"[Android Debug] Platform: {Application.platform}");
            PiperLogger.LogInfo($"[Android Debug] System Language: {Application.systemLanguage}");
            
            // Check paths
            PiperLogger.LogInfo($"[Android Debug] Persistent Data Path: {Application.persistentDataPath}");
            PiperLogger.LogInfo($"[Android Debug] Streaming Assets Path: {Application.streamingAssetsPath}");
            
            // Check OpenJTalk dictionary
            try
            {
                string dictPath = uPiper.Core.Platform.AndroidPathResolver.GetOpenJTalkDictionaryPath();
                PiperLogger.LogInfo($"[Android Debug] OpenJTalk Dictionary Path: {dictPath}");
                
                if (System.IO.Directory.Exists(dictPath))
                {
                    PiperLogger.LogInfo("[Android Debug] ✓ Dictionary directory exists");
                    
                    // Check for required files
                    string[] requiredFiles = { "char.bin", "sys.dic", "unk.dic", "matrix.bin", "left-id.def", "pos-id.def", "rewrite.def", "right-id.def" };
                    foreach (var file in requiredFiles)
                    {
                        string filePath = System.IO.Path.Combine(dictPath, file);
                        if (System.IO.File.Exists(filePath))
                        {
                            var fileInfo = new System.IO.FileInfo(filePath);
                            PiperLogger.LogInfo($"[Android Debug] ✓ {file}: {fileInfo.Length} bytes");
                        }
                        else
                        {
                            PiperLogger.LogWarning($"[Android Debug] ✗ {file}: NOT FOUND");
                        }
                    }
                }
                else
                {
                    PiperLogger.LogWarning("[Android Debug] ✗ Dictionary directory does not exist");
                    PiperLogger.LogInfo("[Android Debug] Will attempt to extract from StreamingAssets on first use");
                }
            }
            catch (Exception e)
            {
                PiperLogger.LogError($"[Android Debug] Error checking dictionary: {e.Message}");
            }
            
            // Check native library loading
            try
            {
                // On Android, native libraries are loaded from the APK
                PiperLogger.LogInfo("[Android Debug] Checking native library loading...");
                
                // Expected library name on Android
                string expectedLibraryName = "libopenjtalk_wrapper.so";
                PiperLogger.LogInfo($"[Android Debug] Expected library name: {expectedLibraryName}");
                
                // The actual check will happen when OpenJTalkPhonemizer is initialized
                // Here we just log that we expect the library to be loaded from APK
                PiperLogger.LogInfo("[Android Debug] Native libraries on Android are loaded directly from APK");
                PiperLogger.LogInfo("[Android Debug] Library loading will be verified during OpenJTalk initialization");
                
                // Check if we can access the native method
                try
                {
                    // This will help verify if the library is accessible
                    var testPhon = new OpenJTalkPhonemizer();
                    PiperLogger.LogInfo("[Android Debug] ✓ OpenJTalkPhonemizer instance created successfully");
                    testPhon.Dispose();
                }
                catch (Exception libEx)
                {
                    PiperLogger.LogWarning($"[Android Debug] ✗ Failed to create OpenJTalkPhonemizer: {libEx.Message}");
                }
            }
            catch (Exception e)
            {
                PiperLogger.LogError($"[Android Debug] Error checking native library: {e.Message}");
            }
            
            // Text encoding test
            PiperLogger.LogInfo("[Android Debug] === Text Encoding Test ===");
            string testText = new string(new char[] { 'こ', 'ん', 'に', 'ち', 'は' });
            byte[] utf8Bytes = System.Text.Encoding.UTF8.GetBytes(testText);
            PiperLogger.LogInfo($"[Android Debug] Test text: {testText}");
            PiperLogger.LogInfo($"[Android Debug] UTF-8 bytes ({utf8Bytes.Length}): {BitConverter.ToString(utf8Bytes)}");
            
            // Check if we can properly decode it back
            string decoded = System.Text.Encoding.UTF8.GetString(utf8Bytes);
            PiperLogger.LogInfo($"[Android Debug] Decoded back: {decoded}");
            PiperLogger.LogInfo($"[Android Debug] Encoding round-trip success: {testText == decoded}");
            
            // Expected UTF-8 bytes for "こんにちは"
            byte[] expectedBytes = new byte[] { 0xE3, 0x81, 0x93, 0xE3, 0x82, 0x93, 0xE3, 0x81, 0xAB, 0xE3, 0x81, 0xA1, 0xE3, 0x81, 0xAF };
            bool bytesMatch = utf8Bytes.Length == expectedBytes.Length;
            if (bytesMatch)
            {
                for (int i = 0; i < utf8Bytes.Length; i++)
                {
                    if (utf8Bytes[i] != expectedBytes[i])
                    {
                        bytesMatch = false;
                        break;
                    }
                }
            }
            PiperLogger.LogInfo($"[Android Debug] UTF-8 bytes match expected: {bytesMatch}");
            
            PiperLogger.LogInfo("[Android Debug] === End Android Setup Debug ===");
        }
#endif
    }
}