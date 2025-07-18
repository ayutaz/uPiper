#if !UNITY_WEBGL
using System;
using System.Threading;
using System.Threading.Tasks;
using uPiper.Core.Platform;

namespace uPiper.Core.Phonemizers.Implementations
{
    /// <summary>
    /// Placeholder for OpenJTalk phonemizer implementation.
    /// This will be implemented in Phase 1.8 with P/Invoke bindings.
    /// </summary>
    public class OpenJTalkPhonemizer : BasePhonemizer
    {
        public override string Name => "OpenJTalk";
        public override string Version => "1.0.0";
        public override string[] SupportedLanguages => new[] { "ja" };

        protected override Task<PhonemeResult> PhonemizeInternalAsync(
            string text, 
            string language, 
            CancellationToken cancellationToken)
        {
            // TODO: Implement in Phase 1.8
            throw new NotImplementedException("OpenJTalk phonemizer will be implemented in Phase 1.8");
        }
    }
}
#endif