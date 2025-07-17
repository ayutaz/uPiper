#ifndef OPENJTALK_PHONEMIZER_H
#define OPENJTALK_PHONEMIZER_H

#include <stdbool.h>
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

#ifdef __cplusplus
extern "C" {
#endif

// OpenJTalk phoneme definitions (compatible with pyopenjtalk)
typedef enum {
    // Special
    PHONEME_PAU = 0,    // Pause
    PHONEME_SIL = 1,    // Silence
    
    // Vowels
    PHONEME_A = 2,
    PHONEME_I = 3,
    PHONEME_U = 4,
    PHONEME_E = 5,
    PHONEME_O = 6,
    
    // Consonants
    PHONEME_K = 7,
    PHONEME_G = 8,
    PHONEME_S = 9,
    PHONEME_SH = 10,
    PHONEME_Z = 11,
    PHONEME_T = 12,
    PHONEME_CH = 13,
    PHONEME_TS = 14,
    PHONEME_D = 15,
    PHONEME_N = 16,
    PHONEME_H = 17,
    PHONEME_F = 18,
    PHONEME_B = 19,
    PHONEME_P = 20,
    PHONEME_M = 21,
    PHONEME_Y = 22,
    PHONEME_R = 23,
    PHONEME_W = 24,
    
    // Special moras
    PHONEME_NY = 25,    // にゃ行
    PHONEME_HY = 26,    // ひゃ行
    PHONEME_MY = 27,    // みゃ行
    PHONEME_RY = 28,    // りゃ行
    PHONEME_GY = 29,    // ぎゃ行
    PHONEME_BY = 30,    // びゃ行
    PHONEME_PY = 31,    // ぴゃ行
    
    // Others
    PHONEME_CL = 32,    // Closure (っ)
    PHONEME_V = 33,     // ヴ
    PHONEME_Q = 34,     // Glottal stop
    PHONEME_J = 35,     // じ
    PHONEME_DY = 36,    // でぃ
    PHONEME_TY = 37,    // てぃ
    
    PHONEME_COUNT = 38,
    
    // Special value for unknown phonemes
    PHONEME_UNKNOWN = 99
} PhonemeID;

// Phoneme with accent information
typedef struct {
    PhonemeID id;
    int accent_type;    // 0: no accent, 1: high, -1: low
    int mora_position;  // Position in mora
    float duration;     // Duration hint (optional)
} PhonemeInfo;

// OpenJTalk phonemizer context
typedef struct OpenJTalkPhonemizer OpenJTalkPhonemizer;

// Create/destroy phonemizer
OPENJTALK_API OpenJTalkPhonemizer* openjtalk_phonemizer_create(void);
OPENJTALK_API void openjtalk_phonemizer_destroy(OpenJTalkPhonemizer* phonemizer);

// Initialize with dictionary
OPENJTALK_API bool openjtalk_phonemizer_initialize(OpenJTalkPhonemizer* phonemizer, 
                                     const char* dic_path);

// Main phonemization function
OPENJTALK_API int openjtalk_phonemizer_phonemize(OpenJTalkPhonemizer* phonemizer,
                                    const char* text,
                                    PhonemeInfo* phonemes,
                                    int max_phonemes);

// Simple ID-only version (pyopenjtalk compatible)
OPENJTALK_API int openjtalk_phonemizer_get_ids(OpenJTalkPhonemizer* phonemizer,
                                  const char* text,
                                  int* phoneme_ids,
                                  int max_phonemes);

// Get phoneme string representation
OPENJTALK_API const char* openjtalk_phonemizer_get_phoneme_string(PhonemeID id);

// Error handling
OPENJTALK_API const char* openjtalk_phonemizer_get_error(OpenJTalkPhonemizer* phonemizer);

// Utility functions
OPENJTALK_API bool openjtalk_phonemizer_set_option(OpenJTalkPhonemizer* phonemizer,
                                      const char* key,
                                      const char* value);

#ifdef __cplusplus
}
#endif

#endif // OPENJTALK_PHONEMIZER_H