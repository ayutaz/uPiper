using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using uPiper.Core.Logging;

namespace uPiper.Core.Phonemizers.Implementations
{
    /// <summary>
    /// Mock implementation of a phonemizer for testing purposes.
    /// </summary>
    public class MockPhonemizer : BasePhonemizer
    {
        private readonly Dictionary<string, PhonemeResult> _mockResults;
        private readonly Dictionary<string, Exception> _mockErrors;
        private readonly TimeSpan _simulatedDelay;
        private readonly Random _random;

        /// <summary>
        /// Gets the name of the phonemizer.
        /// </summary>
        public override string Name => "Mock Phonemizer";

        /// <summary>
        /// Gets the version of the phonemizer.
        /// </summary>
        public override string Version => "1.0.0";

        /// <summary>
        /// Gets the list of supported language codes.
        /// </summary>
        public override string[] SupportedLanguages => new[] { "ja", "en", "zh", "ko", "de", "fr", "es" };

        /// <summary>
        /// Gets the number of times PhonemizeInternalAsync has been called.
        /// </summary>
        public int CallCount { get; private set; }

        /// <summary>
        /// Gets the last text that was phonemized.
        /// </summary>
        public string LastProcessedText { get; private set; }

        /// <summary>
        /// Gets the last language that was used.
        /// </summary>
        public string LastProcessedLanguage { get; private set; }

        /// <summary>
        /// Creates a new instance of MockPhonemizer.
        /// </summary>
        /// <param name="simulatedDelay">Optional delay to simulate processing time.</param>
        /// <param name="cacheSize">Size of the LRU cache.</param>
        public MockPhonemizer(TimeSpan? simulatedDelay = null, int? cacheSize = null) 
            : base(cacheSize)
        {
            _simulatedDelay = simulatedDelay ?? TimeSpan.FromMilliseconds(10);
            _mockResults = new Dictionary<string, PhonemeResult>();
            _mockErrors = new Dictionary<string, Exception>();
            _random = new Random();
            
            SetupDefaultMockData();
        }

        /// <summary>
        /// Sets a mock result for specific text.
        /// </summary>
        /// <param name="text">The input text.</param>
        /// <param name="result">The mock result to return.</param>
        public void SetMockResult(string text, PhonemeResult result)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (result == null) throw new ArgumentNullException(nameof(result));
            
            _mockResults[text] = result;
            PiperLogger.LogDebug($"Mock result set for: \"{text}\"");
        }

        /// <summary>
        /// Sets up the phonemizer to throw an exception for specific text.
        /// </summary>
        /// <param name="text">The input text.</param>
        /// <param name="exception">The exception to throw.</param>
        public void SimulateError(string text, Exception exception)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (exception == null) throw new ArgumentNullException(nameof(exception));
            
            _mockErrors[text] = exception;
            PiperLogger.LogDebug($"Mock error set for: \"{text}\"");
        }

        /// <summary>
        /// Resets the call count and last processed values.
        /// </summary>
        public void ResetCallTracking()
        {
            CallCount = 0;
            LastProcessedText = null;
            LastProcessedLanguage = null;
        }

        /// <summary>
        /// Clears all mock results and errors.
        /// </summary>
        public void ClearMockData()
        {
            _mockResults.Clear();
            _mockErrors.Clear();
            SetupDefaultMockData();
        }

        /// <summary>
        /// Performs the mock phonemization.
        /// </summary>
        protected override async Task<PhonemeResult> PhonemizeInternalAsync(string normalizedText, string language, CancellationToken cancellationToken)
        {
            // Track the call
            CallCount++;
            LastProcessedText = normalizedText;
            LastProcessedLanguage = language;

            // Simulate processing delay
            if (_simulatedDelay > TimeSpan.Zero)
            {
                await Task.Delay(_simulatedDelay, cancellationToken);
            }

            // Check for simulated errors
            if (_mockErrors.TryGetValue(normalizedText, out var error))
            {
                throw error;
            }

            // Check for predefined results
            if (_mockResults.TryGetValue(normalizedText, out var mockResult))
            {
                return mockResult.Clone();
            }

            // Generate default mock phonemes based on language
            return GenerateMockPhonemes(normalizedText, language);
        }

        /// <summary>
        /// Sets up default mock data for common test cases.
        /// </summary>
        private void SetupDefaultMockData()
        {
            // Japanese test data
            _mockResults["こんにちは"] = new PhonemeResult
            {
                Phonemes = new[] { "k", "o", "N", "n", "i", "ch", "i", "w", "a" },
                PhonemeIds = new[] { 23, 35, 42, 42, 21, 16, 21, 49, 6 },
                Durations = new[] { 0.1f, 0.1f, 0.1f, 0.1f, 0.1f, 0.1f, 0.1f, 0.1f, 0.1f },
                Pitches = new[] { 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f }
            };

            // English test data
            _mockResults["hello world"] = new PhonemeResult
            {
                Phonemes = new[] { "h", "ə", "l", "oʊ", " ", "w", "ɜr", "l", "d" },
                PhonemeIds = new[] { 19, 28, 25, 34, 0, 49, 30, 25, 17 },
                Durations = new[] { 0.08f, 0.06f, 0.08f, 0.12f, 0.05f, 0.08f, 0.10f, 0.08f, 0.10f },
                Pitches = new[] { 1.0f, 1.0f, 1.0f, 1.2f, 1.0f, 1.0f, 0.9f, 1.0f, 0.8f }
            };

            // Empty text
            _mockResults[""] = new PhonemeResult
            {
                Phonemes = Array.Empty<string>(),
                PhonemeIds = Array.Empty<int>(),
                Durations = Array.Empty<float>(),
                Pitches = Array.Empty<float>()
            };
        }

        /// <summary>
        /// Generates mock phonemes for text that doesn't have predefined results.
        /// </summary>
        private PhonemeResult GenerateMockPhonemes(string text, string language)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new PhonemeResult();
            }

            var phonemes = new List<string>();
            var phonemeIds = new List<int>();
            var durations = new List<float>();
            var pitches = new List<float>();

            // Simple mock phoneme generation based on characters
            foreach (char c in text.ToLowerInvariant())
            {
                if (char.IsLetter(c))
                {
                    // Generate mock phoneme based on character
                    var phoneme = GetMockPhoneme(c, language);
                    phonemes.Add(phoneme);
                    phonemeIds.Add(_random.Next(1, 50));
                    durations.Add(0.05f + (float)_random.NextDouble() * 0.1f);
                    pitches.Add(0.8f + (float)_random.NextDouble() * 0.4f);
                }
                else if (char.IsWhiteSpace(c))
                {
                    phonemes.Add(" ");
                    phonemeIds.Add(0);
                    durations.Add(0.05f);
                    pitches.Add(1.0f);
                }
                else if (char.IsPunctuation(c))
                {
                    // Add pause for punctuation
                    phonemes.Add("_");
                    phonemeIds.Add(0);
                    durations.Add(0.1f);
                    pitches.Add(1.0f);
                }
            }

            return new PhonemeResult
            {
                Phonemes = phonemes.ToArray(),
                PhonemeIds = phonemeIds.ToArray(),
                Durations = durations.ToArray(),
                Pitches = pitches.ToArray(),
                Metadata = "Generated by MockPhonemizer"
            };
        }

        /// <summary>
        /// Gets a mock phoneme for a character based on language.
        /// </summary>
        private string GetMockPhoneme(char c, string language)
        {
            // Simple mapping for demonstration
            return language switch
            {
                "ja" => c switch
                {
                    'a' => "a",
                    'i' => "i",
                    'u' => "u",
                    'e' => "e",
                    'o' => "o",
                    'k' => "k",
                    'g' => "g",
                    's' => "s",
                    'z' => "z",
                    't' => "t",
                    'd' => "d",
                    'n' => "n",
                    'h' => "h",
                    'b' => "b",
                    'p' => "p",
                    'm' => "m",
                    'y' => "y",
                    'r' => "r",
                    'w' => "w",
                    _ => c.ToString()
                },
                "en" => c switch
                {
                    'a' => "æ",
                    'e' => "ɛ",
                    'i' => "ɪ",
                    'o' => "ɑ",
                    'u' => "ʌ",
                    _ => c.ToString()
                },
                _ => c.ToString()
            };
        }

        /// <summary>
        /// Initializes language information with test data.
        /// </summary>
        protected override void InitializeLanguages()
        {
            base.InitializeLanguages();

            // Add more detailed info for testing
            if (_languageInfos.ContainsKey("ja"))
            {
                _languageInfos["ja"].RequiresPreprocessing = true;
                _languageInfos["ja"].SupportsAccent = true;
                _languageInfos["ja"].PhonemeSetType = "Japanese";
                _languageInfos["ja"].AvailableVoices = new[] { "test-ja-1", "test-ja-2" };
                _languageInfos["ja"].DefaultVoice = "test-ja-1";
            }

            if (_languageInfos.ContainsKey("en"))
            {
                _languageInfos["en"].RequiresPreprocessing = false;
                _languageInfos["en"].SupportsAccent = false;
                _languageInfos["en"].PhonemeSetType = "English-US";
                _languageInfos["en"].AvailableVoices = new[] { "test-en-1", "test-en-2", "test-en-3" };
                _languageInfos["en"].DefaultVoice = "test-en-1";
            }
        }
    }
}