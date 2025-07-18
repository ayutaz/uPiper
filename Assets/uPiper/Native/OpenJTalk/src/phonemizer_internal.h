#ifndef PHONEMIZER_INTERNAL_H
#define PHONEMIZER_INTERNAL_H

#include "openjtalk_wrapper.h"
#include <stdbool.h>

// Internal phonemizer structure
typedef struct {
    void* mecab;              // Mecab instance (future)
    void* njd;                // NJD instance (future)
    void* jpcommon;           // JPCommon instance (future)
    char* dict_path;          // Dictionary path
    int last_error;           // Last error code
    void* memory_pool;        // Memory pool for allocations
    bool initialized;         // Initialization flag
} OpenJTalkPhonemizer;

// Internal functions
bool phonemizer_init(OpenJTalkPhonemizer* phonemizer, const char* dict_path);
void phonemizer_cleanup(OpenJTalkPhonemizer* phonemizer);
PhonemeResult* phonemizer_process(OpenJTalkPhonemizer* phonemizer, const char* text);

// Phoneme mapping structure
typedef struct {
    const char* grapheme;
    const char* phoneme;
    int phoneme_id;
} PhonemeMapping;

// Get phoneme mappings
const PhonemeMapping* get_phoneme_mappings(void);
int get_phoneme_mapping_count(void);

#endif // PHONEMIZER_INTERNAL_H