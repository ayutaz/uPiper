using UnityEngine;

namespace uPiper.Core.Backends
{
    /// <summary>
    /// Configuration for the Camb AI cloud TTS backend.
    ///
    /// Create an instance via <c>Assets &gt; Create &gt; uPiper &gt; Camb AI Settings</c>
    /// and reference it from <see cref="CambBackend"/>. The API key is read from
    /// this asset or, if empty, from the <c>CAMB_API_KEY</c> environment variable.
    ///
    /// All endpoints derive from <see cref="BaseUrl"/>. The real Camb AI REST surface
    /// lives under <c>https://client.camb.ai/apis/</c> and uses the
    /// <c>x-api-key</c> header. Default TTS path is <c>tts-stream</c>, which
    /// returns raw WAV bytes in a single response (no task polling).
    /// </summary>
    [CreateAssetMenu(
        fileName = "CambSettings",
        menuName = "uPiper/Camb AI Settings",
        order = 100)]
    public class CambSettings : ScriptableObject
    {
        private const string DefaultBaseUrl = "https://client.camb.ai/apis/";
        private const string DefaultSpeechModel = "mars-flash";
        private const string DefaultVoice = "default";
        private const int DefaultVoiceId = 156549;
        private const string DefaultLanguageCode = "en-us";
        private const int DefaultSampleRate = 24000;
        private const int DefaultTimeoutMs = 60000;

        [Header("Authentication")]
        [Tooltip("Camb AI API key. Sent as x-api-key header. Leave empty to read from CAMB_API_KEY env var.")]
        [SerializeField]
        private string _apiKey = string.Empty;

        [Header("Endpoint")]
        [Tooltip("Camb AI base URL. Defaults to https://client.camb.ai/apis/ . Must end with a trailing slash.")]
        [SerializeField]
        private string _baseUrl = DefaultBaseUrl;

        [Header("Voice & Model")]
        [Tooltip("Camb speech_model id. 'mars-flash' is recommended for low latency.")]
        [SerializeField]
        private string _speechModel = DefaultSpeechModel;

        [Tooltip("Default Camb voice id (task-based fallback only). tts-stream does not require a voice id.")]
        [SerializeField]
        private string _defaultVoice = DefaultVoice;

        [Tooltip("Numeric voice_id REQUIRED by tts-stream. Look one up via GET /list-voices. 156549 is a verified en-us voice.")]
        [SerializeField]
        private int _voiceId = DefaultVoiceId;

        [Tooltip("Default short-name language code sent to Camb (e.g. 'en-us', 'zh-cn'). Resolved to numeric id via source-languages.")]
        [SerializeField]
        private string _language = DefaultLanguageCode;

        [Header("Audio")]
        [Tooltip("Expected sample rate of returned PCM/WAV when there is no RIFF header. Camb mars-flash defaults to 24kHz.")]
        [SerializeField]
        private int _sampleRate = DefaultSampleRate;

        [Header("Network")]
        [Tooltip("Request timeout in milliseconds.")]
        [SerializeField]
        private int _timeoutMs = DefaultTimeoutMs;

        /// <summary>Effective API key (serialized field, falling back to env var).</summary>
        public string ApiKey
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_apiKey))
                {
                    return _apiKey.Trim();
                }
                return System.Environment.GetEnvironmentVariable("CAMB_API_KEY") ?? string.Empty;
            }
        }

        public string BaseUrl
        {
            get
            {
                var b = string.IsNullOrWhiteSpace(_baseUrl) ? DefaultBaseUrl : _baseUrl.Trim();
                return b.EndsWith("/") ? b : b + "/";
            }
        }

        public string SpeechModel => string.IsNullOrWhiteSpace(_speechModel) ? DefaultSpeechModel : _speechModel;
        public string DefaultVoice => string.IsNullOrWhiteSpace(_defaultVoice) ? DefaultVoice : _defaultVoice;
        public int VoiceId => _voiceId > 0 ? _voiceId : DefaultVoiceId;
        public string Language => string.IsNullOrWhiteSpace(_language) ? DefaultLanguageCode : _language.Trim().ToLowerInvariant();

        /// <summary>Back-compat alias for <see cref="Language"/>.</summary>
        public string DefaultLanguage => Language;

        /// <summary>Back-compat alias for <see cref="SpeechModel"/>.</summary>
        public string Model => SpeechModel;

        /// <summary>Streaming TTS endpoint — POST JSON, returns WAV bytes.</summary>
        public string TtsStreamUrl => BaseUrl + "tts-stream";

        /// <summary>Task-based TTS create endpoint (fallback).</summary>
        public string TtsCreateUrl => BaseUrl + "tts";

        public string TtsStatusUrl(string taskId) => BaseUrl + "tts/" + taskId;
        public string TtsResultUrl(string runId) => BaseUrl + "tts-result/" + runId;

        public string SourceLanguagesUrl => BaseUrl + "source-languages";

        /// <summary>Back-compat — some old code referenced a single Endpoint. Points at the preferred streaming URL.</summary>
        public string Endpoint => TtsStreamUrl;

        public int SampleRate => _sampleRate > 0 ? _sampleRate : DefaultSampleRate;
        public int TimeoutMs => _timeoutMs > 0 ? _timeoutMs : DefaultTimeoutMs;

        /// <summary>Create an in-memory settings object (useful for tests/runtime construction).</summary>
        public static CambSettings CreateRuntime(
            string apiKey,
            string speechModel = DefaultSpeechModel,
            string defaultVoice = DefaultVoice,
            string baseUrl = DefaultBaseUrl,
            string language = DefaultLanguageCode)
        {
            var settings = CreateInstance<CambSettings>();
            settings._apiKey = apiKey ?? string.Empty;
            settings._speechModel = speechModel;
            settings._defaultVoice = defaultVoice;
            settings._baseUrl = baseUrl;
            settings._language = language;
            return settings;
        }
    }
}
