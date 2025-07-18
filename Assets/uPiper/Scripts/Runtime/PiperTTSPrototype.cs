using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.InferenceEngine;
using UnityEngine;

namespace uPiper.Runtime
{
    /// <summary>
    /// Piper TTS プロトタイプ
    /// 実際のPiper TTSモデルを使用して音声合成を行う
    /// </summary>
    public class PiperTTSPrototype : MonoBehaviour
    {
        [Header("Model Settings")]
        [SerializeField] private string _modelName = "ja_JP-test-medium";
        [SerializeField] private BackendType _backendType = BackendType.GPUCompute;

        [Header("Audio Settings")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private int _sampleRate = 22050;

        [Header("Test Settings")]
        [SerializeField] private bool _autoPlayOnStart = true;
        [SerializeField] private string _testText = "こんにちは";

        [Header("Status")]
        [SerializeField] private string _status = "Not initialized";

        private Model _model;
        private Worker _worker;
        private Dictionary<string, int> _phonemeIdMap = new Dictionary<string, int>();

        // 日本語の音素マッピング（簡易版）
        // 実際にはOpenJTalkなどの音素化エンジンが必要
        private readonly Dictionary<string, string[]> _hiraganaToPhonemes = new Dictionary<string, string[]>
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

        private void Start()
        {
            if (_audioSource == null)
            {
                _audioSource = GetComponent<AudioSource>();
                if (_audioSource == null)
                {
                    _audioSource = gameObject.AddComponent<AudioSource>();
                }
            }

            LoadModel();

            if (_autoPlayOnStart && _model != null)
            {
                GenerateAndPlayTTS(_testText);
            }
        }

        private void OnDestroy()
        {
            CleanupResources();
        }

        private void LoadModel()
        {
            try
            {
                _status = "Loading model...";
                Debug.Log($"Loading Piper TTS model: {_modelName}");

                // モデル設定を読み込む
                // Resources.Load では拡張子を含めない
                var configAsset = Resources.Load<TextAsset>($"{_modelName}.onnx");
                if (configAsset != null)
                {
                    Debug.Log($"Config JSON loaded, length: {configAsset.text.Length}");
                    var configJson = JObject.Parse(configAsset.text);
                    LoadPhonemeMapFromJson(configJson);

                    // サンプルレートを取得
                    if (configJson["audio"]?["sample_rate"] != null)
                    {
                        _sampleRate = configJson["audio"]["sample_rate"].Value<int>();
                        Debug.Log($"Loaded config: sample rate: {_sampleRate}");
                    }
                }
                else
                {
                    Debug.LogError($"Config file not found: {_modelName}.onnx in Resources");
                }

                // ONNXモデルを読み込む
                var modelAsset = Resources.Load<ModelAsset>(_modelName);
                if (modelAsset != null)
                {
                    _model = ModelLoader.Load(modelAsset);
                    _worker = new Worker(_model, _backendType);

                    _status = "Model loaded successfully";
                    Debug.Log($"Model loaded: inputs={_model.inputs.Count}, outputs={_model.outputs.Count}");

                    // モデルの入出力情報をログ
                    foreach (var input in _model.inputs)
                    {
                        Debug.Log($"Input: {input.name}, shape: {string.Join("x", input.shape)}");
                    }
                    foreach (var output in _model.outputs)
                    {
                        Debug.Log($"Output: {output.name}");
                    }
                }
                else
                {
                    throw new Exception($"Model asset '{_modelName}' not found in Resources");
                }
            }
            catch (Exception e)
            {
                _status = $"Error loading model: {e.Message}";
                Debug.LogError($"Failed to load model: {e}");
            }
        }

        private void LoadPhonemeMapFromJson(JObject configJson)
        {
            _phonemeIdMap.Clear();

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
                        _phonemeIdMap[phoneme] = id;
                    }
                }

                Debug.Log($"Loaded phoneme map with {_phonemeIdMap.Count} entries from JSON");
            }
            else
            {
                // フォールバック: ハードコード版
                Debug.LogWarning("Failed to parse phoneme_id_map from JSON. Using fallback values.");
                _phonemeIdMap["_"] = 0;  // pad
                _phonemeIdMap["^"] = 1;  // bos
                _phonemeIdMap["$"] = 2;  // eos
                _phonemeIdMap["k"] = 25;
                _phonemeIdMap["o"] = 11;
                _phonemeIdMap["n"] = 50;
                _phonemeIdMap["N"] = 22;
                _phonemeIdMap["i"] = 8;
                _phonemeIdMap["t"] = 31;
                _phonemeIdMap["h"] = 47;
                _phonemeIdMap["a"] = 7;
                _phonemeIdMap["w"] = 56;
                _phonemeIdMap["s"] = 41;
            }
        }

        [ContextMenu("Generate TTS")]
        public void GenerateTestTTS()
        {
            GenerateAndPlayTTS(_testText);
        }

        public void GenerateAndPlayTTS(string text)
        {
            if (_model == null || _worker == null)
            {
                Debug.LogError("Model not loaded");
                return;
            }

            try
            {
                _status = $"Generating TTS for: {text}";
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

                _status = "Playing...";
                Debug.Log("=== TTS Generation Complete ===");
            }
            catch (Exception e)
            {
                _status = $"Error: {e.Message}";
                Debug.LogError($"TTS generation failed: {e}");
            }
        }

        private int[] TextToPhonemeIds(string text)
        {
            var phonemeIds = new List<int>();

            // phonemeIdMapが空の場合は警告
            if (_phonemeIdMap.Count == 0)
            {
                Debug.LogWarning("Phoneme ID map is empty. Using default values.");
                // デフォルト値を返す
                return new int[] { 1, 25, 11, 50, 8, 31, 8, 47, 7, 2 }; // "こんにちは"の仮の音素ID
            }

            // BOS (beginning of sentence)
            if (_phonemeIdMap.TryGetValue("^", out int bosId))
                phonemeIds.Add(bosId);

            // テキストを音素に変換（簡易実装）
            foreach (char c in text)
            {
                string charStr = c.ToString();
                if (_hiraganaToPhonemes.TryGetValue(charStr, out string[] phonemes))
                {
                    foreach (string phoneme in phonemes)
                    {
                        if (_phonemeIdMap.TryGetValue(phoneme, out int id))
                        {
                            phonemeIds.Add(id);
                        }
                    }
                }
            }

            // EOS (end of sentence)
            if (_phonemeIdMap.TryGetValue("$", out int eosId))
                phonemeIds.Add(eosId);

            return phonemeIds.ToArray();
        }

        private float[] GenerateWaveformFromPhonemes(int[] phonemeIds)
        {
            // デモ用の簡易波形生成
            // 実際にはONNXモデルで推論を行う
            int samplesPerPhoneme = _sampleRate / 10; // 0.1秒/音素
            float[] waveform = new float[phonemeIds.Length * samplesPerPhoneme];

            for (int i = 0; i < phonemeIds.Length; i++)
            {
                int phonemeId = phonemeIds[i];
                float frequency = 220f * (1f + phonemeId / 50f); // 音素IDに基づく周波数

                for (int j = 0; j < samplesPerPhoneme; j++)
                {
                    int sampleIndex = i * samplesPerPhoneme + j;
                    float t = sampleIndex / (float)_sampleRate;

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
                _sampleRate,
                false
            );

            clip.SetData(waveform, 0);
            return clip;
        }

        private void PlayAudioClip(AudioClip clip)
        {
            _audioSource.clip = clip;
            _audioSource.Play();

            StartCoroutine(WaitForAudioEnd(clip.length));
        }

        private System.Collections.IEnumerator WaitForAudioEnd(float duration)
        {
            yield return new WaitForSeconds(duration);
            _status = "Ready";
        }

        private void CleanupResources()
        {
            _worker?.Dispose();
            _worker = null;
            _model = null;
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 400, 200));
            GUILayout.Label("Piper TTS Prototype", GUI.skin.box);
            GUILayout.Space(10);

            GUILayout.Label($"Status: {_status}");
            GUILayout.Label($"Model: {_modelName}");
            GUILayout.Label($"Backend: {_backendType}");
            GUILayout.Label($"Sample Rate: {_sampleRate}Hz");

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Text:", GUILayout.Width(50));
            _testText = GUILayout.TextField(_testText, GUILayout.Width(200));
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Generate TTS"))
            {
                GenerateAndPlayTTS(_testText);
            }

            GUILayout.EndArea();
        }
    }
}