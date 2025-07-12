using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using uPiper.Phonemizers;

namespace uPiper.Tests
{
    [TestFixture]
    public class OpenJTalkPhonemizerTests
    {
        [Test]
        public void OpenJTalkPhonemizer_CheckAvailability()
        {
            // This test checks if we can query OpenJTalk availability
            // It may fail if native library is not built yet
            bool isAvailable = false;
            
            Assert.DoesNotThrow(() =>
            {
                isAvailable = OpenJTalkPhonemizer.IsOpenJTalkAvailable();
            });
            
            // Log the result for debugging
            Debug.Log($"[Test] OpenJTalk available: {isAvailable}");
            
            // We don't assert true here because OpenJTalk might not be installed
            // This is more of a smoke test
        }
        
        [Test]
        public void OpenJTalkPhonemizer_GetVersion()
        {
            string version = "Unknown";
            
            Assert.DoesNotThrow(() =>
            {
                version = OpenJTalkPhonemizer.GetWrapperVersion();
            });
            
            Debug.Log($"[Test] OpenJTalk wrapper version: {version}");
            Assert.IsNotNull(version);
            Assert.IsNotEmpty(version);
        }
        
        [Test]
        public async Task OpenJTalkPhonemizer_CreateAndDispose()
        {
            var phonemizer = new OpenJTalkPhonemizer();
            Assert.IsNotNull(phonemizer);
            
            // Try to initialize (may fail if OpenJTalk not installed)
            bool initialized = await phonemizer.InitializeAsync();
            Debug.Log($"[Test] OpenJTalk initialization result: {initialized}");
            
            // Dispose should always work
            Assert.DoesNotThrow(() =>
            {
                phonemizer.Dispose();
            });
        }
        
        [Test]
        public void OpenJTalkPhonemizer_CacheSettings()
        {
            // Test with cache enabled
            var phonemizerWithCache = new OpenJTalkPhonemizer(useCache: true, maxCacheSize: 100);
            Assert.IsNotNull(phonemizerWithCache);
            phonemizerWithCache.Dispose();
            
            // Test with cache disabled
            var phonemizerNoCache = new OpenJTalkPhonemizer(useCache: false);
            Assert.IsNotNull(phonemizerNoCache);
            phonemizerNoCache.Dispose();
        }
        
        [Test]
        public async Task OpenJTalkPhonemizer_BasicPhonemization()
        {
            var phonemizer = new OpenJTalkPhonemizer();
            
            bool initialized = await phonemizer.InitializeAsync();
            if (!initialized)
            {
                Debug.LogWarning("[Test] OpenJTalk not available, skipping phonemization test");
                phonemizer.Dispose();
                return;
            }
            
            try
            {
                // Test basic Japanese text
                var phonemes = phonemizer.Phonemize("こんにちは", "ja");
                
                Assert.IsNotNull(phonemes);
                Assert.Greater(phonemes.Length, 0);
                
                Debug.Log($"[Test] Phonemes for 'こんにちは': {string.Join(" ", phonemes)}");
                
                // Test empty string
                var emptyPhonemes = phonemizer.Phonemize("", "ja");
                Assert.IsNotNull(emptyPhonemes);
                Assert.AreEqual(0, emptyPhonemes.Length);
                
                // Test with spaces
                var spacedPhonemes = phonemizer.Phonemize("  テスト  ", "ja");
                Assert.IsNotNull(spacedPhonemes);
                Assert.Greater(spacedPhonemes.Length, 0);
            }
            finally
            {
                phonemizer.Dispose();
            }
        }
        
        [Test]
        public void OpenJTalkPhonemizer_NotInitialized_ThrowsException()
        {
            var phonemizer = new OpenJTalkPhonemizer();
            
            Assert.Throws<System.InvalidOperationException>(() =>
            {
                phonemizer.Phonemize("test", "ja");
            });
            
            phonemizer.Dispose();
        }
    }
}