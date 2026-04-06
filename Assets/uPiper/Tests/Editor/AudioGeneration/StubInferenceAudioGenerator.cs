using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.InferenceEngine;
using uPiper.Core;
using uPiper.Core.AudioGeneration;

namespace uPiper.Tests.Editor.AudioGeneration
{
    /// <summary>
    /// IInferenceAudioGenerator のテスト用スタブ実装。
    /// 設定可能な固定audio dataを返し、呼び出し回数・引数を記録する。
    /// </summary>
    internal sealed class StubInferenceAudioGenerator : IInferenceAudioGenerator
    {
        public bool IsInitialized { get; set; } = true;
        public int SampleRate { get; set; } = 22050;
        public bool SupportsProsody { get; set; } = false;
        public bool SupportsMultiSpeaker { get; set; } = false;
        public bool SupportsLanguageId { get; set; } = false;

        /// <summary>
        /// GenerateAudioAsync が返す固定データ。
        /// null の場合はデフォルト（0.1f x 100 サンプル）。
        /// </summary>
        public float[] AudioDataToReturn { get; set; }

        /// <summary>GenerateAudioAsync の呼び出し回数</summary>
        public int GenerateCallCount { get; private set; }

        /// <summary>最後の GenerateAudioAsync 呼び出し時の phonemeIds</summary>
        public int[] LastPhonemeIds { get; private set; }

        /// <summary>最後の GenerateAudioAsync 呼び出し時の prosodyA1</summary>
        public int[] LastProsodyA1 { get; private set; }

        public Task InitializeAsync(ModelAsset modelAsset, PiperVoiceConfig config,
            CancellationToken cancellationToken = default)
        {
            IsInitialized = true;
            return Task.CompletedTask;
        }

        public Task InitializeAsync(ModelAsset modelAsset, PiperVoiceConfig config,
            PiperConfig piperConfig, CancellationToken cancellationToken = default)
        {
            IsInitialized = true;
            return Task.CompletedTask;
        }

        public Task<float[]> GenerateAudioAsync(
            int[] phonemeIds,
            int[] prosodyA1 = null, int[] prosodyA2 = null, int[] prosodyA3 = null,
            float lengthScale = 1.0f, float noiseScale = 0.667f, float noiseW = 0.8f,
            int speakerId = 0, int languageId = 0,
            CancellationToken cancellationToken = default)
        {
            GenerateCallCount++;
            LastPhonemeIds = phonemeIds;
            LastProsodyA1 = prosodyA1;

            var data = AudioDataToReturn ?? CreateDefaultAudioData();
            return Task.FromResult(data);
        }

        private static float[] CreateDefaultAudioData()
        {
            var data = new float[100];
            for (var i = 0; i < data.Length; i++)
                data[i] = 0.1f;
            return data;
        }

        public void Dispose() { }
    }
}