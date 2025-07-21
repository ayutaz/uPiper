#ifndef OPENJTALK_WRAPPER_V2_H
#define OPENJTALK_WRAPPER_V2_H

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

// Phoneme data structure (extended version)
typedef struct {
    int phoneme_id;          // Phoneme ID
    char phoneme_str[16];    // Phoneme string
    int accent_type;         // Accent type (0=none, 1=high, -1=low)
    int mora_position;       // Position in mora
    int duration_ms;         // Duration in milliseconds
} PhonemeData;

// Version information
OPENJTALK_API const char* openjtalk_get_version(void);

// Create/destroy phonemizer
OPENJTALK_API void* openjtalk_create(const char* dict_path);
OPENJTALK_API void openjtalk_destroy(void* handle);

// Simple phonemization (pyopenjtalk compatible)
OPENJTALK_API int openjtalk_text_to_phonemes(void* handle, const char* text, 
                                             int* phoneme_ids, int max_phonemes);

// Extended phonemization with details
OPENJTALK_API int openjtalk_text_to_phonemes_ex(void* handle, const char* text, 
                                                PhonemeData* phonemes, int max_phonemes);

// Error handling
OPENJTALK_API const char* openjtalk_get_last_error(void* handle);

// Options
OPENJTALK_API int openjtalk_set_option(void* handle, const char* key, const char* value);
OPENJTALK_API const char* openjtalk_get_option(void* handle, const char* key);

// Status
OPENJTALK_API int openjtalk_is_initialized(void* handle);
OPENJTALK_API int openjtalk_get_phoneme_count(void* handle, const char* text);
OPENJTALK_API void openjtalk_clear(void* handle);

#ifdef __cplusplus
}
#endif

#endif // OPENJTALK_WRAPPER_V2_H