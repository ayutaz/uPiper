using System;

namespace uPiper.Phonemizers
{
    public interface IPhonemizer : IDisposable
    {
        /// <summary>
        /// Converts text to phonemes
        /// </summary>
        /// <param name="text">The text to phonemize</param>
        /// <param name="language">Language code (e.g., "ja" for Japanese)</param>
        /// <returns>Array of phonemes</returns>
        string[] Phonemize(string text, string language);
    }
}