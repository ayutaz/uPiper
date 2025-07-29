using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace uPiper.Core.Phonemizers.Backend
{
    /// <summary>
    /// Base class for phonemizer backend implementations.
    /// </summary>
    public abstract class PhonemizerBackendBase : IPhonemizerBackend
    {
        private bool isDisposed;
        protected bool isInitialized;

        /// <inheritdoc/>
        public abstract string Name { get; }

        /// <inheritdoc/>
        public abstract string Version { get; }

        /// <inheritdoc/>
        public abstract string License { get; }

        /// <inheritdoc/>
        public abstract string[] SupportedLanguages { get; }

        /// <inheritdoc/>
        public virtual int Priority { get; protected set; } = 50;

        /// <inheritdoc/>
        public bool IsAvailable => isInitialized && !isDisposed;

        /// <inheritdoc/>
        public virtual async Task<bool> InitializeAsync(
            PhonemizerBackendOptions options = null,
            CancellationToken cancellationToken = default)
        {
            if (isInitialized)
                return true;

            try
            {
                Debug.Log($"Initializing {Name} backend...");
                var result = await InitializeInternalAsync(options, cancellationToken);
                isInitialized = result;

                if (result)
                {
                    Debug.Log($"{Name} backend initialized successfully.");
                }
                else
                {
                    Debug.LogWarning($"{Name} backend initialization failed.");
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error initializing {Name} backend: {ex.Message}");
                isInitialized = false;
                return false;
            }
        }

        /// <summary>
        /// Internal initialization logic to be implemented by derived classes.
        /// </summary>
        protected abstract Task<bool> InitializeInternalAsync(
            PhonemizerBackendOptions options,
            CancellationToken cancellationToken);

        /// <inheritdoc/>
        public abstract Task<PhonemeResult> PhonemizeAsync(
            string text,
            string language,
            PhonemeOptions options = null,
            CancellationToken cancellationToken = default);

        /// <inheritdoc/>
        public virtual bool SupportsLanguage(string language)
        {
            if (string.IsNullOrEmpty(language))
                return false;

            // Case-insensitive comparison
            foreach (var supported in SupportedLanguages)
            {
                if (string.Equals(supported, language, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <inheritdoc/>
        public abstract long GetMemoryUsage();

        /// <inheritdoc/>
        public abstract BackendCapabilities GetCapabilities();

        /// <summary>
        /// Validates input text before processing.
        /// </summary>
        protected virtual bool ValidateInput(string text, string language, out string error)
        {
            error = null;

            if (string.IsNullOrEmpty(text))
            {
                error = "Input text is null or empty";
                return false;
            }

            if (text.Length > 10000)
            {
                error = "Input text exceeds maximum length (10000 characters)";
                return false;
            }

            if (!SupportsLanguage(language))
            {
                error = $"Language '{language}' is not supported by {Name}";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Creates a failed result with error message.
        /// </summary>
        protected PhonemeResult CreateErrorResult(string error, string language = null)
        {
            return new PhonemeResult
            {
                Success = false,
                ErrorMessage = error,
                Language = language,
                Backend = Name,
                ProcessingTimeMs = 0
            };
        }

        /// <summary>
        /// Ensures the backend is initialized.
        /// </summary>
        protected void EnsureInitialized()
        {
            if (!isInitialized)
                throw new InvalidOperationException($"{Name} backend is not initialized");

            if (isDisposed)
                throw new ObjectDisposedException(Name);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases resources.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    DisposeInternal();
                }

                isDisposed = true;
                isInitialized = false;
            }
        }

        /// <summary>
        /// Internal disposal logic to be implemented by derived classes.
        /// </summary>
        protected abstract void DisposeInternal();

        ~PhonemizerBackendBase()
        {
            Dispose(false);
        }
    }
}