#if UNITY_IOS && !UNITY_EDITOR
using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using uPiper.Core.Phonemizers.Implementations;
using uPiper.Core.Platform;

namespace uPiper.Tests.Runtime.Core.Phonemizers
{
    /// <summary>
    /// iOS-specific tests for OpenJTalkPhonemizer implementation.
    /// </summary>
    [TestFixture]
    [Category("iOS")]
    [Category("RequiresNativeLibrary")]
    public class OpenJTalkPhonemizerIOSTest
    {
        private OpenJTalkPhonemizer _phonemizer;

        [SetUp]
        public void SetUp()
        {
            try
            {
                _phonemizer = new OpenJTalkPhonemizer();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[OpenJTalkPhonemizerIOSTest] Failed to create phonemizer: {ex}");
                Assert.Ignore("OpenJTalk native library not available on iOS. Skipping test.");
            }
        }

        [TearDown]
        public void TearDown()
        {
            _phonemizer?.Dispose();
        }

        [Test]
        public void Platform_IsIOS()
        {
            Assert.IsTrue(PlatformHelper.IsIOS);
            Assert.AreEqual(RuntimePlatform.IPhonePlayer, Application.platform);
        }

        [Test]
        public void NativeLibrary_IsStaticallyLinked()
        {
            // On iOS, native libraries are statically linked
            // This test verifies that the phonemizer can be created without DLL loading errors
            Assert.NotNull(_phonemizer);
            Assert.AreEqual("OpenJTalk", _phonemizer.Name);
        }

        [Test]
        public void DictionaryPath_UsesIOSSpecificPath()
        {
            // iOS uses Application.dataPath + /Raw for StreamingAssets
            var expectedPathPrefix = Path.Combine(Application.dataPath, "Raw", "uPiper", "OpenJTalk");
            
            // Note: We can't directly access the private _dictionaryPath field,
            // but we can verify that phonemization works, which indirectly validates the path
            var result = _phonemizer.Phonemize("テスト");
            Assert.NotNull(result);
            Assert.Greater(result.Phonemes?.Length ?? 0, 0);
        }

        [Test]
        public void Phonemize_JapaneseText_WorksOnIOS()
        {
            var testTexts = new[]
            {
                "こんにちは",
                "音声合成",
                "iOS対応"
            };

            foreach (var text in testTexts)
            {
                var result = _phonemizer.Phonemize(text);
                Assert.NotNull(result, $"Result should not be null for '{text}'");
                Assert.NotNull(result.Phonemes, $"Phonemes should not be null for '{text}'");
                Assert.Greater(result.Phonemes.Length, 0, $"Should have phonemes for '{text}'");
                
                Debug.Log($"[iOS] Phonemized '{text}' -> {string.Join(" ", result.Phonemes)}");
            }
        }

        [Test]
        public void MemoryUsage_IsOptimized()
        {
            // Test memory usage on iOS
            var initialMemory = GC.GetTotalMemory(false);
            
            // Perform multiple phonemizations
            for (int i = 0; i < 100; i++)
            {
                var result = _phonemizer.Phonemize($"テスト{i}");
                Assert.NotNull(result);
            }
            
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var finalMemory = GC.GetTotalMemory(false);
            var memoryIncrease = finalMemory - initialMemory;
            
            // Memory increase should be reasonable (less than 10MB for 100 operations)
            Assert.Less(memoryIncrease, 10 * 1024 * 1024, 
                $"Memory usage increased by {memoryIncrease / 1024 / 1024}MB, which is too high for iOS");
        }

        [Test]
        public void CacheStatistics_WorksOnIOS()
        {
            // Test cache functionality on iOS
            var text = "キャッシュテスト";
            
            // First call - should not be cached
            var result1 = _phonemizer.Phonemize(text);
            Assert.IsFalse(result1.FromCache);
            
            // Second call - should be cached
            var result2 = _phonemizer.Phonemize(text);
            Assert.IsTrue(result2.FromCache);
            
            // Verify cache statistics
            var stats = _phonemizer.GetCacheStatistics();
            Assert.Greater(stats.HitCount, 0);
            Assert.AreEqual(1, stats.UniqueEntries);
        }

        [Test]
        public void ThreadSafety_OnIOS()
        {
            // Test thread safety on iOS
            var errors = 0;
            var tasks = new System.Threading.Tasks.Task[5];
            
            for (int i = 0; i < tasks.Length; i++)
            {
                var taskId = i;
                tasks[i] = System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        for (int j = 0; j < 10; j++)
                        {
                            var result = _phonemizer.Phonemize($"スレッド{taskId}テスト{j}");
                            Assert.NotNull(result);
                            Assert.NotNull(result.Phonemes);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Thread {taskId} error: {ex}");
                        System.Threading.Interlocked.Increment(ref errors);
                    }
                });
            }
            
            System.Threading.Tasks.Task.WaitAll(tasks);
            Assert.AreEqual(0, errors, "Thread safety test failed with errors");
        }

        [Test]
        public void LowMemoryWarning_HandledGracefully()
        {
            // Simulate low memory warning handling
            // Note: We can't actually trigger a low memory warning in tests,
            // but we can verify the phonemizer continues to work after clearing cache
            
            // Fill cache
            for (int i = 0; i < 50; i++)
            {
                _phonemizer.Phonemize($"メモリテスト{i}");
            }
            
            // Clear cache (simulating response to low memory)
            _phonemizer.ClearCache();
            
            // Verify it still works
            var result = _phonemizer.Phonemize("低メモリ後のテスト");
            Assert.NotNull(result);
            Assert.NotNull(result.Phonemes);
            Assert.Greater(result.Phonemes.Length, 0);
            
            // Verify cache was cleared
            var stats = _phonemizer.GetCacheStatistics();
            Assert.AreEqual(1, stats.UniqueEntries);
        }
    }
}
#endif