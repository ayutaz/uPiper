using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;
using uPiper.Core.Logging;
using uPiper.Core.Phonemizers.Multilingual;

namespace uPiper.Core.AudioGeneration
{
    /// <summary>
    /// 音素列→AudioClip変換パイプラインを一元管理する。
    /// PhonemeEncoder（エンコード）・IInferenceAudioGenerator（推論）・
    /// ISplitInferenceOrchestrator（句分割）・AudioClipBuilder（AudioClip構築）を組み合わせる。
    /// PiperTTS.Inference.csの2メソッドにあった重複ロジックを排除する。
    /// </summary>
    internal sealed class TTSSynthesisOrchestrator
    {
        private readonly IInferenceAudioGenerator _generator;
        private readonly ISplitInferenceOrchestrator _splitOrchestrator;
        private readonly PhonemeEncoder _phonemeEncoder;
        private readonly AudioClipBuilder _audioClipBuilder;
        private readonly IPiperConfigReadOnly _config;
        private readonly PiperVoiceConfig _voiceConfig;
        private readonly AudioSynthesisCache _audioCache;
        private readonly PuaTokenMapper _puaTokenMapper;

        /// <summary>
        /// Initializes the orchestrator with the specified dependencies.
        /// </summary>
        /// <param name="generator">ONNX inference audio generator (required).</param>
        /// <param name="splitOrchestrator">Silence-based split inference orchestrator (required).</param>
        /// <param name="phonemeEncoder">Phoneme-to-ID encoder (required).</param>
        /// <param name="audioClipBuilder">Audio clip builder (required).</param>
        /// <param name="config">Optional validated config. When null, defaults to:
        /// audio normalization ON (0.95 peak), silence-based splitting OFF.
        /// This allows SynthesizeAsync to work without a PiperConfig when only basic
        /// synthesis is needed.</param>
        /// <param name="voiceConfig">Voice config with phoneme ID map (optional, required for silence split).</param>
        /// <param name="audioCache">Optional audio synthesis cache. When non-null, inference results
        /// are cached and reused for identical phonemeIds + parameters.</param>
        /// <param name="puaTokenMapper">Optional PUA token mapper for timing calculation.
        /// When non-null, TimingCalculator uses it to reverse-map PUA characters to
        /// human-readable phoneme strings. When null, timing entries use raw ID fallback.</param>
        public TTSSynthesisOrchestrator(
            IInferenceAudioGenerator generator,
            ISplitInferenceOrchestrator splitOrchestrator,
            PhonemeEncoder phonemeEncoder,
            AudioClipBuilder audioClipBuilder,
            IPiperConfigReadOnly config,
            PiperVoiceConfig voiceConfig,
            AudioSynthesisCache audioCache = null,
            PuaTokenMapper puaTokenMapper = null)
        {
            _generator = generator ?? throw new ArgumentNullException(nameof(generator));
            _splitOrchestrator = splitOrchestrator ?? throw new ArgumentNullException(nameof(splitOrchestrator));
            _phonemeEncoder = phonemeEncoder ?? throw new ArgumentNullException(nameof(phonemeEncoder));
            _audioClipBuilder = audioClipBuilder ?? throw new ArgumentNullException(nameof(audioClipBuilder));
            _config = config; // nullable: defaults to normalization ON, silence split OFF
            _voiceConfig = voiceConfig;
            _audioCache = audioCache;
            _puaTokenMapper = puaTokenMapper;
        }

        /// <summary>
        /// CoreSynthesisResult: 内部共通コアメソッドの戻り値。
        /// AudioClip と（オプションの）タイミング情報を集約する。
        /// </summary>
        private readonly struct CoreSynthesisResult
        {
            public readonly AudioClip Clip;
            public readonly List<PhonemeTimingEntry> Timings;
            public readonly bool HasTimings;

            public CoreSynthesisResult(AudioClip clip, List<PhonemeTimingEntry> timings)
            {
                Clip = clip;
                Timings = timings;
                HasTimings = timings != null;
            }
        }

        /// <summary>
        /// 音素列からAudioClipを生成する。
        /// request.HasProsodyがfalseの場合はProsodyなしでエンコードする。
        /// NativeArrayの最終Dispose地点。AudioClip.SetData完了後にDisposeする。
        /// </summary>
        /// <param name="request">音声合成リクエスト（音素・Prosody・合成パラメータを集約）</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <remarks>Must be called from the main thread.</remarks>
        public async Task<AudioClip> SynthesizeAsync(
            SynthesisRequest request,
            CancellationToken cancellationToken = default)
        {
            var result = await SynthesizeWithTimingCoreAsync(request, cancellationToken);
            return result.Clip;
        }

        /// <summary>
        /// タイミング情報付きで音声を合成する。
        /// 内部コアメソッドを呼び出し、<see cref="SynthesisWithTimingResult"/> にラップして返す。
        /// </summary>
        /// <param name="request">音声合成リクエスト</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <remarks>Must be called from the main thread.</remarks>
        internal async Task<SynthesisWithTimingResult> SynthesizeWithTimingAsync(
            SynthesisRequest request,
            CancellationToken cancellationToken = default)
        {
            var result = await SynthesizeWithTimingCoreAsync(request, cancellationToken);
            return new SynthesisWithTimingResult(
                result.Clip, result.Timings,
                result.Clip != null ? result.Clip.length : 0f);
        }

        /// <summary>
        /// 共通コア: 音素列からAudioClipとタイミング情報を生成する。
        /// SynthesizeAsync / SynthesizeWithTimingAsync の両方から呼び出される。
        /// </summary>
        private async Task<CoreSynthesisResult> SynthesizeWithTimingCoreAsync(
            SynthesisRequest request,
            CancellationToken cancellationToken)
        {
            if (request.Phonemes == null || request.Phonemes.Length == 0)
                throw new ArgumentException("Phonemes cannot be null or empty.");

            // 1. 音素をIDにエンコード（Prosodyあり/なし）
            int[] phonemeIds;
            int[] expandedProsodyFlat = null;

            if (request.HasProsody)
            {
                var encResult = _phonemeEncoder.EncodeWithProsody(
                    request.Phonemes, request.ProsodyFlat);
                phonemeIds = encResult.PhonemeIds;
                expandedProsodyFlat = encResult.ExpandedProsodyFlat;
            }
            else
            {
                phonemeIds = _phonemeEncoder.Encode(request.Phonemes);
            }

            // 2. キャッシュチェック（エンコード後のphonemeIds + パラメータでキーを生成）
            long cacheKey = 0;
            if (_audioCache != null)
            {
                cacheKey = AudioSynthesisCache.GenerateKey(
                    phonemeIds, expandedProsodyFlat,
                    request.LengthScale, request.NoiseScale, request.NoiseW,
                    request.SpeakerId, request.LanguageId);

                if (_audioCache.TryGet(
                    cacheKey, out var cachedSamples,
                    out var cachedSampleRate, out var cachedTimings))
                {
                    PiperLogger.LogInfo(
                        "[TTSSynthesisOrchestrator] Cache hit — skipping inference ({0} samples)",
                        cachedSamples.Length);
                    var tempArray = new NativeArray<float>(cachedSamples, Allocator.Temp);
                    try
                    {
                        var cachedClip = _audioClipBuilder.BuildAudioClip(
                            tempArray, cachedSampleRate,
                            $"TTS_{Guid.NewGuid():N}");
                        var cachedTimingList = cachedTimings != null
                            ? new List<PhonemeTimingEntry>(cachedTimings)
                            : null;
                        return new CoreSynthesisResult(cachedClip, cachedTimingList);
                    }
                    finally
                    {
                        tempArray.Dispose();
                    }
                }
            }

            // 3. 音声を生成（句分割あり/なし）
            var silenceParsed = _config?.Silence.ParsedPhonemeSilence;
            var useSilenceSplit = _config != null
                && _config.Silence.EnablePhonemeSilence
                && silenceParsed?.Count > 0
                && _voiceConfig?.PhonemeIdMap != null;

            InferenceOutput output = null;
            try
            {
                if (useSilenceSplit)
                {
                    output = await _splitOrchestrator.GenerateWithSilenceSplitAsync(
                        phonemeIds, expandedProsodyFlat,
                        silenceParsed,
                        _voiceConfig.PhonemeIdMap,
                        _generator.Capabilities.SampleRate,
                        request.LengthScale, request.NoiseScale, request.NoiseW,
                        request.SpeakerId, request.LanguageId,
                        cancellationToken: cancellationToken);
                }
                else
                {
                    output = await _generator.GenerateAudioAsync(
                        phonemeIds, expandedProsodyFlat,
                        request.LengthScale, request.NoiseScale, request.NoiseW,
                        request.SpeakerId, request.LanguageId,
                        cancellationToken);
                }

                // 4. 正規化してAudioClipを構築（設定で無効化可能、後方互換のためnull時は正規化）
                if (_config == null || _config.Audio.NormalizeAudio)
                {
                    AudioNormalizer.NormalizeInPlace(output.Audio, 0.95f);
                }

                // 5. タイミング計算
                var timings = CalculateTimings(phonemeIds, output.Durations, useSilenceSplit);

                // 6. キャッシュに保存（NativeArray Dispose前にmanaged配列へコピー）
                if (_audioCache != null && output.Audio.IsCreated)
                {
                    _audioCache.Set(
                        cacheKey, output.Audio.ToArray(),
                        _generator.Capabilities.SampleRate,
                        timings?.ToArray());
                }

                var clip = _audioClipBuilder.BuildAudioClip(
                    output.Audio,
                    _generator.Capabilities.SampleRate,
                    $"TTS_{Guid.NewGuid():N}");

                PiperLogger.LogInfo(
                    $"[TTSSynthesisOrchestrator] Synthesized {output.Audio.Length} samples " +
                    $"from {request.Phonemes.Length} phonemes");

                return new CoreSynthesisResult(clip, timings);
            }
            finally
            {
                output?.Dispose();
            }
        }

        /// <summary>
        /// 音素IDとdurationsからタイミング情報を算出するヘルパー。
        /// 句分割使用時はタイミング計算をスキップする（句分割後のdurationsは非連続のため不正確）。
        /// </summary>
        private List<PhonemeTimingEntry> CalculateTimings(
            int[] phonemeIds, NativeArray<float> durations, bool usedSilenceSplit)
        {
            if (usedSilenceSplit)
            {
                PiperLogger.LogDebug(
                    "[TTSSynthesisOrchestrator] Silence split was used — " +
                    "timing calculation skipped");
                return null;
            }

            if (!durations.IsCreated || durations.Length == 0)
                return null;

            var durationsArray = durations.ToArray();
            var timingEntries = TimingCalculator.Calculate(
                phonemeIds, durationsArray,
                _voiceConfig.PhonemeIdMap, _puaTokenMapper,
                _generator.Capabilities.SampleRate);

            PiperLogger.LogDebug(
                $"[TTSSynthesisOrchestrator] TimingCalculator: " +
                $"{timingEntries.Count} entries");
            return timingEntries;
        }
    }
}
