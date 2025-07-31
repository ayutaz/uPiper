using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace uPiper.Core.Phonemizers.Backend
{
    /// <summary>
    /// Factory for managing and selecting phonemizer backends.
    /// </summary>
    public class PhonemizerBackendFactory
    {
        private readonly List<IPhonemizerBackend> backends = new();
        private readonly Dictionary<string, IPhonemizerBackend> backendsByName = new();
        private readonly object lockObject = new();

        /// <summary>
        /// Singleton instance.
        /// </summary>
        public static PhonemizerBackendFactory Instance { get; } = new PhonemizerBackendFactory();

        private PhonemizerBackendFactory()
        {
            // Private constructor for singleton
        }

        /// <summary>
        /// Registers a phonemizer backend.
        /// </summary>
        /// <param name="backend">The backend to register.</param>
        public void RegisterBackend(IPhonemizerBackend backend)
        {
            if (backend == null)
                throw new ArgumentNullException(nameof(backend));

            lock (lockObject)
            {
                if (backendsByName.ContainsKey(backend.Name))
                {
                    Debug.LogWarning($"Backend '{backend.Name}' is already registered. Replacing.");
                    UnregisterBackend(backend.Name);
                }

                backends.Add(backend);
                backendsByName[backend.Name] = backend;

                // Sort by priority (descending)
                backends.Sort((a, b) => b.Priority.CompareTo(a.Priority));

                Debug.Log($"Registered phonemizer backend: {backend.Name} (License: {backend.License}, Priority: {backend.Priority})");
            }
        }

        /// <summary>
        /// Unregisters a backend by name.
        /// </summary>
        /// <param name="name">The name of the backend to unregister.</param>
        public void UnregisterBackend(string name)
        {
            lock (lockObject)
            {
                if (backendsByName.TryGetValue(name, out var backend))
                {
                    backends.Remove(backend);
                    backendsByName.Remove(name);
                    backend.Dispose();

                    Debug.Log($"Unregistered phonemizer backend: {name}");
                }
            }
        }

        /// <summary>
        /// Gets the best available backend for a language.
        /// </summary>
        /// <param name="language">The language code.</param>
        /// <param name="requiredLicense">Optional license requirement (e.g., "MIT").</param>
        /// <returns>The best available backend, or null if none found.</returns>
        public IPhonemizerBackend GetBackend(string language, string requiredLicense = null)
        {
            lock (lockObject)
            {
                return backends
                    .Where(b => b.IsAvailable && b.SupportsLanguage(language))
                    .Where(b => string.IsNullOrEmpty(requiredLicense) ||
                               b.License.Contains(requiredLicense, StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefault();
            }
        }

        /// <summary>
        /// Gets a backend by name.
        /// </summary>
        /// <param name="name">The backend name.</param>
        /// <returns>The backend, or null if not found.</returns>
        public IPhonemizerBackend GetBackendByName(string name)
        {
            lock (lockObject)
            {
                return backendsByName.TryGetValue(name, out var backend) ? backend : null;
            }
        }

        /// <summary>
        /// Gets all registered backends.
        /// </summary>
        /// <returns>List of all backends.</returns>
        public IReadOnlyList<IPhonemizerBackend> GetAllBackends()
        {
            lock (lockObject)
            {
                return backends.ToList().AsReadOnly();
            }
        }

        /// <summary>
        /// Gets all available languages across all backends.
        /// </summary>
        /// <returns>Distinct list of supported languages.</returns>
        public string[] GetAvailableLanguages()
        {
            lock (lockObject)
            {
                return backends
                    .Where(b => b.IsAvailable)
                    .SelectMany(b => b.SupportedLanguages)
                    .Distinct()
                    .OrderBy(l => l)
                    .ToArray();
            }
        }

        /// <summary>
        /// Initializes all registered backends.
        /// </summary>
        /// <param name="options">Initialization options.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of successfully initialized backends.</returns>
        public async Task<int> InitializeAllBackendsAsync(
            PhonemizerBackendOptions options = null,
            CancellationToken cancellationToken = default)
        {
            var tasks = new List<Task<bool>>();

            lock (lockObject)
            {
                foreach (var backend in backends)
                {
                    if (!backend.IsAvailable)
                    {
                        tasks.Add(InitializeBackendAsync(backend, options, cancellationToken));
                    }
                }
            }

            var results = await Task.WhenAll(tasks);
            return results.Count(r => r);
        }

        private async Task<bool> InitializeBackendAsync(
            IPhonemizerBackend backend,
            PhonemizerBackendOptions options,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await backend.InitializeAsync(options, cancellationToken);
                if (result)
                {
                    Debug.Log($"Initialized backend: {backend.Name}");
                }
                else
                {
                    Debug.LogWarning($"Failed to initialize backend: {backend.Name}");
                }
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error initializing backend {backend.Name}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Disposes all registered backends.
        /// </summary>
        public void DisposeAll()
        {
            lock (lockObject)
            {
                foreach (var backend in backends)
                {
                    try
                    {
                        backend.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error disposing backend {backend.Name}: {ex.Message}");
                    }
                }

                backends.Clear();
                backendsByName.Clear();
            }
        }

        /// <summary>
        /// Gets backend information for UI/debugging.
        /// </summary>
        /// <returns>Backend information.</returns>
        public BackendInfo[] GetBackendInfo()
        {
            lock (lockObject)
            {
                return backends.Select(b => new BackendInfo
                {
                    Name = b.Name,
                    Version = b.Version,
                    License = b.License,
                    Priority = b.Priority,
                    IsAvailable = b.IsAvailable,
                    SupportedLanguages = b.SupportedLanguages,
                    MemoryUsage = b.GetMemoryUsage(),
                    Capabilities = b.GetCapabilities()
                }).ToArray();
            }
        }
    }

    /// <summary>
    /// Backend information for UI/debugging.
    /// </summary>
    public class BackendInfo
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string License { get; set; }
        public int Priority { get; set; }
        public bool IsAvailable { get; set; }
        public string[] SupportedLanguages { get; set; }
        public long MemoryUsage { get; set; }
        public BackendCapabilities Capabilities { get; set; }
    }
}