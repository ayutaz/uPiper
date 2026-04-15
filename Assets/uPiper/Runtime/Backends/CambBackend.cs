using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using uPiper.Core.AudioGeneration;
using uPiper.Core.Logging;

namespace uPiper.Core.Backends
{
    /// <summary>
    /// Cloud TTS backend that calls the Camb AI REST API instead of running
    /// ONNX inference locally. Implements <see cref="IPiperTTS"/> so it can be
    /// used as a drop-in replacement for the local CPU/GPU backends.
    ///
    /// Unlike the local backends, this one skips G2P entirely — Camb accepts
    /// raw text. The returned PCM/WAV bytes are decoded and handed back as a
    /// Unity <see cref="AudioClip"/>.
    /// </summary>
    public class CambBackend : IPiperTTS
    {
        private const string AuthHeader = "x-api-key";

        private readonly PiperConfig _config;
        private readonly CambSettings _settings;
        private readonly AudioClipBuilder _clipBuilder;
        private readonly Dictionary<string, PiperVoiceConfig> _voices;

        private string _currentVoiceId;
        private bool _isInitialized;
        private bool _isDisposed;

        // Cached numeric language id resolved from GET source-languages. -1 means unresolved.
        private int _cachedLanguageId = -1;
        private string _cachedLanguageShortName;

        private event Action<bool> _onInitialized;
        private event Action<PiperVoiceConfig> _onVoiceLoaded;
        private event Action<PiperException> _onError;
        private event Action<string> _onLanguageDetected;

        public CambBackend(PiperConfig config, CambSettings settings)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _clipBuilder = new AudioClipBuilder();
            _voices = new Dictionary<string, PiperVoiceConfig>();
        }

        #region IPiperTTS properties

        public PiperConfig Configuration => _config;
        public bool IsInitialized => _isInitialized;
        public PiperVoiceConfig CurrentVoice =>
            _currentVoiceId != null && _voices.TryGetValue(_currentVoiceId, out var v) ? v : null;

        public event Action<bool> OnInitialized
        {
            add => _onInitialized += value;
            remove => _onInitialized -= value;
        }
        public event Action<PiperVoiceConfig> OnVoiceLoaded
        {
            add => _onVoiceLoaded += value;
            remove => _onVoiceLoaded -= value;
        }
        public event Action<PiperException> OnError
        {
            add => _onError += value;
            remove => _onError -= value;
        }
        public event Action<string> OnLanguageDetected
        {
            add => _onLanguageDetected += value;
            remove => _onLanguageDetected -= value;
        }

        #endregion

        #region Lifecycle

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                var ex = new PiperException(
                    "CambBackend: missing API key. Set it on the CambSettings asset or via CAMB_API_KEY env var.");
                _onError?.Invoke(ex);
                _onInitialized?.Invoke(false);
                throw ex;
            }

            _isInitialized = true;
            PiperLogger.LogInfo(
                "[CambBackend] Initialized (baseUrl={0}, speechModel={1}, language={2})",
                _settings.BaseUrl, _settings.SpeechModel, _settings.Language);
            _onInitialized?.Invoke(true);
            return Task.CompletedTask;
        }

        public Task LoadVoiceAsync(PiperVoiceConfig voiceConfig, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (voiceConfig == null)
            {
                throw new ArgumentNullException(nameof(voiceConfig));
            }

            _voices[voiceConfig.VoiceId] = voiceConfig;
            _currentVoiceId = voiceConfig.VoiceId;
            _onVoiceLoaded?.Invoke(voiceConfig);
            return Task.CompletedTask;
        }

        public IReadOnlyList<PiperVoiceConfig> GetAvailableVoices()
        {
            var list = new List<PiperVoiceConfig>(_voices.Values);
            return list;
        }

        public void ClearCache() { /* cloud backend has no local phoneme cache */ }

        public CacheStatistics GetCacheStatistics() => new();

        public string DetectLanguage(string text) => _config.DefaultLanguage;

        public IReadOnlyList<string> GetSupportedLanguages() =>
            _config.SupportedLanguages ?? new List<string> { _settings.DefaultLanguage };

        #endregion

        #region Generation

        public Task<AudioClip> GenerateAudioAsync(string text, CancellationToken cancellationToken = default)
            => GenerateAudioAsync(text, CurrentVoice, cancellationToken);

        public Task<AudioClip> GenerateAudioAsync(string text, string language, CancellationToken cancellationToken = default)
            => GenerateInternalAsync(text, CurrentVoice, language, cancellationToken);

        public Task<AudioClip> GenerateAudioAsync(string text, PiperVoiceConfig voiceConfig, CancellationToken cancellationToken = default)
            => GenerateInternalAsync(text, voiceConfig, voiceConfig?.Language, cancellationToken);

        public async IAsyncEnumerable<AudioChunk> StreamAudioAsync(
            string text,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var clip = await GenerateAudioAsync(text, cancellationToken).ConfigureAwait(false);
            yield return ToChunk(clip);
        }

        public async IAsyncEnumerable<AudioChunk> StreamAudioAsync(
            string text,
            PiperVoiceConfig voiceConfig,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var clip = await GenerateAudioAsync(text, voiceConfig, cancellationToken).ConfigureAwait(false);
            yield return ToChunk(clip);
        }

        private static AudioChunk ToChunk(AudioClip clip)
        {
            var samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);
            return new AudioChunk(
                samples: samples,
                sampleRate: clip.frequency,
                channels: clip.channels,
                chunkIndex: 0,
                isFinal: true);
        }

        private async Task<AudioClip> GenerateInternalAsync(
            string text,
            PiperVoiceConfig voiceConfig,
            string language,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (!_isInitialized)
            {
                throw new PiperException("CambBackend not initialized. Call InitializeAsync first.");
            }
            if (string.IsNullOrEmpty(text))
            {
                throw new ArgumentException("Text cannot be null or empty", nameof(text));
            }

            var voiceId = voiceConfig?.VoiceId ?? _settings.DefaultVoice;
            var lang = !string.IsNullOrWhiteSpace(language) ? language.Trim().ToLowerInvariant() : _settings.Language;
            _onLanguageDetected?.Invoke(lang);

            // Resolve & cache numeric language id (not strictly required by tts-stream which
            // accepts short codes, but validates the code and matches the Node/Python SDKs).
            await EnsureLanguageIdAsync(lang, cancellationToken).ConfigureAwait(false);

            var body = BuildStreamRequestJson(text, lang);
            var bytes = await PostStreamAsync(body, cancellationToken).ConfigureAwait(false);

            var (samples, sampleRate) = DecodeWav(bytes, _settings.SampleRate);
            if (_config.NormalizeAudio)
            {
                samples = _clipBuilder.NormalizeAudio(samples);
            }

            var clipName = $"CambTTS_{voiceId}_{DateTime.Now:HHmmss}";
            return _clipBuilder.BuildAudioClip(samples, sampleRate, clipName);
        }

        #endregion

        #region HTTP

        /// <summary>
        /// Build the JSON body for POST tts-stream. Shape matches the Node SDK
        /// (<c>c.textToSpeech.tts({ text, language, speech_model })</c>) and the
        /// Python SDK (<c>c.text_to_speech.tts(text=..., language='en-us', speech_model='mars-flash')</c>).
        /// </summary>
        private string BuildStreamRequestJson(string text, string language)
        {
            var sb = new StringBuilder(256);
            sb.Append('{');
            sb.Append("\"text\":").Append(JsonEncode(text)).Append(',');
            sb.Append("\"language\":").Append(JsonEncode(language)).Append(',');
            sb.Append("\"speech_model\":").Append(JsonEncode(_settings.SpeechModel)).Append(',');
            sb.Append("\"voice_id\":").Append(_settings.VoiceId.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',');
            sb.Append("\"output_configuration\":{\"format\":\"wav\",\"sample_rate\":22050}");
            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>
        /// GET source-languages and cache the numeric id matching the configured
        /// short code. Exact match on <c>short_name</c> is preferred; otherwise
        /// matches on prefix (so "en" resolves to "en-us", id 1).
        /// </summary>
        private async Task EnsureLanguageIdAsync(string shortCode, CancellationToken cancellationToken)
        {
            if (_cachedLanguageId >= 0 && _cachedLanguageShortName == shortCode)
            {
                return;
            }

            using var req = UnityWebRequest.Get(_settings.SourceLanguagesUrl);
            req.SetRequestHeader(AuthHeader, _settings.ApiKey);
            req.SetRequestHeader("Accept", "application/json");
            req.timeout = Mathf.Max(1, _settings.TimeoutMs / 1000);

            var op = req.SendWebRequest();
            while (!op.isDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    req.Abort();
                    cancellationToken.ThrowIfCancellationRequested();
                }
                await Task.Yield();
            }

#if UNITY_2020_2_OR_NEWER
            if (req.result != UnityWebRequest.Result.Success)
#else
            if (req.isNetworkError || req.isHttpError)
#endif
            {
                PiperLogger.LogWarning(
                    "[CambBackend] source-languages lookup failed ({0}): {1}. Continuing without cached id.",
                    (int)req.responseCode, req.error);
                return;
            }

            var json = req.downloadHandler.text;
            var id = ParseLanguageId(json, shortCode);
            if (id >= 0)
            {
                _cachedLanguageId = id;
                _cachedLanguageShortName = shortCode;
                PiperLogger.LogInfo("[CambBackend] Resolved language '{0}' -> id {1}", shortCode, id);
            }
            else
            {
                PiperLogger.LogWarning("[CambBackend] source-languages did not contain '{0}'.", shortCode);
            }
        }

        /// <summary>
        /// Tiny JSON scanner over the <c>source-languages</c> array. The SDKs
        /// resolve by exact <c>short_name</c> first, then prefix match (so "en"
        /// matches "en-us"). Returns -1 if not found.
        /// </summary>
        internal static int ParseLanguageId(string json, string shortCode)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(shortCode))
            {
                return -1;
            }

            var code = shortCode.Trim().ToLowerInvariant();
            var baseCode = code.Split('-', '_')[0];

            int exactId = -1;
            int prefixId = -1;

            var i = 0;
            while (i < json.Length)
            {
                var braceOpen = json.IndexOf('{', i);
                if (braceOpen < 0) break;
                var braceClose = json.IndexOf('}', braceOpen + 1);
                if (braceClose < 0) break;
                var obj = json.Substring(braceOpen, braceClose - braceOpen + 1);
                i = braceClose + 1;

                var shortName = ExtractStringField(obj, "short_name");
                if (shortName == null) continue;
                shortName = shortName.ToLowerInvariant();

                var idStr = ExtractNumberField(obj, "id");
                if (idStr == null) continue;
                if (!int.TryParse(idStr, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var id))
                {
                    continue;
                }

                if (shortName == code && exactId < 0)
                {
                    exactId = id;
                }
                else if (prefixId < 0 && shortName.StartsWith(baseCode, StringComparison.Ordinal))
                {
                    prefixId = id;
                }
            }

            if (exactId >= 0) return exactId;
            return prefixId;
        }

        private static string ExtractStringField(string obj, string field)
        {
            var key = "\"" + field + "\"";
            var k = obj.IndexOf(key, StringComparison.Ordinal);
            if (k < 0) return null;
            var colon = obj.IndexOf(':', k + key.Length);
            if (colon < 0) return null;
            var q1 = obj.IndexOf('"', colon + 1);
            if (q1 < 0) return null;
            var q2 = obj.IndexOf('"', q1 + 1);
            if (q2 < 0) return null;
            return obj.Substring(q1 + 1, q2 - q1 - 1);
        }

        private static string ExtractNumberField(string obj, string field)
        {
            var key = "\"" + field + "\"";
            var k = obj.IndexOf(key, StringComparison.Ordinal);
            if (k < 0) return null;
            var colon = obj.IndexOf(':', k + key.Length);
            if (colon < 0) return null;
            var p = colon + 1;
            while (p < obj.Length && (obj[p] == ' ' || obj[p] == '\t')) p++;
            var start = p;
            while (p < obj.Length && (char.IsDigit(obj[p]) || obj[p] == '-'))
            {
                p++;
            }
            if (p == start) return null;
            return obj.Substring(start, p - start);
        }

        private async Task<byte[]> PostStreamAsync(string json, CancellationToken cancellationToken)
        {
            using var req = new UnityWebRequest(_settings.TtsStreamUrl, UnityWebRequest.kHttpVerbPOST);
            var payload = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(payload) { contentType = "application/json" };
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Accept", "audio/wav");
            req.SetRequestHeader(AuthHeader, _settings.ApiKey);
            req.timeout = Mathf.Max(1, _settings.TimeoutMs / 1000);

            var op = req.SendWebRequest();
            while (!op.isDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    req.Abort();
                    cancellationToken.ThrowIfCancellationRequested();
                }
                await Task.Yield();
            }

#if UNITY_2020_2_OR_NEWER
            if (req.result != UnityWebRequest.Result.Success)
#else
            if (req.isNetworkError || req.isHttpError)
#endif
            {
                var err = new PiperException(
                    $"Camb AI request failed ({(int)req.responseCode}): {req.error}. Body: {req.downloadHandler?.text}");
                _onError?.Invoke(err);
                throw err;
            }

            return req.downloadHandler.data;
        }

        private static string JsonEncode(string value)
        {
            if (value == null)
            {
                return "null";
            }

            var sb = new StringBuilder(value.Length + 2);
            sb.Append('"');
            foreach (var c in value)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        #endregion

        #region WAV decoding

        /// <summary>
        /// Decode a WAV (RIFF) payload into float32 mono/interleaved samples.
        /// Falls back to treating the buffer as raw 16-bit PCM at
        /// <paramref name="fallbackSampleRate"/> if no RIFF header is found.
        /// Supports PCM (fmt code 1) 8/16/24/32-bit and IEEE float (fmt code 3) 32-bit.
        /// </summary>
        internal static (float[] samples, int sampleRate) DecodeWav(byte[] data, int fallbackSampleRate)
        {
            if (data == null || data.Length < 12)
            {
                throw new PiperException("Camb AI returned an empty or truncated audio payload.");
            }

            // Detect RIFF/WAVE.
            if (data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F'
                && data[8] == 'W' && data[9] == 'A' && data[10] == 'V' && data[11] == 'E')
            {
                return DecodeRiffWave(data);
            }

            // Fallback: assume 16-bit signed PCM mono.
            PiperLogger.LogWarning("[CambBackend] No RIFF header; decoding as raw 16-bit PCM mono at {0}Hz.", fallbackSampleRate);
            var sampleCount = data.Length / 2;
            var samples = new float[sampleCount];
            for (var i = 0; i < sampleCount; i++)
            {
                var s = (short)(data[i * 2] | (data[i * 2 + 1] << 8));
                samples[i] = s / 32768f;
            }
            return (samples, fallbackSampleRate);
        }

        private static (float[] samples, int sampleRate) DecodeRiffWave(byte[] data)
        {
            var pos = 12;
            short formatCode = 1;
            short channels = 1;
            var sampleRate = 22050;
            short bitsPerSample = 16;
            var dataOffset = -1;
            var dataLength = 0;

            while (pos + 8 <= data.Length)
            {
                var id = Encoding.ASCII.GetString(data, pos, 4);
                var size = BitConverter.ToInt32(data, pos + 4);
                pos += 8;
                if (id == "fmt ")
                {
                    formatCode = BitConverter.ToInt16(data, pos);
                    channels = BitConverter.ToInt16(data, pos + 2);
                    sampleRate = BitConverter.ToInt32(data, pos + 4);
                    bitsPerSample = BitConverter.ToInt16(data, pos + 14);
                }
                else if (id == "data")
                {
                    dataOffset = pos;
                    dataLength = size;
                    break;
                }
                // Chunks are word-aligned.
                pos += size + (size & 1);
            }

            if (dataOffset < 0)
            {
                throw new PiperException("WAV payload missing 'data' chunk.");
            }

            var bytesPerSample = bitsPerSample / 8;
            if (bytesPerSample <= 0)
            {
                throw new PiperException($"Unsupported WAV bit depth: {bitsPerSample}");
            }

            var totalSamples = dataLength / bytesPerSample;
            var samples = new float[totalSamples];

            if (formatCode == 3 && bitsPerSample == 32)
            {
                Buffer.BlockCopy(data, dataOffset, samples, 0, dataLength);
            }
            else if (formatCode == 1)
            {
                switch (bitsPerSample)
                {
                    case 8:
                        for (var i = 0; i < totalSamples; i++)
                        {
                            samples[i] = (data[dataOffset + i] - 128) / 128f;
                        }
                        break;
                    case 16:
                        for (var i = 0; i < totalSamples; i++)
                        {
                            var s = (short)(data[dataOffset + i * 2] | (data[dataOffset + i * 2 + 1] << 8));
                            samples[i] = s / 32768f;
                        }
                        break;
                    case 24:
                        for (var i = 0; i < totalSamples; i++)
                        {
                            var o = dataOffset + i * 3;
                            var s = data[o] | (data[o + 1] << 8) | (data[o + 2] << 16);
                            if ((s & 0x800000) != 0) { s |= unchecked((int)0xFF000000); }
                            samples[i] = s / 8388608f;
                        }
                        break;
                    case 32:
                        for (var i = 0; i < totalSamples; i++)
                        {
                            var s = BitConverter.ToInt32(data, dataOffset + i * 4);
                            samples[i] = s / 2147483648f;
                        }
                        break;
                    default:
                        throw new PiperException($"Unsupported PCM bit depth: {bitsPerSample}");
                }
            }
            else
            {
                throw new PiperException($"Unsupported WAV format code: {formatCode}");
            }

            // Downmix to mono if needed — matches local backends which emit mono.
            if (channels > 1)
            {
                var frames = totalSamples / channels;
                var mono = new float[frames];
                for (var i = 0; i < frames; i++)
                {
                    var sum = 0f;
                    for (var c = 0; c < channels; c++)
                    {
                        sum += samples[i * channels + c];
                    }
                    mono[i] = sum / channels;
                }
                samples = mono;
            }

            return (samples, sampleRate);
        }

        #endregion

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }
            _isDisposed = true;
            _voices.Clear();
            _isInitialized = false;
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(CambBackend));
            }
        }
    }
}
