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
        private readonly int[] _prosodyFlat;

        public StubG2PHandler(
            string languageCode,
            string[] phonemes = null,
            int[] prosodyFlat = null)
        {
            LanguageCode = languageCode;
            _phonemes = phonemes ?? Array.Empty<string>();
            _prosodyFlat = prosodyFlat ?? new int[_phonemes.Length * 3];
        }

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            IsInitialized = true;
            return Task.CompletedTask;
        }

        public (string[] Phonemes, int[] ProsodyFlat) Process(string text)
        {
            ProcessCallCount++;
            LastProcessedText = text;
            return (_phonemes, _prosodyFlat);
        }

        public void Dispose() { }
    }
}