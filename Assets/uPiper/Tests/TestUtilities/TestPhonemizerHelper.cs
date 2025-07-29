using System.Threading;
using System.Threading.Tasks;
using uPiper.Core.Phonemizers.Backend;
using uPiper.Core.Phonemizers.Backend.RuleBased;

namespace uPiper.Tests.TestUtilities
{
    /// <summary>
    /// Helper class for phonemizer tests
    /// </summary>
    public static class TestPhonemizerHelper
    {
        /// <summary>
        /// Initialize a phonemizer backend with test-appropriate settings
        /// </summary>
        public static async Task<bool> InitializeForTestAsync(
            PhonemizerBackendBase backend, 
            CancellationToken cancellationToken = default)
        {
            var options = new PhonemizerBackendOptions
            {
                // Use sample dictionary for tests
                DataPath = GetTestDictionaryPath(),
                MaxMemoryUsage = 10 * 1024 * 1024 // 10MB limit for tests
            };
            
            // Initialize with timeout
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                cts.CancelAfter(5000); // 5 second timeout
                return await backend.InitializeAsync(options, cts.Token);
            }
        }
        
        /// <summary>
        /// Get path to test dictionary file
        /// </summary>
        public static string GetTestDictionaryPath()
        {
            // Use the sample dictionary which is smaller and faster to load
            return System.IO.Path.Combine(
                UnityEngine.Application.streamingAssetsPath,
                "uPiper",
                "Phonemizers",
                "cmudict-sample.txt"
            );
        }
        
        /// <summary>
        /// Create a minimal CMU dictionary for testing
        /// </summary>
        public static CMUDictionary CreateTestDictionary()
        {
            return CMUDictionary.CreateMinimal();
        }
        
        /// <summary>
        /// Initialize RuleBasedPhonemizer with minimal dictionary
        /// </summary>
        public static async Task<RuleBasedPhonemizer> CreateTestRuleBasedPhonemizerAsync()
        {
            var phonemizer = new RuleBasedPhonemizer();
            
            // Use reflection to inject minimal dictionary
            var dictField = typeof(RuleBasedPhonemizer)
                .GetField("cmuDictionary", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (dictField != null)
            {
                dictField.SetValue(phonemizer, CreateTestDictionary());
            }
            
            // Initialize with minimal settings
            var options = new PhonemizerBackendOptions
            {
                DataPath = null, // Will trigger minimal dictionary
                MaxMemoryUsage = 1024 * 1024 // 1MB
            };
            
            await phonemizer.InitializeAsync(options);
            return phonemizer;
        }
    }
}