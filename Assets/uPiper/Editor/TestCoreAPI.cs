using UnityEditor;
using UnityEngine;
using uPiper.Core;

namespace uPiper.Editor
{
    public static class TestCoreAPI
    {
        [MenuItem("uPiper/Test/Create Configuration")]
        public static void CreateTestConfiguration()
        {
            // PiperConfigのインスタンスを作成
            var config = PiperConfig.CreateDefault();
            config.EnableDebugLogging = true;
            config.DefaultLanguage = "ja";
            config.MaxCacheSizeMB = 200;
            config.SampleRate = 22050;

            // 検証
            config.Validate();

            Debug.Log("[uPiper] PiperConfig created:");
            Debug.Log($"  Debug Logging: {config.EnableDebugLogging}");
            Debug.Log($"  Default Language: {config.DefaultLanguage}");
            Debug.Log($"  Max Cache Size: {config.MaxCacheSizeMB} MB");
            Debug.Log($"  Sample Rate: {config.SampleRate} Hz");
            Debug.Log($"  Worker Threads: {config.WorkerThreads}");
            Debug.Log($"  Backend: {config.Backend}");
        }

        [MenuItem("uPiper/Test/Create Voice Configuration")]
        public static void CreateTestVoiceConfiguration()
        {
            // PiperVoiceConfigのインスタンスを作成
            var voiceConfig = PiperVoiceConfig.FromModelPath(
                "path/to/ja_JP-test-medium.onnx",
                "path/to/ja_JP-test-medium.json"
            );

            Debug.Log("[uPiper] PiperVoiceConfig created:");
            Debug.Log($"  Voice ID: {voiceConfig.VoiceId}");
            Debug.Log($"  Display Name: {voiceConfig.DisplayName}");
            Debug.Log($"  Language: {voiceConfig.Language}");
            Debug.Log($"  Model Path: {voiceConfig.ModelPath}");
            Debug.Log($"  Config Path: {voiceConfig.ConfigPath}");
            Debug.Log($"  Sample Rate: {voiceConfig.SampleRate}");
            Debug.Log($"  Gender: {voiceConfig.Gender}");
            Debug.Log($"  Quality: {voiceConfig.Quality}");

            // 検証
            bool isValid = voiceConfig.Validate();
            Debug.Log($"  Is Valid: {isValid}");
        }

        [MenuItem("uPiper/Test/Create Audio Chunk")]
        public static void CreateTestAudioChunk()
        {
            // テスト用の音声データを作成
            int sampleRate = 22050;
            float duration = 0.5f;
            int samples = Mathf.RoundToInt(sampleRate * duration);
            float[] audioData = new float[samples];

            // サイン波を生成
            float frequency = 440f; // A4
            for (int i = 0; i < samples; i++)
            {
                audioData[i] = Mathf.Sin(2f * Mathf.PI * frequency * i / sampleRate) * 0.5f;
            }

            // AudioChunkを作成
            var chunk = new AudioChunk(
                audioData,
                sampleRate,
                1, // モノラル
                0, // 最初のチャンク
                false, // 最後のチャンクではない
                "テスト音声",
                0f
            );

            Debug.Log("[uPiper] AudioChunk created:");
            Debug.Log($"  Sample Rate: {chunk.SampleRate} Hz");
            Debug.Log($"  Channels: {chunk.Channels}");
            Debug.Log($"  Duration: {chunk.Duration:F3} seconds");
            Debug.Log($"  Chunk Index: {chunk.ChunkIndex}");
            Debug.Log($"  Is Final: {chunk.IsFinal}");
            Debug.Log($"  Text Segment: {chunk.TextSegment}");

            // AudioClipに変換
            var audioClip = chunk.ToAudioClip("TestChunk");
            Debug.Log($"  AudioClip Name: {audioClip.name}");
            Debug.Log($"  AudioClip Length: {audioClip.length:F3} seconds");
        }

        [MenuItem("uPiper/Test/Test Cache Statistics")]
        public static void TestCacheStatistics()
        {
            // CacheStatisticsのインスタンスを作成
            var stats = new CacheStatistics
            {
                EntryCount = 150,
                TotalSizeBytes = 15 * 1024 * 1024, // 15MB
                MaxSizeBytes = 100 * 1024 * 1024,   // 100MB
                HitCount = 850,
                MissCount = 150,
                EvictionCount = 25
            };

            Debug.Log("[uPiper] CacheStatistics test:");
            stats.LogStatistics();

            // ヒットを記録
            stats.RecordHit();
            stats.RecordHit();
            stats.RecordMiss();

            Debug.Log($"\n[uPiper] After recording 2 hits and 1 miss:");
            Debug.Log(stats.ToString());
        }

        [MenuItem("uPiper/Test/Test Exception Hierarchy")]
        public static void TestExceptionHierarchy()
        {
            Debug.Log("[uPiper] Testing exception hierarchy:");

            // 各種例外を作成してテスト
            var exceptions = new PiperException[]
            {
                new PiperInitializationException("Failed to initialize TTS engine"),
                new PiperModelLoadException("/path/to/model.onnx", "Model file not found"),
                new PiperInferenceException("Inference failed due to invalid input"),
                new PiperPhonemizationException("こんにちは", "ja", "Failed to phonemize Japanese text"),
                new PiperConfigurationException("Invalid sample rate specified"),
                new PiperPlatformNotSupportedException("WebGL"),
                new PiperTimeoutException(30000, "Model loading")
            };

            foreach (var ex in exceptions)
            {
                Debug.Log($"  {ex.GetType().Name}: {ex.Message} (Code: {ex.ErrorCode})");
            }
        }
    }
}