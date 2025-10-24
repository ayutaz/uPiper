using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace uPiper.Core.Phonemizers.Backend.CustomEnglishPhonemizer
{
    /// <summary>
    /// Holds the CMU Pronouncing Dictionary
    /// </summary>
    public class CMUDict
    {
        private Dictionary<string, string[]> _dict;

        public CMUDict()
        {
        }

        /// <summary>
        /// Load CMUDict from file
        /// </summary>
        public async Task<bool> LoadFromPath(string filePath)
        {
            _dict = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrEmpty(filePath))
            {
                Debug.LogError("No CMUDict asset path was set.");
                return false;
            }

            if (!File.Exists(filePath))
            {
                Debug.LogError("No CMUDict file found at: " + filePath);
                return false;
            }

            // Split the TextAsset text into lines
            var lines = await File.ReadAllLinesAsync(filePath);

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith(";;;"))
                {
                    continue;
                }

                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    continue;
                }

                var word = parts[0];
                var phonemes = parts.Skip(1).ToArray();

                if (!_dict.ContainsKey(word))
                {
                    _dict[word] = phonemes;
                }
            }

            Debug.Log($"Loaded {_dict.Count} entries from CMUDict.");

            return true;
        }

        public bool TryGetPhonemes(string word, out string[] phonemes)
        {
            return _dict.TryGetValue(word, out phonemes);
        }
    }
}
