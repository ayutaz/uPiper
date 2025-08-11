using System;
using System.Collections;
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
using uPiper.Core.Phonemizers.Implementations;
#if UNITY_WEBGL && !UNITY_EDITOR
using uPiper.Core.Phonemizers.WebGL;
#endif

namespace uPiper.Demo
{
    // Temporary workaround for compilation issue
    internal static class ArpabetToIPAConverterTemp
    {
        private static readonly Dictionary<string, string> ArpabetToIPA = new()
        {
            // Vowels
            ["AA"] = "ɑ",
            ["AE"] = "æ",
            ["AH"] = "ʌ",
            ["AO"] = "ɔ",
            ["AW"] = "a",
            ["AY"] = "a",
            ["EH"] = "ɛ",
            ["ER"] = "ɚ",  // Simplified diphthongs
            ["EY"] = "e",
            ["IH"] = "ɪ",
            ["IY"] = "i",
            ["OW"] = "o",   // Simplified diphthongs
            ["OY"] = "ɔ",
            ["UH"] = "ʊ",
            ["UW"] = "u",                 // Simplified diphthongs
            // Consonants
            ["B"] = "b",
            ["CH"] = "tʃ",
            ["D"] = "d",
            ["DH"] = "ð",
            ["F"] = "f",
            ["G"] = "ɡ",
            ["HH"] = "h",
            ["JH"] = "dʒ",
            ["K"] = "k",
            ["L"] = "l",
            ["M"] = "m",
            ["N"] = "n",
            ["NG"] = "ŋ",
            ["P"] = "p",
            ["R"] = "ɹ",
            ["S"] = "s",
            ["SH"] = "ʃ",
            ["T"] = "t",
            ["TH"] = "θ",
            ["V"] = "v",
            ["W"] = "w",
            ["Y"] = "j",
            ["Z"] = "z",
            ["ZH"] = "ʒ",
        };

        public static string[] ConvertAll(string[] arpabetPhonemes)
        {
            var result = new string[arpabetPhonemes.Length];
            for (int i = 0; i < arpabetPhonemes.Length; i++)
            {
                var basePhoneme = arpabetPhonemes[i].TrimEnd('0', '1', '2');
                result[i] = ArpabetToIPA.TryGetValue(basePhoneme.ToUpper(), out var ipa)
                    ? ipa : arpabetPhonemes[i].ToLower();
            }
            return result;
        }
    }

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
        [SerializeField] private TextMeshProUGUI _backendInfoText;

        [Header("GPU Inference UI")]
        [SerializeField] private TMP_Dropdown _backendDropdown;

        [Header("Settings")]
        [SerializeField] private string _defaultJapaneseText = "";  // Will be set in Start()
        [SerializeField] private string _defaultEnglishText = "Hello world";

        [Header("Font Settings")]
        [SerializeField] private TMP_FontAsset _chineseFontAsset;
        [SerializeField] private TMP_FontAsset _japaneseFontAsset;
        [SerializeField] private TMP_FontAsset _defaultFontAsset;

        private InferenceAudioGenerator _generator;
        private PhonemeEncoder _encoder;
        private AudioClipBuilder _audioBuilder;
        private PiperVoiceConfig _currentConfig;
        private bool _isGenerating;
        private InferenceBackend _selectedBackend = InferenceBackend.CPU;
        private GPUInferenceSettings _gpuSettings;
        private ITextPhonemizer _phonemizer;
#if !UNITY_WEBGL
        private Core.Phonemizers.Backend.ChinesePhonemizer _chinesePhonemizer;
        private Core.Phonemizers.Backend.Flite.FliteLTSPhonemizer _englishPhonemizer;
#endif

        private Dictionary<string, string> _modelLanguages = new()
        {
            { "ja_JP-test-medium", "ja" },
            { "en_US-ljspeech-medium", "en" },
            { "zh_CN-huayan-medium", "zh" }
        };

        // テスト用の定型文 - will be initialized in Start() to avoid encoding issues
        private List<string> _japaneseTestPhrases;
        private List<string> _chineseTestPhrases;

        private List<string> _englishTestPhrases = new()
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
            // Set default Japanese text from UTF-8 bytes to avoid encoding issues
            var konnichiwaBytes = new byte[] { 0xE3, 0x81, 0x93, 0xE3, 0x82, 0x93, 0xE3, 0x81, 0xAB, 0xE3, 0x81, 0xA1, 0xE3, 0x81, 0xAF };
            _defaultJapaneseText = System.Text.Encoding.UTF8.GetString(konnichiwaBytes);

            // Setup Chinese font as fallback
            SetupChineseFontFallback();

            // Initialize Japanese test phrases from UTF-8 bytes
            _japaneseTestPhrases = new List<string>
            {
                "自由入力",  // Custom input option - ASCII so no encoding issue
                System.Text.Encoding.UTF8.GetString(new byte[] { 0xE3, 0x81, 0x93, 0xE3, 0x82, 0x93, 0xE3, 0x81, 0xAB, 0xE3, 0x81, 0xA1, 0xE3, 0x81, 0xAF }), // こんにちは
                System.Text.Encoding.UTF8.GetString(new byte[] { 0xE3, 0x81, 0x93, 0xE3, 0x82, 0x93, 0xE3, 0x81, 0xAB, 0xE3, 0x81, 0xA1, 0xE3, 0x81, 0xAF, 0xE3, 0x80, 0x81, 0xE4, 0xB8, 0x96, 0xE7, 0x95, 0x8C, 0xEF, 0xBC, 0x81 }), // こんにちは、世界！
                System.Text.Encoding.UTF8.GetString(new byte[] { 0xE3, 0x81, 0x82, 0xE3, 0x82, 0x8A, 0xE3, 0x81, 0x8C, 0xE3, 0x81, 0xA8, 0xE3, 0x81, 0x86, 0xE3, 0x81, 0x94, 0xE3, 0x81, 0x96, 0xE3, 0x81, 0x84, 0xE3, 0x81, 0xBE, 0xE3, 0x81, 0x99 }), // ありがとうございます
                System.Text.Encoding.UTF8.GetString(new byte[] { 0xE6, 0x97, 0xA5, 0xE6, 0x9C, 0xAC, 0xE3, 0x81, 0xAE, 0xE6, 0x97, 0xA5, 0xE6, 0x9C, 0xAC, 0xE6, 0xA9, 0x8B, 0xE3, 0x81, 0xAE, 0xE4, 0xB8, 0x8A, 0xE3, 0x81, 0xA7, 0xE7, 0xAE, 0xB8, 0xE3, 0x82, 0x92, 0xE4, 0xBD, 0xBF, 0xE3, 0x81, 0xA3, 0xE3, 0x81, 0xA6, 0xE3, 0x81, 0x94, 0xE9, 0xA3, 0xAF, 0xE3, 0x82, 0x92, 0xE9, 0xA3, 0x9F, 0xE3, 0x81, 0xB9, 0xE3, 0x82, 0x8B }), // 日本の日本橋の上で箸を使ってご飯を食べる
                System.Text.Encoding.UTF8.GetString(new byte[] { 0xE7, 0xA7, 0x81, 0xE3, 0x81, 0xAF, 0xE6, 0x9D, 0xB1, 0xE4, 0xBA, 0xAC, 0xE3, 0x81, 0xAB, 0xE4, 0xBD, 0x8F, 0xE3, 0x82, 0x93, 0xE3, 0x81, 0xA7, 0xE3, 0x81, 0x84, 0xE3, 0x81, 0xBE, 0xE3, 0x81, 0x99 }), // 私は東京に住んでいます
                System.Text.Encoding.UTF8.GetString(new byte[] { 0xE4, 0xBB, 0x8A, 0xE6, 0x97, 0xA5, 0xE3, 0x81, 0xAF, 0xE3, 0x81, 0x84, 0xE3, 0x81, 0x84, 0xE5, 0xA4, 0xA9, 0xE6, 0xB0, 0x97, 0xE3, 0x81, 0xA7, 0xE3, 0x81, 0x99, 0xE3, 0x81, 0xAD }), // 今日はいい天気ですね
                System.Text.Encoding.UTF8.GetString(new byte[] { 0xE9, 0x9F, 0xB3, 0xE5, 0xA3, 0xB0, 0xE5, 0x90, 0x88, 0xE6, 0x88, 0x90, 0xE3, 0x81, 0xAE, 0xE3, 0x83, 0x86, 0xE3, 0x82, 0xB9, 0xE3, 0x83, 0x88, 0xE3, 0x81, 0xA7, 0xE3, 0x81, 0x99 }), // 音声合成のテストです
                System.Text.Encoding.UTF8.GetString(new byte[] { 0xE3, 0x83, 0xA6, 0xE3, 0x83, 0x8B, 0xE3, 0x83, 0x86, 0xE3, 0x82, 0xA3, 0xE3, 0x81, 0xA7, 0xE6, 0x97, 0xA5, 0xE6, 0x9C, 0xAC, 0xE8, 0xAA, 0x9E, 0xE9, 0x9F, 0xB3, 0xE5, 0xA3, 0xB0, 0xE5, 0x90, 0x88, 0xE6, 0x88, 0x90, 0xE3, 0x81, 0x8C, 0xE3, 0x81, 0xA7, 0xE3, 0x81, 0x8D, 0xE3, 0x81, 0xBE, 0xE3, 0x81, 0x97, 0xE3, 0x81, 0x9F }), // ユニティで日本語音声合成ができました
                System.Text.Encoding.UTF8.GetString(new byte[] { 0xE3, 0x81, 0x8A, 0xE3, 0x81, 0xAF, 0xE3, 0x82, 0x88, 0xE3, 0x81, 0x86, 0xE3, 0x81, 0x94, 0xE3, 0x81, 0x96, 0xE3, 0x81, 0x84, 0xE3, 0x81, 0xBE, 0xE3, 0x81, 0x99, 0xE3, 0x80, 0x81, 0xE4, 0xBB, 0x8A, 0xE6, 0x97, 0xA5, 0xE3, 0x82, 0x82, 0xE4, 0xB8, 0x80, 0xE6, 0x97, 0xA5, 0xE9, 0xA0, 0x91, 0xE5, 0xBC, 0xB5, 0xE3, 0x82, 0x8A, 0xE3, 0x81, 0xBE, 0xE3, 0x81, 0x97, 0xE3, 0x82, 0x87, 0xE3, 0x81, 0x86 }), // おはようございます、今日も一日頑張りましょう
                System.Text.Encoding.UTF8.GetString(new byte[] { 0xE3, 0x81, 0x99, 0xE3, 0x81, 0xBF, 0xE3, 0x81, 0xBE, 0xE3, 0x81, 0x9B, 0xE3, 0x82, 0x93, 0xE3, 0x80, 0x81, 0xE3, 0x81, 0xA1, 0xE3, 0x82, 0x87, 0xE3, 0x81, 0xA3, 0xE3, 0x81, 0xA8, 0xE3, 0x81, 0x8A, 0xE8, 0x81, 0x9E, 0xE3, 0x81, 0x8D, 0xE3, 0x81, 0x97, 0xE3, 0x81, 0x9F, 0xE3, 0x81, 0x84, 0xE3, 0x81, 0x93, 0xE3, 0x81, 0xA8, 0xE3, 0x81, 0x8C, 0xE3, 0x81, 0x82, 0xE3, 0x82, 0x8A, 0xE3, 0x81, 0xBE, 0xE3, 0x81, 0x99 }) // すみません、ちょっとお聞きしたいことがあります
            };

            // Initialize Chinese test phrases
            _chineseTestPhrases = new List<string>
            {
                "自定义输入",  // Custom input option
                "你好",
                "你好，世界！",
                "谢谢你",
                "欢迎使用Unity推理引擎",
                "今天天气真好",
                "这是一个语音合成测试",
                "中文语音合成效果如何？",
                "我们正在测试中文语音合成",
                "Unity中文语音合成已经实现了",
                "早上好，今天也要加油啊"
            };

            _generator = new InferenceAudioGenerator();
            _audioBuilder = new AudioClipBuilder();

            // Initialize GPU settings
            _gpuSettings = new GPUInferenceSettings();

            // Set up audio configuration change handler
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;

            // Debug OpenJTalk library loading on non-WebGL platforms in builds
#if !UNITY_WEBGL && !UNITY_EDITOR
            PiperLogger.LogInfo("[InferenceEngineDemo] Running OpenJTalk debug helper...");
            OpenJTalkDebugHelper.DebugLibraryLoading();

            // Additional Android debugging
#if UNITY_ANDROID
            DebugAndroidSetup();
            // Preload dictionary asynchronously for better performance
            uPiper.Core.Platform.OptimizedAndroidPathResolver.PreloadDictionaryAsync();
            // Auto-test TTS generation after 2 seconds
            StartCoroutine(AutoTestTTSGeneration());
#endif
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL環境では直接WebGLOpenJTalkUnityPhonemizerを使用
            // _phonemizerは使わず、音声生成時に直接インスタンスを取得する
            PiperLogger.LogInfo("[InferenceEngineDemo] WebGL mode - will use WebGLOpenJTalkUnityPhonemizer on demand");
            _phonemizer = null;
#else
            // Initialize OpenJTalk phonemizer for Japanese (non-WebGL)
            try
            {
#if !UNITY_WEBGL
                var openJTalk = new Core.Phonemizers.Implementations.OpenJTalkPhonemizer();
                _phonemizer = new TextPhonemizerAdapter(openJTalk);
#else
                _phonemizer = null; // OpenJTalk not available in WebGL non-editor mode
#endif
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

#if !UNITY_WEBGL
            // Initialize Chinese phonemizer
            Task.Run(async () =>
            {
                try
                {
                    _chinesePhonemizer = new Core.Phonemizers.Backend.ChinesePhonemizer();
                    var initialized = await _chinesePhonemizer.InitializeAsync(new Core.Phonemizers.Backend.PhonemizerBackendOptions());
                    if (initialized)
                    {
                        PiperLogger.LogInfo("[InferenceEngineDemo] Chinese phonemizer initialized successfully");
                    }
                    else
                    {
                        PiperLogger.LogError("[InferenceEngineDemo] Failed to initialize Chinese phonemizer");
                        _chinesePhonemizer = null;
                    }
                }
                catch (Exception ex)
                {
                    PiperLogger.LogError($"[InferenceEngineDemo] Failed to initialize Chinese phonemizer: {ex.Message}");
                    _chinesePhonemizer = null;
                }
            });

            // Initialize English phonemizer (Flite LTS)
            Task.Run(async () =>
            {
                try
                {
                    _englishPhonemizer = new Core.Phonemizers.Backend.Flite.FliteLTSPhonemizer();
                    var initialized = await _englishPhonemizer.InitializeAsync(new Core.Phonemizers.Backend.PhonemizerBackendOptions());
                    if (initialized)
                    {
                        PiperLogger.LogInfo("[InferenceEngineDemo] Flite LTS phonemizer initialized successfully");
                    }
                    else
                    {
                        PiperLogger.LogError("[InferenceEngineDemo] Failed to initialize Flite LTS phonemizer");
                        _englishPhonemizer = null;
                    }
                }
                catch (Exception ex)
                {
                    PiperLogger.LogError($"[InferenceEngineDemo] Failed to initialize Flite LTS phonemizer: {ex.Message}");
                    _englishPhonemizer = null;
                }
            });
#endif
#endif

            SetupUI();
            SetStatus("準備完了");
        }

        private void SetupChineseFontFallback()
        {
            // Store default font if not set
            if (_defaultFontAsset == null && _inputField != null)
            {
                _defaultFontAsset = _inputField.fontAsset;
            }

            // Setup font fallback chain for multi-language support
            SetupFontFallbackChain();
        }

        private void SetupFontFallbackChain()
        {
            // Create a fallback chain: Default -> Japanese -> Chinese
            if (_defaultFontAsset != null)
            {
                if (_defaultFontAsset.fallbackFontAssetTable == null)
                {
                    _defaultFontAsset.fallbackFontAssetTable = new List<TMP_FontAsset>();
                }

                // Add Japanese font as fallback if available and not already present
                if (_japaneseFontAsset != null && !_defaultFontAsset.fallbackFontAssetTable.Contains(_japaneseFontAsset))
                {
                    _defaultFontAsset.fallbackFontAssetTable.Add(_japaneseFontAsset);
                    PiperLogger.LogInfo($"[InferenceEngineDemo] Added Japanese font as fallback to default font");
                }

                // Add Chinese font as fallback if available and not already present
                if (_chineseFontAsset != null && !_defaultFontAsset.fallbackFontAssetTable.Contains(_chineseFontAsset))
                {
                    _defaultFontAsset.fallbackFontAssetTable.Add(_chineseFontAsset);
                    PiperLogger.LogInfo($"[InferenceEngineDemo] Added Chinese font as fallback to default font");
                }
            }

            // Also setup Japanese font to have Chinese as fallback
            if (_japaneseFontAsset != null && _chineseFontAsset != null)
            {
                if (_japaneseFontAsset.fallbackFontAssetTable == null)
                {
                    _japaneseFontAsset.fallbackFontAssetTable = new List<TMP_FontAsset>();
                }

                if (!_japaneseFontAsset.fallbackFontAssetTable.Contains(_chineseFontAsset))
                {
                    _japaneseFontAsset.fallbackFontAssetTable.Add(_chineseFontAsset);
                    PiperLogger.LogInfo($"[InferenceEngineDemo] Added Chinese font as fallback to Japanese font");
                }
            }
        }

        private void ApplyChineseFont()
        {
            ApplyLanguageFont("zh");
        }

        private void ApplyLanguageFont(string language)
        {
            TMP_FontAsset targetFont = null;

            // Select appropriate font based on language
            switch (language)
            {
                case "ja":
                    targetFont = _japaneseFontAsset ?? _defaultFontAsset;
                    break;
                case "zh":
                    targetFont = _chineseFontAsset ?? _defaultFontAsset;
                    break;
                case "en":
                default:
                    targetFont = _defaultFontAsset;
                    break;
            }

            if (targetFont == null)
            {
                PiperLogger.LogWarning($"[InferenceEngineDemo] No font available for language: {language}");
                return;
            }

            // Apply font to all text components
            ApplyFontToUIElements(targetFont);
            PiperLogger.LogInfo($"[InferenceEngineDemo] Applied {targetFont.name} font for language: {language}");
        }

        private void ApplyFontToUIElements(TMP_FontAsset font)
        {
            // Apply font to input field
            if (_inputField != null)
            {
                _inputField.fontAsset = font;
            }

            // Apply font to phrase dropdown
            if (_phraseDropdown != null)
            {
                var dropdownText = _phraseDropdown.captionText;
                if (dropdownText != null)
                {
                    dropdownText.font = font;
                }

                var itemText = _phraseDropdown.itemText;
                if (itemText != null)
                {
                    itemText.font = font;
                }
            }

            // Apply font to status text
            if (_statusText != null)
            {
                _statusText.font = font;
            }

            // Apply font to phoneme details text  
            if (_phonemeDetailsText != null)
            {
                _phonemeDetailsText.font = font;
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from audio configuration changes
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;

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
            _chinesePhonemizer?.Dispose();
            _englishPhonemizer?.Dispose();
#endif
        }

        private void SetupUI()
        {
            PiperLogger.LogInfo($"[SetupUI] Starting UI setup");
            PiperLogger.LogInfo($"[SetupUI] _japaneseTestPhrases count: {_japaneseTestPhrases?.Count ?? 0}");
            PiperLogger.LogInfo($"[SetupUI] _chineseTestPhrases count: {_chineseTestPhrases?.Count ?? 0}");
            PiperLogger.LogInfo($"[SetupUI] _englishTestPhrases count: {_englishTestPhrases?.Count ?? 0}");
            PiperLogger.LogInfo($"[SetupUI] _phraseDropdown is null: {_phraseDropdown == null}");

            // モデル選択ドロップダウンの設定
            if (_modelDropdown != null)
            {
                _modelDropdown.ClearOptions();
                _modelDropdown.AddOptions(new List<string> { "ja_JP-test-medium", "en_US-ljspeech-medium", "zh_CN-huayan-medium" });
                _modelDropdown.onValueChanged.AddListener(OnModelChanged);
            }

            // フレーズ選択ドロップダウンの設定
            if (_phraseDropdown != null)
            {
                PiperLogger.LogInfo("[SetupUI] Setting up phrase dropdown");
                _phraseDropdown.ClearOptions();
                if (_japaneseTestPhrases != null && _japaneseTestPhrases.Count > 0)
                {
                    PiperLogger.LogInfo($"[SetupUI] Adding {_japaneseTestPhrases.Count} Japanese phrases to dropdown");
                    _phraseDropdown.AddOptions(_japaneseTestPhrases);
                }
                else
                {
                    // フォールバック
                    PiperLogger.LogWarning("[SetupUI] Japanese test phrases not initialized, using fallback");
                    _phraseDropdown.AddOptions(new List<string> { "Custom Input", "Hello", "Test" });
                }
                _phraseDropdown.onValueChanged.AddListener(OnPhraseChanged);
                PiperLogger.LogInfo($"[SetupUI] Phrase dropdown options count: {_phraseDropdown.options.Count}");
            }
            else
            {
                PiperLogger.LogError("[SetupUI] _phraseDropdown is null! Please check Unity Inspector");
            }

            // GPU推論バックエンドドロップダウンの設定
            if (_backendDropdown != null)
            {
                _backendDropdown.ClearOptions();
                // GPU ComputeはVITSモデルとの互換性問題があるため除外
                // AutoもGPU Computeを選択する可能性があるため除外
                var options = new List<string> { "CPU", "GPU Pixel" };
                _backendDropdown.AddOptions(options);
                _backendDropdown.value = 0; // CPU
                _selectedBackend = InferenceBackend.CPU; // 初期値を明示的に設定
                _backendDropdown.onValueChanged.AddListener(OnBackendChanged);
            }

            // CPUフォールバックは InferenceAudioGenerator 側で自動的に処理される

            // Float16は無効（デフォルト設定）
            _gpuSettings.UseFloat16 = false;

            // 生成ボタンの設定
            _generateButton?.onClick.AddListener(() => _ = GenerateAudioAsync());

            // 初期テキストの設定
            if (_inputField != null)
            {
                _inputField.text = _defaultJapaneseText;
            }
        }

        private void OnModelChanged(int index)
        {
            var modelNames = new[] { "ja_JP-test-medium", "en_US-ljspeech-medium", "zh_CN-huayan-medium" };
            var modelName = modelNames[index];
            var language = _modelLanguages[modelName];

            // フレーズドロップダウンを更新
            if (_phraseDropdown != null)
            {
                PiperLogger.LogInfo($"[OnModelChanged] Updating phrase dropdown for language: {language}");
                _phraseDropdown.ClearOptions();
                if (language == "ja")
                {
                    if (_japaneseTestPhrases != null && _japaneseTestPhrases.Count > 0)
                    {
                        PiperLogger.LogInfo($"[OnModelChanged] Adding {_japaneseTestPhrases.Count} Japanese phrases");
                        _phraseDropdown.AddOptions(_japaneseTestPhrases);
                    }
                    else
                    {
                        PiperLogger.LogWarning("[OnModelChanged] Japanese phrases not available, using fallback");
                        _phraseDropdown.AddOptions(new List<string> { "Custom Input", "こんにちは" });
                    }

                    if (_inputField != null)
                        _inputField.text = _defaultJapaneseText;

                    // Apply Japanese font
                    ApplyLanguageFont("ja");
                }
                else if (language == "zh")
                {
                    if (_chineseTestPhrases != null && _chineseTestPhrases.Count > 0)
                    {
                        PiperLogger.LogInfo($"[OnModelChanged] Adding {_chineseTestPhrases.Count} Chinese phrases");
                        _phraseDropdown.AddOptions(_chineseTestPhrases);
                    }
                    else
                    {
                        PiperLogger.LogWarning("[OnModelChanged] Chinese phrases not available, using fallback");
                        _phraseDropdown.AddOptions(new List<string> { "Custom Input", "你好" });
                    }

                    if (_inputField != null)
                        _inputField.text = "你好";

                    // Apply Chinese font to UI elements if available
                    ApplyLanguageFont("zh");
                }
                else
                {
                    if (_englishTestPhrases != null && _englishTestPhrases.Count > 0)
                    {
                        PiperLogger.LogInfo($"[OnModelChanged] Adding {_englishTestPhrases.Count} English phrases");
                        _phraseDropdown.AddOptions(_englishTestPhrases);
                    }
                    else
                    {
                        PiperLogger.LogWarning("[OnModelChanged] English phrases not available, using fallback");
                        _phraseDropdown.AddOptions(new List<string> { "Custom Input", "Hello" });
                    }

                    if (_inputField != null)
                        _inputField.text = _defaultEnglishText;

                    // Apply English/default font
                    ApplyLanguageFont("en");
                }
                PiperLogger.LogInfo($"[OnModelChanged] Phrase dropdown now has {_phraseDropdown.options.Count} options");
                _phraseDropdown.value = 1; // デフォルトフレーズを選択
            }
            else
            {
                PiperLogger.LogError("[OnModelChanged] _phraseDropdown is null! Please check Unity Inspector");
            }
        }

        private void OnPhraseChanged(int index)
        {
            if (_phraseDropdown == null || _inputField == null)
                return;

            // モデルに応じたフレーズリストを取得
            List<string> phrases;
            var modelIndex = _modelDropdown?.value ?? 0;
            if (modelIndex == 0)  // Japanese
                phrases = _japaneseTestPhrases;
            else if (modelIndex == 2)  // Chinese
                phrases = _chineseTestPhrases;
            else  // English
                phrases = _englishTestPhrases;

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
                    var modelName = GetModelNameForIndex(_modelDropdown?.value ?? 0);
                    var language = _modelLanguages[modelName];
                    _inputField.text = GetDefaultTextForLanguage(language);
                }
                _inputField.Select(); // フォーカスを設定
            }
        }

        private void OnBackendChanged(int index)
        {
            // AutoとGPU Computeを除外したため、インデックスが変更されている
            _selectedBackend = index switch
            {
                0 => InferenceBackend.CPU,
                1 => InferenceBackend.GPUPixel,
                _ => InferenceBackend.CPU
            };
            PiperLogger.LogDebug($"Backend changed to: {_selectedBackend}");
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
            var inputText = _inputField.text;
            PiperLogger.LogInfo($"Starting audio generation for text: {inputText}");

#if UNITY_ANDROID
            // Additional encoding debug on Android
            var utf8Bytes = System.Text.Encoding.UTF8.GetBytes(inputText);
            PiperLogger.LogInfo($"[Android] Text length: {inputText.Length} chars, UTF-8 bytes: {utf8Bytes.Length}");
            PiperLogger.LogInfo($"[Android] First few bytes: {string.Join(" ", utf8Bytes.Take(20).Select(b => b.ToString("X2")))}");

            // Try to detect if the text is correctly encoded by checking UTF-8 bytes
            var testPhraseBytes = new byte[] { 0xE3, 0x81, 0x93, 0xE3, 0x82, 0x93, 0xE3, 0x81, 0xAB, 0xE3, 0x81, 0xA1, 0xE3, 0x81, 0xAF };
            var testPhrase = System.Text.Encoding.UTF8.GetString(testPhraseBytes);
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
                var modelNames = new[] { "ja_JP-test-medium", "en_US-ljspeech-medium", "zh_CN-huayan-medium" };
                var modelName = modelNames[_modelDropdown?.value ?? 0];
                PiperLogger.LogDebug($"Selected model: {modelName}");

                // モデルをロード
                SetStatus("モデルをロード中...");
                var loadStopwatch = Stopwatch.StartNew();
                PiperLogger.LogDebug($"Loading model asset: Models/{modelName}");

                var modelAsset = Resources.Load<ModelAsset>($"Models/{modelName}") ?? throw new Exception($"モデルが見つかりません: {modelName}");
                PiperLogger.LogDebug($"Model asset loaded successfully");
                timings["ModelLoad"] = loadStopwatch.ElapsedMilliseconds;

                // JSONコンフィグをロード
                PiperLogger.LogDebug($"Loading config: Models/{modelName}.onnx.json");

                // デバッグ: 利用可能なTextAssetをリスト
                var allTextAssets = Resources.LoadAll<TextAsset>("Models");
                PiperLogger.LogInfo($"Available TextAssets in Resources/Models: {allTextAssets.Length}");
                foreach (var asset in allTextAssets)
                {
                    PiperLogger.LogInfo($"  - {asset.name}");
                }

                var jsonAsset = Resources.Load<TextAsset>($"Models/{modelName}.onnx.json");
                if (jsonAsset == null)
                {
                    // 拡張子なしで試す
                    PiperLogger.LogDebug($"Trying without extension: Models/{modelName}.onnx");
                    jsonAsset = Resources.Load<TextAsset>($"Models/{modelName}.onnx");
                }

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

                // Create PiperConfig with GPU settings from UI
                var piperConfig = new PiperConfig
                {
                    Backend = _selectedBackend,
                    AllowFallbackToCPU = true, // CPUフォールバックは常に有効
                    GPUSettings = _gpuSettings
                };

                // Use the overload that accepts PiperConfig
                if (_generator is InferenceAudioGenerator inferenceGen)
                {
                    await inferenceGen.InitializeAsync(modelAsset, config, piperConfig);

                    // Update backend info text
                    if (_backendInfoText != null)
                    {
                        _backendInfoText.text = $"Backend: {inferenceGen.ActualBackendType}";
                    }
                }
                else
                {
                    await _generator.InitializeAsync(modelAsset, config);
                }

                PiperLogger.LogDebug("Generator initialized successfully");

                // 音素に変換
                SetStatus("音素に変換中...");
                var phonemeStopwatch = Stopwatch.StartNew();
                string[] phonemes;
                var language = _modelLanguages[modelName];

                // Define konnichiwa string from UTF-8 bytes for special debugging
                var konnichiwaBytes = new byte[] { 0xE3, 0x81, 0x93, 0xE3, 0x82, 0x93, 0xE3, 0x81, 0xAB, 0xE3, 0x81, 0xA1, 0xE3, 0x81, 0xAF };
                var konnichiwa = System.Text.Encoding.UTF8.GetString(konnichiwaBytes);

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
                else if (language == "zh" && _chinesePhonemizer != null)
                {
                    PiperLogger.LogDebug("[InferenceEngineDemo] Using Chinese phonemizer for Chinese text");
                    PiperLogger.LogInfo($"[InferenceEngineDemo] Input text: '{_inputField.text}'");

                    var chineseStopwatch = Stopwatch.StartNew();
                    var phonemeResult = await _chinesePhonemizer.PhonemizeAsync(_inputField.text, "zh");
                    timings["ChinesePhonemizer"] = chineseStopwatch.ElapsedMilliseconds;

                    phonemes = phonemeResult.Phonemes;
                    PiperLogger.LogInfo($"[Chinese] Phonemes ({phonemes.Length}): {string.Join(" ", phonemes)}");

                    // Show phoneme details in UI
                    if (_phonemeDetailsText != null)
                    {
                        _phonemeDetailsText.text = $"Chinese: {string.Join(" ", phonemes)}";
                    }
                }
                else if (language == "zh")
                {
                    // Chinese phonemizer is required for Chinese text
                    var errorMsg = "Chinese phonemizer is required for Chinese text but is not available.\n" +
                                  "This should not happen as it's built into the plugin.";
                    throw new Exception(errorMsg);
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
                else if (language == "en" && _englishPhonemizer != null)
                {
                    PiperLogger.LogDebug("[InferenceEngineDemo] Using Flite LTS phonemizer for English text");
                    PiperLogger.LogInfo($"[InferenceEngineDemo] Input text: '{_inputField.text}'");

                    var englishStopwatch = Stopwatch.StartNew();
                    var phonemeResult = await _englishPhonemizer.PhonemizeAsync(_inputField.text, "en");
                    timings["FliteLTS"] = englishStopwatch.ElapsedMilliseconds;

                    // Convert Arpabet to IPA for eSpeak-compatible model
                    var arpabetPhonemes = phonemeResult.Phonemes;
                    PiperLogger.LogInfo($"[English] Arpabet phonemes ({arpabetPhonemes.Length}): {string.Join(" ", arpabetPhonemes)}");

                    // Arpabet音素の詳細をログ出力
                    for (int i = 0; i < arpabetPhonemes.Length; i++)
                    {
                        PiperLogger.LogDebug($"  Arpabet[{i}]: '{arpabetPhonemes[i]}'");
                    }

                    phonemes = ArpabetToIPAConverterTemp.ConvertAll(arpabetPhonemes);
                    PiperLogger.LogInfo($"[English] IPA phonemes ({phonemes.Length}): {string.Join(" ", phonemes)}");

                    // IPA音素の詳細をログ出力
                    for (int i = 0; i < phonemes.Length; i++)
                    {
                        PiperLogger.LogDebug($"  IPA[{i}]: '{phonemes[i]}'");
                    }

                    // Show phoneme details in UI
                    if (_phonemeDetailsText != null)
                    {
                        _phonemeDetailsText.text = $"English (Flite LTS):\nArpabet: {string.Join(" ", arpabetPhonemes)}\nIPA: {string.Join(" ", phonemes)}";
                    }
                }
                else
                {
                    // Fallback: Basic word splitting for unknown languages
                    phonemes = _inputField.text.ToLower()
                        .Replace(",", " _")
                        .Replace(".", " _")
                        .Replace("!", " _")
                        .Replace("?", " _")
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    PiperLogger.LogInfo($"Fallback phonemes ({phonemes.Length}): {string.Join(" ", phonemes)}");
                }
#else
                // WebGL platform
#if UNITY_WEBGL && !UNITY_EDITOR
                if (language == "ja")
                {
                    // Use WebGL OpenJTalk phonemizer for Japanese
                    PiperLogger.LogDebug("[InferenceEngineDemo] Using WebGL OpenJTalk for Japanese text");
                    PiperLogger.LogInfo($"[InferenceEngineDemo] Input text: '{_inputField.text}'");
                    
                    try
                    {
                        // WebGLOpenJTalkUnityPhonemizerのシングルトンインスタンスを取得
                        var webglPhonemizer = WebGLOpenJTalkUnityPhonemizer.GetInstance();
                        
                        // 初期化（すでに初期化済みの場合はスキップされる）
                        await webglPhonemizer.InitializeAsync();
                        
                        var webglStopwatch = Stopwatch.StartNew();
                        var phonemeResult = await webglPhonemizer.PhonemizeAsync(_inputField.text, language);
                        timings["WebGLOpenJTalk"] = webglStopwatch.ElapsedMilliseconds;
                        var openJTalkPhonemes = phonemeResult.Phonemes;
                        
                        PiperLogger.LogInfo($"[WebGL OpenJTalk] Raw phonemes ({openJTalkPhonemes.Length}): {string.Join(" ", openJTalkPhonemes)}");
                        
                        // Special debug for 'こんにちは'
                        if (_inputField.text == "こんにちは")
                        {
                            PiperLogger.LogInfo($"=== Special debug for 'こんにちは' ===");
                            for (int i = 0; i < openJTalkPhonemes.Length; i++)
                            {
                                var p = openJTalkPhonemes[i];
                                PiperLogger.LogInfo($"  Phoneme[{i}]: '{p}' (length: {p.Length})");
                                if (p == "__chi__")
                                {
                                    PiperLogger.LogInfo($"    -> Found __chi__ marker at index {i}");
                                }
                                else if (i > 0 && openJTalkPhonemes[i-1] == "__chi__" && p == "i")
                                {
                                    PiperLogger.LogInfo($"    -> This is the 'i' after __chi__");
                                }
                                else if (p == "i" && i < openJTalkPhonemes.Length - 1)
                                {
                                    PiperLogger.LogInfo($"    -> This is the 'chi' sound component");
                                }
                            }
                        }
                        
                        // Convert OpenJTalk phonemes to Piper phonemes
                        phonemes = OpenJTalkToPiperMapping.ConvertToPiperPhonemes(openJTalkPhonemes);
                        PiperLogger.LogInfo($"[WebGL OpenJTalk] Converted to Piper phonemes ({phonemes.Length}): {string.Join(" ", phonemes)}");
                        
                        // Show phoneme details in UI
                        if (_phonemeDetailsText != null)
                        {
                            _phonemeDetailsText.text = $"WebGL OpenJTalk: {string.Join(" ", openJTalkPhonemes)}\nPiper: {string.Join(" ", phonemes)}";
                        }
                    }
                    catch (Exception ex)
                    {
                        PiperLogger.LogError($"[WebGL OpenJTalk] Failed: {ex.Message}");
                        throw new Exception($"WebGL OpenJTalk phonemization failed: {ex.Message}", ex);
                    }
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
                    PiperLogger.LogInfo($"Fallback phonemes ({phonemes.Length}): {string.Join(" ", phonemes)}");
                }
#else
                // Unity Editor or other platforms without OpenJTalk
                if (language == "ja")
                {
                    throw new Exception("Japanese text-to-speech is not supported on this platform. OpenJTalk native library is required.");
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
#endif

                // 音素変換の詳細をログ出力
                PiperLogger.LogDebug($"Input text: '{_inputField.text}'");

                // 「こんにちは」の場合、特に詳しくログ
                if (_inputField.text == konnichiwa)
                {
                    PiperLogger.LogInfo("=== Special debug for 'こんにちは' ===");
                    for (var i = 0; i < phonemes.Length; i++)
                    {
                        PiperLogger.LogInfo($"  Phoneme[{i}]: '{phonemes[i]}' (length: {phonemes[i].Length})");
                        
                        // WebGL環境でのマーカー確認
#if UNITY_WEBGL && !UNITY_EDITOR
                        if (phonemes[i] == "__CH__")
                        {
                            PiperLogger.LogInfo($"    -> WebGL: __CH__ marker detected at position {i}");
                        }
#endif
                        
                        // PUA文字の確認
                        if (phonemes[i].Length == 1 && phonemes[i][0] >= '\ue000' && phonemes[i][0] <= '\uf8ff')
                        {
                            var puaCode = ((int)phonemes[i][0]).ToString("X4", System.Globalization.CultureInfo.InvariantCulture);
                            PiperLogger.LogInfo($"    -> PUA character detected: U+{puaCode}");
                        }
                        
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
                
                // WebGL環境での詳細デバッグ
#if UNITY_WEBGL && !UNITY_EDITOR
                if (_inputField.text == konnichiwa)
                {
                    PiperLogger.LogInfo("[WebGL] === Phoneme ID Debug ===");
                    PiperLogger.LogInfo($"[WebGL] Expected 'ch' ID: 39");
                    for (var i = 0; i < phonemeIds.Length; i++)
                    {
                        if (phonemeIds[i] == 39)
                        {
                            PiperLogger.LogInfo($"[WebGL] Found ID 39 at position {i} (this should be 'ch')");
                        }
                        else if (phonemeIds[i] == 32)
                        {
                            PiperLogger.LogWarning($"[WebGL] Found ID 32 at position {i} (wrong! this is 'ty', not 'ch')");
                        }
                    }
                }
#endif
                
                timings["Encoding"] = encodeStopwatch.ElapsedMilliseconds;

                // Log phoneme to ID mapping for debugging
                var phonemeIdPairs = new List<string>();
                for (var i = 0; i < Math.Min(phonemes.Length, phonemeIds.Length); i++)
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

                // 音声データの詳細な統計情報
                PiperLogger.LogInfo($"Audio statistics before processing:");
                PiperLogger.LogInfo($"  - Sample count: {audioData.Length}");
                PiperLogger.LogInfo($"  - Duration: {audioData.Length / (float)config.SampleRate:F2} seconds");
                PiperLogger.LogInfo($"  - Max absolute value: {maxVal:F6}");
                PiperLogger.LogInfo($"  - Mean absolute value: {audioData.Select(x => Math.Abs(x)).Average():F6}");
                PiperLogger.LogInfo($"  - First 10 samples: {string.Join(", ", audioData.Take(10).Select(x => x.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)))}");

                if (maxVal < 0.01f)
                {
                    // 音声が小さすぎる場合は増幅
                    SetStatus("音声データを増幅中...");
                    PiperLogger.LogWarning($"Audio data is extremely quiet (max: {maxVal:F6}). This may indicate a model output issue.");
                    var amplificationFactor = 0.3f / maxVal; // 最大値を0.3にする
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

                    // Add GPU info
                    if (_generator is InferenceAudioGenerator infGen)
                    {
                        _phonemeDetailsText.text += $"\n\n[GPU Info]\nBackend: {infGen.ActualBackendType}";
                        _phonemeDetailsText.text += $"\nFloat16: {(_gpuSettings.UseFloat16 ? "Enabled" : "Disabled")}";
                        _phonemeDetailsText.text += $"\nBatch Size: {_gpuSettings.MaxBatchSize}";
                    }
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

        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            if (deviceWasChanged)
            {
                PiperLogger.LogWarning("[InferenceEngineDemo] Audio device was changed. Resetting audio configuration...");

                // Get current audio configuration
                var config = AudioSettings.GetConfiguration();

                // Log current configuration
                PiperLogger.LogInfo($"[InferenceEngineDemo] Audio config - Sample Rate: {config.sampleRate}, Buffer Size: {config.dspBufferSize}, Speaker Mode: {config.speakerMode}");

                // Reset audio system with current configuration
                AudioSettings.Reset(config);

                // Recreate AudioSource if needed
                if (_audioSource == null || !_audioSource.enabled)
                {
                    PiperLogger.LogWarning("[InferenceEngineDemo] AudioSource was lost. Attempting to recreate...");
                    _audioSource = gameObject.GetComponent<AudioSource>();
                    if (_audioSource == null)
                    {
                        _audioSource = gameObject.AddComponent<AudioSource>();
                        PiperLogger.LogInfo("[InferenceEngineDemo] AudioSource recreated.");
                    }
                }
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
            if (jsonObj["phoneme_id_map"] is JObject phonemeIdMap)
            {
                PiperLogger.LogDebug($"[ParseConfig] Found phoneme_id_map with {phonemeIdMap.Count} entries");

                foreach (var kvp in phonemeIdMap)
                {
                    if (kvp.Value is JArray idArray && idArray.Count > 0)
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
            
            // Create string from UTF-8 bytes directly
            byte[] konnichiwaBytes = new byte[] { 0xE3, 0x81, 0x93, 0xE3, 0x82, 0x93, 0xE3, 0x81, 0xAB, 0xE3, 0x81, 0xA1, 0xE3, 0x81, 0xAF };
            string testText = System.Text.Encoding.UTF8.GetString(konnichiwaBytes);
            PiperLogger.LogInfo($"[Android Debug] Test text from UTF-8 bytes: {testText}");
            
            // Also test with char array
            string charArrayText = new string(new char[] { 'こ', 'ん', 'に', 'ち', 'は' });
            PiperLogger.LogInfo($"[Android Debug] Test text from char array: {charArrayText}");
            
            // Get UTF-8 bytes from both
            byte[] utf8FromBytes = System.Text.Encoding.UTF8.GetBytes(testText);
            byte[] utf8FromChars = System.Text.Encoding.UTF8.GetBytes(charArrayText);
            
            PiperLogger.LogInfo($"[Android Debug] UTF-8 from bytes text ({utf8FromBytes.Length}): {BitConverter.ToString(utf8FromBytes)}");
            PiperLogger.LogInfo($"[Android Debug] UTF-8 from chars text ({utf8FromChars.Length}): {BitConverter.ToString(utf8FromChars)}");
            
            // Check if bytes match expected
            bool bytesMatchExpected = true;
            if (utf8FromBytes.Length == konnichiwaBytes.Length)
            {
                for (int i = 0; i < utf8FromBytes.Length; i++)
                {
                    if (utf8FromBytes[i] != konnichiwaBytes[i])
                    {
                        bytesMatchExpected = false;
                        break;
                    }
                }
            }
            else
            {
                bytesMatchExpected = false;
            }
            
            PiperLogger.LogInfo($"[Android Debug] UTF-8 bytes match expected: {bytesMatchExpected}");
            
            // Test if we can create correct string from bytes
            PiperLogger.LogInfo($"[Android Debug] Direct UTF-8 string successful: {testText.Length == 5}");
            
            PiperLogger.LogInfo("[Android Debug] === End Android Setup Debug ===");
        }
        
        private System.Collections.IEnumerator AutoTestTTSGeneration()
        {
            PiperLogger.LogInfo("[uPiper] Waiting 2 seconds before auto-testing TTS...");
            yield return new WaitForSeconds(2f);
            
            PiperLogger.LogInfo("[uPiper] Starting auto TTS test...");
            
            // Set Japanese text from UTF-8 bytes to avoid encoding issues
            if (_inputField != null)
            {
                byte[] konnichiwaBytes = new byte[] { 0xE3, 0x81, 0x93, 0xE3, 0x82, 0x93, 0xE3, 0x81, 0xAB, 0xE3, 0x81, 0xA1, 0xE3, 0x81, 0xAF };
                _inputField.text = System.Text.Encoding.UTF8.GetString(konnichiwaBytes);
                PiperLogger.LogInfo($"[uPiper] Set input text: {_inputField.text}");
            }
            
            // Try to generate TTS
            if (_generateButton != null && _generateButton.isActiveAndEnabled)
            {
                PiperLogger.LogInfo("[uPiper] Clicking generate button programmatically...");
                _ = GenerateAudioAsync();
                
                // Wait for generation
                yield return new WaitForSeconds(5f);
                
                PiperLogger.LogInfo("[uPiper] Auto test completed. Check if audio was generated.");
            }
            else
            {
                PiperLogger.LogError("[uPiper] Generate button not available for auto test");
            }
        }
#endif

        /// <summary>
        /// モデルのインデックスからモデル名を取得
        /// </summary>
        private string GetModelNameForIndex(int index)
        {
            var modelNames = new[] { "ja_JP-test-medium", "en_US-ljspeech-medium", "zh_CN-huayan-medium" };
            return index >= 0 && index < modelNames.Length ? modelNames[index] : modelNames[0];
        }

        /// <summary>
        /// 言語に応じたデフォルトテキストを取得
        /// </summary>
        private string GetDefaultTextForLanguage(string language)
        {
            return language switch
            {
                "ja" => _defaultJapaneseText,
                "zh" => "你好",
                "en" => _defaultEnglishText,
                _ => _defaultEnglishText
            };
        }
    }
}