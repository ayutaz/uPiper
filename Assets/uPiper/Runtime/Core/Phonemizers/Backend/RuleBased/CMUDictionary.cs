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

                // Try multiple paths in priority order
                var actualFilePath = filePath;
                var fileName = Path.GetFileName(filePath);
                
                // Build list of paths to check
                var pathsToCheck = new List<string>();
                
                // 1. Original path (might be from StreamingAssets)
                pathsToCheck.Add(filePath);
                
                // 2. Unity Package installation (Assets/StreamingAssets/)
                pathsToCheck.Add(Path.Combine(Application.dataPath, "StreamingAssets", "uPiper", "Phonemizers", fileName));
                
                // 3. Package Manager installation
                var assemblyPath = typeof(CMUDictionary).Assembly.Location;
                if (!string.IsNullOrEmpty(assemblyPath) && assemblyPath.Contains("PackageCache"))
                {
                    var packageCacheIndex = assemblyPath.IndexOf("PackageCache");
                    if (packageCacheIndex > 0)
                    {
                        var pathAfterCache = assemblyPath.Substring(packageCacheIndex + "PackageCache".Length + 1);
                        var packageNameEnd = pathAfterCache.IndexOf(Path.DirectorySeparatorChar);
                        if (packageNameEnd > 0)
                        {
                            var packageName = pathAfterCache.Substring(0, packageNameEnd);
                            var packagePath = Path.Combine(Application.dataPath, "..", "Library", "PackageCache", packageName);
                            pathsToCheck.Add(Path.Combine(packagePath, "StreamingAssets", "uPiper", "Phonemizers", fileName));
                        }
                    }
                }
                
                // Find the first existing file
                foreach (var path in pathsToCheck)
                {
                    if (File.Exists(path))
                    {
                        actualFilePath = path;
                        string source = "";
                        if (path.Contains("Assets/StreamingAssets"))
                            source = " (Unity Package)";
                        else if (path.Contains("PackageCache"))
                            source = " (Package Manager)";
                        else if (path == filePath)
                            source = " (Original path)";
                            
                        Debug.Log($"[CMUDictionary] Found dictionary at: {path}{source}");
                        break;
                    }
                }

                // Debug path information
                Debug.Log($"[CMUDictionary] Attempting to load from: {actualFilePath}");
                Debug.Log($"[CMUDictionary] File exists: {File.Exists(actualFilePath)}");
                Debug.Log($"[CMUDictionary] Directory exists: {Directory.Exists(Path.GetDirectoryName(actualFilePath))}");

                // Check if file exists
                if (!File.Exists(actualFilePath))
                {
                    Debug.LogWarning($"CMU dictionary file not found at: {actualFilePath}. Using minimal built-in dictionary.");

                    // Try to list files in the directory for debugging
                    var dir = Path.GetDirectoryName(filePath);
                    if (Directory.Exists(dir))
                    {
                        var files = Directory.GetFiles(dir, "*.txt");
                        var fileNames = new List<string>();
                        foreach (var file in files)
                        {
                            fileNames.Add(Path.GetFileName(file));
                        }
                        Debug.Log($"[CMUDictionary] Files in {dir}: {string.Join(", ", fileNames)}");
                    }

                    LoadMinimalDictionary();
                    return;
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
                Debug.LogWarning("CMU dictionary loading was cancelled. Using minimal dictionary.");
                LoadMinimalDictionary();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load CMU dictionary: {ex.Message}. Using minimal dictionary.");
                LoadMinimalDictionary();
            }
        }

        /// <summary>
        /// Load a minimal built-in dictionary for basic functionality
        /// </summary>
        private void LoadMinimalDictionary()
        {
            var minimal = CreateMinimal();
            lock (lockObject)
            {
                pronunciations = minimal.pronunciations;
                isLoaded = true;
            }
            Debug.Log($"Loaded minimal dictionary with {pronunciations.Count} words");
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

        /// <summary>
        /// Creates a minimal dictionary for testing.
        /// </summary>
        public static CMUDictionary CreateMinimal()
        {
            var dict = new CMUDictionary
            {
                pronunciations = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    ["HELLO"] = new[] { "HH", "AH0", "L", "OW1" },
                    ["WORLD"] = new[] { "W", "ER1", "L", "D" },
                    ["TEST"] = new[] { "T", "EH1", "S", "T" },
                    ["UNITY"] = new[] { "Y", "UW1", "N", "AH0", "T", "IY0" }
                },
                isLoaded = true
            };
            return dict;
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