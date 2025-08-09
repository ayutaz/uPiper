using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_WEBGL && !UNITY_EDITOR
using uPiper.Core.Phonemizers.WebGL;
#endif

namespace uPiper.Samples.WebGL
{
    /// <summary>
    /// Unity WebGLでOpenJTalkをテストするためのコンポーネント
    /// </summary>
    public class OpenJTalkTestComponent : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private InputField inputField;
        [SerializeField] private Button phonemizeButton;
        [SerializeField] private Text resultText;
        [SerializeField] private Text statusText;

#if UNITY_WEBGL && !UNITY_EDITOR
        private WebGLOpenJTalkUnityPhonemizer phonemizer;
#endif
        private bool isInitialized = false;

        void Start()
        {
            Debug.Log("[OpenJTalkTest] Starting...");
            
            // ボタンのリスナー設定
            if (phonemizeButton != null)
            {
                phonemizeButton.onClick.AddListener(OnPhonemizeButtonClick);
                phonemizeButton.interactable = false;
            }

            // デフォルトテキスト設定
            if (inputField != null)
            {
                inputField.text = "こんにちは";
            }

            // ステータス更新
            UpdateStatus("初期化中...");

            // 初期化開始
            StartCoroutine(InitializePhonemizerCoroutine());
        }

        IEnumerator InitializePhonemizerCoroutine()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log("[OpenJTalkTest] Initializing WebGL phonemizer...");
            
            try
            {
                phonemizer = new WebGLOpenJTalkUnityPhonemizer();
                
                // 非同期初期化をコルーチンで待つ
                var initTask = phonemizer.InitializeAsync();
                
                while (!initTask.IsCompleted)
                {
                    yield return null;
                }
                
                if (initTask.IsFaulted)
                {
                    throw initTask.Exception.InnerException;
                }
                
                isInitialized = true;
                UpdateStatus("✓ 初期化完了");
                
                if (phonemizeButton != null)
                {
                    phonemizeButton.interactable = true;
                }
                
                Debug.Log("[OpenJTalkTest] Initialization complete");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[OpenJTalkTest] Initialization failed: {ex.Message}");
                UpdateStatus($"✗ 初期化失敗: {ex.Message}");
            }
#else
            Debug.LogWarning("[OpenJTalkTest] Not in WebGL build, using dummy mode");
            UpdateStatus("エディタモード（ダミーデータ使用）");
            isInitialized = true;
            
            if (phonemizeButton != null)
            {
                phonemizeButton.interactable = true;
            }
            
            yield return null;
#endif
        }

        void OnPhonemizeButtonClick()
        {
            if (!isInitialized)
            {
                Debug.LogWarning("[OpenJTalkTest] Not initialized yet");
                return;
            }

            string text = inputField != null ? inputField.text : "テスト";
            
            if (string.IsNullOrEmpty(text))
            {
                UpdateResult("テキストを入力してください");
                return;
            }

            Debug.Log($"[OpenJTalkTest] Phonemizing: {text}");
            StartCoroutine(PhonemizeCoroutine(text));
        }

        IEnumerator PhonemizeCoroutine(string text)
        {
            UpdateStatus("音素化中...");
            phonemizeButton.interactable = false;

#if UNITY_WEBGL && !UNITY_EDITOR
            if (phonemizer != null)
            {
                try
                {
                    var phonemizeTask = phonemizer.TextToPhonemesAsync(text);
                    
                    while (!phonemizeTask.IsCompleted)
                    {
                        yield return null;
                    }
                    
                    if (phonemizeTask.IsFaulted)
                    {
                        throw phonemizeTask.Exception.InnerException;
                    }
                    
                    var phonemes = phonemizeTask.Result;
                    DisplayPhonemes(phonemes);
                    UpdateStatus("✓ 音素化完了");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[OpenJTalkTest] Phonemization failed: {ex.Message}");
                    UpdateResult($"エラー: {ex.Message}");
                    UpdateStatus("✗ 音素化失敗");
                }
            }
#else
            // エディタモードではダミーデータを返す
            yield return new WaitForSeconds(0.5f); // 処理をシミュレート
            
            var dummyPhonemes = new string[] { "^", "t", "e", "s", "u", "t", "o", "$" };
            DisplayPhonemes(dummyPhonemes);
            UpdateStatus("✓ 音素化完了（ダミー）");
#endif

            phonemizeButton.interactable = true;
        }

        void DisplayPhonemes(string[] phonemes)
        {
            if (phonemes == null || phonemes.Length == 0)
            {
                UpdateResult("音素がありません");
                return;
            }

            // 音素を表示用に整形
            string phonemeStr = string.Join(" ", phonemes);
            string result = $"音素数: {phonemes.Length}\n";
            result += $"音素: {phonemeStr}\n\n";
            
            // PUA文字の検出
            int puaCount = 0;
            foreach (var phoneme in phonemes)
            {
                if (phoneme.Length > 0 && phoneme[0] >= '\ue000' && phoneme[0] <= '\uf8ff')
                {
                    puaCount++;
                    result += $"PUA文字検出: U+{((int)phoneme[0]):X4}\n";
                }
            }
            
            if (puaCount > 0)
            {
                result += $"\nPUA文字数: {puaCount} (マルチ文字音素)";
            }

            UpdateResult(result);
            Debug.Log($"[OpenJTalkTest] Phonemes: {phonemeStr}");
        }

        void UpdateStatus(string status)
        {
            if (statusText != null)
            {
                statusText.text = $"ステータス: {status}";
            }
            Debug.Log($"[OpenJTalkTest] Status: {status}");
        }

        void UpdateResult(string result)
        {
            if (resultText != null)
            {
                resultText.text = result;
            }
        }

        void OnDestroy()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (phonemizer != null)
            {
                phonemizer.Dispose();
                Debug.Log("[OpenJTalkTest] Phonemizer disposed");
            }
#endif
        }

        // デバッグ用：ブラウザコンソールから呼び出し可能
        public void TestPhonemize(string text)
        {
            Debug.Log($"[OpenJTalkTest] Test phonemize called with: {text}");
            if (isInitialized)
            {
                StartCoroutine(PhonemizeCoroutine(text));
            }
            else
            {
                Debug.LogWarning("[OpenJTalkTest] Not initialized yet");
            }
        }
    }
}