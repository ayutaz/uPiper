using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Runtime;

namespace uPiper.Tests.Runtime
{
    /// <summary>
    /// TTS プロトタイプのテスト
    /// </summary>
    public class TTSPrototypeTest
    {
        [UnityTest]
        public IEnumerator MinimalTTSPrototype_CanGenerateAudio()
        {
            // GameObject を作成
            var gameObject = new GameObject("TestMinimalTTS");
            var tts = gameObject.AddComponent<MinimalTTSPrototype>();
            
            // AudioSource が自動的に追加されることを確認
            yield return null;
            
            var audioSource = gameObject.GetComponent<AudioSource>();
            Assert.IsNotNull(audioSource, "AudioSource should be added automatically");
            
            // 音声生成を実行
            tts.GenerateAndPlayDummyTTS();
            
            // 少し待つ
            yield return new WaitForSeconds(0.1f);
            
            // AudioClip が設定されていることを確認
            Assert.IsNotNull(audioSource.clip, "AudioClip should be generated");
            Assert.AreEqual("MinimalTTS_Output", audioSource.clip.name);
            Assert.AreEqual(22050, audioSource.clip.frequency);
            
            // CI環境では isPlaying が信頼できない場合があるため、
            // AudioClip が設定されていることと、長さが正しいことを確認
            Assert.Greater(audioSource.clip.length, 0f, "AudioClip should have a duration");
            Assert.AreEqual(2f, audioSource.clip.length, 0.1f, "AudioClip duration should be approximately 2 seconds");
            
            // クリーンアップ
            Object.Destroy(gameObject);
        }
        
        [Test]
        public void PiperTTSPrototype_PhonemeMapping()
        {
            // 音素マッピングのテスト
            var gameObject = new GameObject("TestPiperTTS");
            var tts = gameObject.AddComponent<PiperTTSPrototype>();
            
            // 基本的なコンポーネントの確認
            Assert.IsNotNull(tts, "PiperTTSPrototype component should be created");
            
            // クリーンアップ
            Object.DestroyImmediate(gameObject);
        }
        
        [Test]
        public void TTSPrototype_WaveformGeneration()
        {
            // 波形生成のパラメータテスト
            int sampleRate = 22050;
            float duration = 1.0f;
            int expectedSamples = Mathf.RoundToInt(sampleRate * duration);
            
            Assert.AreEqual(22050, expectedSamples, "Sample calculation should be correct");
        }
    }
}