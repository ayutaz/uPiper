using System.Collections;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core;
using uPiper.Core.AudioGeneration;

namespace uPiper.Tests.Runtime.Integration
{
    /// <summary>
    /// Tests with real Piper ONNX models
    /// </summary>
    [Category("RealModelTests")]
    public class RealModelTests
    {
        private string _modelsPath;
        private PiperTTS _piperTTS;

        [SetUp]
        public void SetUp()
        {
            _modelsPath = Path.Combine(Application.streamingAssetsPath, "uPiper", "Models");
            
            if (!Directory.Exists(_modelsPath))
            {
                Assert.Ignore("Models directory not found. Copy models to StreamingAssets/uPiper/Models/");
            }
        }

        [TearDown]
        public void TearDown()
        {
            _piperTTS?.Dispose();
        }

        [UnityTest]
        [Category("RequiresModel")]
        public IEnumerator TestJapaneseModel_GeneratesAudio()
        {
            var modelPath = Path.Combine(_modelsPath, "ja_JP-test-medium.onnx");
            
            if (!File.Exists(modelPath))
            {
                Assert.Ignore("Japanese model not found");
            }

            // Create TTS with Japanese configuration
            var config = new PiperConfig
            {
                DefaultLanguage = "ja",
                SampleRate = 22050,
                EnablePhonemeCache = true
            };
            
            _piperTTS = new PiperTTS(config);
            
            // Initialize
            var initTask = _piperTTS.InitializeAsync();
            yield return new WaitUntil(() => initTask.IsCompleted);
            
            Assert.IsFalse(initTask.IsFaulted, $"Init failed: {initTask.Exception?.GetBaseException().Message}");
            Assert.IsTrue(_piperTTS.IsInitialized);
            
            // Generate audio
            var text = "こんにちは、これは日本語のテストです。";
            var generateTask = _piperTTS.GenerateAudioAsync(text);
            
            // Wait with timeout
            float timeout = 30f;
            float elapsed = 0f;
            while (!generateTask.IsCompleted && elapsed < timeout)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }
            
            Assert.IsTrue(generateTask.IsCompleted, "Generation timed out");
            Assert.IsFalse(generateTask.IsFaulted, $"Generation failed: {generateTask.Exception?.GetBaseException().Message}");
            
            var audioClip = generateTask.Result;
            Assert.IsNotNull(audioClip);
            Assert.Greater(audioClip.length, 0);
            Assert.AreEqual(22050, audioClip.frequency);
            
            Debug.Log($"Generated audio: {audioClip.length} seconds, {audioClip.samples} samples");
        }

        [UnityTest]
        [Category("RequiresModel")]
        public IEnumerator TestEnglishModel_GeneratesAudio()
        {
            var modelPath = Path.Combine(_modelsPath, "test_voice.onnx");
            
            if (!File.Exists(modelPath))
            {
                Assert.Ignore("English model not found");
            }

            // Create TTS with English configuration
            var config = new PiperConfig
            {
                DefaultLanguage = "en",
                SampleRate = 16000, // This model uses 16kHz
                EnablePhonemeCache = true
            };
            
            _piperTTS = new PiperTTS(config);
            
            // Initialize
            var initTask = _piperTTS.InitializeAsync();
            yield return new WaitUntil(() => initTask.IsCompleted);
            
            Assert.IsFalse(initTask.IsFaulted, $"Init failed: {initTask.Exception?.GetBaseException().Message}");
            Assert.IsTrue(_piperTTS.IsInitialized);
            
            // Generate audio
            var text = "Hello, this is a test of the English voice model.";
            var generateTask = _piperTTS.GenerateAudioAsync(text);
            
            // Wait with timeout
            float timeout = 30f;
            float elapsed = 0f;
            while (!generateTask.IsCompleted && elapsed < timeout)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }
            
            Assert.IsTrue(generateTask.IsCompleted, "Generation timed out");
            Assert.IsFalse(generateTask.IsFaulted, $"Generation failed: {generateTask.Exception?.GetBaseException().Message}");
            
            var audioClip = generateTask.Result;
            Assert.IsNotNull(audioClip);
            Assert.Greater(audioClip.length, 0);
            Assert.AreEqual(16000, audioClip.frequency);
            
            Debug.Log($"Generated audio: {audioClip.length} seconds, {audioClip.samples} samples");
        }

        [UnityTest]
        [Category("RequiresModel")]
        public IEnumerator TestModelLoading_LoadsConfigFromJSON()
        {
            var loader = new ModelLoader();
            var modelPath = Path.Combine(_modelsPath, "ja_JP-test-medium.onnx");
            
            if (!File.Exists(modelPath))
            {
                Assert.Ignore("Model not found");
            }
            
            var loadTask = loader.LoadModelAsync(modelPath);
            yield return new WaitUntil(() => loadTask.IsCompleted);
            
            Assert.IsFalse(loadTask.IsFaulted, $"Load failed: {loadTask.Exception?.GetBaseException().Message}");
            
            var modelInfo = loadTask.Result;
            Assert.IsNotNull(modelInfo);
            Assert.AreEqual(22050, modelInfo.SampleRate);
            Assert.AreEqual("ja_JP-test-medium", modelInfo.Name);
            
            loader.Dispose();
        }

        [UnityTest]
        [Category("RequiresModel")]
        [Category("Performance")]
        public IEnumerator TestPerformance_MeasuresGenerationTime()
        {
            var modelPath = Path.Combine(_modelsPath, "ja_JP-test-medium.onnx");
            
            if (!File.Exists(modelPath))
            {
                Assert.Ignore("Model not found");
            }

            var config = new PiperConfig
            {
                DefaultLanguage = "ja",
                SampleRate = 22050,
                EnablePhonemeCache = false // Disable cache for accurate timing
            };
            
            _piperTTS = new PiperTTS(config);
            
            // Initialize
            var initTask = _piperTTS.InitializeAsync();
            yield return new WaitUntil(() => initTask.IsCompleted);
            
            // Warmup
            var warmupTask = _piperTTS.GenerateAudioAsync("テスト");
            yield return new WaitUntil(() => warmupTask.IsCompleted);
            
            // Performance test
            var texts = new[]
            {
                "短いテキスト",
                "これは少し長めのテキストで、音声合成の性能を測定します。",
                "最後に、かなり長いテキストを使用して、処理時間がどのように変化するかを確認します。日本語の音声合成は、英語と比較して複雑な処理が必要です。"
            };
            
            foreach (var text in texts)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var task = _piperTTS.GenerateAudioAsync(text);
                yield return new WaitUntil(() => task.IsCompleted);
                sw.Stop();
                
                var audioClip = task.Result;
                var rtf = audioClip.length / (sw.ElapsedMilliseconds / 1000f);
                
                Debug.Log($"Text length: {text.Length} chars");
                Debug.Log($"Generation time: {sw.ElapsedMilliseconds}ms");
                Debug.Log($"Audio duration: {audioClip.length:F2}s");
                Debug.Log($"Real-time factor: {rtf:F2}x");
                Debug.Log("---");
                
                // Should be faster than real-time
                Assert.Greater(rtf, 0.5f, "Generation too slow");
            }
        }

        [UnityTest]
        [Category("RequiresModel")]
        public IEnumerator TestCaching_ImprovedPerformanceOnSecondRun()
        {
            var config = new PiperConfig
            {
                DefaultLanguage = "ja",
                SampleRate = 22050,
                EnablePhonemeCache = true
            };
            
            _piperTTS = new PiperTTS(config);
            
            // Initialize
            var initTask = _piperTTS.InitializeAsync();
            yield return new WaitUntil(() => initTask.IsCompleted);
            
            var text = "キャッシュのテストです";
            
            // First run (cache miss)
            var sw1 = System.Diagnostics.Stopwatch.StartNew();
            var task1 = _piperTTS.GenerateAudioAsync(text);
            yield return new WaitUntil(() => task1.IsCompleted);
            sw1.Stop();
            var firstRunTime = sw1.ElapsedMilliseconds;
            
            // Second run (cache hit)
            var sw2 = System.Diagnostics.Stopwatch.StartNew();
            var task2 = _piperTTS.GenerateAudioAsync(text);
            yield return new WaitUntil(() => task2.IsCompleted);
            sw2.Stop();
            var secondRunTime = sw2.ElapsedMilliseconds;
            
            Debug.Log($"First run: {firstRunTime}ms");
            Debug.Log($"Second run: {secondRunTime}ms (cached)");
            Debug.Log($"Speed improvement: {(float)firstRunTime / secondRunTime:F2}x");
            
            // Second run should be faster
            Assert.Less(secondRunTime, firstRunTime);
            
            // Check cache statistics
            var stats = _piperTTS.GetCacheStatistics();
            Assert.Greater(stats.HitCount, 0);
        }
    }
}