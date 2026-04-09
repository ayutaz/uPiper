using uPiper.Core.Phonemizers.Multilingual.Handlers;

namespace uPiper.Core.Phonemizers.Multilingual
{
    /// <summary>
    /// Registry entry that pairs a handler with ownership information.
    /// When <see cref="IsOwned"/> is true, the handler was created internally
    /// and should be disposed by the registry owner.
    /// </summary>
    internal readonly struct HandlerEntry
    {
        public ILanguageG2PHandler Handler { get; }
        public bool IsOwned { get; }

        public HandlerEntry(ILanguageG2PHandler handler, bool isOwned)
        {
            Handler = handler;
            IsOwned = isOwned;
        }
    }
}
