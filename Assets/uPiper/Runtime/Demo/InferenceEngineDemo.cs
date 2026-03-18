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
using uPiper.Core.Phonemizers.Implementations;
using uPiper.Core.Phonemizers.Multilingual;

namespace uPiper.Demo
{
    /// <summary>
    /// Unity.InferenceEngineを使用したPiper TTSデモ（多言語対応版）
    ///
    /// ARCHITECTURE OVERVIEW
    /// ====================
    /// This demo implements neural text-to-speech using the following pipeline:
    ///
    /// 1. Text Input (Japanese/English)
    ///    ↓
    /// 2. Phonemization
    ///    - Japanese: DotNetG2P (pure C# MeCab + dictionary) → phonemes
    ///    - English: Flite LTS → phonemes
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
    /// DotNetG2P provides phoneme durations via MeCab tokenization.
    /// VITS models have built-in Duration Predictor that re-estimates
    /// timing during inference, so precise input timing is not required.
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

        [Header("GPU Inference UI")]
        [SerializeField] private TMP_Dropdown _backendDropdown;

        [Header("Language Selection")]
        [SerializeField] private TMP_Dropdown _languageDropdown;

        [Header("Font Settings")]
        [SerializeField] private TMP_FontAsset _japaneseFontAsset;
        [SerializeField] private TMP_FontAsset _defaultFontAsset;

        private InferenceAudioGenerator _generator;
        private PhonemeEncoder _encoder;
        private AudioClipBuilder _audioBuilder;
        private PiperVoiceConfig _currentConfig;
        private bool _isGenerating;
        private InferenceBackend _selectedBackend = InferenceBackend.Auto;
        private GPUInferenceSettings _gpuSettings;
        private DotNetG2PPhonemizer _japanesePhonemizer;
        private MultilingualPhonemizer _multilingualPhonemizer;
        private string _selectedLanguage = "ja";
        private string _currentLatinDefault = "en";

        // 対応言語リスト（multilingual-test-mediumモデル: ja/en/zh/es/fr/pt）
        private static readonly string[] SupportedLanguages = { "ja", "en", "zh", "es", "fr", "pt" };

        private static readonly Dictionary<string, string> LanguageDisplayNames = new()
        {
            { "ja", "日本語 (Japanese)" },
            { "en", "English" },
            { "zh", "中文 (Chinese)" },
            { "es", "Español (Spanish)" },
            { "fr", "Français (French)" },
            { "pt", "Português (Portuguese)" }
        };

        // 各言語のテスト用定型文
        private Dictionary<string, List<string>> _testPhrases;

        private async void Start()
        {
            // Font setup
            SetupFontFallback();

            // Initialize test phrases for all languages
            InitializeTestPhrases();

            _generator = new InferenceAudioGenerator();
            _audioBuilder = new AudioClipBuilder();

            // Initialize GPU settings
            _gpuSettings = new GPUInferenceSettings();

            // Set up audio configuration change handler
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;

#if !UNITY_WEBGL && !UNITY_EDITOR
#if UNITY_IOS
            uPiper.Core.Platform.IOSAudioSessionHelper.Initialize();
#endif
#endif

            // Initialize DotNetG2P phonemizer for Japanese (shared with MultilingualPhonemizer)
            try
            {
                _japanesePhonemizer = new DotNetG2PPhonemizer();
#if UNITY_WEBGL && !UNITY_EDITOR
                await _japanesePhonemizer.InitializeAsync();
#endif
                PiperLogger.LogInfo("[InferenceEngineDemo] DotNetG2P phonemizer initialized successfully");
            }
            catch (Exception ex)
            {
                PiperLogger.LogError($"[InferenceEngineDemo] Failed to initialize DotNetG2P: {ex.Message}");
                _japanesePhonemizer = null;
            }

            // Initialize MultilingualPhonemizer (manages all language backends)
            await InitializeMultilingualPhonemizerAsync(_currentLatinDefault);

            SetupUI();
            SetStatus("準備完了");
#if !UNITY_WEBGL || UNITY_EDITOR
            await System.Threading.Tasks.Task.CompletedTask;
#endif
        }

        private void InitializeTestPhrases()
        {
            _testPhrases = new Dictionary<string, List<string>>
            {
                ["ja"] = new List<string>
                {
                    "自由入力",
                    "こんにちは",
                    "こんにちは、世界！",
                    "ありがとうございます",
                    "日本の日本橋の上で箸を使ってご飯を食べる",
                    "私は東京に住んでいます",
                    "今日はいい天気ですね",
                    "音声合成のテストです",
                    "ユニティで日本語音声合成ができました",
                    "おはようございます、今日も一日頑張りましょう",
                    "DockerとGitHubを使った開発",
                    "ChatGPTとClaudeの違い"
                },
                ["en"] = new List<string>
                {
                    "Custom Input",
                    "Hello world",
                    "Welcome to Unity",
                    "This is a test of the text to speech system",
                    "The quick brown fox jumps over the lazy dog",
                    "How are you doing today?",
                    "Unity Inference Engine is amazing",
                    "Can you hear me clearly?"
                },
                ["zh"] = new List<string>
                {
                    "自由输入",
                    "你好",
                    "你好，世界！",
                    "谢谢你",
                    "今天天气很好",
                    "我在学习中文",
                    "语音合成测试",
                    "欢迎使用语音系统"
                },
                ["es"] = new List<string>
                {
                    "Entrada libre",
                    "Hola mundo",
                    "Buenos días",
                    "Gracias por tu ayuda",
                    "El clima es agradable hoy",
                    "Prueba de síntesis de voz",
                    "Bienvenido al sistema de voz"
                },
                ["fr"] = new List<string>
                {
                    "Saisie libre",
                    "Bonjour le monde",
                    "Bonjour",
                    "Merci beaucoup",
                    "Il fait beau aujourd'hui",
                    "Test de synthèse vocale",
                    "Bienvenue dans le système vocal"
                },
                ["pt"] = new List<string>
                {
                    "Entrada livre",
                    "Olá mundo",
                    "Bom dia",
                    "Muito obrigado",
                    "O tempo está bom hoje",
                    "Teste de síntese de voz",
                    "Bem-vindo ao sistema de voz"
                }
            };
        }

        private async Task InitializeMultilingualPhonemizerAsync(string defaultLatinLanguage)
        {
            try
            {
                _multilingualPhonemizer?.Dispose();
                _multilingualPhonemizer = new MultilingualPhonemizer(
                    SupportedLanguages,
                    defaultLatinLanguage: defaultLatinLanguage,
                    jaPhonemizer: _japanesePhonemizer);
                await _multilingualPhonemizer.InitializeAsync();
                _currentLatinDefault = defaultLatinLanguage;
                PiperLogger.LogInfo($"[InferenceEngineDemo] MultilingualPhonemizer initialized (defaultLatin={defaultLatinLanguage})");
            }
            catch (Exception ex)
            {
                PiperLogger.LogError($"[InferenceEngineDemo] Failed to initialize MultilingualPhonemizer: {ex.Message}");
                _multilingualPhonemizer = null;
            }
        }

        private void SetupFontFallback()
        {
            // Auto-detect fonts from loaded assets
            AutoDetectFonts();

            // Store default font if not set
            if (_defaultFontAsset == null && _inputField != null)
            {
                _defaultFontAsset = _inputField.fontAsset;
            }

            // Setup font fallback chain for multi-language support
            SetupFontFallbackChain();
        }

        private void AutoDetectFonts()
        {
            // Resources.FindObjectsOfTypeAll doesn't have a new API in Unity 2023.1+
            var allFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();

            // Auto-detect default/English font if not assigned
            if (_defaultFontAsset == null)
            {
                foreach (var font in allFonts)
                {
                    if (font.name.Contains("LiberationSans"))
                    {
                        _defaultFontAsset = font;
                        PiperLogger.LogInfo($"[InferenceEngineDemo] Auto-detected default font: {font.name}");
                        break;
                    }
                }
            }

            // Auto-detect Japanese font if not assigned
            if (_japaneseFontAsset == null)
            {
                foreach (var font in allFonts)
                {
                    if (font.name.Contains("NotoSans") && (font.name.Contains("JP") || font.name.Contains("CJK")))
                    {
                        _japaneseFontAsset = font;
                        PiperLogger.LogInfo($"[InferenceEngineDemo] Auto-detected Japanese font: {font.name}");
                        break;
                    }
                }
            }

#if UNITY_EDITOR
            // In Editor, try to load from Samples folder if not found
            var samplesPath = System.IO.Path.Combine(Application.dataPath, "Samples", "uPiper");

            if (_japaneseFontAsset == null && System.IO.Directory.Exists(samplesPath))
            {
                var fontPaths = System.IO.Directory.GetFiles(samplesPath, "NotoSansJP-Regular SDF.asset", System.IO.SearchOption.AllDirectories);
                if (fontPaths.Length > 0)
                {
                    var relativePath = "Assets" + fontPaths[0].Replace(Application.dataPath, "").Replace('\\', '/');
                    _japaneseFontAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(relativePath);
                    if (_japaneseFontAsset != null)
                    {
                        PiperLogger.LogInfo($"[InferenceEngineDemo] Loaded Japanese font from Samples: {relativePath}");
                    }
                }
            }

            if (_defaultFontAsset == null && System.IO.Directory.Exists(samplesPath))
            {
                var fontPaths = System.IO.Directory.GetFiles(samplesPath, "LiberationSans SDF.asset", System.IO.SearchOption.AllDirectories);
                if (fontPaths.Length > 0)
                {
                    var relativePath = "Assets" + fontPaths[0].Replace(Application.dataPath, "").Replace('\\', '/');
                    _defaultFontAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(relativePath);
                    if (_defaultFontAsset != null)
                    {
                        PiperLogger.LogInfo($"[InferenceEngineDemo] Loaded default font from Samples: {relativePath}");
                    }
                }
            }
#endif
        }

        private void SetupFontFallbackChain()
        {
            // Create a fallback chain: Default -> Japanese
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

                    // Apply fallback to all TextMeshProUGUI components in the scene
                    ApplyFontFallbackToAllTextComponents();
                }
                else if (_japaneseFontAsset == null)
                {
                    UnityEngine.Debug.LogWarning("[InferenceEngineDemo] Japanese font asset is not assigned. Japanese text may not display correctly.");
                    UnityEngine.Debug.LogWarning("[InferenceEngineDemo] To fix this, please assign a Japanese-compatible TMP font asset (e.g., NotoSansCJK-Regular SDF) to the InferenceEngineDemo component.");
                }

            }

            // Japanese font setup
            // No additional fallback needed
        }

        private void ApplyFontFallbackToAllTextComponents()
        {
            // Find all TextMeshProUGUI components in the scene
#if UNITY_2023_1_OR_NEWER
            var allTextComponents = FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None);
#else
            var allTextComponents = FindObjectsOfType<TextMeshProUGUI>();
#endif
            foreach (var textComponent in allTextComponents)
            {
                if (textComponent.font != null && textComponent.font != _japaneseFontAsset)
                {
                    var fontAsset = textComponent.font as TMP_FontAsset;
                    if (fontAsset != null)
                    {
                        if (fontAsset.fallbackFontAssetTable == null)
                        {
                            fontAsset.fallbackFontAssetTable = new List<TMP_FontAsset>();
                        }

                        if (!fontAsset.fallbackFontAssetTable.Contains(_japaneseFontAsset))
                        {
                            fontAsset.fallbackFontAssetTable.Add(_japaneseFontAsset);
                        }
                    }
                }
            }

            PiperLogger.LogInfo($"[InferenceEngineDemo] Applied Japanese font fallback to {allTextComponents.Length} text components");
        }


        private void ApplyLanguageFont(string language)
        {
            TMP_FontAsset targetFont = null;

            // Select appropriate font based on language
            switch (language)
            {
                case "ja":
                case "zh":
                    targetFont = _japaneseFontAsset ?? _defaultFontAsset;
                    break;
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
            _multilingualPhonemizer?.Dispose();
            _japanesePhonemizer?.Dispose();
        }

        private void SetupUI()
        {
            PiperLogger.LogInfo("[SetupUI] Starting UI setup");

            // モデル選択ドロップダウンの設定
            if (_modelDropdown != null)
            {
                _modelDropdown.ClearOptions();
                _modelDropdown.AddOptions(new List<string> { "multilingual-test-medium" });
                _modelDropdown.onValueChanged.AddListener(OnModelChanged);
            }

            // 言語選択ドロップダウンの設定
            if (_languageDropdown != null)
            {
                _languageDropdown.ClearOptions();
                var langOptions = new List<string>();
                foreach (var lang in SupportedLanguages)
                {
                    langOptions.Add(LanguageDisplayNames.TryGetValue(lang, out var name) ? name : lang);
                }
                _languageDropdown.AddOptions(langOptions);
                _languageDropdown.value = 0; // ja
                _languageDropdown.onValueChanged.AddListener(OnLanguageChanged);
            }

            // フレーズ選択ドロップダウンの設定
            if (_phraseDropdown != null)
            {
                _phraseDropdown.onValueChanged.AddListener(OnPhraseChanged);
            }
            UpdatePhraseDropdown(_selectedLanguage);

            // GPU推論バックエンドドロップダウンの設定
            if (_backendDropdown != null)
            {
                _backendDropdown.ClearOptions();
                var options = new List<string> { "Auto", "CPU", "GPU Pixel" };
                _backendDropdown.AddOptions(options);
                _backendDropdown.value = 0;
                _selectedBackend = InferenceBackend.Auto;
                _backendDropdown.onValueChanged.AddListener(OnBackendChanged);
            }

            _gpuSettings.UseFloat16 = false;

            // 生成ボタンの設定
            _generateButton?.onClick.AddListener(() => _ = GenerateAudioAsync());

            // 初期テキストの設定
            if (_inputField != null)
            {
                _inputField.text = GetDefaultTextForLanguage(_selectedLanguage);
            }
        }

        private void OnModelChanged(int index)
        {
            // モデルは multilingual-test-medium のみ。言語切り替えは OnLanguageChanged で処理
            PiperLogger.LogInfo("[OnModelChanged] Model: multilingual-test-medium");
        }

        private async void OnLanguageChanged(int index)
        {
            if (index < 0 || index >= SupportedLanguages.Length)
                return;

            var language = SupportedLanguages[index];
            _selectedLanguage = language;
            PiperLogger.LogInfo($"[OnLanguageChanged] Language changed to: {language}");

            // ラテン文字言語の場合、MultilingualPhonemizer を再初期化して正しいバックエンドを使用
            if (LanguageConstants.IsLatinLanguage(language) && _currentLatinDefault != language)
            {
                SetStatus($"{LanguageDisplayNames[language]} のPhonemizerを初期化中...");
                await InitializeMultilingualPhonemizerAsync(language);
            }

            // フレーズドロップダウンとフォントを更新
            UpdatePhraseDropdown(language);
            ApplyLanguageFont(language);

            // デフォルトテキストを設定
            if (_inputField != null)
            {
                _inputField.text = GetDefaultTextForLanguage(language);
            }

            SetStatus("準備完了");
        }

        private void UpdatePhraseDropdown(string language)
        {
            if (_phraseDropdown == null) return;

            _phraseDropdown.ClearOptions();
            if (_testPhrases != null && _testPhrases.TryGetValue(language, out var phrases) && phrases.Count > 0)
            {
                _phraseDropdown.AddOptions(phrases);
            }
            else
            {
                _phraseDropdown.AddOptions(new List<string> { "Custom Input" });
            }

            _phraseDropdown.value = 1; // デフォルトフレーズを選択
        }

        private void OnPhraseChanged(int index)
        {
            if (_phraseDropdown == null || _inputField == null)
                return;

            if (!_testPhrases.TryGetValue(_selectedLanguage, out var phrases))
                return;

            if (index > 0 && index < phrases.Count)
            {
                _inputField.text = phrases[index];
                _inputField.interactable = false;
            }
            else
            {
                _inputField.interactable = true;
                if (string.IsNullOrEmpty(_inputField.text) || phrases.Contains(_inputField.text))
                {
                    _inputField.text = GetDefaultTextForLanguage(_selectedLanguage);
                }
                _inputField.Select();
            }
        }

        private void OnBackendChanged(int index)
        {
            // AutoとGPU Computeを除外したため、インデックスが変更されている
            _selectedBackend = index switch
            {
                0 => InferenceBackend.Auto,
                1 => InferenceBackend.CPU,
                2 => InferenceBackend.GPUPixel,
                _ => InferenceBackend.Auto
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
                var modelNames = new[] { "multilingual-test-medium" };
                var modelName = modelNames[_modelDropdown?.value ?? 0];
                PiperLogger.LogDebug($"Selected model: {modelName}");

                // モデルをロード
                SetStatus("モデルをロード中...");
                var loadStopwatch = Stopwatch.StartNew();
                PiperLogger.LogDebug($"Loading model asset: Models/{modelName}");

                // Try loading from both possible paths (new location first, then old location)
                var modelAsset = Resources.Load<ModelAsset>($"uPiper/Models/{modelName}") ??
                                Resources.Load<ModelAsset>($"Models/{modelName}") ??
                                throw new Exception($"モデルが見つかりません: {modelName}");
                PiperLogger.LogDebug($"Model asset loaded successfully");
                timings["ModelLoad"] = loadStopwatch.ElapsedMilliseconds;

                // JSONコンフィグをロード
                PiperLogger.LogDebug($"Loading config: Models/{modelName}.onnx.json");

                // デバッグ: 利用可能なTextAssetをリスト
                var allTextAssets = Resources.LoadAll<TextAsset>("uPiper/Models");
                if (allTextAssets.Length == 0)
                {
                    allTextAssets = Resources.LoadAll<TextAsset>("Models");
                }
                PiperLogger.LogInfo($"Available TextAssets in Resources: {allTextAssets.Length}");
                foreach (var asset in allTextAssets)
                {
                    PiperLogger.LogInfo($"  - {asset.name}");
                }

                // Try loading JSON configuration with fallback
                var jsonAsset = LoadTextAssetWithFallback($"{modelName}.onnx.json");
                if (jsonAsset == null)
                {
                    // 拡張子なしで試す
                    PiperLogger.LogDebug($"Trying without extension: {modelName}.onnx");
                    jsonAsset = LoadTextAssetWithFallback($"{modelName}.onnx");
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
                }
                else
                {
                    await _generator.InitializeAsync(modelAsset, config);
                }

                PiperLogger.LogDebug("Generator initialized successfully");

                // 音素に変換（MultilingualPhonemizer で全言語対応）
                SetStatus("音素に変換中...");
                var phonemeStopwatch = Stopwatch.StartNew();
                var language = _selectedLanguage;
                var languageId = LanguageConstants.GetLanguageId(language);
                PiperLogger.LogInfo($"[InferenceEngineDemo] Language: {language} (ID: {languageId})");
                PiperLogger.LogInfo($"[InferenceEngineDemo] Input text: '{_inputField.text}'");

                if (_multilingualPhonemizer == null)
                    throw new Exception("MultilingualPhonemizer is not initialized.");

                var g2pStopwatch = Stopwatch.StartNew();
                var multiResult = await _multilingualPhonemizer.PhonemizeWithProsodyAsync(_inputField.text);
                timings["G2P"] = g2pStopwatch.ElapsedMilliseconds;

                var phonemes = multiResult.Phonemes;
                var prosodyA1 = multiResult.ProsodyA1;
                var prosodyA2 = multiResult.ProsodyA2;
                var prosodyA3 = multiResult.ProsodyA3;
                var useProsody = _generator.SupportsProsody && prosodyA1 != null && prosodyA1.Length > 0;

                PiperLogger.LogInfo($"[G2P] Detected language: {multiResult.DetectedPrimaryLanguage}, Phonemes ({phonemes.Length}): {string.Join(" ", phonemes)}");
                if (useProsody)
                {
                    PiperLogger.LogInfo($"[G2P] Prosody A1: [{string.Join(",", prosodyA1.Take(Math.Min(10, prosodyA1.Length)))}...]");
                }

                // Show phoneme details in UI
                if (_phonemeDetailsText != null)
                {
                    var langInfo = $"Lang: {language} (detected: {multiResult.DetectedPrimaryLanguage})";
                    var prosodyInfo = useProsody ? $"\nProsody: A1=[{string.Join(",", prosodyA1.Take(5))}...], A2=[{string.Join(",", prosodyA2.Take(5))}...], A3=[{string.Join(",", prosodyA3.Take(5))}...]" : "";
                    _phonemeDetailsText.text = $"{langInfo}\nPhonemes: {string.Join(" ", phonemes)}{prosodyInfo}";
                }

                timings["Phonemization"] = phonemeStopwatch.ElapsedMilliseconds;

                // 音素をIDに変換（Prosody対応時は展開済みProsody配列も取得）
                var encodeStopwatch = Stopwatch.StartNew();
                int[] phonemeIds;
                int[] expandedA1 = null, expandedA2 = null, expandedA3 = null;

                if (useProsody)
                {
                    var encodingResult = _encoder.EncodeWithProsody(phonemes, prosodyA1, prosodyA2, prosodyA3);
                    phonemeIds = encodingResult.PhonemeIds;
                    expandedA1 = encodingResult.ExpandedProsodyA1;
                    expandedA2 = encodingResult.ExpandedProsodyA2;
                    expandedA3 = encodingResult.ExpandedProsodyA3;
                    PiperLogger.LogInfo($"Encoded with prosody: {phonemes.Length} phonemes -> {phonemeIds.Length} IDs");
                }
                else
                {
                    phonemeIds = _encoder.Encode(phonemes);
                }

                PiperLogger.LogInfo($"Phoneme IDs ({phonemeIds.Length}): {string.Join(", ", phonemeIds)}");
                timings["Encoding"] = encodeStopwatch.ElapsedMilliseconds;

                // 音声生成（languageId を渡す）
                SetStatus("音声を生成中...");
                var synthesisStopwatch = Stopwatch.StartNew();

                float[] audioData;
                if (useProsody && expandedA1 != null)
                {
                    PiperLogger.LogDebug($"Calling GenerateAudioWithProsodyAsync (languageId={languageId})...");
                    audioData = await _generator.GenerateAudioWithProsodyAsync(
                        phonemeIds, expandedA1, expandedA2, expandedA3,
                        languageId: languageId);
                }
                else
                {
                    PiperLogger.LogDebug($"Calling GenerateAudioAsync (languageId={languageId})...");
                    audioData = await _generator.GenerateAudioAsync(phonemeIds, languageId: languageId);
                }
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
#if UNITY_IOS && !UNITY_EDITOR
                    // Ensure AudioSession is active before playback on iOS
                    uPiper.Core.Platform.IOSAudioSessionHelper.EnsureActive();

                    // Log AudioSession status for debugging
                    uPiper.Core.Platform.IOSAudioSessionHelper.LogStatus();
#endif

                    _audioSource.clip = audioClip;
                    _audioSource.volume = 1.0f; // Ensure volume is maximum
                    _audioSource.Play();
                    PiperLogger.LogInfo("Audio playback started");

                    // Log AudioSource state for debugging
                    PiperLogger.LogDebug($"[AudioSource] isPlaying={_audioSource.isPlaying}, volume={_audioSource.volume}, mute={_audioSource.mute}, enabled={_audioSource.enabled}");
#if UNITY_IOS && !UNITY_EDITOR
                    PiperLogger.LogDebug($"[AudioSource] Hardware volume: {uPiper.Core.Platform.IOSAudioSessionHelper.GetVolume():F2}");
#endif
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
                    if (timings.ContainsKey("DotNetG2P"))
                    {
                        _phonemeDetailsText.text += $"\nDotNetG2P: {timings["DotNetG2P"]}ms";
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

            var config = new PiperVoiceConfig
            {
                VoiceId = modelName,
                DisplayName = modelName,
                Language = "ja",
                SampleRate = 22050,
                PhonemeIdMap = new Dictionary<string, int>()
            };

            try
            {
                // Parse JSON using Newtonsoft.Json for accurate parsing
                var jsonObj = JObject.Parse(json);

                // Extract language code
                if (jsonObj["language"]?["code"] != null)
                {
                    config.Language = jsonObj["language"]["code"].ToString();
                }

                // Extract sample rate
                if (jsonObj["audio"]?["sample_rate"] != null)
                {
                    config.SampleRate = jsonObj["audio"]["sample_rate"].ToObject<int>();
                }

                // Extract inference parameters
                if (jsonObj["inference"]?["noise_scale"] != null)
                {
                    config.NoiseScale = jsonObj["inference"]["noise_scale"].ToObject<float>();
                }

                if (jsonObj["inference"]?["length_scale"] != null)
                {
                    config.LengthScale = jsonObj["inference"]["length_scale"].ToObject<float>();
                }

                if (jsonObj["inference"]?["noise_w"] != null)
                {
                    config.NoiseW = jsonObj["inference"]["noise_w"].ToObject<float>();
                }

                // Extract phoneme_type (espeak or openjtalk)
                if (jsonObj["phoneme_type"] != null)
                {
                    config.PhonemeType = jsonObj["phoneme_type"].ToString();
                    PiperLogger.LogDebug($"[ParseConfig] PhonemeType: {config.PhonemeType}");
                }
                else
                {
                    // Default to espeak if not specified
                    config.PhonemeType = "espeak";
                    PiperLogger.LogWarning("[ParseConfig] No phoneme_type found, defaulting to 'espeak'");
                }

                PiperLogger.LogDebug($"[ParseConfig] Language: {config.Language}, SampleRate: {config.SampleRate}");

                // Extract phoneme_id_map
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

                // Extract num_speakers and speaker_id_map
                if (jsonObj["num_speakers"] != null)
                {
                    config.NumSpeakers = jsonObj["num_speakers"].ToObject<int>();
                }
                if (jsonObj["speaker_id_map"] is JObject speakerIdMap)
                {
                    config.SpeakerIdMap = new Dictionary<string, int>();
                    foreach (var kvp in speakerIdMap)
                    {
                        config.SpeakerIdMap[kvp.Key] = kvp.Value.ToObject<int>();
                    }
                    PiperLogger.LogDebug($"[ParseConfig] Parsed {config.SpeakerIdMap.Count} speaker mappings");
                }

                // Extract num_languages and language_id_map
                if (jsonObj["num_languages"] != null)
                {
                    config.NumLanguages = jsonObj["num_languages"].ToObject<int>();
                    PiperLogger.LogDebug($"[ParseConfig] NumLanguages: {config.NumLanguages}");
                }
                if (jsonObj["language_id_map"] is JObject languageIdMap)
                {
                    config.LanguageIdMap = new Dictionary<string, int>();
                    foreach (var kvp in languageIdMap)
                    {
                        config.LanguageIdMap[kvp.Key] = kvp.Value.ToObject<int>();
                    }
                    PiperLogger.LogDebug($"[ParseConfig] Parsed {config.LanguageIdMap.Count} language mappings");
                }
            }
            catch (Exception ex)
            {
                PiperLogger.LogError($"[ParseConfig] Error parsing JSON: {ex.Message}");
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

        /// <summary>
        /// Load model asset with fallback paths
        /// </summary>
        private ModelAsset LoadModelAssetWithFallback(string modelName)
        {
            // Try new location first
            var asset = Resources.Load<ModelAsset>($"{uPiperPaths.RESOURCES_MODELS_PATH}/{modelName}");
            if (asset != null) return asset;

            // Fallback to legacy location
            return Resources.Load<ModelAsset>($"{uPiperPaths.LEGACY_MODELS_PATH}/{modelName}");
        }

        /// <summary>
        /// Load text asset with fallback paths
        /// </summary>
        private TextAsset LoadTextAssetWithFallback(string fileName)
        {
            // Try new location first
            var asset = Resources.Load<TextAsset>($"{uPiperPaths.RESOURCES_MODELS_PATH}/{fileName}");
            if (asset != null) return asset;

            // Fallback to legacy location
            return Resources.Load<TextAsset>($"{uPiperPaths.LEGACY_MODELS_PATH}/{fileName}");
        }

        /// <summary>
        /// 言語に応じたデフォルトテキストを取得
        /// </summary>
        private string GetDefaultTextForLanguage(string language)
        {
            return language switch
            {
                "ja" => "こんにちは",
                "en" => "Hello world",
                "zh" => "你好",
                "es" => "Hola mundo",
                "fr" => "Bonjour le monde",
                "pt" => "Olá mundo",
                _ => "Hello world"
            };
        }
    }
}