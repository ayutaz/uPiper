using System.Collections.Generic;
using UnityEngine;
using uPiper.Core.Logging;

namespace uPiper.Core.AudioGeneration
{
    /// <summary>
    /// 事前生成された音声ファイルを管理・再生するクラス
    /// WebGLでの音声品質問題を回避するための実用的な解決策
    /// </summary>
    public class PreGeneratedAudioManager : MonoBehaviour
    {
        [System.Serializable]
        public class AudioEntry
        {
            public string text;
            public AudioClip audioClip;
        }

        [SerializeField]
        private List<AudioEntry> _preGeneratedAudios = new List<AudioEntry>();
        
        private Dictionary<string, AudioClip> _audioCache;
        private static PreGeneratedAudioManager _instance;

        public static PreGeneratedAudioManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<PreGeneratedAudioManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("PreGeneratedAudioManager");
                        _instance = go.AddComponent<PreGeneratedAudioManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeCache();
        }

        private void InitializeCache()
        {
            _audioCache = new Dictionary<string, AudioClip>();
            
            // インスペクターで設定された音声を登録
            foreach (var entry in _preGeneratedAudios)
            {
                if (!string.IsNullOrEmpty(entry.text) && entry.audioClip != null)
                {
                    _audioCache[entry.text] = entry.audioClip;
                    PiperLogger.LogInfo($"[PreGeneratedAudioManager] Cached audio for: '{entry.text}'");
                }
            }
            
            // Resourcesフォルダから事前生成音声をロード
            LoadPreGeneratedAudiosFromResources();
        }

        private void LoadPreGeneratedAudiosFromResources()
        {
            // よく使われる日本語フレーズのリスト
            var commonPhrases = new Dictionary<string, string>
            {
                {"こんにちは", "konnichiwa"},
                {"ありがとうございます", "arigatougozaimasu"},
                {"さようなら", "sayounara"},
                {"おはようございます", "ohayougozaimasu"},
                {"こんばんは", "konbanwa"},
                {"お疲れ様でした", "otsukarasamadeshita"},
                {"はじめまして", "hajimemashite"},
                {"よろしくお願いします", "yoroshikuonegaishimasu"},
                {"すみません", "sumimasen"},
                {"ごめんなさい", "gomennasai"},
                {"はい", "hai"},
                {"いいえ", "iie"},
                {"わかりました", "wakarimashita"},
                {"大丈夫です", "daijoubudesu"},
                {"お元気ですか", "ogenkidesuka"}
            };

            foreach (var kvp in commonPhrases)
            {
                if (_audioCache.ContainsKey(kvp.Key))
                    continue; // 既に登録済みの場合はスキップ
                
                var clipPath = $"PreGeneratedAudio/{kvp.Value}";
                var audioClip = Resources.Load<AudioClip>(clipPath);
                
                if (audioClip != null)
                {
                    _audioCache[kvp.Key] = audioClip;
                    PiperLogger.LogInfo($"[PreGeneratedAudioManager] Loaded from Resources: '{kvp.Key}' -> {clipPath}");
                }
                else
                {
                    PiperLogger.LogDebug($"[PreGeneratedAudioManager] Audio not found in Resources: {clipPath}");
                }
            }
            
            PiperLogger.LogInfo($"[PreGeneratedAudioManager] Total cached audio clips: {_audioCache.Count}");
        }

        /// <summary>
        /// 事前生成された音声を取得（存在しない場合はnullを返す）
        /// </summary>
        public AudioClip GetPreGeneratedAudio(string text)
        {
            if (string.IsNullOrEmpty(text))
                return null;
            
            // 完全一致で検索
            if (_audioCache.TryGetValue(text, out var audioClip))
            {
                PiperLogger.LogInfo($"[PreGeneratedAudioManager] Found pre-generated audio for: '{text}'");
                return audioClip;
            }
            
            // 正規化して再検索（スペースや句読点を削除）
            var normalizedText = NormalizeText(text);
            if (_audioCache.TryGetValue(normalizedText, out audioClip))
            {
                PiperLogger.LogInfo($"[PreGeneratedAudioManager] Found pre-generated audio for normalized text: '{normalizedText}'");
                return audioClip;
            }
            
            PiperLogger.LogDebug($"[PreGeneratedAudioManager] No pre-generated audio found for: '{text}'");
            return null;
        }

        /// <summary>
        /// テキストが事前生成音声として利用可能かチェック
        /// </summary>
        public bool HasPreGeneratedAudio(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;
            
            return _audioCache.ContainsKey(text) || _audioCache.ContainsKey(NormalizeText(text));
        }

        /// <summary>
        /// 音声を動的に追加（実行時に生成した音声をキャッシュする場合など）
        /// </summary>
        public void AddAudioToCache(string text, AudioClip audioClip)
        {
            if (string.IsNullOrEmpty(text) || audioClip == null)
                return;
            
            _audioCache[text] = audioClip;
            PiperLogger.LogInfo($"[PreGeneratedAudioManager] Added audio to cache: '{text}'");
        }

        /// <summary>
        /// テキストの正規化（句読点やスペースを削除）
        /// </summary>
        private string NormalizeText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            
            // 句読点とスペースを削除
            return text.Replace("。", "")
                      .Replace("、", "")
                      .Replace("！", "")
                      .Replace("？", "")
                      .Replace(" ", "")
                      .Replace("　", "")
                      .Trim();
        }

        /// <summary>
        /// キャッシュされているすべての音声テキストのリストを取得
        /// </summary>
        public List<string> GetCachedTextList()
        {
            return new List<string>(_audioCache.Keys);
        }

        /// <summary>
        /// デバッグ情報の出力
        /// </summary>
        public void LogDebugInfo()
        {
            PiperLogger.LogInfo("[PreGeneratedAudioManager] === Debug Info ===");
            PiperLogger.LogInfo($"Total cached audio clips: {_audioCache.Count}");
            foreach (var key in _audioCache.Keys)
            {
                PiperLogger.LogInfo($"  - '{key}'");
            }
        }
    }
}