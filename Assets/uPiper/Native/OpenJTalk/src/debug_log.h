#ifndef OPENJTALK_DEBUG_LOG_H
#define OPENJTALK_DEBUG_LOG_H

#include <stdio.h>

// Debug log levels
#define LOG_LEVEL_NONE  0
#define LOG_LEVEL_ERROR 1
#define LOG_LEVEL_WARN  2
#define LOG_LEVEL_INFO  3
#define LOG_LEVEL_DEBUG 4

// Set default log level based on build type
#ifndef LOG_LEVEL
    #ifdef NDEBUG
        #define LOG_LEVEL LOG_LEVEL_ERROR
    #else
        #define LOG_LEVEL LOG_LEVEL_DEBUG
    #endif
#endif

// Log macros
#if LOG_LEVEL >= LOG_LEVEL_ERROR
    #define LOG_ERROR(...) fprintf(stderr, "[ERROR] " __VA_ARGS__)
#else
    #define LOG_ERROR(...) ((void)0)
#endif

#if LOG_LEVEL >= LOG_LEVEL_WARN
    #define LOG_WARN(...) fprintf(stderr, "[WARN] " __VA_ARGS__)
#else
    #define LOG_WARN(...) ((void)0)
#endif

#if LOG_LEVEL >= LOG_LEVEL_INFO
    #define LOG_INFO(...) fprintf(stdout, "[INFO] " __VA_ARGS__)
#else
    #define LOG_INFO(...) ((void)0)
#endif

#if LOG_LEVEL >= LOG_LEVEL_DEBUG
    #define LOG_DEBUG(...) fprintf(stdout, "[DEBUG] " __VA_ARGS__)
#else
    #define LOG_DEBUG(...) ((void)0)
#endif

#endif // OPENJTALK_DEBUG_LOG_H