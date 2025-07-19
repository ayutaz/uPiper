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

        /// <summary>
        /// 音素配列をID配列にエンコードする
        /// </summary>
        /// <param name="phonemes">音素配列</param>
        /// <returns>ID配列</returns>
        public int[] Encode(string[] phonemes)
        {
            if (phonemes == null || phonemes.Length == 0)
            {
                PiperLogger.LogWarning("Empty phoneme array provided, returning minimal sequence");
                return new[] { GetBosId(), GetEosId() };
            }

            var ids = new List<int>();
            
            // BOS (Beginning of Sequence) トークンを追加
            ids.Add(GetBosId());

            // 各音素をIDに変換
            foreach (var phoneme in phonemes)
            {
                if (string.IsNullOrEmpty(phoneme))
                    continue;

                if (_phonemeToId.TryGetValue(phoneme, out var id))
                {
                    ids.Add(id);
                }
                else
                {
                    PiperLogger.LogWarning($"Unknown phoneme: {phoneme}, using PAD token");
                    ids.Add(GetPadId());
                }
            }

            // EOS (End of Sequence) トークンを追加
            ids.Add(GetEosId());

            PiperLogger.LogDebug($"Encoded {phonemes.Length} phonemes to {ids.Count} IDs");
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

            int nextId = 3; // 特殊トークンの後から開始
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