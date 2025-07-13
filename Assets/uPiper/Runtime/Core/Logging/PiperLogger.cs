using Unity.Logging;
using Unity.Logging.Sinks;

namespace uPiper.Core.Logging
{
    /// <summary>
    /// Central logging utility for uPiper
    /// </summary>
    public static class PiperLogger
    {
        private static Logger logger;
        
        /// <summary>
        /// Logger instance for uPiper
        /// </summary>
        public static Logger Logger
        {
            get
            {
                if (logger == null)
                {
                    Initialize();
                }
                return logger;
            }
        }
        
        /// <summary>
        /// Initialize the logger with default configuration
        /// </summary>
        public static void Initialize()
        {
            var config = new LoggerConfig();
            
            // Configure minimum log level based on debug setting
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            config.MinimumLevel = LogLevel.Debug;
            #else
            config.MinimumLevel = LogLevel.Info;
            #endif
            
            // Add Unity console sink
            config.WriteTo.UnityDebugLog();
            
            // Create logger
            logger = new Logger(config);
        }
        
        /// <summary>
        /// Configure the logger with custom settings
        /// </summary>
        public static void Configure(LoggerConfig config)
        {
            logger?.Dispose();
            logger = new Logger(config);
        }
        
        /// <summary>
        /// Set minimum log level
        /// </summary>
        public static void SetMinimumLevel(LogLevel level)
        {
            if (logger != null)
            {
                var config = new LoggerConfig();
                config.MinimumLevel = level;
                config.WriteTo.UnityDebugLog();
                logger.Dispose();
                logger = new Logger(config);
            }
        }
        
        /// <summary>
        /// Cleanup logger resources
        /// </summary>
        public static void Shutdown()
        {
            logger?.Dispose();
            logger = null;
        }
    }
}