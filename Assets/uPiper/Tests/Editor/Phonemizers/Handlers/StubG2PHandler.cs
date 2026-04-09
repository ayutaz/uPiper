using System;
using System.Threading;
using System.Threading.Tasks;
using uPiper.Core.Phonemizers.Multilingual.Handlers;

namespace uPiper.Tests.Editor.Phonemizers.Handlers
{
    internal sealed class StubG2PHandler : ILanguageG2PHandler
    {
        public string LanguageCode { get; }
        public bool IsInitialized { get; private set; }
        public int ProcessCallCount { get; private set; }
        public string LastProcessedText { get; private set; }

        private readonly string[] _phonemes;
        private readonly int[] _a1;
        private readonly int[] _a2;
        private readonly int[] _a3;

        public StubG2PHandler(
            string languageCode,
            string[] phonemes = null,
            int[] a1 = null,
            int[] a2 = null,
            int[] a3 = null)
        {
            LanguageCode = languageCode;
            _phonemes = phonemes ?? Array.Empty<string>();
            _a1 = a1 ?? new int[_phonemes.Length];
            _a2 = a2 ?? new int[_phonemes.Length];
            _a3 = a3 ?? new int[_phonemes.Length];
        }

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            IsInitialized = true;
            return Task.CompletedTask;
        }

        public (string[] Phonemes, int[] A1, int[] A2, int[] A3) Process(string text)
        {
            ProcessCallCount++;
            LastProcessedText = text;
            return (_phonemes, _a1, _a2, _a3);
        }

        public void Dispose() { }
    }
}
