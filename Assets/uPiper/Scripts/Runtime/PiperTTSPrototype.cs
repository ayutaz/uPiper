using System;
using System.Collections.Generic;
using System.Linq;
using Unity.InferenceEngine;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace uPiper.Runtime
{
    /// <summary>
    /// Piper TTS プロトタイプ
    /// 実際のPiper TTSモデルを使用して音声合成を行う
    /// </summary>
    public class PiperTTSPrototype : MonoBehaviour
    {
        [Header("Model Settings")]
        [SerializeField] private string modelName = "ja_JP-test-medium";
        [SerializeField] private BackendType backendType = BackendType.GPUCompute;
        
        [Header("Audio Settings")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private int sampleRate = 22050;
        
        [Header("Test Settings")]
        [SerializeField] private bool autoPlayOnStart = true;
        [SerializeField] private string testText = "こんにちは";
        
        [Header("Status")]
        [SerializeField] private string status = "Not initialized";
        
        private Model model;
        private Worker worker;
        private Dictionary<string, int> phonemeIdMap = new Dictionary<string, int>();
        
        // 日本語の音素マッピング（簡易版）
        // 実際にはOpenJTalkなどの音素化エンジンが必要
        private readonly Dictionary<string, string[]> HIRAGANA_TO_PHONEMES = new Dictionary<string, string[]>
        {
            { "こ", new[] { "k", "o" } },
            { "ん", new[] { "N" } },
            { "に", new[] { "n", "i" } },
            { "ち", new[] { "t", "i" } },
            { "は", new[] { "h", "a" } },
            { "わ", new[] { "w", "a" } },
            { "た", new[] { "t", "a" } },
            { "し", new[] { "s", "i" } }
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
            
            LoadModel();
            
            if (autoPlayOnStart && model != null)
            {
                GenerateAndPlayTTS(testText);
            }
        }

        void OnDestroy()
        {
            CleanupResources();
        }

        private void LoadModel()
        {
            try
            {
                status = "Loading model...";
                Debug.Log($"Loading Piper TTS model: {modelName}");
                
                // モデル設定を読み込む
                var configAsset = Resources.Load<TextAsset>($"{modelName}.onnx.json");
                if (configAsset != null)
                {
                    var configJson = JObject.Parse(configAsset.text);
                    LoadPhonemeMapFromJson(configJson);
                    
                    // サンプルレートを取得
                    if (configJson["audio"]?["sample_rate"] != null)
                    {
                        sampleRate = configJson["audio"]["sample_rate"].Value<int>();
                        Debug.Log($"Loaded config: sample rate: {sampleRate}");
                    }
                }
                
                // ONNXモデルを読み込む
                var modelAsset = Resources.Load<ModelAsset>(modelName);
                if (modelAsset != null)
                {
                    model = ModelLoader.Load(modelAsset);
                    worker = new Worker(model, backendType);
                    
                    status = "Model loaded successfully";
                    Debug.Log($"Model loaded: inputs={model.inputs.Count}, outputs={model.outputs.Count}");
                    
                    // モデルの入出力情報をログ
                    foreach (var input in model.inputs)
                    {
                        Debug.Log($"Input: {input.name}, shape: {string.Join("x", input.shape)}");
                    }
                    foreach (var output in model.outputs)
                    {
                        Debug.Log($"Output: {output.name}");
                    }
                }
                else
                {
                    throw new Exception($"Model asset '{modelName}' not found in Resources");
                }
            }
            catch (Exception e)
            {
                status = $"Error loading model: {e.Message}";
                Debug.LogError($"Failed to load model: {e}");
            }
        }

        private void LoadPhonemeMapFromJson(JObject configJson)
        {
            phonemeIdMap.Clear();
            
            // phoneme_id_map をパース
            var phonemeIdMapJson = configJson["phoneme_id_map"] as JObject;
            if (phonemeIdMapJson != null)
            {
                foreach (var kvp in phonemeIdMapJson)
                {
                    string phoneme = kvp.Key;
                    var idArray = kvp.Value as JArray;
                    if (idArray != null && idArray.Count > 0)
                    {
                        int id = idArray[0].Value<int>();
                        phonemeIdMap[phoneme] = id;
                    }
                }
                
                Debug.Log($"Loaded phoneme map with {phonemeIdMap.Count} entries from JSON");
            }
            else
            {
                // フォールバック: ハードコード版
                Debug.LogWarning("Failed to parse phoneme_id_map from JSON. Using fallback values.");
                phonemeIdMap["_"] = 0;  // pad
                phonemeIdMap["^"] = 1;  // bos
                phonemeIdMap["$"] = 2;  // eos
                phonemeIdMap["k"] = 25;
                phonemeIdMap["o"] = 11;
                phonemeIdMap["n"] = 50;
                phonemeIdMap["N"] = 22;
                phonemeIdMap["i"] = 8;
                phonemeIdMap["t"] = 31;
                phonemeIdMap["h"] = 47;
                phonemeIdMap["a"] = 7;
                phonemeIdMap["w"] = 56;
                phonemeIdMap["s"] = 41;
            }
        }

        [ContextMenu("Generate TTS")]
        public void GenerateTestTTS()
        {
            GenerateAndPlayTTS(testText);
        }

        public void GenerateAndPlayTTS(string text)
        {
            if (model == null || worker == null)
            {
                Debug.LogError("Model not loaded");
                return;
            }
            
            try
            {
                status = $"Generating TTS for: {text}";
                Debug.Log($"=== Piper TTS Generation ===");
                Debug.Log($"Text: {text}");
                
                // Step 1: テキストを音素IDに変換
                int[] phonemeIds = TextToPhonemeIds(text);
                Debug.Log($"Phoneme IDs: [{string.Join(", ", phonemeIds)}]");
                
                // Step 2: 音素IDから音声を生成（デモ用の簡易実装）
                // 実際のPiper TTSではONNXモデルで推論を行う
                float[] waveform = GenerateWaveformFromPhonemes(phonemeIds);
                
                // Step 3: AudioClipを作成して再生
                AudioClip clip = CreateAudioClip(waveform);
                PlayAudioClip(clip);
                
                status = "Playing...";
                Debug.Log("=== TTS Generation Complete ===");
            }
            catch (Exception e)
            {
                status = $"Error: {e.Message}";
                Debug.LogError($"TTS generation failed: {e}");
            }
        }

        private int[] TextToPhonemeIds(string text)
        {
            var phonemeIds = new List<int>();
            
            // phonemeIdMapが空の場合は警告
            if (phonemeIdMap.Count == 0)
            {
                Debug.LogWarning("Phoneme ID map is empty. Using default values.");
                // デフォルト値を返す
                return new int[] { 1, 25, 11, 50, 8, 31, 8, 47, 7, 2 }; // "こんにちは"の仮の音素ID
            }
            
            // BOS (beginning of sentence)
            if (phonemeIdMap.TryGetValue("^", out int bosId))
                phonemeIds.Add(bosId);
            
            // テキストを音素に変換（簡易実装）
            foreach (char c in text)
            {
                string charStr = c.ToString();
                if (HIRAGANA_TO_PHONEMES.TryGetValue(charStr, out string[] phonemes))
                {
                    foreach (string phoneme in phonemes)
                    {
                        if (phonemeIdMap.TryGetValue(phoneme, out int id))
                        {
                            phonemeIds.Add(id);
                        }
                    }
                }
            }
            
            // EOS (end of sentence)
            if (phonemeIdMap.TryGetValue("$", out int eosId))
                phonemeIds.Add(eosId);
            
            return phonemeIds.ToArray();
        }

        private float[] GenerateWaveformFromPhonemes(int[] phonemeIds)
        {
            // デモ用の簡易波形生成
            // 実際にはONNXモデルで推論を行う
            int samplesPerPhoneme = sampleRate / 10; // 0.1秒/音素
            float[] waveform = new float[phonemeIds.Length * samplesPerPhoneme];
            
            for (int i = 0; i < phonemeIds.Length; i++)
            {
                int phonemeId = phonemeIds[i];
                float frequency = 220f * (1f + phonemeId / 50f); // 音素IDに基づく周波数
                
                for (int j = 0; j < samplesPerPhoneme; j++)
                {
                    int sampleIndex = i * samplesPerPhoneme + j;
                    float t = sampleIndex / (float)sampleRate;
                    
                    // エンベロープ付きサイン波
                    float envelope = 1f - (j / (float)samplesPerPhoneme);
                    waveform[sampleIndex] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * 0.3f;
                }
            }
            
            return waveform;
        }

        private AudioClip CreateAudioClip(float[] waveform)
        {
            AudioClip clip = AudioClip.Create(
                "PiperTTS_Output",
                waveform.Length,
                1,
                sampleRate,
                false
            );
            
            clip.SetData(waveform, 0);
            return clip;
        }

        private void PlayAudioClip(AudioClip clip)
        {
            audioSource.clip = clip;
            audioSource.Play();
            
            StartCoroutine(WaitForAudioEnd(clip.length));
        }

        private System.Collections.IEnumerator WaitForAudioEnd(float duration)
        {
            yield return new WaitForSeconds(duration);
            status = "Ready";
        }

        private void CleanupResources()
        {
            worker?.Dispose();
            worker = null;
            model = null;
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 400, 200));
            GUILayout.Label("Piper TTS Prototype", GUI.skin.box);
            GUILayout.Space(10);
            
            GUILayout.Label($"Status: {status}");
            GUILayout.Label($"Model: {modelName}");
            GUILayout.Label($"Backend: {backendType}");
            GUILayout.Label($"Sample Rate: {sampleRate}Hz");
            
            GUILayout.Space(10);
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("Text:", GUILayout.Width(50));
            testText = GUILayout.TextField(testText, GUILayout.Width(200));
            GUILayout.EndHorizontal();
            
            if (GUILayout.Button("Generate TTS"))
            {
                GenerateAndPlayTTS(testText);
            }
            
            GUILayout.EndArea();
        }
    }
}