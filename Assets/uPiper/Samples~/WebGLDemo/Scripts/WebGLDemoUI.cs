using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using uPiper.Core.Logging;

namespace uPiper.Samples.WebGLDemo
{
    public class WebGLDemoUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private InputField textInput;
        [SerializeField] private Button generateButton;
        [SerializeField] private Text statusText;
        [SerializeField] private Slider progressSlider;
        [SerializeField] private AudioSource audioSource;

        [Header("Demo Settings")]
        [SerializeField] private string defaultText = "こんにちは、uPiper WebGLデモへようこそ！";

        private void Start()
        {
            // デフォルトテキストを設定
            if (textInput != null)
            {
                textInput.text = defaultText;
            }

            // ボタンイベントを設定
            if (generateButton != null)
            {
                generateButton.onClick.AddListener(OnGenerateButtonClick);
            }

            // 初期状態を設定
            UpdateStatus("準備完了");
            SetProgress(0);
        }

        private void OnGenerateButtonClick()
        {
            if (string.IsNullOrEmpty(textInput.text))
            {
                UpdateStatus("テキストを入力してください");
                return;
            }

            StartCoroutine(GenerateAudioDemo());
        }

        private IEnumerator GenerateAudioDemo()
        {
            // UIを無効化
            generateButton.interactable = false;
            UpdateStatus("音声生成中...");
            SetProgress(0.3f);

            // デモ用の待機（実際のTTS実装まで）
            yield return new WaitForSeconds(1.0f);
            SetProgress(0.6f);

            yield return new WaitForSeconds(0.5f);
            SetProgress(1.0f);

            // 完了
            UpdateStatus("音声生成完了！");
            
            // デモ用のビープ音を再生（実際のTTSが実装されるまで）
            if (audioSource != null)
            {
                audioSource.pitch = 1.0f + Random.Range(-0.1f, 0.1f);
                audioSource.PlayOneShot(CreateBeepSound());
            }

            // UIを再有効化
            generateButton.interactable = true;
            yield return new WaitForSeconds(2.0f);
            UpdateStatus("準備完了");
            SetProgress(0);
        }

        private void UpdateStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = $"状態: {message}";
            }
            
            PiperLogger.LogInfo($"WebGLDemo - {message}");
        }

        private void SetProgress(float value)
        {
            if (progressSlider != null)
            {
                progressSlider.value = value;
            }
        }

        // デモ用のビープ音を生成
        private AudioClip CreateBeepSound()
        {
            int sampleRate = 44100;
            float frequency = 440.0f;
            float duration = 0.3f;
            int sampleCount = (int)(sampleRate * duration);
            
            AudioClip clip = AudioClip.Create("Beep", sampleCount, 1, sampleRate, false);
            float[] data = new float[sampleCount];
            
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                data[i] = Mathf.Sin(2 * Mathf.PI * frequency * t) * 0.5f;
                
                // フェードイン/アウト
                float fade = 1.0f;
                if (t < 0.05f) fade = t / 0.05f;
                else if (t > duration - 0.05f) fade = (duration - t) / 0.05f;
                data[i] *= fade;
            }
            
            clip.SetData(data, 0);
            return clip;
        }
    }
}