#ifndef OPENJTALK_WRAPPER_H
#define OPENJTALK_WRAPPER_H

#ifdef __cplusplus
extern "C" {
#endif

#include <stddef.h>

// Export/Import macros for Windows DLL
#ifdef _WIN32
    #ifdef OPENJTALK_EXPORTS
        #define OPENJTALK_API __declspec(dllexport)
    #else
        #define OPENJTALK_API __declspec(dllimport)
    #endif
#else
    #define OPENJTALK_API
#endif

// Error codes
typedef enum {
    OPENJTALK_SUCCESS = 0,
    OPENJTALK_ERROR_INVALID_HANDLE = -1,
    OPENJTALK_ERROR_INVALID_INPUT = -2,
    OPENJTALK_ERROR_MEMORY_ALLOCATION = -3,
    OPENJTALK_ERROR_DICTIONARY_NOT_FOUND = -4,
    OPENJTALK_ERROR_INITIALIZATION_FAILED = -5,
    OPENJTALK_ERROR_PHONEMIZATION_FAILED = -6,
    OPENJTALK_ERROR_PROCESSING = -7,
    OPENJTALK_ERROR_INVALID_OPTION = -8,
    OPENJTALK_ERROR_INVALID_DICTIONARY = -9,
    OPENJTALK_ERROR_INVALID_UTF8 = -10
} OpenJTalkError;

// Phoneme result structure
typedef struct {
    char* phonemes;          // Space-separated phoneme string
    int* phoneme_ids;        // Array of phoneme IDs
    int phoneme_count;       // Number of phonemes
    float* durations;        // Duration of each phoneme in seconds
    float total_duration;    // Total duration in seconds
} PhonemeResult;

// Version information
OPENJTALK_API const char* openjtalk_get_version(void);

// Initialization and cleanup
OPENJTALK_API void* openjtalk_create(const char* dict_path);
OPENJTALK_API void openjtalk_destroy(void* handle);

// Phonemization
OPENJTALK_API PhonemeResult* openjtalk_phonemize(void* handle, const char* text);
OPENJTALK_API void openjtalk_free_result(PhonemeResult* result);

// Error handling
OPENJTALK_API int openjtalk_get_last_error(void* handle);
OPENJTALK_API const char* openjtalk_get_error_string(int error_code);

// Configuration
OPENJTALK_API int openjtalk_set_option(void* handle, const char* key, const char* value);
OPENJTALK_API const char* openjtalk_get_option(void* handle, const char* key);

#ifdef __cplusplus
}
#endif

#endif // OPENJTALK_WRAPPER_H