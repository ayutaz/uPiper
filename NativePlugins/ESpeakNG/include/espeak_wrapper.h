#ifndef ESPEAK_WRAPPER_H
#define ESPEAK_WRAPPER_H

#ifdef __cplusplus
extern "C" {
#endif

#ifdef _WIN32
    #ifdef ESPEAK_WRAPPER_EXPORTS
        #define ESPEAK_API __declspec(dllexport)
    #else
        #define ESPEAK_API __declspec(dllimport)
    #endif
#else
    #define ESPEAK_API __attribute__((visibility("default")))
#endif

// Error codes
#define ESPEAK_OK                    0
#define ESPEAK_ERROR_INIT           -1
#define ESPEAK_ERROR_INVALID_PARAM  -2
#define ESPEAK_ERROR_MEMORY         -3
#define ESPEAK_ERROR_NOT_INIT       -4
#define ESPEAK_ERROR_PHONEMIZE      -5

// Voice variants
#define ESPEAK_VOICE_DEFAULT         0
#define ESPEAK_VOICE_MALE1           1
#define ESPEAK_VOICE_MALE2           2
#define ESPEAK_VOICE_MALE3           3
#define ESPEAK_VOICE_FEMALE1         4
#define ESPEAK_VOICE_FEMALE2         5
#define ESPEAK_VOICE_FEMALE3         6

// Result structure
typedef struct {
    char* phonemes;           // IPA phoneme string
    int* phoneme_ids;         // Phoneme ID array for Piper
    int phoneme_count;        // Number of phonemes
    float* durations;         // Duration for each phoneme (optional)
    char* error_message;      // Error message if any
} ESpeakResult;

// Version information
typedef struct {
    int major;
    int minor;
    int patch;
    char* version_string;
} ESpeakVersion;

/**
 * Initialize eSpeak-NG engine
 * @param data_path Path to eSpeak-ng-data directory (NULL for default)
 * @return ESPEAK_OK on success, error code otherwise
 */
ESPEAK_API int espeak_wrapper_initialize(const char* data_path);

/**
 * Check if eSpeak-NG is initialized
 * @return 1 if initialized, 0 otherwise
 */
ESPEAK_API int espeak_wrapper_is_initialized(void);

/**
 * Phonemize text
 * @param text Input text to phonemize
 * @param language Language code (e.g., "en", "es", "fr")
 * @param voice_variant Voice variant (ESPEAK_VOICE_*)
 * @return ESpeakResult pointer on success, NULL on failure
 */
ESPEAK_API ESpeakResult* espeak_wrapper_phonemize(
    const char* text,
    const char* language,
    int voice_variant
);

/**
 * Phonemize text with options
 * @param text Input text to phonemize
 * @param language Language code
 * @param voice_variant Voice variant
 * @param phoneme_mode 0=IPA, 1=eSpeak, 2=Kirshenbaum
 * @param include_stress Include stress marks
 * @return ESpeakResult pointer on success, NULL on failure
 */
ESPEAK_API ESpeakResult* espeak_wrapper_phonemize_ex(
    const char* text,
    const char* language,
    int voice_variant,
    int phoneme_mode,
    int include_stress
);

/**
 * Free phonemization result
 * @param result Result to free
 */
ESPEAK_API void espeak_wrapper_free_result(ESpeakResult* result);

/**
 * Get supported languages
 * @param count Output parameter for language count
 * @return Array of language codes (do not free)
 */
ESPEAK_API const char** espeak_wrapper_get_languages(int* count);

/**
 * Check if a language is supported
 * @param language Language code to check
 * @return 1 if supported, 0 otherwise
 */
ESPEAK_API int espeak_wrapper_is_language_supported(const char* language);

/**
 * Get version information
 * @return Version structure (do not free)
 */
ESPEAK_API const ESpeakVersion* espeak_wrapper_get_version(void);

/**
 * Set voice parameters
 * @param speed Speaking speed (80-450, default 175)
 * @param pitch Pitch (0-99, default 50)
 * @param volume Volume (0-200, default 100)
 * @return ESPEAK_OK on success, error code otherwise
 */
ESPEAK_API int espeak_wrapper_set_voice_params(int speed, int pitch, int volume);

/**
 * Terminate eSpeak-NG engine
 */
ESPEAK_API void espeak_wrapper_terminate(void);

/**
 * Get last error message
 * @return Error message string (do not free)
 */
ESPEAK_API const char* espeak_wrapper_get_last_error(void);

#ifdef __cplusplus
}
#endif

#endif // ESPEAK_WRAPPER_H