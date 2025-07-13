using System;
using System.Linq;
using Unity.InferenceEngine;
using UnityEngine;

namespace uPiper.Runtime
{
    /// <summary>
    /// 最小限のTTSプロトタイプ
    /// 固定音素IDから音声波形を生成してAudioClipで再生する
    /// </summary>
    public class MinimalTTSPrototype : MonoBehaviour
    {
        [Header("TTS Settings")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private bool autoPlayOnStart = true;
        
        [Header("Wave Generation Settings")]
        [SerializeField] private int sampleRate = 22050;
        [SerializeField] private float frequency = 440f; // A4音
        [SerializeField] private float duration = 2f;
        
        [Header("Status")]
        [SerializeField] private string status = "Not initialized";
        
        // 固定音素ID配列（「こんにちは」を想定した仮の値）
        // 実際のPiper TTSでは、これらのIDは音素化エンジンから生成される
        private readonly int[] FIXED_PHONEME_IDS = { 
            23, 45, 67, 12, 89, 34, 56, 78, 90, 11, // "こ"
            33, 55, 77, 99, 22, 44, 66, 88, 10, 32, // "ん"
            54, 76, 98, 21, 43, 65, 87, 9,  31, 53, // "に"
            75, 97, 20, 42, 64, 86, 8,  30, 52, 74, // "ち"
            96, 19, 41, 63, 85, 7,  29, 51, 73, 95  // "は"
        };

        void Start()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
            }
            
            if (autoPlayOnStart)
            {
                GenerateAndPlayDummyTTS();
            }
        }

        [ContextMenu("Generate and Play TTS")]
        public void GenerateAndPlayDummyTTS()
        {
            try
            {
                status = "Generating audio...";
                Debug.Log("=== Minimal TTS Prototype ===");
                Debug.Log($"Fixed phoneme IDs count: {FIXED_PHONEME_IDS.Length}");
                Debug.Log($"Phoneme IDs: [{string.Join(", ", FIXED_PHONEME_IDS.Take(10))}...]");
                
                // Step 1: 音素IDからダミー音声波形を生成
                float[] waveform = GenerateDummyWaveform();
                Debug.Log($"Generated waveform samples: {waveform.Length}");
                
                // Step 2: AudioClipを作成
                AudioClip audioClip = CreateAudioClip(waveform);
                Debug.Log($"Created AudioClip: {audioClip.name}, {audioClip.frequency}Hz, {audioClip.length}s");
                
                // Step 3: 再生
                PlayAudioClip(audioClip);
                
                status = "Playing audio...";
                Debug.Log("=== TTS Prototype Success ===");
            }
            catch (Exception e)
            {
                status = $"Error: {e.Message}";
                Debug.LogError($"TTS Prototype failed: {e}");
            }
        }

        /// <summary>
        /// 固定音素IDを基にダミー音声波形を生成
        /// 実際のTTSでは、ここでONNXモデルによる推論が行われる
        /// </summary>
        private float[] GenerateDummyWaveform()
        {
            int totalSamples = Mathf.RoundToInt(sampleRate * duration);
            float[] waveform = new float[totalSamples];
            
            // 音素IDを基に周波数を変調（簡単なデモ用）
            float baseFrequency = frequency;
            int phonemeIndex = 0;
            float samplesPerPhoneme = totalSamples / (float)FIXED_PHONEME_IDS.Length;
            
            for (int i = 0; i < totalSamples; i++)
            {
                // 現在の音素インデックスを計算
                phonemeIndex = Mathf.Min(
                    Mathf.FloorToInt(i / samplesPerPhoneme), 
                    FIXED_PHONEME_IDS.Length - 1
                );
                
                // 音素IDに基づいて周波数を調整
                float phonemeFrequency = baseFrequency * (1f + FIXED_PHONEME_IDS[phonemeIndex] / 100f);
                
                // サイン波を生成（エンベロープ付き）
                float t = i / (float)sampleRate;
                float envelope = CalculateEnvelope(i, totalSamples, phonemeIndex, FIXED_PHONEME_IDS.Length);
                waveform[i] = Mathf.Sin(2f * Mathf.PI * phonemeFrequency * t) * envelope * 0.5f;
            }
            
            return waveform;
        }

        /// <summary>
        /// 音素ごとのエンベロープを計算
        /// </summary>
        private float CalculateEnvelope(int sampleIndex, int totalSamples, int phonemeIndex, int totalPhonemes)
        {
            float samplesPerPhoneme = totalSamples / (float)totalPhonemes;
            float phonemeProgress = (sampleIndex % samplesPerPhoneme) / samplesPerPhoneme;
            
            // 各音素の開始と終了でフェードイン/アウト
            float attackTime = 0.1f;
            float releaseTime = 0.1f;
            
            if (phonemeProgress < attackTime)
            {
                return phonemeProgress / attackTime;
            }
            else if (phonemeProgress > (1f - releaseTime))
            {
                return (1f - phonemeProgress) / releaseTime;
            }
            else
            {
                return 1f;
            }
        }

        /// <summary>
        /// 波形データからAudioClipを作成
        /// </summary>
        private AudioClip CreateAudioClip(float[] waveform)
        {
            AudioClip clip = AudioClip.Create(
                "MinimalTTS_Output",
                waveform.Length,
                1, // モノラル
                sampleRate,
                false
            );
            
            clip.SetData(waveform, 0);
            return clip;
        }

        /// <summary>
        /// AudioClipを再生
        /// </summary>
        private void PlayAudioClip(AudioClip clip)
        {
            audioSource.clip = clip;
            audioSource.Play();
            
            // 再生終了時のコールバック
            StartCoroutine(WaitForAudioEnd(clip.length));
        }

        private System.Collections.IEnumerator WaitForAudioEnd(float duration)
        {
            yield return new WaitForSeconds(duration);
            status = "Playback completed";
            Debug.Log("Audio playback completed");
        }

        void OnGUI()
        {
            // 簡単なGUIで状態表示
            GUILayout.BeginArea(new Rect(10, 10, 300, 150));
            GUILayout.Label("Minimal TTS Prototype", GUI.skin.box);
            GUILayout.Space(10);
            
            GUILayout.Label($"Status: {status}");
            GUILayout.Label($"Sample Rate: {sampleRate}Hz");
            GUILayout.Label($"Duration: {duration}s");
            GUILayout.Label($"Phoneme IDs: {FIXED_PHONEME_IDS.Length}");
            
            GUILayout.Space(10);
            
            if (GUILayout.Button("Generate and Play"))
            {
                GenerateAndPlayDummyTTS();
            }
            
            GUILayout.EndArea();
        }
    }
}