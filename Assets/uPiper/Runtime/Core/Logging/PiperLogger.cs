using System.Diagnostics;
using UnityEngine;

namespace uPiper.Core.Logging
{
    /// <summary>
    /// Central logging utility for uPiper
    /// </summary>
    public static class PiperLogger
    {
        private const string LOG_PREFIX = "[uPiper]";

        /// <summary>
        /// Log level enumeration
        /// </summary>
        public enum LogLevel
        {
            Debug = 0,
            Info = 1,
            Warning = 2,
            Error = 3
        }

        private static LogLevel s_minimumLevel = LogLevel.Info;

        /// <summary>
        /// Get current minimum log level
        /// </summary>
        public static LogLevel MinimumLevel => s_minimumLevel;

        /// <summary>
        /// Set minimum log level
        /// </summary>
        public static void SetMinimumLevel(LogLevel level)
        {
            s_minimumLevel = level;
        }

        /// <summary>
        /// Log debug message
        /// </summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void LogDebug(string message, params object[] args)
        {
            if (s_minimumLevel <= LogLevel.Debug)
            {
                if (message == null) message = string.Empty;
                string formattedMessage = args != null && args.Length > 0 ? string.Format(message, args) : message;
                UnityEngine.Debug.Log($"{LOG_PREFIX} {formattedMessage}");
            }
        }

        /// <summary>
        /// Log info message
        /// </summary>
        public static void LogInfo(string message, params object[] args)
        {
            if (s_minimumLevel <= LogLevel.Info)
            {
                if (message == null) message = string.Empty;
                string formattedMessage = args != null && args.Length > 0 ? string.Format(message, args) : message;
                UnityEngine.Debug.Log($"{LOG_PREFIX} {formattedMessage}");
            }
        }

        /// <summary>
        /// Log warning message
        /// </summary>
        public static void LogWarning(string message, params object[] args)
        {
            if (s_minimumLevel <= LogLevel.Warning)
            {
                if (message == null) message = string.Empty;
                string formattedMessage = args != null && args.Length > 0 ? string.Format(message, args) : message;
                UnityEngine.Debug.LogWarning($"{LOG_PREFIX} {formattedMessage}");
            }
        }

        /// <summary>
        /// Log error message
        /// </summary>
        public static void LogError(string message, params object[] args)
        {
            if (s_minimumLevel <= LogLevel.Error)
            {
                if (message == null) message = string.Empty;
                string formattedMessage = args != null && args.Length > 0 ? string.Format(message, args) : message;
                UnityEngine.Debug.LogError($"{LOG_PREFIX} {formattedMessage}");
            }
        }

        /// <summary>
        /// Initialize logger (for compatibility)
        /// </summary>
        public static void Initialize()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            s_minimumLevel = LogLevel.Debug;
#else
            s_minimumLevel = LogLevel.Info;
#endif
        }
    }
}