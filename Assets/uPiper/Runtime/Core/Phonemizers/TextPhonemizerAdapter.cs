#if !UNITY_WEBGL

using System.Threading;
using System.Threading.Tasks;
using uPiper.Core.Phonemizers.Backend;
using uPiper.Core.Phonemizers.Implementations;

namespace uPiper.Core.Phonemizers
{
    /// <summary>
    /// Adapter class to make OpenJTalkPhonemizer implement ITextPhonemizer interface.
    /// This provides a bridge between the existing BasePhonemizer hierarchy and the simplified interface.
    /// </summary>
    public class TextPhonemizerAdapter : ITextPhonemizer
    {
        private readonly OpenJTalkPhonemizer _phonemizer;

        public string Name => _phonemizer.Name;
        public string[] SupportedLanguages => _phonemizer.SupportedLanguages;

        public TextPhonemizerAdapter(OpenJTalkPhonemizer phonemizer)
        {
            _phonemizer = phonemizer;
        }

        public async Task<PhonemeResult> PhonemizeAsync(string text, string language, CancellationToken cancellationToken = default)
        {
            return await _phonemizer.PhonemizeAsync(text, language, cancellationToken);
        }

        public PhonemeResult Phonemize(string text, string language)
        {
            // Use the async method synchronously
            return Task.Run(() => _phonemizer.PhonemizeAsync(text, language)).GetAwaiter().GetResult();
        }
    }
}

#endif