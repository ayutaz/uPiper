using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using uPiper.Core;

namespace uPiper.Samples.StreamingTTS
{
    /// <summary>
    /// ストリーミング音声生成のデモ実装
    /// 文節単位でリアルタイムに音声を生成・再生します
    /// </summary>
    public class StreamingTTSDemo : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_InputField _inputField;
        [SerializeField] private Button _generateButton;
        [SerializeField] private Button _stopButton;
        [SerializeField] private Slider _progressSlider;
        [SerializeField] private TextMeshProUGUI _statusText;
        [SerializeField] private TextMeshProUGUI _chunkText;
        [SerializeField] private Toggle _autoPlayToggle;
        [SerializeField] private Slider _overlapSlider;
        [SerializeField] private TextMeshProUGUI _overlapText;

        [Header("TTS Settings")]
        [SerializeField] private PiperConfig _piperConfig;
        [SerializeField] private string _voiceId = "ja_JP-test-medium";
        [SerializeField] private int _audioSourcePoolSize = 4;
        [SerializeField] private float _crossfadeDuration = 0.1f;

        private PiperTTS _tts;
        private Queue<AudioSource> _audioSourcePool;
        private List<AudioChunk> _pendingChunks;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isGenerating;
        private int _totalChunks;
        private int _processedChunks;
        private AudioSource _currentAudioSource;
        private float _lastPlaybackPosition;

        private async void Start()
        {
            InitializeUI();
            InitializeAudioPool();

            try
            {
                await InitializeTTS();
                _statusText.text = "準備完了";
                _generateButton.interactable = true;
            }
            catch (Exception ex)
            {
                _statusText.text = $"初期化エラー: {ex.Message}";
                Debug.LogError($"Failed to initialize TTS: {ex}");
            }
        }

        private void InitializeUI()
        {
            _generateButton.onClick.AddListener(OnGenerateClicked);
            _stopButton.onClick.AddListener(OnStopClicked);
            _overlapSlider.onValueChanged.AddListener(OnOverlapChanged);

            _stopButton.gameObject.SetActive(false);
            _progressSlider.value = 0;
            _chunkText.text = "";

            OnOverlapChanged(_overlapSlider.value);
        }

        private void InitializeAudioPool()
        {
            _audioSourcePool = new Queue<AudioSource>();
            _pendingChunks = new List<AudioChunk>();

            for (int i = 0; i < _audioSourcePoolSize; i++)
            {
                var audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                _audioSourcePool.Enqueue(audioSource);
            }
        }

        private async Task InitializeTTS()
        {
            _tts = new PiperTTS();

            // GPU推論設定を適用
            if (_piperConfig == null)
            {
                _piperConfig = PiperConfig.CreateDefault();
            }

            await _tts.InitializeAsync(_piperConfig);

            // 音声モデルをロード
            var voiceConfig = new PiperVoiceConfig
            {
                VoiceId = _voiceId,
                Language = "ja",
                SampleRate = 22050
            };

            await _tts.LoadVoiceAsync(voiceConfig);
        }

        private async void OnGenerateClicked()
        {
            if (string.IsNullOrWhiteSpace(_inputField.text))
            {
                _statusText.text = "テキストを入力してください";
                return;
            }

            _isGenerating = true;
            _generateButton.gameObject.SetActive(false);
            _stopButton.gameObject.SetActive(true);
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                await GenerateStreamingAudio(_inputField.text, _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                _statusText.text = "生成をキャンセルしました";
            }
            catch (Exception ex)
            {
                _statusText.text = $"エラー: {ex.Message}";
                Debug.LogError($"Streaming generation error: {ex}");
            }
            finally
            {
                _isGenerating = false;
                _generateButton.gameObject.SetActive(true);
                _stopButton.gameObject.SetActive(false);
            }
        }

        private void OnStopClicked()
        {
            _cancellationTokenSource?.Cancel();
            StopAllAudioSources();
        }

        private void OnOverlapChanged(float value)
        {
            _crossfadeDuration = value;
            _overlapText.text = $"Overlap: {value:F2}s";
        }

        private async Task GenerateStreamingAudio(string text, CancellationToken cancellationToken)
        {
            _statusText.text = "音声生成中...";
            _progressSlider.value = 0;
            _totalChunks = 0;
            _processedChunks = 0;
            _pendingChunks.Clear();

            // テキストを文節に分割
            var chunks = SplitTextIntoChunks(text);
            _totalChunks = chunks.Count;

            // 並列で音声生成を開始
            var generationTask = GenerateAudioChunksAsync(chunks, cancellationToken);

            // 自動再生が有効な場合は再生を開始
            if (_autoPlayToggle.isOn)
            {
                StartCoroutine(PlaybackCoroutine(cancellationToken));
            }

            await generationTask;

            _statusText.text = _cancellationTokenSource.IsCancellationRequested ? "キャンセルされました" : "生成完了";
        }

        private List<string> SplitTextIntoChunks(string text)
        {
            // 簡易的な文節分割（実際のアプリケーションではより高度な分割が必要）
            var chunks = new List<string>();
            var sentences = text.Split(new[] { '。', '！', '？', '、' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var sentence in sentences)
            {
                if (!string.IsNullOrWhiteSpace(sentence))
                {
                    chunks.Add(sentence.Trim());
                }
            }

            return chunks;
        }

        private async Task GenerateAudioChunksAsync(List<string> chunks, CancellationToken cancellationToken)
        {
            var tasks = new List<Task>();
            var semaphore = new SemaphoreSlim(2); // 並列度を制限

            for (int i = 0; i < chunks.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                int chunkIndex = i;
                var chunk = chunks[i];

                var task = Task.Run(async () =>
                {
                    await semaphore.WaitAsync(cancellationToken);
                    try
                    {
                        _chunkText.text = $"生成中: \"{chunk}\"";

                        // ストリーミング生成
                        await foreach (var audioChunk in _tts.StreamAudioAsync(chunk, cancellationToken))
                        {
                            lock (_pendingChunks)
                            {
                                _pendingChunks.Add(audioChunk);
                            }
                        }

                        Interlocked.Increment(ref _processedChunks);
                        UpdateProgress();
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken);

                tasks.Add(task);
            }

            await Task.WhenAll(tasks);
        }

        private IEnumerator PlaybackCoroutine(CancellationToken cancellationToken)
        {
            yield return new WaitForSeconds(0.5f); // 初期バッファリング

            while (!cancellationToken.IsCancellationRequested && (_isGenerating || _pendingChunks.Count > 0))
            {
                AudioChunk chunkToPlay = null;

                lock (_pendingChunks)
                {
                    if (_pendingChunks.Count > 0)
                    {
                        chunkToPlay = _pendingChunks[0];
                        _pendingChunks.RemoveAt(0);
                    }
                }

                if (chunkToPlay != null)
                {
                    yield return PlayAudioChunk(chunkToPlay);
                }
                else
                {
                    yield return new WaitForSeconds(0.1f);
                }
            }
        }

        private IEnumerator PlayAudioChunk(AudioChunk chunk)
        {
            var audioSource = GetNextAudioSource();
            var audioClip = chunk.ToAudioClip();

            audioSource.clip = audioClip;
            audioSource.volume = 0;
            audioSource.Play();

            // フェードイン
            float fadeTime = 0;
            while (fadeTime < _crossfadeDuration)
            {
                audioSource.volume = Mathf.Lerp(0, 1, fadeTime / _crossfadeDuration);

                // 前の音源をフェードアウト
                if (_currentAudioSource != null && _currentAudioSource != audioSource)
                {
                    _currentAudioSource.volume = Mathf.Lerp(1, 0, fadeTime / _crossfadeDuration);
                }

                fadeTime += Time.deltaTime;
                yield return null;
            }

            audioSource.volume = 1;

            // 前の音源を停止
            if (_currentAudioSource != null && _currentAudioSource != audioSource)
            {
                _currentAudioSource.Stop();
                _audioSourcePool.Enqueue(_currentAudioSource);
            }

            _currentAudioSource = audioSource;

            // 再生完了まで待機
            yield return new WaitForSeconds(audioClip.length - _crossfadeDuration);
        }

        private AudioSource GetNextAudioSource()
        {
            if (_audioSourcePool.Count > 0)
            {
                return _audioSourcePool.Dequeue();
            }

            // プールが空の場合は新しく作成
            var audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            return audioSource;
        }

        private void StopAllAudioSources()
        {
            foreach (var audioSource in GetComponents<AudioSource>())
            {
                audioSource.Stop();
                if (!_audioSourcePool.Contains(audioSource))
                {
                    _audioSourcePool.Enqueue(audioSource);
                }
            }

            _currentAudioSource = null;
        }

        private void UpdateProgress()
        {
            if (_totalChunks > 0)
            {
                float progress = (float)_processedChunks / _totalChunks;
                _progressSlider.value = progress;
                _statusText.text = $"生成中: {_processedChunks}/{_totalChunks} チャンク";
            }
        }

        private void OnDestroy()
        {
            _cancellationTokenSource?.Cancel();
            _tts?.Dispose();
        }
    }
}