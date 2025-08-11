using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using uPiper.Core.Phonemizers.Backend;

namespace uPiper.Core.Phonemizers
{
    /// <summary>
    /// Generic adapter to convert any IPhonemizer to ITextPhonemizer.
    /// This adapter works with any IPhonemizer implementation, including WebGL phonemizers.
    /// </summary>
    public class PhonemizerToTextPhonemizerAdapter : ITextPhonemizer
    {
        private readonly IPhonemizer _phonemizer;

        public string Name => _phonemizer.Name;
        public string[] SupportedLanguages => _phonemizer.SupportedLanguages;

        public PhonemizerToTextPhonemizerAdapter(IPhonemizer phonemizer)
        {
            _phonemizer = phonemizer;
        }

        public async Task<PhonemeResult> PhonemizeAsync(string text, string language, CancellationToken cancellationToken = default)
        {
            return await _phonemizer.PhonemizeAsync(text, language, cancellationToken);
        }

        public PhonemeResult Phonemize(string text, string language)
        {
            return _phonemizer.Phonemize(text, language);
        }
    }
}