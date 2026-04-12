using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.InferenceEngine;
using uPiper.Core;
using uPiper.Core.AudioGeneration;

namespace uPiper.Tests.Editor.AudioGeneration
{
    /// <summary>
    /// IInferenceAudioGenerator のテスト用スタブ実装。
    /// 設定可能な固定audio dataを返し、呼び出し回数・引数を記録する。
    /// </summary>
    internal sealed class StubInferenceAudioGenerator : IInferenceAudioGenerator, IModelCapabilities
    {
        public bool IsInitialized { get; set; } = true;
        public IModelCapabilities Capabilities => this;
        public int SampleRate { get; set; } = 22050;
        public bool SupportsProsody { get; set; } = false;
        public bool SupportsMultiSpeaker { get; set; } = false;
        public bool SupportsLanguageId { get; set; } = false;
        public bool SupportsDurations { get; set; } = false;

        /// <summary>
        /// GenerateAudioAsync が返す固定 durations データ（managed配列）。
        /// null の場合は durations なし（InferenceOutput.HasDurations == false）。
        /// 呼び出しごとに新しい NativeArray にコピーして返す。
        /// </summary>
        public float[] DurationsToReturn { get; set; }

        /// <summary>
        /// GenerateAudioAsync が返す固定データ（managed配列）。
        /// null の場合はデフォルト（0.1f x 100 サンプル）。
        /// 呼び出しごとに新しい NativeArray にコピーして返す。
        /// </summary>
        public float[] AudioDataToReturn { get; set; }

        /// <summary>GenerateAudioAsync の呼び出し回数</summary>
        public int GenerateCallCount { get; private set; }

        /// <summary>最後の GenerateAudioAsync 呼び出し時の phonemeIds</summary>
        public int[] LastPhonemeIds { get; private set; }

        /// <summary>最後の GenerateAudioAsync 呼び出し時の prosodyFlat</summary>
        public int[] LastProsodyFlat { get; private set; }

        /// <summary>最後の GenerateAudioAsync 呼び出し時の lengthScale</summary>
        public float LastLengthScale { get; private set; }

        /// <summary>最後の GenerateAudioAsync 呼び出し時の noiseScale</summary>
        public float LastNoiseScale { get; private set; }

        /// <summary>最後の GenerateAudioAsync 呼び出し時の noiseW</summary>
        public float LastNoiseW { get; private set; }

        /// <summary>最後の GenerateAudioAsync 呼び出し時の speakerId</summary>
        public int LastSpeakerId { get; private set; }

        /// <summary>最後の GenerateAudioAsync 呼び出し時の languageId</summary>
        public int LastLanguageId { get; private set; }

        /// <summary>最後の GenerateAudioAsync 呼び出しで返された durations の有無</summary>
        public bool LastHadDurations { get; private set; }

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

        public Task<InferenceOutput> GenerateAudioAsync(
            int[] phonemeIds,
            int[] prosodyFlat = null,
            float lengthScale = 1.0f, float noiseScale = 0.667f, float noiseW = 0.8f,
            int speakerId = 0, int languageId = 0,
            CancellationToken cancellationToken = default)
        {
            GenerateCallCount++;
            LastPhonemeIds = phonemeIds;
            LastProsodyFlat = prosodyFlat;
            LastLengthScale = lengthScale;
            LastNoiseScale = noiseScale;
            LastNoiseW = noiseW;
            LastSpeakerId = speakerId;
            LastLanguageId = languageId;

            // Audio
            var audioSource = AudioDataToReturn ?? CreateDefaultAudioData();
            var audioNative = new NativeArray<float>(audioSource.Length, Allocator.Persistent);
            audioNative.CopyFrom(audioSource);

            // Durations
            NativeArray<float> durationsNative = default;
            if (DurationsToReturn != null)
            {
                durationsNative = new NativeArray<float>(
                    DurationsToReturn.Length, Allocator.Persistent);
                durationsNative.CopyFrom(DurationsToReturn);
                LastHadDurations = true;
            }
            else
            {
                LastHadDurations = false;
            }

            var output = new InferenceOutput(audioNative, durationsNative);
            return Task.FromResult(output);
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