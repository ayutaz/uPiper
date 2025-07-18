using System;

namespace uPiper.Core.AudioGeneration
{
    /// <summary>
    /// Interface for encoding phonemes to model-compatible format
    /// </summary>
    public interface IPhonemeEncoder
    {
        /// <summary>
        /// Encode phoneme strings to ID array
        /// </summary>
        /// <param name="phonemes">Array of phoneme strings</param>
        /// <returns>Array of phoneme IDs</returns>
        int[] EncodePhonemes(string[] phonemes);

        /// <summary>
        /// Encode phoneme result to ID array
        /// </summary>
        /// <param name="phonemeResult">Phoneme result from phonemizer</param>
        /// <returns>Array of phoneme IDs</returns>
        int[] EncodePhonemes(Phonemizers.PhonemeResult phonemeResult);

        /// <summary>
        /// Add padding to phoneme ID sequence
        /// </summary>
        /// <param name="phonemeIds">Original phoneme IDs</param>
        /// <param name="targetLength">Target sequence length</param>
        /// <param name="padId">ID to use for padding (default: 0)</param>
        /// <returns>Padded phoneme ID array</returns>
        int[] AddPadding(int[] phonemeIds, int targetLength, int padId = 0);

        /// <summary>
        /// Add special tokens (start/end) to sequence
        /// </summary>
        /// <param name="phonemeIds">Original phoneme IDs</param>
        /// <param name="startToken">Start token ID (optional)</param>
        /// <param name="endToken">End token ID (optional)</param>
        /// <returns>Phoneme IDs with special tokens</returns>
        int[] AddSpecialTokens(int[] phonemeIds, int? startToken = null, int? endToken = null);

        /// <summary>
        /// Get the phoneme vocabulary size
        /// </summary>
        int VocabularySize { get; }

        /// <summary>
        /// Get the padding token ID
        /// </summary>
        int PadTokenId { get; }

        /// <summary>
        /// Get the unknown token ID
        /// </summary>
        int UnknownTokenId { get; }
    }
}