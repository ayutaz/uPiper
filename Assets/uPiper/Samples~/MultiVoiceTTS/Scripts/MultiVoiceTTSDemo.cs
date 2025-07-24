using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using uPiper.Core;

namespace uPiper.Samples.MultiVoiceTTS
{
    /// <summary>
    /// 複数音声同時処理のデモ実装
    /// 最大4つの異なる音声を同時に生成・再生します
    /// </summary>
    public class MultiVoiceTTSDemo : MonoBehaviour
    {
        [System.Serializable]
        public class VoiceChannel
        {
            public string channelName = "Channel 1";
            public TMP_InputField inputField;
            public Button generateButton;
            public Slider volumeSlider;
            public Slider pitchSlider;
            public TextMeshProUGUI statusText;
            public AudioSource audioSource;
            public Image statusIndicator;

            [Header("Voice Settings")]
            public string voiceId = "ja_JP-test-medium";
            public float lengthScale = 1.0f;
            public float noiseScale = 0.667f;

            [HideInInspector]
            public PiperTTS tts;
            [HideInInspector]
            public bool isGenerating;
            [HideInInspector]
            public CancellationTokenSource cancellationTokenSource;
        }

        [Header("Channels")]
        [SerializeField] private List<VoiceChannel> _channels = new List<VoiceChannel>();

        [Header("Global Settings")]
        [SerializeField] private PiperConfig _globalConfig;
        [SerializeField] private Button _generateAllButton;
        [SerializeField] private Button _stopAllButton;
        [SerializeField] private TextMeshProUGUI _globalStatusText;
        [SerializeField] private TextMeshProUGUI _performanceText;

        [Header("Performance Monitoring")]
        [SerializeField] private bool _showPerformanceStats = true;
        [SerializeField] private float _statsUpdateInterval = 0.5f;

        private readonly Color _idleColor = Color.gray;
        private readonly Color _generatingColor = Color.yellow;
        private readonly Color _playingColor = Color.green;
        private readonly Color _errorColor = Color.red;

        private float _lastStatsUpdate;
        private int _activeGenerations;
        private float _totalGenerationTime;
        private int _completedGenerations;

        private async void Start()
        {
            InitializeUI();
            await InitializeChannels();
        }

        private void InitializeUI()
        {
            // チャンネル毎のUI設定
            foreach (var channel in _channels)
            {
                var ch = channel; // Capture for closure
                ch.generateButton.onClick.AddListener(() => OnGenerateChannel(ch));
                ch.volumeSlider.onValueChanged.AddListener(value => ch.audioSource.volume = value);
                ch.pitchSlider.onValueChanged.AddListener(value => ch.audioSource.pitch = value);

                ch.statusIndicator.color = _idleColor;
                ch.statusText.text = "待機中";
            }

            // グローバルボタン
            _generateAllButton.onClick.AddListener(OnGenerateAll);
            _stopAllButton.onClick.AddListener(OnStopAll);

            _globalStatusText.text = $"準備中... (0/{_channels.Count} チャンネル)";
        }

        private async Task InitializeChannels()
        {
            // グローバル設定の準備
            if (_globalConfig == null)
            {
                _globalConfig = PiperConfig.CreateDefault();
                // マルチボイス用の最適化
                _globalConfig.GPUSettings.MaxBatchSize = 4;
                _globalConfig.EnableMultiThreadedInference = true;
            }

            int initializedCount = 0;

            // 各チャンネルを並列で初期化
            var tasks = _channels.Select(async channel =>
            {
                try
                {
                    channel.tts = new PiperTTS();

                    // チャンネル毎に異なる設定を適用可能
                    var channelConfig = ScriptableObject.Instantiate(_globalConfig);
                    await channel.tts.InitializeAsync(channelConfig);

                    // 音声モデルをロード
                    var voiceConfig = new PiperVoiceConfig
                    {
                        VoiceId = channel.voiceId,
                        Language = DetectLanguageFromVoiceId(channel.voiceId),
                        SampleRate = 22050
                    };

                    await channel.tts.LoadVoiceAsync(voiceConfig);

                    channel.statusText.text = "準備完了";
                    channel.statusIndicator.color = _idleColor;
                    Interlocked.Increment(ref initializedCount);

                    _globalStatusText.text = $"初期化中... ({initializedCount}/{_channels.Count} チャンネル)";
                }
                catch (Exception ex)
                {
                    channel.statusText.text = $"エラー: {ex.Message}";
                    channel.statusIndicator.color = _errorColor;
                    Debug.LogError($"Failed to initialize channel {channel.channelName}: {ex}");
                }
            }).ToArray();

            await Task.WhenAll(tasks);

            _globalStatusText.text = $"準備完了 ({initializedCount}/{_channels.Count} チャンネル)";
            _generateAllButton.interactable = initializedCount > 0;
        }

        private string DetectLanguageFromVoiceId(string voiceId)
        {
            if (voiceId.StartsWith("ja_JP")) return "ja";
            if (voiceId.StartsWith("en_US")) return "en";
            if (voiceId.StartsWith("zh_CN")) return "zh";
            // デフォルト
            return "ja";
        }

        private async void OnGenerateChannel(VoiceChannel channel)
        {
            if (channel.isGenerating || string.IsNullOrWhiteSpace(channel.inputField.text))
                return;

            channel.cancellationTokenSource = new CancellationTokenSource();
            await GenerateAudioForChannel(channel, channel.cancellationTokenSource.Token);
        }

        private async void OnGenerateAll()
        {
            var channelsToGenerate = _channels
                .Where(ch => !ch.isGenerating && !string.IsNullOrWhiteSpace(ch.inputField.text))
                .ToList();

            if (channelsToGenerate.Count == 0)
            {
                _globalStatusText.text = "生成可能なチャンネルがありません";
                return;
            }

            _generateAllButton.interactable = false;
            _globalStatusText.text = $"一括生成中... (0/{channelsToGenerate.Count})";

            var completedCount = 0;
            var tasks = channelsToGenerate.Select(async channel =>
            {
                channel.cancellationTokenSource = new CancellationTokenSource();
                await GenerateAudioForChannel(channel, channel.cancellationTokenSource.Token);

                Interlocked.Increment(ref completedCount);
                _globalStatusText.text = $"一括生成中... ({completedCount}/{channelsToGenerate.Count})";
            }).ToArray();

            await Task.WhenAll(tasks);

            _globalStatusText.text = "一括生成完了";
            _generateAllButton.interactable = true;
        }

        private void OnStopAll()
        {
            foreach (var channel in _channels)
            {
                channel.cancellationTokenSource?.Cancel();
                channel.audioSource.Stop();
            }

            _globalStatusText.text = "全チャンネル停止";
        }

        private async Task GenerateAudioForChannel(VoiceChannel channel, CancellationToken cancellationToken)
        {
            channel.isGenerating = true;
            channel.generateButton.interactable = false;
            channel.statusIndicator.color = _generatingColor;
            channel.statusText.text = "生成中...";

            Interlocked.Increment(ref _activeGenerations);
            var startTime = Time.realtimeSinceStartup;

            try
            {
                // 音声生成
                var audioClip = await channel.tts.GenerateAudioAsync(
                    channel.inputField.text,
                    cancellationToken
                );

                if (!cancellationToken.IsCancellationRequested)
                {
                    // 再生
                    channel.audioSource.clip = audioClip;
                    channel.audioSource.Play();
                    channel.statusIndicator.color = _playingColor;
                    channel.statusText.text = "再生中";
                }
            }
            catch (OperationCanceledException)
            {
                channel.statusText.text = "キャンセル";
                channel.statusIndicator.color = _idleColor;
            }
            catch (Exception ex)
            {
                channel.statusText.text = $"エラー: {ex.Message}";
                channel.statusIndicator.color = _errorColor;
                Debug.LogError($"Channel {channel.channelName} error: {ex}");
            }
            finally
            {
                channel.isGenerating = false;
                channel.generateButton.interactable = true;

                Interlocked.Decrement(ref _activeGenerations);
                var generationTime = Time.realtimeSinceStartup - startTime;
                _totalGenerationTime += generationTime;
                _completedGenerations++;
            }
        }

        private void Update()
        {
            // チャンネル状態の更新
            foreach (var channel in _channels)
            {
                if (!channel.isGenerating && channel.audioSource.isPlaying)
                {
                    // 再生終了チェック
                    if (channel.audioSource.time >= channel.audioSource.clip.length - 0.1f)
                    {
                        channel.statusIndicator.color = _idleColor;
                        channel.statusText.text = "待機中";
                    }
                }
            }

            // パフォーマンス統計の更新
            if (_showPerformanceStats && Time.time - _lastStatsUpdate > _statsUpdateInterval)
            {
                UpdatePerformanceStats();
                _lastStatsUpdate = Time.time;
            }
        }

        private void UpdatePerformanceStats()
        {
            var activeChannels = _channels.Count(ch => ch.audioSource.isPlaying);
            var avgGenerationTime = _completedGenerations > 0
                ? _totalGenerationTime / _completedGenerations
                : 0;

            _performanceText.text = $"Performance Stats:\n" +
                                  $"Active Generations: {_activeGenerations}\n" +
                                  $"Playing Channels: {activeChannels}/{_channels.Count}\n" +
                                  $"Completed: {_completedGenerations}\n" +
                                  $"Avg Generation Time: {avgGenerationTime:F2}s\n" +
                                  $"GPU Memory: {SystemInfo.graphicsMemorySize}MB";

            // InferenceAudioGeneratorから実際のバックエンド情報を取得
            var backendInfo = "";
            foreach (var channel in _channels)
            {
                if (channel.tts != null && channel.tts.IsInitialized)
                {
                    // 設定されたバックエンド情報を表示
                    backendInfo = "\nBackend: " + _globalConfig.Backend;
                    break;
                }
            }

            _performanceText.text += backendInfo;
        }

        private void OnDestroy()
        {
            foreach (var channel in _channels)
            {
                channel.cancellationTokenSource?.Cancel();
                channel.tts?.Dispose();
            }
        }
    }
}