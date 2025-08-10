using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_WEBGL && !UNITY_EDITOR
using uPiper.Core.Phonemizers.WebGL;
using uPiper.Core.Phonemizers.Backend;
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
            
            phonemizer = WebGLOpenJTalkUnityPhonemizer.GetInstance();
            
            Debug.Log("[OpenJTalkTest] Created phonemizer instance");
            
            // 非同期初期化をコルーチンで待つ
            var initTask = phonemizer.InitializeAsync();
            
            Debug.Log("[OpenJTalkTest] Started async initialization, waiting for completion...");
            
            // 進行状況を定期的に報告
            float startTime = Time.time;
            float lastReportTime = startTime;
            
            // try-catch の外で yield return を使用
            while (!initTask.IsCompleted)
            {
                float currentTime = Time.time;
                if (currentTime - lastReportTime >= 1.0f) // 1秒ごとに報告
                {
                    float elapsedTime = currentTime - startTime;
                    Debug.Log($"[OpenJTalkTest] Still initializing... ({elapsedTime:F1}s elapsed)");
                    UpdateStatus($"初期化中... ({elapsedTime:F0}s)");
                    lastReportTime = currentTime;
                }
                yield return null;
            }
            
            Debug.Log($"[OpenJTalkTest] Initialization task completed. IsCompleted: {initTask.IsCompleted}, IsFaulted: {initTask.IsFaulted}");
            
            if (initTask.IsFaulted)
            {
                var exception = initTask.Exception?.InnerException ?? initTask.Exception;
                Debug.LogError($"[OpenJTalkTest] Initialization failed: {exception?.Message}");
                Debug.LogError($"[OpenJTalkTest] Full exception: {exception}");
                UpdateStatus($"✗ 初期化失敗: {exception?.Message}");
            }
            else
            {
                isInitialized = true;
                Debug.Log("[OpenJTalkTest] Initialization completed successfully");
                UpdateStatus("✓ 初期化完了");
                
                if (phonemizeButton != null)
                {
                    phonemizeButton.interactable = true;
                }
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
                // PhonemizeAsync を使用（TextToPhonemesAsync ではない）
                var phonemizeTask = phonemizer.PhonemizeAsync(text);
                
                // try-catch の外で yield return を使用
                while (!phonemizeTask.IsCompleted)
                {
                    yield return null;
                }
                
                if (phonemizeTask.IsFaulted)
                {
                    Debug.LogError($"[OpenJTalkTest] Phonemization failed: {phonemizeTask.Exception.InnerException?.Message}");
                    UpdateResult($"エラー: {phonemizeTask.Exception.InnerException?.Message}");
                    UpdateStatus("✗ 音素化失敗");
                }
                else
                {
                    var result = phonemizeTask.Result;
                    DisplayPhonemes(result.Phonemes);
                    UpdateStatus("✓ 音素化完了");
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