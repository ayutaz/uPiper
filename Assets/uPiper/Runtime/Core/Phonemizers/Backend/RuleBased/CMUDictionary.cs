using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace uPiper.Core.Phonemizers.Backend.RuleBased
{
    /// <summary>
    /// Carnegie Mellon University Pronouncing Dictionary handler.
    /// The CMU dictionary is in the public domain.
    /// </summary>
    public class CMUDictionary : IDisposable
    {
        private Dictionary<string, string[]> pronunciations;
        private readonly object lockObject = new();
        private bool isLoaded;

        /// <summary>
        /// Gets the number of words in the dictionary.
        /// </summary>
        public int WordCount => pronunciations?.Count ?? 0;

        /// <summary>
        /// Loads the CMU dictionary from a file.
        /// </summary>
        /// <param name="filePath">Path to the CMU dictionary file.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task LoadAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (isLoaded)
                return;

            try
            {
                var dict = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

                // After initial setup, files should be in fixed location
                var actualFilePath = filePath;
                var fileName = Path.GetFileName(filePath);

                // Primary path after setup
                var primaryPath = Path.Combine(Application.dataPath, "StreamingAssets", "uPiper", "Phonemizers", fileName);

                // Check primary path first
                if (File.Exists(primaryPath))
                {
                    actualFilePath = primaryPath;
                    Debug.Log($"[CMUDictionary] Found dictionary at: {primaryPath}");
                }
                else if (File.Exists(filePath))
                {
                    // Use original path if it exists
                    actualFilePath = filePath;
                    Debug.Log($"[CMUDictionary] Found dictionary at original path: {filePath}");
                }
                else
                {
                    Debug.LogError($"[CMUDictionary] Dictionary file not found at: {primaryPath}");
                    Debug.LogError($"[CMUDictionary] Please run 'uPiper/Setup/Run Initial Setup' from the menu.");
                }

                // Debug path information
                Debug.Log($"[CMUDictionary] Attempting to load from: {actualFilePath}");
                Debug.Log($"[CMUDictionary] File exists: {File.Exists(actualFilePath)}");
                Debug.Log($"[CMUDictionary] Directory exists: {Directory.Exists(Path.GetDirectoryName(actualFilePath))}");

                // Check if file exists
                if (!File.Exists(actualFilePath))
                {
                    Debug.LogError($"[CMUDictionary] Dictionary file not found at: {actualFilePath}");
                    Debug.LogError($"[CMUDictionary] Please import the CMU Dictionary from Package Manager:");
                    Debug.LogError($"[CMUDictionary] 1. Open Window > Package Manager");
                    Debug.LogError($"[CMUDictionary] 2. Select 'uPiper' package");
                    Debug.LogError($"[CMUDictionary] 3. Import 'CMU Pronouncing Dictionary' sample");
                    Debug.LogError($"[CMUDictionary] 4. Run 'uPiper/Setup/Install from Samples'");

                    throw new FileNotFoundException(
                        $"CMU dictionary not found. Please import from Package Manager Samples and run setup.",
                        actualFilePath);
                }

                // Check if we need to extract from StreamingAssets
                var actualPath = actualFilePath;
                if (actualFilePath.Contains("StreamingAssets") && Application.platform == RuntimePlatform.Android)
                {
                    // On Android, we need to copy from StreamingAssets
                    actualPath = await CopyFromStreamingAssets(actualFilePath, cancellationToken);
                }

                // Read the dictionary file with timeout
                var readTask = Task.Run(async () =>
                {
                    using (var reader = new StreamReader(actualPath))
                    {
                        string line;
                        while ((line = await reader.ReadLineAsync()) != null)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            // Skip comments and empty lines
                            if (string.IsNullOrWhiteSpace(line) || line.StartsWith(";;;"))
                                continue;

                            // Parse the line - find first space as separator
                            var spaceIndex = line.IndexOf(' ');
                            if (spaceIndex > 0 && spaceIndex < line.Length - 1)
                            {
                                var word = line[..spaceIndex].Trim();
                                var phonemesPart = line[(spaceIndex + 1)..].Trim();
                                var phonemes = phonemesPart.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                                // Handle multiple pronunciations (e.g., "WORD(1)", "WORD(2)")
                                var baseWord = ExtractBaseWord(word);

                                lock (lockObject)
                                {
                                    if (!dict.ContainsKey(baseWord))
                                    {
                                        dict[baseWord] = phonemes;
                                    }
                                    // For now, we only keep the first pronunciation
                                    // Could be extended to support multiple pronunciations
                                }
                            }
                        }
                    }
                    return dict;
                }, cancellationToken);

                // Wait with timeout
                if (await Task.WhenAny(readTask, Task.Delay(TimeSpan.FromSeconds(10), cancellationToken)) == readTask)
                {
                    dict = await readTask;
                    lock (lockObject)
                    {
                        pronunciations = dict;
                        isLoaded = true;
                    }
                    Debug.Log($"Loaded CMU dictionary with {pronunciations.Count} words from {filePath}");
                }
                else
                {
                    throw new TimeoutException("Dictionary loading timed out after 10 seconds");
                }
            }
            catch (OperationCanceledException)
            {
                Debug.LogError("CMU dictionary loading was cancelled.");
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load CMU dictionary: {ex.Message}");
                throw;
            }
        }


        /// <summary>
        /// Tries to get the pronunciation for a word.
        /// </summary>
        /// <param name="word">The word to look up.</param>
        /// <param name="pronunciation">The pronunciation (ARPABET phonemes).</param>
        /// <returns>True if found, false otherwise.</returns>
        public bool TryGetPronunciation(string word, out string[] pronunciation)
        {
            pronunciation = null;

            if (!isLoaded || string.IsNullOrEmpty(word))
                return false;

            lock (lockObject)
            {
                return pronunciations.TryGetValue(word.ToUpper(), out pronunciation);
            }
        }

        /// <summary>
        /// Gets all words that start with a given prefix.
        /// </summary>
        /// <param name="prefix">The prefix to search for.</param>
        /// <param name="maxResults">Maximum number of results.</param>
        /// <returns>List of matching words.</returns>
        public List<string> GetWordsWithPrefix(string prefix, int maxResults = 10)
        {
            if (!isLoaded || string.IsNullOrEmpty(prefix))
                return new List<string>();

            lock (lockObject)
            {
                return pronunciations.Keys
                    .Where(w => w.StartsWith(prefix.ToUpper()))
                    .Take(maxResults)
                    .ToList();
            }
        }

        /// <summary>
        /// Checks if a word exists in the dictionary.
        /// </summary>
        /// <param name="word">The word to check.</param>
        /// <returns>True if the word exists.</returns>
        public bool Contains(string word)
        {
            if (!isLoaded || string.IsNullOrEmpty(word))
                return false;

            lock (lockObject)
            {
                return pronunciations.ContainsKey(word.ToUpper());
            }
        }

        /// <summary>
        /// Gets the estimated memory usage in bytes.
        /// </summary>
        public long GetMemoryUsage()
        {
            if (!isLoaded)
                return 0;

            lock (lockObject)
            {
                // Rough estimation
                long total = 0;
                foreach (var kvp in pronunciations)
                {
                    total += kvp.Key.Length * 2; // Unicode chars
                    total += kvp.Value.Sum(p => p.Length * 2);
                    total += 24; // Overhead per entry
                }
                return total;
            }
        }


        private string ExtractBaseWord(string word)
        {
            // Remove pronunciation variant markers like "(1)", "(2)"
            var parenIndex = word.IndexOf('(');
            return parenIndex > 0 ? word[..parenIndex] : word;
        }

        private async Task<string> CopyFromStreamingAssets(string streamingPath, CancellationToken cancellationToken)
        {
            // For Android, we need to use UnityWebRequest to access StreamingAssets
            var fileName = Path.GetFileName(streamingPath);
            var persistentPath = Path.Combine(Application.persistentDataPath, "Dictionaries", fileName);

            // Create directory if needed
            var dir = Path.GetDirectoryName(persistentPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Check if already copied
            if (File.Exists(persistentPath))
            {
                return persistentPath;
            }

            // Copy file (platform-specific implementation needed)
            using var www = UnityEngine.Networking.UnityWebRequest.Get(streamingPath);
            await www.SendWebRequest();

            if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                File.WriteAllBytes(persistentPath, www.downloadHandler.data);
                return persistentPath;
            }
            else
            {
                throw new IOException($"Failed to load dictionary from StreamingAssets: {www.error}");
            }
        }

        public void Dispose()
        {
            lock (lockObject)
            {
                pronunciations?.Clear();
                pronunciations = null;
                isLoaded = false;
            }
        }
    }

    /// <summary>
    /// Extension to make UnityWebRequest awaitable.
    /// </summary>
    public static class UnityWebRequestExtensions
    {
        public static TaskAwaiter GetAwaiter(this UnityEngine.Networking.UnityWebRequestAsyncOperation asyncOp)
        {
            var tcs = new TaskCompletionSource<object>();
            asyncOp.completed += _ => tcs.SetResult(null);
            return ((Task)tcs.Task).GetAwaiter();
        }
    }
}