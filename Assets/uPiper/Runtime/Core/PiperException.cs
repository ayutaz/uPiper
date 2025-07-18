using System;

namespace uPiper.Core
{
    /// <summary>
    /// Base exception class for all Piper TTS errors
    /// </summary>
    public class PiperException : Exception
    {
        /// <summary>
        /// Error code for categorization
        /// </summary>
        public PiperErrorCode ErrorCode { get; }

        /// <summary>
        /// Additional context information
        /// </summary>
        public string Context { get; }

        public PiperException(string message) : base(message)
        {
            ErrorCode = PiperErrorCode.Unknown;
        }

        public PiperException(string message, Exception innerException)
            : base(message, innerException)
        {
            ErrorCode = PiperErrorCode.Unknown;
        }

        public PiperException(PiperErrorCode errorCode, string message)
            : base(message)
        {
            ErrorCode = errorCode;
        }

        public PiperException(PiperErrorCode errorCode, string message, Exception innerException)
            : base(message, innerException)
        {
            ErrorCode = errorCode;
        }

        public PiperException(PiperErrorCode errorCode, string message, string context)
            : base(message)
        {
            ErrorCode = errorCode;
            Context = context;
        }
    }

    /// <summary>
    /// Exception thrown during initialization
    /// </summary>
    public class PiperInitializationException : PiperException
    {
        public PiperInitializationException(string message)
            : base(PiperErrorCode.InitializationFailed, message) { }

        public PiperInitializationException(string message, Exception innerException)
            : base(PiperErrorCode.InitializationFailed, message, innerException) { }
    }

    /// <summary>
    /// Exception thrown when model loading fails
    /// </summary>
    public class PiperModelLoadException : PiperException
    {
        public string ModelPath { get; }

        public PiperModelLoadException(string modelPath, string message)
            : base(PiperErrorCode.ModelLoadFailed, message)
        {
            ModelPath = modelPath;
        }

        public PiperModelLoadException(string modelPath, string message, Exception innerException)
            : base(PiperErrorCode.ModelLoadFailed, message, innerException)
        {
            ModelPath = modelPath;
        }
    }

    /// <summary>
    /// Exception thrown during inference
    /// </summary>
    public class PiperInferenceException : PiperException
    {
        public PiperInferenceException(string message)
            : base(PiperErrorCode.InferenceFailed, message) { }

        public PiperInferenceException(string message, Exception innerException)
            : base(PiperErrorCode.InferenceFailed, message, innerException) { }
    }

    /// <summary>
    /// Exception thrown during phonemization
    /// </summary>
    public class PiperPhonemizationException : PiperException
    {
        public string InputText { get; }
        public string Language { get; }

        public PiperPhonemizationException(string inputText, string language, string message)
            : base(PiperErrorCode.PhonemizationFailed, message)
        {
            InputText = inputText;
            Language = language;
        }

        public PiperPhonemizationException(string inputText, string language, string message, Exception innerException)
            : base(PiperErrorCode.PhonemizationFailed, message, innerException)
        {
            InputText = inputText;
            Language = language;
        }
    }

    /// <summary>
    /// Exception thrown for configuration errors
    /// </summary>
    public class PiperConfigurationException : PiperException
    {
        public PiperConfigurationException(string message)
            : base(PiperErrorCode.ConfigurationError, message) { }

        public PiperConfigurationException(string message, Exception innerException)
            : base(PiperErrorCode.ConfigurationError, message, innerException) { }
    }

    /// <summary>
    /// Exception thrown when platform is not supported
    /// </summary>
    public class PiperPlatformNotSupportedException : PiperException
    {
        public string Platform { get; }

        public PiperPlatformNotSupportedException(string platform)
            : base(PiperErrorCode.PlatformNotSupported, $"Platform '{platform}' is not supported")
        {
            Platform = platform;
        }
    }

    /// <summary>
    /// Exception thrown when operation times out
    /// </summary>
    public class PiperTimeoutException : PiperException
    {
        public int TimeoutMs { get; }

        public PiperTimeoutException(int timeoutMs, string operation)
            : base(PiperErrorCode.Timeout, $"Operation '{operation}' timed out after {timeoutMs}ms")
        {
            TimeoutMs = timeoutMs;
        }
    }

    /// <summary>
    /// Error codes for categorizing exceptions
    /// </summary>
    public enum PiperErrorCode
    {
        Unknown = 0,
        InitializationFailed = 1,
        ModelLoadFailed = 2,
        InferenceFailed = 3,
        PhonemizationFailed = 4,
        ConfigurationError = 5,
        PlatformNotSupported = 6,
        Timeout = 7,
        ResourceNotFound = 8,
        InvalidInput = 9,
        MemoryError = 10,
        CacheFull = 11,
        AudioGenerationFailed = 12
    }
}