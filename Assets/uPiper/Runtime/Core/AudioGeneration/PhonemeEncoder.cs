using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using uPiper.Core.Logging;

namespace uPiper.Core.AudioGeneration
{
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
        }

        // Multi-character phonemes to PUA mapping (must match openjtalk_phonemize.cpp)
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
            ["ch"] = "\ue00a",  // Same as ty in ja_JP-test-medium model
            ["ts"] = "\ue00f",
            ["sh"] = "\ue010",
            ["zy"] = "\ue011",
            ["hy"] = "\ue012",
            ["ny"] = "\ue013",
            ["my"] = "\ue014",
            ["ry"] = "\ue015"
        };

        /// <summary>
        /// 音素配列をID配列にエンコードする
        /// </summary>
        /// <param name="phonemes">音素配列</param>
        /// <returns>ID配列</returns>
        public int[] Encode(string[] phonemes)
        {
            if (phonemes == null || phonemes.Length == 0)
            {
                PiperLogger.LogWarning("Empty phoneme array provided, returning empty array");
                return Array.Empty<int>();
            }

            var ids = new List<int>();

            // 日本語モデルは音素だけ、英語/中国語などのeSpeak方式は各音素の後にPADを追加
            var isJapaneseModel = _config.VoiceId != null && _config.VoiceId.Contains("ja_JP");
            var isESpeakModel = !isJapaneseModel;

            if (isESpeakModel)
            {
                // eSpeak方式: BOSトークン(^)を追加
                if (_phonemeToId.TryGetValue("^", out var bosId))
                {
                    ids.Add(bosId);
                    PiperLogger.LogDebug($"Added BOS token '^' with ID {bosId}");
                }
                else
                {
                    PiperLogger.LogWarning("BOS token '^' not found in phoneme map");
                }
            }

            // 各音素をIDに変換
            foreach (var phoneme in phonemes)
            {
                if (string.IsNullOrEmpty(phoneme))
                    continue;

                var phonemeToLookup = phoneme;

                // Multi-character phonemes need to be mapped to PUA characters first
                if (multiCharPhonemeMap.TryGetValue(phoneme, out var puaChar))
                {
                    phonemeToLookup = puaChar;
                    var puaCode = ((int)puaChar[0]).ToString("X4", System.Globalization.CultureInfo.InvariantCulture);
                    PiperLogger.LogInfo($"Mapped multi-char phoneme '{phoneme}' to PUA U+{puaCode}");
                }

                if (_phonemeToId.TryGetValue(phonemeToLookup, out var id))
                {
                    ids.Add(id);
                    
                    // eSpeak方式では各音素の後にPADを追加
                    if (isESpeakModel)
                    {
                        if (_phonemeToId.TryGetValue("_", out var padId))
                        {
                            ids.Add(padId);
                        }
                        else
                        {
                            PiperLogger.LogWarning("PAD token '_' not found in phoneme map");
                        }
                    }
                }
                else
                {
                    // 未知の音素はスキップ（PADトークンも使用しない）
                    PiperLogger.LogWarning($"Unknown phoneme: {phoneme} (mapped as: {phonemeToLookup}), skipping");
                }
            }

            // EOSトークンを追加
            if (isESpeakModel)
            {
                if (_phonemeToId.TryGetValue("$", out var eosId))
                {
                    ids.Add(eosId);
                    PiperLogger.LogDebug($"Added EOS token '$' with ID {eosId}");
                }
                else
                {
                    PiperLogger.LogWarning("EOS token '$' not found in phoneme map");
                }
            }

            // 空の結果になった場合は、無音を表すPADトークンを1つ追加
            if (ids.Count == 0)
            {
                ids.Add(GetPadId());
            }

            PiperLogger.LogDebug($"Encoded {phonemes.Length} phonemes to {ids.Count} IDs (model type: {(isJapaneseModel ? "Japanese" : "eSpeak")})");
            return ids.ToArray();
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