using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace uPiper.Core.Phonemizers.Implementations
{
    /// <summary>
    /// Mock phonemizer for testing when OpenJTalk is not available
    /// </summary>
    public class MockPhonemizer : IPhonemizer
    {
        private bool _isDisposed;
        
        private readonly Dictionary<string, string[]> _mockJapanesePhonemes = new Dictionary<string, string[]>
        {
            { "こ", new[] { "k", "o" } },
            { "ん", new[] { "N" } },
            { "に", new[] { "n", "i" } },
            { "ち", new[] { "t", "i" } },
            { "は", new[] { "h", "a" } },
            { "、", new[] { "?" } },
            { "世", new[] { "s", "e" } },
            { "界", new[] { "k", "a", "i" } },
            { "！", new[] { "!" } }
        };

        public string Name => "MockPhonemizer";
        public string Version => "1.0.0";
        public string[] SupportedLanguages => new[] { "ja", "en" };
        public bool UseCache { get; set; } = false;
        public string Language => "ja";

        public Task<PhonemeResult> PhonemizeAsync(string text, string language = null, CancellationToken cancellationToken = default)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(MockPhonemizer));

            if (string.IsNullOrEmpty(text))
            {
                return Task.FromResult(new PhonemeResult
                {
                    OriginalText = text,
                    Phonemes = Array.Empty<string>(),
                    Language = language ?? Language
                });
            }

            var phonemes = new List<string>();
            
            if (language == "ja" || language == null)
            {
                // Simple Japanese mock phonemization
                foreach (char c in text)
                {
                    var key = c.ToString();
                    if (_mockJapanesePhonemes.TryGetValue(key, out var ph))
                    {
                        phonemes.AddRange(ph);
                    }
                    else if (char.IsWhiteSpace(c))
                    {
                        phonemes.Add(" ");
                    }
                }
            }
            else
            {
                // For other languages, just split by spaces
                phonemes.AddRange(text.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            }

            return Task.FromResult(new PhonemeResult
            {
                OriginalText = text,
                Phonemes = phonemes.ToArray(),
                Language = language ?? Language
            });
        }

        public PhonemeResult Phonemize(string text, string language = "ja")
        {
            return PhonemizeAsync(text, language).GetAwaiter().GetResult();
        }

        public async Task<PhonemeResult[]> PhonemizeBatchAsync(string[] texts, string language = "ja", CancellationToken cancellationToken = default)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(MockPhonemizer));

            var tasks = texts.Select(text => PhonemizeAsync(text, language, cancellationToken)).ToArray();
            return await Task.WhenAll(tasks);
        }

        public void ClearCache()
        {
            // Mock phonemizer doesn't use cache
        }

        public CacheStatistics GetCacheStatistics()
        {
            return new CacheStatistics
            {
                HitCount = 0,
                MissCount = 0,
                EvictionCount = 0,
                EntryCount = 0,
                TotalSizeBytes = 0,
                MaxSizeBytes = 0
            };
        }

        public bool IsLanguageSupported(string language)
        {
            return SupportedLanguages.Contains(language);
        }

        public LanguageInfo GetLanguageInfo(string language)
        {
            if (!IsLanguageSupported(language))
                return null;

            return new LanguageInfo
            {
                Code = language,
                Name = language == "ja" ? "Japanese" : "English",
                NativeName = language == "ja" ? "日本語" : "English",
                Direction = TextDirection.LeftToRight
            };
        }

        public void Dispose()
        {
            _isDisposed = true;
        }
    }
}