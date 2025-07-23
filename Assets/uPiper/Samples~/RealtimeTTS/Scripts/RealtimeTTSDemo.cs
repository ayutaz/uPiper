using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using uPiper.Core;
using Debug = UnityEngine.Debug;

namespace uPiper.Samples.RealtimeTTS
{
    /// <summary>
    /// リアルタイム音声生成のデモ実装
    /// 低レイテンシでインタラクティブな音声応答を実現します
    /// </summary>
    public class RealtimeTTSDemo : MonoBehaviour
    {
        [System.Serializable]
        public class TTSRequest
        {
            public string text;
            public int priority;
            public Action<AudioClip> onComplete;
            public CancellationTokenSource cancellationTokenSource;
            public float requestTime;
        }

        [Header("UI References")]
        [SerializeField] private TMP_InputField _inputField;
        [SerializeField] private Button _speakButton;
        [SerializeField] private Button _interruptButton;
        [SerializeField] private TextMeshProUGUI _statusText;
        [SerializeField] private TextMeshProUGUI _latencyText;
        [SerializeField] private Slider _emotionSlider;
        [SerializeField] private TextMeshProUGUI _emotionText;
        [SerializeField] private Toggle _preloadToggle;
        [SerializeField] private Toggle _cacheToggle;

        [Header("Quick Response Buttons")]
        [SerializeField] private Button[] _quickResponseButtons;
        [SerializeField] private string[] _quickResponses = new string[]
        {
            "はい",
            "いいえ",
            "わかりました",
            "もう一度お願いします",
            "ありがとうございます"
        };

        [Header("TTS Settings")]
        [SerializeField] private PiperConfig _config;
        [SerializeField] private string _voiceId = "ja_JP-test-medium";
        [SerializeField] private int _maxConcurrentRequests = 2;
        [SerializeField] private float _targetLatencyMs = 100f;

        [Header("Audio Settings")]
        [SerializeField] private AudioSource _primaryAudioSource;
        [SerializeField] private AudioSource _secondaryAudioSource;
        [SerializeField] private float _interruptFadeTime = 0.05f;

        private PiperTTS _tts;
        private readonly Queue<TTSRequest> _requestQueue = new Queue<TTSRequest>();
        private readonly Dictionary<string, AudioClip> _audioCache = new Dictionary<string, AudioClip>();
        private readonly List<string> _preloadPhrases = new List<string>();
        private CancellationTokenSource _currentRequestCancellation;
        private bool _isProcessing;
        private int _activeRequests;
        private float _lastLatency;
        private float _averageLatency;
        private int _latencySamples;
        private AudioSource _currentAudioSource;

        private async void Start()
        {
            InitializeUI();
            await InitializeTTS();
            
            if (_preloadToggle.isOn)
            {
                await PreloadCommonPhrases();
            }
        }

        private void InitializeUI()
        {
            _speakButton.onClick.AddListener(OnSpeakClicked);
            _interruptButton.onClick.AddListener(OnInterruptClicked);
            _emotionSlider.onValueChanged.AddListener(OnEmotionChanged);
            
            // クイックレスポンスボタンの設定
            for (int i = 0; i < _quickResponseButtons.Length && i < _quickResponses.Length; i++)
            {
                int index = i;
                _quickResponseButtons[i].onClick.AddListener(() => OnQuickResponse(index));
                _quickResponseButtons[i].GetComponentInChildren<TextMeshProUGUI>().text = _quickResponses[index];
            }
            
            _interruptButton.gameObject.SetActive(false);
            OnEmotionChanged(_emotionSlider.value);
        }

        private async Task InitializeTTS()
        {
            try
            {
                _statusText.text = "初期化中...";
                
                _tts = new PiperTTS();
                
                // リアルタイム用の最適化設定
                if (_config == null)
                {
                    _config = PiperConfig.CreateDefault();
                }
                
                _config.Backend = InferenceBackend.GPUCompute; // GPU優先
                _config.GPUSettings.MaxBatchSize = 1; // レイテンシ優先
                _config.EnablePhonemeCache = true; // キャッシュ有効
                _config.WorkerThreads = 2; // 並列処理
                
                await _tts.InitializeAsync(_config);
                
                // 音声モデルをロード
                var voiceConfig = new PiperVoiceConfig
                {
                    VoiceId = _voiceId,
                    Language = "ja",
                    SampleRate = 22050
                };
                
                await _tts.LoadVoiceAsync(voiceConfig);
                
                _statusText.text = "準備完了";
                _speakButton.interactable = true;
                
                // バックグラウンド処理を開始
                _ = ProcessRequestQueue();
            }
            catch (Exception ex)
            {
                _statusText.text = $"初期化エラー: {ex.Message}";
                Debug.LogError($"TTS initialization failed: {ex}");
            }
        }

        private async Task PreloadCommonPhrases()
        {
            _statusText.text = "フレーズをプリロード中...";
            
            // よく使うフレーズをプリロード
            _preloadPhrases.AddRange(new[]
            {
                "はい", "いいえ", "わかりました",
                "もう一度お願いします", "ありがとうございます",
                "こんにちは", "さようなら", "おはようございます"
            });
            
            foreach (var phrase in _preloadPhrases)
            {
                try
                {
                    if (!_audioCache.ContainsKey(phrase))
                    {
                        var audioClip = await _tts.GenerateAudioAsync(phrase);
                        _audioCache[phrase] = audioClip;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to preload phrase '{phrase}': {ex.Message}");
                }
            }
            
            _statusText.text = $"準備完了 ({_audioCache.Count}フレーズキャッシュ済み)";
        }

        private void OnSpeakClicked()
        {
            if (string.IsNullOrWhiteSpace(_inputField.text))
                return;
                
            EnqueueRequest(_inputField.text, 1);
        }

        private void OnQuickResponse(int index)
        {
            if (index < 0 || index >= _quickResponses.Length)
                return;
                
            var text = _quickResponses[index];
            EnqueueRequest(text, 2); // クイックレスポンスは高優先度
        }

        private void OnInterruptClicked()
        {
            InterruptCurrentPlayback();
        }

        private void OnEmotionChanged(float value)
        {
            // 感情パラメータの表示（実際の音声生成では未実装）
            string emotion = value switch
            {
                < 0.2f => "冷静",
                < 0.4f => "普通",
                < 0.6f => "やや感情的",
                < 0.8f => "感情的",
                _ => "とても感情的"
            };
            
            _emotionText.text = $"感情: {emotion}";
        }

        private void EnqueueRequest(string text, int priority)
        {
            var request = new TTSRequest
            {
                text = text,
                priority = priority,
                cancellationTokenSource = new CancellationTokenSource(),
                requestTime = Time.realtimeSinceStartup,
                onComplete = PlayAudioClip
            };
            
            lock (_requestQueue)
            {
                // 優先度順に挿入
                var list = _requestQueue.ToList();
                list.Add(request);
                list = list.OrderByDescending(r => r.priority).ToList();
                
                _requestQueue.Clear();
                foreach (var r in list)
                {
                    _requestQueue.Enqueue(r);
                }
            }
            
            _statusText.text = $"キュー: {_requestQueue.Count}件";
        }

        private async Task ProcessRequestQueue()
        {
            while (!_tts.IsDisposed)
            {
                TTSRequest request = null;
                
                lock (_requestQueue)
                {
                    if (_requestQueue.Count > 0 && _activeRequests < _maxConcurrentRequests)
                    {
                        request = _requestQueue.Dequeue();
                    }
                }
                
                if (request != null)
                {
                    _activeRequests++;
                    _ = ProcessRequest(request);
                }
                else
                {
                    await Task.Delay(10);
                }
            }
        }

        private async Task ProcessRequest(TTSRequest request)
        {
            try
            {
                var sw = Stopwatch.StartNew();
                AudioClip audioClip = null;
                
                // キャッシュチェック
                if (_cacheToggle.isOn && _audioCache.TryGetValue(request.text, out var cachedClip))
                {
                    audioClip = cachedClip;
                    _statusText.text = "キャッシュから再生";
                }
                else
                {
                    // 音声生成
                    _statusText.text = "生成中...";
                    audioClip = await _tts.GenerateAudioAsync(request.text, request.cancellationTokenSource.Token);
                    
                    // キャッシュに追加
                    if (_cacheToggle.isOn && audioClip != null)
                    {
                        _audioCache[request.text] = audioClip;
                        
                        // キャッシュサイズ制限
                        if (_audioCache.Count > 100)
                        {
                            var oldestKey = _audioCache.Keys.First();
                            Destroy(_audioCache[oldestKey]);
                            _audioCache.Remove(oldestKey);
                        }
                    }
                }
                
                sw.Stop();
                UpdateLatency((float)sw.ElapsedMilliseconds);
                
                if (!request.cancellationTokenSource.Token.IsCancellationRequested)
                {
                    request.onComplete?.Invoke(audioClip);
                }
            }
            catch (OperationCanceledException)
            {
                _statusText.text = "キャンセル";
            }
            catch (Exception ex)
            {
                _statusText.text = $"エラー: {ex.Message}";
                Debug.LogError($"TTS request failed: {ex}");
            }
            finally
            {
                _activeRequests--;
            }
        }

        private void PlayAudioClip(AudioClip clip)
        {
            if (clip == null)
                return;
                
            // 現在の再生を中断
            if (_currentAudioSource != null && _currentAudioSource.isPlaying)
            {
                StartCoroutine(FadeOutAndStop(_currentAudioSource, _interruptFadeTime));
            }
            
            // 新しい音声を再生
            var audioSource = GetAvailableAudioSource();
            audioSource.clip = clip;
            audioSource.volume = 1f;
            audioSource.Play();
            
            _currentAudioSource = audioSource;
            _statusText.text = "再生中";
            
            _interruptButton.gameObject.SetActive(true);
            _speakButton.gameObject.SetActive(false);
            
            // 再生終了を監視
            StartCoroutine(WaitForPlaybackComplete(audioSource));
        }

        private AudioSource GetAvailableAudioSource()
        {
            if (!_primaryAudioSource.isPlaying)
                return _primaryAudioSource;
            if (!_secondaryAudioSource.isPlaying)
                return _secondaryAudioSource;
            
            // 両方再生中の場合は、プライマリを強制停止
            _primaryAudioSource.Stop();
            return _primaryAudioSource;
        }

        private void InterruptCurrentPlayback()
        {
            _currentRequestCancellation?.Cancel();
            
            if (_currentAudioSource != null && _currentAudioSource.isPlaying)
            {
                StartCoroutine(FadeOutAndStop(_currentAudioSource, _interruptFadeTime));
            }
            
            lock (_requestQueue)
            {
                _requestQueue.Clear();
            }
            
            _statusText.text = "中断されました";
            _interruptButton.gameObject.SetActive(false);
            _speakButton.gameObject.SetActive(true);
        }

        private IEnumerator<WaitForEndOfFrame> FadeOutAndStop(AudioSource audioSource, float fadeTime)
        {
            float startVolume = audioSource.volume;
            float elapsed = 0;
            
            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                audioSource.volume = Mathf.Lerp(startVolume, 0, elapsed / fadeTime);
                yield return new WaitForEndOfFrame();
            }
            
            audioSource.Stop();
            audioSource.volume = 1f;
        }

        private IEnumerator<WaitForEndOfFrame> WaitForPlaybackComplete(AudioSource audioSource)
        {
            while (audioSource.isPlaying)
            {
                yield return new WaitForEndOfFrame();
            }
            
            if (_currentAudioSource == audioSource)
            {
                _statusText.text = "待機中";
                _interruptButton.gameObject.SetActive(false);
                _speakButton.gameObject.SetActive(true);
            }
        }

        private void UpdateLatency(float latencyMs)
        {
            _lastLatency = latencyMs;
            _averageLatency = (_averageLatency * _latencySamples + latencyMs) / (_latencySamples + 1);
            _latencySamples++;
            
            var color = latencyMs <= _targetLatencyMs ? Color.green : 
                       latencyMs <= _targetLatencyMs * 2 ? Color.yellow : 
                       Color.red;
                       
            _latencyText.text = $"レイテンシ: <color=#{ColorUtility.ToHtmlStringRGB(color)}>{latencyMs:F0}ms</color> (平均: {_averageLatency:F0}ms)";
        }

        private void OnDestroy()
        {
            _currentRequestCancellation?.Cancel();
            
            foreach (var clip in _audioCache.Values)
            {
                if (clip != null)
                    Destroy(clip);
            }
            
            _tts?.Dispose();
        }
    }
}