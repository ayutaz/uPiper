using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using uPiper.Core.Logging;

namespace uPiper.Core
{
    /// <summary>
    /// モデルJSON設定ファイル（.onnx.json）のパーサー。
    /// PiperVoiceConfigを構築する。
    /// </summary>
    internal static class ModelConfigParser
    {
        /// <summary>
        /// モデルJSON設定ファイルをパースしてPiperVoiceConfigを構築する。
        /// </summary>
        internal static PiperVoiceConfig Parse(string voiceId, string json)
        {
            var config = new PiperVoiceConfig
            {
                VoiceId = voiceId,
                DisplayName = voiceId,
                Language = "ja",
                SampleRate = 22050,
                PhonemeIdMap = new Dictionary<string, int[]>()
            };

            var jsonObj = JObject.Parse(json);

            if (jsonObj["language"]?["code"] != null)
                config.Language = jsonObj["language"]["code"].ToString();

            if (jsonObj["audio"]?["sample_rate"] != null)
                config.SampleRate = jsonObj["audio"]["sample_rate"].ToObject<int>();

            if (jsonObj["inference"]?["noise_scale"] != null)
                config.NoiseScale = jsonObj["inference"]["noise_scale"].ToObject<float>();

            if (jsonObj["inference"]?["length_scale"] != null)
                config.LengthScale = jsonObj["inference"]["length_scale"].ToObject<float>();

            if (jsonObj["inference"]?["noise_w"] != null)
                config.NoiseW = jsonObj["inference"]["noise_w"].ToObject<float>();

            if (jsonObj["phoneme_type"] != null)
                config.PhonemeType = jsonObj["phoneme_type"].ToString();
            else
                config.PhonemeType = "espeak";

            if (jsonObj["phoneme_id_map"] is JObject phonemeIdMap)
            {
                foreach (var kvp in phonemeIdMap)
                {
                    if (kvp.Value is JArray idArray && idArray.Count > 0)
                        config.PhonemeIdMap[kvp.Key] = idArray.ToObject<int[]>();
                }
            }

            if (jsonObj["num_speakers"] != null)
                config.NumSpeakers = jsonObj["num_speakers"].ToObject<int>();

            if (jsonObj["speaker_id_map"] is JObject speakerIdMap)
            {
                config.SpeakerIdMap = new Dictionary<string, int>();
                foreach (var kvp in speakerIdMap)
                    config.SpeakerIdMap[kvp.Key] = kvp.Value.ToObject<int>();
            }

            if (jsonObj["num_languages"] != null)
                config.NumLanguages = jsonObj["num_languages"].ToObject<int>();

            if (jsonObj["language_id_map"] is JObject languageIdMap)
            {
                config.LanguageIdMap = new Dictionary<string, int>();
                foreach (var kvp in languageIdMap)
                    config.LanguageIdMap[kvp.Key] = kvp.Value.ToObject<int>();
            }

            return config;
        }
    }
}