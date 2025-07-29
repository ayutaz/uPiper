using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace uPiper.Core.Phonemizers.Data
{
    /// <summary>
    /// Manages phonemizer data files with staged downloading and caching.
    /// </summary>
    public class PhonemizerDataManager : IDataManager
    {
        private readonly string dataRoot; private readonly Dictionary<string, DataPackageInfo> packages;
        private readonly SemaphoreSlim downloadSemaphore;
        private DataManifest manifest;

        /// <summary>
        /// Gets the singleton instance.
        /// </summary>
        public static PhonemizerDataManager Instance { get; } = new PhonemizerDataManager();

        /// <summary>
        /// Event fired when download progress updates.
        /// </summary>
        public event Action<string, float> DownloadProgress;

        /// <summary>
        /// Event fired when a package is downloaded.
        /// </summary>
        public event Action<string, bool> PackageDownloaded;

        public PhonemizerDataManager()
        {
            dataRoot = Path.Combine(Application.persistentDataPath, "uPiper", "PhonemizerData");
            packages = new Dictionary<string, DataPackageInfo>();
            downloadSemaphore = new SemaphoreSlim(1, 1);

            // Ensure directory exists
            Directory.CreateDirectory(dataRoot);
        }

        /// <inheritdoc/>
        public async Task<bool> IsDataAvailable(string language)
        {
            var packageInfo = await GetPackageInfo(language);
            if (packageInfo == null)
                return false;

            var localPath = GetLocalPath(packageInfo);
            return File.Exists(localPath) && await VerifyChecksum(localPath, packageInfo.Checksum);
        }

        /// <inheritdoc/>
        public async Task<bool> DownloadDataAsync(string language, IProgress<float> progress = null)
        {
            var packageInfo = await GetPackageInfo(language);
            if (packageInfo == null)
            {
                Debug.LogError($"No package found for language: {language}");
                return false;
            }

            await downloadSemaphore.WaitAsync();
            try
            {
                return await DownloadPackageInternal(packageInfo, progress);
            }
            finally
            {
                downloadSemaphore.Release();
            }
        }

        /// <inheritdoc/>
        public long GetDataSize(string language)
        {
            if (packages.TryGetValue(language, out var info))
                return info.Size;

            return 0;
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteDataAsync(string language)
        {
            var packageInfo = await GetPackageInfo(language);
            if (packageInfo == null)
                return false;

            var localPath = GetLocalPath(packageInfo);
            if (File.Exists(localPath))
            {
                try
                {
                    File.Delete(localPath);

                    // Also delete extracted files if any
                    var extractPath = Path.Combine(dataRoot, language);
                    if (Directory.Exists(extractPath))
                    {
                        Directory.Delete(extractPath, true);
                    }

                    Debug.Log($"Deleted data for language: {language}");
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to delete data: {ex.Message}");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Gets the path to data for a specific language.
        /// </summary>
        public async Task<string> GetDataPath(string language)
        {
            if (!await IsDataAvailable(language))
                return null;

            return Path.Combine(dataRoot, language);
        }

        /// <summary>
        /// Gets information about all available packages.
        /// </summary>
        public async Task<DataPackageInfo[]> GetAvailablePackages()
        {
            await EnsureManifestLoaded();
            return packages.Values.ToArray();
        }

        /// <summary>
        /// Gets downloaded packages.
        /// </summary>
        public async Task<DataPackageInfo[]> GetDownloadedPackages()
        {
            await EnsureManifestLoaded();
            var downloaded = new List<DataPackageInfo>();

            foreach (var package in packages.Values)
            {
                if (await IsDataAvailable(package.Language))
                {
                    downloaded.Add(package);
                }
            }

            return downloaded.ToArray();
        }

        /// <summary>
        /// Downloads essential data for basic functionality.
        /// </summary>
        public async Task<bool> DownloadEssentialData(IProgress<float> progress = null)
        {
            await EnsureManifestLoaded();

            var essentialPackages = packages.Values
                .Where(p => p.Priority == PackagePriority.Essential)
                .ToArray();

            if (essentialPackages.Length == 0)
                return true;

            var totalSize = essentialPackages.Sum(p => p.Size);
            var downloadedSize = 0L;
            var overallProgress = new Progress<float>(p =>
            {
                progress?.Report((downloadedSize + p * essentialPackages[0].Size) / totalSize);
            });

            foreach (var package in essentialPackages)
            {
                if (!await DownloadPackageInternal(package, overallProgress))
                    return false;

                downloadedSize += package.Size;
            }

            return true;
        }

        private async Task<bool> DownloadPackageInternal(DataPackageInfo packageInfo, IProgress<float> progress)
        {
            var localPath = GetLocalPath(packageInfo);

            // Check if already downloaded
            if (File.Exists(localPath) && await VerifyChecksum(localPath, packageInfo.Checksum))
            {
                Debug.Log($"Package already downloaded: {packageInfo.Name}");
                return true;
            }

            Debug.Log($"Downloading package: {packageInfo.Name} ({FormatBytes(packageInfo.Size)})");

            try
            {
                using var www = UnityWebRequest.Get(packageInfo.Url);
                // Set up progress tracking
                var downloadHandler = new DownloadHandlerBuffer();
                www.downloadHandler = downloadHandler;

                // Start download
                var operation = www.SendWebRequest();

                // Track progress
                while (!operation.isDone)
                {
                    var downloadProgress = operation.progress;
                    progress?.Report(downloadProgress);
                    DownloadProgress?.Invoke(packageInfo.Language, downloadProgress);
                    await Task.Yield();
                }

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Download failed: {www.error}");
                    PackageDownloaded?.Invoke(packageInfo.Language, false);
                    return false;
                }

                // Save to file
                var data = www.downloadHandler.data;
                var tempPath = localPath + ".tmp";
                File.WriteAllBytes(tempPath, data);

                // Verify checksum
                if (!await VerifyChecksum(tempPath, packageInfo.Checksum))
                {
                    Debug.LogError($"Checksum verification failed for: {packageInfo.Name}");
                    File.Delete(tempPath);
                    PackageDownloaded?.Invoke(packageInfo.Language, false);
                    return false;
                }

                // Move to final location
                if (File.Exists(localPath))
                    File.Delete(localPath);
                File.Move(tempPath, localPath);

                // Extract if needed
                if (packageInfo.IsCompressed)
                {
                    await ExtractPackage(localPath, Path.Combine(dataRoot, packageInfo.Language));
                }

                Debug.Log($"Successfully downloaded: {packageInfo.Name}");
                PackageDownloaded?.Invoke(packageInfo.Language, true);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Download error: {ex.Message}");
                PackageDownloaded?.Invoke(packageInfo.Language, false);
                return false;
            }
        }

        private async Task EnsureManifestLoaded()
        {
            if (manifest != null)
                return;

            // Try local manifest first
            var localManifestPath = Path.Combine(dataRoot, "manifest.json");
            if (File.Exists(localManifestPath))
            {
                try
                {
                    var json = File.ReadAllText(localManifestPath);
                    manifest = JsonUtility.FromJson<DataManifest>(json);
                    LoadManifestPackages();
                    return;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to load local manifest: {ex.Message}");
                }
            }

            // Load from remote
            await LoadRemoteManifest();
        }

        private async Task LoadRemoteManifest()
        {
            await Task.CompletedTask; // Placeholder for future remote loading
            // For now, use hardcoded manifest
            // In production, this would download from CDN
            manifest = CreateDefaultManifest();
            LoadManifestPackages();

            // Save locally for offline use
            try
            {
                var json = JsonUtility.ToJson(manifest, true);
                var localManifestPath = Path.Combine(dataRoot, "manifest.json");
                File.WriteAllText(localManifestPath, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to save manifest: {ex.Message}");
            }
        }

        private void LoadManifestPackages()
        {
            packages.Clear();
            foreach (var package in manifest.Packages)
            {
                packages[package.Language] = package;
            }
        }

        private DataManifest CreateDefaultManifest()
        {
            return new DataManifest
            {
                Version = "1.0.0",
                Packages = new[]
                {
                    new DataPackageInfo
                    {
                        Language = "en",
                        Name = "English Essential",
                        FileName = "en-essential.dat",
                        Url = "https://your-cdn.com/upiper/data/en-essential.dat",
                        Size = 512 * 1024, // 512KB
                        Checksum = "abc123",
                        Priority = PackagePriority.Essential,
                        IsCompressed = false
                    },
                    new DataPackageInfo
                    {
                        Language = "en-full",
                        Name = "English Full (CMU Dictionary)",
                        FileName = "en-cmu-dict.zip",
                        Url = "https://your-cdn.com/upiper/data/en-cmu-dict.zip",
                        Size = 2 * 1024 * 1024, // 2MB
                        Checksum = "def456",
                        Priority = PackagePriority.Standard,
                        IsCompressed = true
                    }
                }
            };
        }

        private async Task<DataPackageInfo> GetPackageInfo(string language)
        {
            await EnsureManifestLoaded();
            packages.TryGetValue(language, out var info);
            return info;
        }

        private string GetLocalPath(DataPackageInfo packageInfo)
        {
            return Path.Combine(dataRoot, "packages", packageInfo.FileName);
        }

        private async Task<bool> VerifyChecksum(string filePath, string expectedChecksum)
        {
            if (string.IsNullOrEmpty(expectedChecksum))
                return true; // No checksum to verify

            try
            {
                // Read file and compute MD5 hash
                using var md5 = System.Security.Cryptography.MD5.Create();
                using var stream = File.OpenRead(filePath);
                // Compute hash asynchronously in chunks
                var buffer = new byte[8192];
                int bytesRead;

                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    md5.TransformBlock(buffer, 0, bytesRead, buffer, 0);
                }

                md5.TransformFinalBlock(new byte[0], 0, 0);
                var hash = md5.Hash;

                // Convert to hex string
                var hashString = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

                // Compare with expected
                var isValid = hashString.Equals(expectedChecksum, StringComparison.OrdinalIgnoreCase);

                if (!isValid)
                {
                    Debug.LogWarning($"Checksum mismatch: expected {expectedChecksum}, got {hashString}");
                }

                return isValid;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error verifying checksum: {ex.Message}");
                return false;
            }
        }

        private async Task ExtractPackage(string zipPath, string extractPath)
        {
            // Simple implementation - in production, use proper ZIP extraction
            await Task.Yield();
            Directory.CreateDirectory(extractPath);
            Debug.Log($"Would extract {zipPath} to {extractPath}");
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double size = bytes;
            var order = 0;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:0.##} {sizes[order]}";
        }
    }

    /// <summary>
    /// Data manager interface.
    /// </summary>
    public interface IDataManager
    {
        public Task<bool> IsDataAvailable(string language);
        public Task<bool> DownloadDataAsync(string language, IProgress<float> progress = null);
        public long GetDataSize(string language);
        public Task<bool> DeleteDataAsync(string language);
    }

    /// <summary>
    /// Data manifest containing package information.
    /// </summary>
    [Serializable]
    public class DataManifest
    {
        public string Version;
        public DataPackageInfo[] Packages;
    }

    /// <summary>
    /// Information about a data package.
    /// </summary>
    [Serializable]
    public class DataPackageInfo
    {
        public string Language;
        public string Name;
        public string FileName;
        public string Url;
        public long Size;
        public string Checksum;
        public PackagePriority Priority;
        public bool IsCompressed;
    }

    /// <summary>
    /// Package priority levels.
    /// </summary>
    public enum PackagePriority
    {
        Essential,  // Required for basic functionality
        Standard,   // Recommended for good quality
        Optional    // Nice to have
    }
}