using System;
using System.Collections.Generic;
using UnityEngine;
using uPiper.Core.Logging;
using uPiper.Core.Phonemizers.Multilingual;

namespace uPiper.Core.AudioGeneration
{
    /// <summary>
    /// Prosody付きエンコーディングの結果
    /// </summary>
    public class ProsodyEncodingResult
    {
        /// <summary>
        /// エンコードされた音素ID配列
        /// </summary>
        public int[] PhonemeIds { get; set; }

        /// <summary>
        /// PhonemeIdsに対応する展開済みProsodyフラット配列 (stride=3).
        /// Layout: [a1_0, a2_0, a3_0, a1_1, a2_1, a3_1, ...].
        /// Length = PhonemeIds.Length * 3.
        /// </summary>
        public int[] ExpandedProsodyFlat { get; set; }
    }

    /// <summary>
    /// 音素をモデル入力用のIDに変換するクラス
    /// </summary>
    public class PhonemeEncoder
    {
        private readonly Dictionary<string, int> _phonemeToId;
        private readonly PiperVoiceConfig _config;
        private readonly PuaTokenMapper _tokenMapper;

        // 特殊トークン
        private const string PAD_TOKEN = "_";
        private const string BOS_TOKEN = "^";
        private const string EOS_TOKEN = "$";
        private const int DEFAULT_PAD_ID = 0;
        private const int DEFAULT_BOS_ID = 1;
        private const int DEFAULT_EOS_ID = 2;

        /// <summary>
        /// Prosody flat array stride. Each phoneme has 3 prosody values (A1, A2, A3).
        /// Use this constant when constructing ProsodyFlat arrays for SynthesisRequest.
        /// </summary>
        public const int ProsodyStride = 3;

        /// <summary>
        /// Threshold ratio of unknown phonemes to total phonemes that triggers an error.
        /// If more than this fraction of phonemes are unknown, a PiperConfigurationException is thrown.
        /// </summary>
        private const float UnknownPhonemeErrorThreshold = 0.5f;

        // EOS-like tokens: These tokens act as sentence terminators (piper-plus #210)
        // When the last phoneme is one of these, we don't add a separate EOS token
        private static readonly HashSet<string> EosLikeTokens =
            new() { "$", "?", "?!", "?.", "?~", "\ue016", "\ue017", "\ue018" };

        /// <summary>
        /// 音素エンコーダーを初期化する
        /// </summary>
        /// <param name="config">音声設定</param>
        /// <param name="tokenMapper">PUA token mapper instance</param>
        public PhonemeEncoder(PiperVoiceConfig config, PuaTokenMapper tokenMapper)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _tokenMapper = tokenMapper ?? throw new ArgumentNullException(nameof(tokenMapper));
            _phonemeToId = new Dictionary<string, int>();

            // Detect multilingual model before initializing phoneme mapping
            _isMultilingualModel = !string.IsNullOrEmpty(_config.PhonemeType)
                && _config.PhonemeType.Equals("multilingual", StringComparison.OrdinalIgnoreCase);

            InitializePhonemeMapping();

            // IPAモデルかどうかを判定（phoneme_id_mapにIPA文字 "ɕ" が含まれているか）
            // Multilingual models: skip IPA detection — phonemes are already in the model's native format
            _useIpaMapping = !_isMultilingualModel && _phonemeToId.ContainsKey("ɕ");

            // Cache PAD ID for intersperse logic
            _padId = GetPadId();

            // デバッグ: PhonemeIdMapの状態を出力
            PiperLogger.LogInfo($"[PhonemeEncoder] PhonemeIdMap count: {_config.PhonemeIdMap?.Count ?? 0}");
            PiperLogger.LogInfo($"[PhonemeEncoder] _phonemeToId count: {_phonemeToId.Count}");
            PiperLogger.LogInfo($"[PhonemeEncoder] _useIpaMapping: {_useIpaMapping}");
            PiperLogger.LogInfo($"[PhonemeEncoder] _isMultilingualModel: {_isMultilingualModel}");

            if (_useIpaMapping)
            {
                PiperLogger.LogInfo("[PhonemeEncoder] Detected IPA-based model, using IPA phoneme mapping");
                // IPA文字の存在確認
                var ipaKeys = new[] { "ɕ", "tɕ", "q", "ɯ", "ɴ" };
                foreach (var key in ipaKeys)
                {
                    var exists = _phonemeToId.ContainsKey(key);
                    PiperLogger.LogInfo($"[PhonemeEncoder] IPA key '{key}': {(exists ? "found" : "NOT FOUND")}");
                }
            }
            else if (_isMultilingualModel)
            {
                PiperLogger.LogInfo("[PhonemeEncoder] Multilingual model detected, using native phoneme format (no IPA/PUA conversion)");
            }
            else
            {
                PiperLogger.LogInfo("[PhonemeEncoder] Using PUA-based model (non-IPA)");
                // PUA文字の存在確認
                var puaKeys = new[] { "\ue00e", "\ue005", "\ue00a" };
                foreach (var key in puaKeys)
                {
                    var exists = _phonemeToId.ContainsKey(key);
                    var code = key.Length > 0 ? $"U+{((int)key[0]):X4}" : "empty";
                    PiperLogger.LogInfo($"[PhonemeEncoder] PUA key {code}: {(exists ? "found" : "NOT FOUND")}");
                }
            }
        }

        // Multi-character phonemes to IPA mapping (for IPA-based models)
        private static readonly Dictionary<string, string> multiCharToIpaMap = new()
        {
            // Palatalized consonants
            ["ky"] = "kʲ",
            ["gy"] = "ɡʲ",
            ["ty"] = "tɕ",
            ["dy"] = "dʲ",
            ["py"] = "pʲ",
            ["by"] = "bʲ",
            ["hy"] = "hʲ",
            ["ny"] = "ɲ",
            ["my"] = "mʲ",
            ["ry"] = "ɽ",
            ["zy"] = "ʑ",
            // Affricates and fricatives
            ["ch"] = "tɕ",
            ["sh"] = "ʃ",  // Use ʃ (ID 42), not ɕ (ID 18) - matches training data
            ["ts"] = "ts",  // Keep as-is (not in IPA model)
            // Special
            ["cl"] = "q",   // 促音 maps to q (glottal stop)
            // Long vowels (IPA models use ɯ for う)
            ["u:"] = "ɯ"
        };

        // フィールド: IPAモデルかどうかを初期化時に判定
        private readonly bool _useIpaMapping;

        // フィールド: 多言語モデルかどうか (PhonemeType == "multilingual")
        private readonly bool _isMultilingualModel;

        // Cached PAD ID for intersperse logic
        private readonly int _padId;

        /// <summary>
        /// 音素配列をID配列にエンコードする
        /// </summary>
        /// <param name="phonemes">音素配列</param>
        /// <returns>ID配列</returns>
        public int[] Encode(string[] phonemes)
        {
            var result = EncodeWithProsody(phonemes, null);
            return result.PhonemeIds;
        }

        /// <summary>
        /// 音素配列をID配列にエンコードし、Prosodyフラット配列も同時に展開する
        /// </summary>
        /// <param name="phonemes">音素配列</param>
        /// <param name="prosodyFlat">Prosodyフラット配列 (stride=3), or null</param>
        /// <returns>エンコード結果（音素IDと展開済みProsodyフラット配列）</returns>
        public ProsodyEncodingResult EncodeWithProsody(string[] phonemes, int[] prosodyFlat)
        {
            if (phonemes == null || phonemes.Length == 0)
            {
                PiperLogger.LogWarning("Empty phoneme array provided for encoding");
                return new ProsodyEncodingResult
                {
                    PhonemeIds = Array.Empty<int>(),
                    ExpandedProsodyFlat = Array.Empty<int>()
                };
            }

            var ids = new List<int>();
            var expandedProsody = new List<int>((phonemes.Length * 2 + 2) * ProsodyStride);

            var needsInterspersePad = NeedsInterspersePadding();
            var hasProsody = prosodyFlat != null;

            // BOSトークン(^)を常に追加
            AddToken("^", ids, expandedProsody, 0, 0, 0, "BOS");

            // PAD after BOS (required by multilingual/espeak models, matches piper-plus post_process_ids)
            if (needsInterspersePad)
            {
                AddPadToken(ids, expandedProsody);
            }

            // 各音素をIDに変換
            var phonemeIndex = 0;
            var unknownPhonemeCount = 0;
            foreach (var phoneme in phonemes)
            {
                if (string.IsNullOrEmpty(phoneme))
                {
                    phonemeIndex++;
                    continue;
                }

                var phonemeToLookup = MapPhoneme(phoneme);

                // 現在の音素のProsody値を取得
                var baseIdx = phonemeIndex * ProsodyStride;
                var a1 = prosodyFlat != null && baseIdx < prosodyFlat.Length ? prosodyFlat[baseIdx + 0] : 0;
                var a2 = prosodyFlat != null && baseIdx + 1 < prosodyFlat.Length ? prosodyFlat[baseIdx + 1] : 0;
                var a3 = prosodyFlat != null && baseIdx + 2 < prosodyFlat.Length ? prosodyFlat[baseIdx + 2] : 0;

                // "ts"の特殊処理
                if (phonemeToLookup == "ts" && !_phonemeToId.ContainsKey("ts"))
                {
                    EncodePhonemeTs(ids, expandedProsody, a1, a2, a3, needsInterspersePad, hasProsody);
                }
                else if (_phonemeToId.TryGetValue(phonemeToLookup, out var phonemeId))
                {
                    ids.Add(phonemeId);
                    expandedProsody.Add(a1);
                    expandedProsody.Add(a2);
                    expandedProsody.Add(a3);

                    if (hasProsody)
                    {
                        PiperLogger.LogDebug($"Phoneme '{phoneme}' -> ID {phonemeId}, prosody=({a1},{a2},{a3})");
                    }
                    else
                    {
                        PiperLogger.LogDebug($"Phoneme '{phoneme}' -> ID {phonemeId}");
                    }

                    // eSpeak/multilingual方式では各音素の後にPADを追加
                    // Skip if the phoneme itself is already PAD (ID 0) to prevent triple-zero sequences
                    if (needsInterspersePad && phonemeId != _padId)
                    {
                        AddPadToken(ids, expandedProsody);
                    }
                }
                else
                {
                    unknownPhonemeCount++;
                    LogUnknownPhoneme(phoneme, phonemeToLookup);
                }

                phonemeIndex++;
            }

            // Check if the last phoneme is an EOS-like token (piper-plus #210)
            // If so, we don't need to add a separate EOS token
            var lastPhoneme = phonemes.Length > 0 ? phonemes[^1] : null;
            var lastPhonemeIsEosLike = lastPhoneme != null && EosLikeTokens.Contains(lastPhoneme);

            if (!lastPhonemeIsEosLike)
            {
                // EOSトークン($)を追加（最後の音素がEOS-likeでない場合のみ）
                AddToken("$", ids, expandedProsody, 0, 0, 0, "EOS");
            }
            else
            {
                PiperLogger.LogDebug($"[PhonemeEncoder] Last phoneme '{lastPhoneme}' is EOS-like, skipping separate EOS token");
            }

            // 空の結果になった場合
            if (ids.Count == 0)
            {
                ids.Add(GetPadId());
                expandedProsody.Add(0);
                expandedProsody.Add(0);
                expandedProsody.Add(0);
            }

            // Check unknown phoneme ratio and escalate if too high
            if (unknownPhonemeCount > 0)
            {
                var unknownRatio = (float)unknownPhonemeCount / phonemes.Length;
                if (unknownRatio > UnknownPhonemeErrorThreshold)
                {
                    PiperLogger.LogError(
                        $"[PhonemeEncoder] {unknownPhonemeCount}/{phonemes.Length} phonemes " +
                        $"({unknownRatio:P0}) were unknown and skipped. " +
                        $"This indicates a phoneme mapping mismatch. " +
                        $"Model PhonemeType: {_config.PhonemeType}, IPA mode: {_useIpaMapping}");
                    throw new PiperConfigurationException(
                        $"Phoneme encoding failed: {unknownPhonemeCount} of {phonemes.Length} " +
                        $"phonemes are unknown to the model. Check that the model's phoneme_id_map " +
                        $"matches the G2P backend output.");
                }
                else
                {
                    PiperLogger.LogWarning(
                        $"[PhonemeEncoder] {unknownPhonemeCount}/{phonemes.Length} phonemes " +
                        $"were unknown and skipped. Output quality may be degraded.");
                }
            }

            var modelType = !needsInterspersePad ? "Japanese/OpenJTalk" : (_isMultilingualModel ? "Multilingual" : "eSpeak");
            if (hasProsody)
            {
                PiperLogger.LogInfo($"Encoded {phonemes.Length} phonemes with prosody to {ids.Count} IDs (model type: {modelType})");
            }
            else
            {
                PiperLogger.LogDebug($"Encoded {phonemes.Length} phonemes to {ids.Count} IDs (model type: {modelType})");
            }

            return new ProsodyEncodingResult
            {
                PhonemeIds = ids.ToArray(),
                ExpandedProsodyFlat = expandedProsody.ToArray()
            };
        }

        /// <summary>
        /// モデルがintersperse padding（音素間PAD挿入）を必要とするかを判定。
        /// espeak方式およびmultilingual方式の場合にtrue。
        /// </summary>
        private bool NeedsInterspersePadding()
        {
            if (!string.IsNullOrEmpty(_config.PhonemeType))
                return _config.PhonemeType.Equals("espeak", StringComparison.OrdinalIgnoreCase)
                    || _config.PhonemeType.Equals("multilingual", StringComparison.OrdinalIgnoreCase);
            return !(_config.VoiceId != null && _config.VoiceId.Contains("ja_JP"));
        }

        /// <summary>
        /// 音素をモデルのIDマップに適した形式にマッピング
        /// </summary>
        private string MapPhoneme(string phoneme)
        {
            // Multilingual models: phonemes are already in the model's native format
            // (PUA chars for multi-char phonemes, IPA chars for single-char phonemes).
            // However, some multi-char text tokens (e.g., N_m, N_n) may not have been
            // PUA-converted yet — use PuaTokenMapper to resolve them.
            if (_isMultilingualModel)
            {
                if (phoneme.Length > 1 &&
                    _tokenMapper.Token2Char.TryGetValue(phoneme, out var puaChar))
                {
                    return puaChar.ToString();
                }
                // NFC normalize decomposed IPA (e.g., u+\u0303 → ũ) to match phoneme_id_map
                var nfc = phoneme.Normalize(System.Text.NormalizationForm.FormC);
                if (nfc != phoneme) return nfc;
                return phoneme;
            }

            if (_useIpaMapping)
            {
                // For IPA models: First convert PUA back to original phoneme, then to IPA
                // Uses PuaTokenMapper.Char2Token for reverse lookup instead of a local dictionary.
                // N variants (N_m, N_n, N_ng, N_uvular) are collapsed to "N" for backward
                // compatibility with single-language IPA models (piper-plus Issue #207/#210).
                var phonemeToConvert = phoneme;
                if (phoneme.Length == 1 &&
                    _tokenMapper.Char2Token.TryGetValue(phoneme[0], out var originalToken))
                {
                    phonemeToConvert = originalToken switch
                    {
                        "N_m" or "N_n" or "N_ng" or "N_uvular" => "N",
                        _ => originalToken
                    };
                    PiperLogger.LogDebug($"Reversed PUA U+{(int)phoneme[0]:X4} to original phoneme '{phonemeToConvert}'");
                }

                // Now convert to IPA if applicable
                if (multiCharToIpaMap.TryGetValue(phonemeToConvert, out var ipaChar))
                {
                    PiperLogger.LogDebug($"Mapped phoneme '{phonemeToConvert}' to IPA '{ipaChar}'");
                    return ipaChar;
                }
                else if (phonemeToConvert != phoneme)
                {
                    return phonemeToConvert;
                }
            }
            else if (_tokenMapper.Token2Char.TryGetValue(phoneme, out var puaCh))
            {
                PiperLogger.LogDebug($"Mapped multi-char phoneme '{phoneme}' to PUA U+{(int)puaCh:X4}");
                return puaCh.ToString();
            }

            return phoneme;
        }

        /// <summary>
        /// 特殊トークン（BOS/EOS）を追加
        /// </summary>
        private void AddToken(string token, List<int> ids, List<int> prosody,
            int prosodyA1, int prosodyA2, int prosodyA3, string tokenName)
        {
            if (_phonemeToId.TryGetValue(token, out var tokenId))
            {
                ids.Add(tokenId);
                prosody.Add(prosodyA1);
                prosody.Add(prosodyA2);
                prosody.Add(prosodyA3);
                PiperLogger.LogDebug($"Added {tokenName} token '{token}' with ID {tokenId}");
            }
            else
            {
                PiperLogger.LogWarning($"{tokenName} token '{token}' not found in phoneme map");
            }
        }

        /// <summary>
        /// PADトークンを追加
        /// </summary>
        private void AddPadToken(List<int> ids, List<int> prosody)
        {
            if (_phonemeToId.TryGetValue("_", out var padId))
            {
                ids.Add(padId);
                prosody.Add(0);
                prosody.Add(0);
                prosody.Add(0);
            }
        }

        /// <summary>
        /// "ts"音素を"t"+"s"に分割���てエンコード
        /// </summary>
        private void EncodePhonemeTs(List<int> ids, List<int> prosody,
            int prosodyA1, int prosodyA2, int prosodyA3, bool needsInterspersePad, bool hasProsody)
        {
            if (_phonemeToId.TryGetValue("t", out var tId) && _phonemeToId.TryGetValue("s", out var sId))
            {
                ids.Add(tId);
                prosody.Add(prosodyA1);
                prosody.Add(prosodyA2);
                prosody.Add(prosodyA3);

                if (hasProsody)
                {
                    PiperLogger.LogDebug($"Split 'ts' -> 't' ID {tId}, prosody=({prosodyA1},{prosodyA2},{prosodyA3})");
                }
                else
                {
                    PiperLogger.LogDebug($"Split 'ts' -> 't' ID {tId}");
                }

                if (needsInterspersePad)
                {
                    AddPadToken(ids, prosody);
                }

                ids.Add(sId);
                prosody.Add(prosodyA1);
                prosody.Add(prosodyA2);
                prosody.Add(prosodyA3);

                if (hasProsody)
                {
                    PiperLogger.LogDebug($"Split 'ts' -> 's' ID {sId}, prosody=({prosodyA1},{prosodyA2},{prosodyA3})");
                }
                else
                {
                    PiperLogger.LogDebug($"Split 'ts' -> 's' ID {sId}");
                }

                if (needsInterspersePad)
                {
                    AddPadToken(ids, prosody);
                }
            }
            else
            {
                PiperLogger.LogWarning($"Cannot split 'ts': 't' or 's' not found in phoneme map");
            }
        }

        /// <summary>
        /// 未知の音素をログ出力
        /// </summary>
        private void LogUnknownPhoneme(string phoneme, string phonemeToLookup)
        {
            const int BmpPuaStart = 0xE000;
            const int BmpPuaEnd = 0xF8FF;
            var phonemeCode = phoneme.Length > 0 && phoneme[0] >= BmpPuaStart && phoneme[0] <= BmpPuaEnd
                ? $"PUA U+{((int)phoneme[0]):X4}"
                : $"'{phoneme}'";
            PiperLogger.LogWarning($"Unknown phoneme: {phonemeCode} (mapped as: '{phonemeToLookup}'), skipping. " +
                $"IPA mode: {_useIpaMapping}, PhonemeIdMap has {_phonemeToId.Count} entries");
        }

        /// <summary>
        /// Flatten separate A1/A2/A3 arrays into a single stride=3 array.
        /// Used at the boundary where dot-net-g2p output (3 separate arrays)
        /// meets the flat-array internal representation.
        /// </summary>
        internal static int[] FlattenProsody(int[] a1, int[] a2, int[] a3, int phonemeCount)
        {
            var flat = new int[phonemeCount * ProsodyStride];
            for (var i = 0; i < phonemeCount; i++)
            {
                flat[i * ProsodyStride + 0] = i < a1.Length ? a1[i] : 0;
                flat[i * ProsodyStride + 1] = i < a2.Length ? a2[i] : 0;
                flat[i * ProsodyStride + 2] = i < a3.Length ? a3[i] : 0;
            }
            return flat;
        }

        /// <summary>
        /// 登録されている音素の数を取得
        /// </summary>
        public int PhonemeCount => _phonemeToId.Count;

        private void InitializePhonemeMapping()
        {
            // 特殊トークンを登録
            _phonemeToId[PAD_TOKEN] = DEFAULT_PAD_ID;
            _phonemeToId[BOS_TOKEN] = DEFAULT_BOS_ID;
            _phonemeToId[EOS_TOKEN] = DEFAULT_EOS_ID;

            // 設定から音素マッピングを読み込む（ids[0]を抽出）
            // PhonemeIdMap は Dictionary<string, int[]> だが、内部の _phonemeToId は
            // Dictionary<string, int> のまま維持。複数ID展開は将来タスク（P2-1b）。
            if (_config.PhonemeIdMap != null && _config.PhonemeIdMap.Count > 0)
            {
                foreach (var kvp in _config.PhonemeIdMap)
                {
                    var phoneme = kvp.Key;
                    var ids = kvp.Value;

                    if (ids != null && ids.Length > 0 && !_phonemeToId.ContainsKey(phoneme))
                    {
                        _phonemeToId[phoneme] = ids[0];
                    }
                }

                PiperLogger.LogDebug($"Loaded {_phonemeToId.Count} phoneme mappings from config");
            }
            else
            {
                PiperLogger.LogError(
                    "[PhonemeEncoder] PhonemeIdMap is null or empty in voice config. " +
                    "The model's .onnx.json configuration file may be missing or corrupted.");
                throw new PiperConfigurationException(
                    "PhonemeIdMap is missing from voice configuration. " +
                    "Ensure the model's .onnx.json file contains a valid 'phoneme_id_map' field. " +
                    $"Voice ID: {_config.VoiceId ?? "(unknown)"}");
            }
        }

        [Obsolete("Default phoneme mapping produces low quality output. Use a proper PhonemeIdMap from model config.")]
        private void LoadDefaultPhonemeMapping()
        {
            // 基本的な音素セット（Piper TTS標準）
            var defaultPhonemes = new[]
            {
                // 母音
                "a", "e", "i", "o", "u",
                // 子音
                "b", "d", "f", "g", "h", "j", "k", "l", "m", "n",
                "p", "r", "s", "t", "v", "w", "y", "z",
                // その他の記号
                " ", ".", ",", "?", "!", "'", "-"
            };

            var nextId = 3; // 特殊トークンの後から開始
            foreach (var phoneme in defaultPhonemes)
            {
                if (!_phonemeToId.ContainsKey(phoneme))
                {
                    _phonemeToId[phoneme] = nextId;
                    nextId++;
                }
            }

            PiperLogger.LogDebug($"Loaded {_phonemeToId.Count} default phoneme mappings");
        }

        private int GetPadId() => _phonemeToId.GetValueOrDefault(PAD_TOKEN, DEFAULT_PAD_ID);
    }
}