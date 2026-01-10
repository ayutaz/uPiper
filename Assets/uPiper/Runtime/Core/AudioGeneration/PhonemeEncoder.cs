using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using uPiper.Core.Logging;

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
        /// PhonemeIdsに対応する展開済みProsody A1配列
        /// </summary>
        public int[] ExpandedProsodyA1 { get; set; }

        /// <summary>
        /// PhonemeIdsに対応する展開済みProsody A2配列
        /// </summary>
        public int[] ExpandedProsodyA2 { get; set; }

        /// <summary>
        /// PhonemeIdsに対応する展開済みProsody A3配列
        /// </summary>
        public int[] ExpandedProsodyA3 { get; set; }
    }

    /// <summary>
    /// 音素をモデル入力用のIDに変換するクラス
    /// </summary>
    public class PhonemeEncoder
    {
        private readonly Dictionary<string, int> _phonemeToId;
        private readonly Dictionary<int, string> _idToPhoneme;
        private readonly PiperVoiceConfig _config;

        // 特殊トークン
        private const string PAD_TOKEN = "_";
        private const string BOS_TOKEN = "^";
        private const string EOS_TOKEN = "$";
        private const int DEFAULT_PAD_ID = 0;
        private const int DEFAULT_BOS_ID = 1;
        private const int DEFAULT_EOS_ID = 2;

        /// <summary>
        /// 音素エンコーダーを初期化する
        /// </summary>
        /// <param name="config">音声設定</param>
        public PhonemeEncoder(PiperVoiceConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _phonemeToId = new Dictionary<string, int>();
            _idToPhoneme = new Dictionary<int, string>();

            InitializePhonemeMapping();

            // IPAモデルかどうかを判定（phoneme_id_mapにIPA文字 "ɕ" が含まれているか）
            _useIpaMapping = _phonemeToId.ContainsKey("ɕ");

            // デバッグ: PhonemeIdMapの状態を出力
            PiperLogger.LogInfo($"[PhonemeEncoder] PhonemeIdMap count: {_config.PhonemeIdMap?.Count ?? 0}");
            PiperLogger.LogInfo($"[PhonemeEncoder] _phonemeToId count: {_phonemeToId.Count}");
            PiperLogger.LogInfo($"[PhonemeEncoder] _useIpaMapping: {_useIpaMapping}");

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

        // Multi-character phonemes to PUA mapping (for PUA-based models like ja_JP-test-medium)
        private static readonly Dictionary<string, string> multiCharPhonemeMap = new()
        {
            // Long vowels
            ["a:"] = "\ue000",
            ["i:"] = "\ue001",
            ["u:"] = "\ue002",
            ["e:"] = "\ue003",
            ["o:"] = "\ue004",
            // Special consonants
            ["cl"] = "\ue005",
            // Palatalized consonants
            ["ky"] = "\ue006",
            ["kw"] = "\ue007",
            ["gy"] = "\ue008",
            ["gw"] = "\ue009",
            ["ty"] = "\ue00a",
            ["dy"] = "\ue00b",
            ["py"] = "\ue00c",
            ["by"] = "\ue00d",
            ["ch"] = "\ue00e",  // ID 39 in PUA models (different from ty)
            ["ts"] = "\ue00f",
            ["sh"] = "\ue010",
            ["zy"] = "\ue011",
            ["hy"] = "\ue012",
            ["ny"] = "\ue013",
            ["my"] = "\ue014",
            ["ry"] = "\ue015"
        };

        // Multi-character phonemes to IPA mapping (for IPA-based models like tsukuyomi-chan)
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
            ["cl"] = "q",   // 促音 maps to q (ID 24) in tsukuyomi-chan (glottal stop)
            // Long vowels (tsukuyomi-chan uses ɯ for う)
            ["u:"] = "ɯ"
        };

        // PUA to original phoneme reverse mapping
        // OpenJTalkToPiperMapping outputs PUA characters, but IPA models need original phonemes first
        private static readonly Dictionary<string, string> puaToPhonemeMap = new()
        {
            ["\ue000"] = "a:",
            ["\ue001"] = "i:",
            ["\ue002"] = "u:",
            ["\ue003"] = "e:",
            ["\ue004"] = "o:",
            ["\ue005"] = "cl",
            ["\ue006"] = "ky",
            ["\ue007"] = "kw",
            ["\ue008"] = "gy",
            ["\ue009"] = "gw",
            ["\ue00a"] = "ty",  // ty maps to 0xE00A
            ["\ue00b"] = "dy",
            ["\ue00c"] = "py",
            ["\ue00d"] = "by",
            ["\ue00e"] = "ch",  // ch maps to 0xE00E (ち、ちゃ sounds)
            ["\ue00f"] = "ts",
            ["\ue010"] = "sh",
            ["\ue011"] = "zy",
            ["\ue012"] = "hy",
            ["\ue013"] = "ny",
            ["\ue014"] = "my",
            ["\ue015"] = "ry",
            // N phoneme variants (piper-plus Issue #207)
            // Map to ASCII "N" (ID 22), NOT IPA "ɴ" (ID 20)
            ["\ue019"] = "N",  // N_m
            ["\ue01a"] = "N",  // N_n
            ["\ue01b"] = "N",  // N_ng
            ["\ue01c"] = "N"   // N_uvular
        };

        // フィールド: IPAモデルかどうかを初期化時に判定
        private readonly bool _useIpaMapping;

        /// <summary>
        /// 音素配列をID配列にエンコードする
        /// </summary>
        /// <param name="phonemes">音素配列</param>
        /// <returns>ID配列</returns>
        public int[] Encode(string[] phonemes)
        {
            var result = EncodeWithProsody(phonemes, null, null, null);
            return result.PhonemeIds;
        }

        /// <summary>
        /// 音素配列をID配列にエンコードし、Prosody配列も同時に展開する
        /// </summary>
        /// <param name="phonemes">音素配列</param>
        /// <param name="prosodyA1">元のProsody A1配列（音素ごと）</param>
        /// <param name="prosodyA2">元のProsody A2配列（音素ごと）</param>
        /// <param name="prosodyA3">元のProsody A3配列（音素ごと）</param>
        /// <returns>エンコード結果（音素IDと展開済みProsody配列）</returns>
        public ProsodyEncodingResult EncodeWithProsody(string[] phonemes, int[] prosodyA1, int[] prosodyA2, int[] prosodyA3)
        {
            if (phonemes == null || phonemes.Length == 0)
            {
                PiperLogger.LogWarning("Empty phoneme array provided for encoding");
                return new ProsodyEncodingResult
                {
                    PhonemeIds = Array.Empty<int>(),
                    ExpandedProsodyA1 = Array.Empty<int>(),
                    ExpandedProsodyA2 = Array.Empty<int>(),
                    ExpandedProsodyA3 = Array.Empty<int>()
                };
            }

            var ids = new List<int>();
            var expandedA1 = new List<int>();
            var expandedA2 = new List<int>();
            var expandedA3 = new List<int>();

            var isESpeakModel = IsESpeakModel();
            var hasProsody = prosodyA1 != null || prosodyA2 != null || prosodyA3 != null;

            // BOSトークン(^)を常に追加
            AddToken("^", ids, expandedA1, expandedA2, expandedA3, 0, 0, 0, "BOS");

            // 各音素をIDに変換
            var prosodyIndex = 0;
            foreach (var phoneme in phonemes)
            {
                if (string.IsNullOrEmpty(phoneme))
                    continue;

                var phonemeToLookup = MapPhoneme(phoneme);

                // 現在の音素のProsody値を取得
                var a1 = prosodyA1 != null && prosodyIndex < prosodyA1.Length ? prosodyA1[prosodyIndex] : 0;
                var a2 = prosodyA2 != null && prosodyIndex < prosodyA2.Length ? prosodyA2[prosodyIndex] : 0;
                var a3 = prosodyA3 != null && prosodyIndex < prosodyA3.Length ? prosodyA3[prosodyIndex] : 0;

                // "ts"の特殊処理
                if (phonemeToLookup == "ts" && !_phonemeToId.ContainsKey("ts"))
                {
                    EncodePhonemeTs(ids, expandedA1, expandedA2, expandedA3, a1, a2, a3, isESpeakModel, hasProsody);
                }
                else if (_phonemeToId.TryGetValue(phonemeToLookup, out var id))
                {
                    ids.Add(id);
                    expandedA1.Add(a1);
                    expandedA2.Add(a2);
                    expandedA3.Add(a3);

                    if (hasProsody)
                    {
                        PiperLogger.LogDebug($"Phoneme '{phoneme}' -> ID {id}, prosody=({a1},{a2},{a3})");
                    }
                    else
                    {
                        PiperLogger.LogDebug($"Phoneme '{phoneme}' -> ID {id}");
                    }

                    // eSpeak方式では各音素の後にPADを追加
                    if (isESpeakModel)
                    {
                        AddPadToken(ids, expandedA1, expandedA2, expandedA3);
                    }
                }
                else
                {
                    LogUnknownPhoneme(phoneme, phonemeToLookup);
                }

                prosodyIndex++;
            }

            // EOSトークン($)を常に追加
            AddToken("$", ids, expandedA1, expandedA2, expandedA3, 0, 0, 0, "EOS");

            // 空の結果になった場合
            if (ids.Count == 0)
            {
                ids.Add(GetPadId());
                expandedA1.Add(0);
                expandedA2.Add(0);
                expandedA3.Add(0);
            }

            var modelType = !isESpeakModel ? "Japanese/OpenJTalk" : "eSpeak";
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
                ExpandedProsodyA1 = expandedA1.ToArray(),
                ExpandedProsodyA2 = expandedA2.ToArray(),
                ExpandedProsodyA3 = expandedA3.ToArray()
            };
        }

        /// <summary>
        /// モデルがeSpeak方式かどうかを判定
        /// </summary>
        private bool IsESpeakModel()
        {
            return !string.IsNullOrEmpty(_config.PhonemeType)
                ? _config.PhonemeType.Equals("espeak", StringComparison.OrdinalIgnoreCase)
                : !(_config.VoiceId != null && _config.VoiceId.Contains("ja_JP"));
        }

        /// <summary>
        /// 音素をモデルのIDマップに適した形式にマッピング
        /// </summary>
        private string MapPhoneme(string phoneme)
        {
            if (_useIpaMapping)
            {
                // For IPA models: First convert PUA back to original phoneme, then to IPA
                var phonemeToConvert = phoneme;
                if (puaToPhonemeMap.TryGetValue(phoneme, out var originalPhoneme))
                {
                    phonemeToConvert = originalPhoneme;
                    PiperLogger.LogDebug($"Reversed PUA U+{((int)phoneme[0]):X4} to original phoneme '{originalPhoneme}'");
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
            else if (multiCharPhonemeMap.TryGetValue(phoneme, out var puaChar))
            {
                var puaCode = ((int)puaChar[0]).ToString("X4", System.Globalization.CultureInfo.InvariantCulture);
                PiperLogger.LogDebug($"Mapped multi-char phoneme '{phoneme}' to PUA U+{puaCode}");
                return puaChar;
            }

            return phoneme;
        }

        /// <summary>
        /// 特殊トークン（BOS/EOS）を追加
        /// </summary>
        private void AddToken(string token, List<int> ids, List<int> a1, List<int> a2, List<int> a3,
            int prosodyA1, int prosodyA2, int prosodyA3, string tokenName)
        {
            if (_phonemeToId.TryGetValue(token, out var tokenId))
            {
                ids.Add(tokenId);
                a1.Add(prosodyA1);
                a2.Add(prosodyA2);
                a3.Add(prosodyA3);
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
        private void AddPadToken(List<int> ids, List<int> a1, List<int> a2, List<int> a3)
        {
            if (_phonemeToId.TryGetValue("_", out var padId))
            {
                ids.Add(padId);
                a1.Add(0);
                a2.Add(0);
                a3.Add(0);
            }
        }

        /// <summary>
        /// "ts"音素を"t"+"s"に分割してエンコード
        /// </summary>
        private void EncodePhonemeTs(List<int> ids, List<int> a1, List<int> a2, List<int> a3,
            int prosodyA1, int prosodyA2, int prosodyA3, bool isESpeakModel, bool hasProsody)
        {
            if (_phonemeToId.TryGetValue("t", out var tId) && _phonemeToId.TryGetValue("s", out var sId))
            {
                ids.Add(tId);
                a1.Add(prosodyA1);
                a2.Add(prosodyA2);
                a3.Add(prosodyA3);

                if (hasProsody)
                {
                    PiperLogger.LogDebug($"Split 'ts' -> 't' ID {tId}, prosody=({prosodyA1},{prosodyA2},{prosodyA3})");
                }
                else
                {
                    PiperLogger.LogDebug($"Split 'ts' -> 't' ID {tId}");
                }

                if (isESpeakModel)
                {
                    AddPadToken(ids, a1, a2, a3);
                }

                ids.Add(sId);
                a1.Add(prosodyA1);
                a2.Add(prosodyA2);
                a3.Add(prosodyA3);

                if (hasProsody)
                {
                    PiperLogger.LogDebug($"Split 'ts' -> 's' ID {sId}, prosody=({prosodyA1},{prosodyA2},{prosodyA3})");
                }
                else
                {
                    PiperLogger.LogDebug($"Split 'ts' -> 's' ID {sId}");
                }

                if (isESpeakModel)
                {
                    AddPadToken(ids, a1, a2, a3);
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
        /// ID配列を音素配列にデコードする
        /// </summary>
        /// <param name="ids">ID配列</param>
        /// <returns>音素配列</returns>
        public string[] Decode(int[] ids)
        {
            if (ids == null || ids.Length == 0)
                return Array.Empty<string>();

            var phonemes = new List<string>();

            foreach (var id in ids)
            {
                if (_idToPhoneme.TryGetValue(id, out var phoneme))
                {
                    // 特殊トークンはスキップ
                    if (phoneme != PAD_TOKEN && phoneme != BOS_TOKEN && phoneme != EOS_TOKEN)
                    {
                        phonemes.Add(phoneme);
                    }
                }
                else
                {
                    PiperLogger.LogWarning($"Unknown ID: {id}");
                }
            }

            return phonemes.ToArray();
        }

        /// <summary>
        /// 音素が登録されているかチェック
        /// </summary>
        /// <param name="phoneme">音素</param>
        /// <returns>登録されている場合true</returns>
        public bool ContainsPhoneme(string phoneme)
        {
            return _phonemeToId.ContainsKey(phoneme);
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

            _idToPhoneme[DEFAULT_PAD_ID] = PAD_TOKEN;
            _idToPhoneme[DEFAULT_BOS_ID] = BOS_TOKEN;
            _idToPhoneme[DEFAULT_EOS_ID] = EOS_TOKEN;

            // 設定から音素マッピングを読み込む
            if (_config.PhonemeIdMap != null)
            {
                foreach (var kvp in _config.PhonemeIdMap)
                {
                    var phoneme = kvp.Key;
                    var id = kvp.Value;

                    if (!_phonemeToId.ContainsKey(phoneme))
                    {
                        _phonemeToId[phoneme] = id;
                        _idToPhoneme[id] = phoneme;
                    }
                }

                PiperLogger.LogDebug($"Loaded {_phonemeToId.Count} phoneme mappings from config");
            }
            else
            {
                // デフォルトの音素マッピングを作成
                LoadDefaultPhonemeMapping();
            }
        }

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
                    _idToPhoneme[nextId] = phoneme;
                    nextId++;
                }
            }

            PiperLogger.LogDebug($"Loaded {_phonemeToId.Count} default phoneme mappings");
        }

        private int GetPadId() => _phonemeToId.GetValueOrDefault(PAD_TOKEN, DEFAULT_PAD_ID);
        private int GetBosId() => _phonemeToId.GetValueOrDefault(BOS_TOKEN, DEFAULT_BOS_ID);
        private int GetEosId() => _phonemeToId.GetValueOrDefault(EOS_TOKEN, DEFAULT_EOS_ID);
    }
}