/**
 * @file openjtalk_wrapper.h
 * @brief OpenJTalk Wrapper - Japanese text-to-phoneme conversion library
 * @version 2.0.0
 * 
 * This library provides a C API for converting Japanese text to phonemes
 * using the OpenJTalk engine with MeCab morphological analysis.
 */

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

/**
 * @brief Error codes returned by OpenJTalk functions
 */
typedef enum {
    OPENJTALK_SUCCESS = 0,                    /**< Operation completed successfully */
    OPENJTALK_ERROR_INVALID_HANDLE = -1,      /**< Invalid handle passed to function */
    OPENJTALK_ERROR_INVALID_INPUT = -2,       /**< Invalid input text or parameters */
    OPENJTALK_ERROR_MEMORY_ALLOCATION = -3,   /**< Memory allocation failed */
    OPENJTALK_ERROR_DICTIONARY_NOT_FOUND = -4,/**< Dictionary file not found */
    OPENJTALK_ERROR_INITIALIZATION_FAILED = -5,/**< Failed to initialize OpenJTalk */
    OPENJTALK_ERROR_PHONEMIZATION_FAILED = -6,/**< Failed to convert text to phonemes */
    OPENJTALK_ERROR_PROCESSING = -7,          /**< Processing error occurred */
    OPENJTALK_ERROR_INVALID_OPTION = -8,      /**< Invalid option key or value */
    OPENJTALK_ERROR_INVALID_DICTIONARY = -9,  /**< Dictionary file is corrupted or invalid */
    OPENJTALK_ERROR_INVALID_UTF8 = -10        /**< Input text contains invalid UTF-8 sequences */
} OpenJTalkError;

/**
 * @brief Result structure for phoneme conversion
 * 
 * Contains the phoneme sequence and timing information
 * generated from Japanese text.
 */
typedef struct {
    char* phonemes;          /**< Space-separated phoneme string (e.g., "k o N n i ch i w a") */
    int* phoneme_ids;        /**< Array of phoneme IDs corresponding to each phoneme */
    int phoneme_count;       /**< Number of phonemes in the result */
    float* durations;        /**< Duration of each phoneme in seconds */
    float total_duration;    /**< Total duration of all phonemes in seconds */
} PhonemeResult;

/**
 * @brief Get the version string of the OpenJTalk wrapper
 * @return Version string (e.g., "2.0.0-full")
 */
OPENJTALK_API const char* openjtalk_get_version(void);

/**
 * @brief Create a new OpenJTalk instance
 * @param dict_path Path to the dictionary directory (can be NULL for default)
 * @return Handle to the OpenJTalk instance, or NULL on failure
 * 
 * @note The dictionary directory should contain:
 * - sys.dic: System dictionary
 * - matrix.bin: Connection cost matrix
 * - char.bin: Character definitions
 * - unk.dic: Unknown word dictionary
 */
OPENJTALK_API void* openjtalk_create(const char* dict_path);

/**
 * @brief Destroy an OpenJTalk instance and free resources
 * @param handle Handle returned by openjtalk_create()
 */
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