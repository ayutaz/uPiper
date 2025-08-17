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
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(5000); // 5 second timeout
            return await backend.InitializeAsync(options, cts.Token);
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
        /// Create a test CMU dictionary
        /// </summary>
        public static async Task<CMUDictionary> CreateTestDictionaryAsync()
        {
            var dict = new CMUDictionary();

            // Try to load the sample dictionary for testing
            var testPath = GetTestDictionaryPath();
            if (System.IO.File.Exists(testPath))
            {
                await dict.LoadAsync(testPath);
            }
            else
            {
                // If sample dictionary doesn't exist, tests should fail
                // This ensures tests run with proper data
                throw new System.IO.FileNotFoundException(
                    "Test dictionary not found. Please ensure sample dictionary is available.",
                    testPath);
            }

            return dict;
        }

        /// <summary>
        /// Initialize RuleBasedPhonemizer with test dictionary
        /// </summary>
        public static async Task<RuleBasedPhonemizer> CreateTestRuleBasedPhonemizerAsync()
        {
            var phonemizer = new RuleBasedPhonemizer();

            // Initialize with test dictionary path
            var options = new PhonemizerBackendOptions
            {
                DataPath = GetTestDictionaryPath(),
                MaxMemoryUsage = 1024 * 1024 // 1MB
            };

            await phonemizer.InitializeAsync(options);
            return phonemizer;
        }
    }
}