#ifndef PHONEME_CONVERTER_H
#define PHONEME_CONVERTER_H

#include <stdint.h>
#include <stdbool.h>
#include "mecab_full.h"

#ifdef __cplusplus
extern "C" {
#endif

// Phoneme structure
typedef struct {
    char phoneme[16];      // Phoneme symbol
    uint32_t duration_ms;  // Duration in milliseconds
    uint8_t accent_type;   // Accent type (0: none, 1: high, 2: low)
    uint8_t stress_level;  // Stress level (0-3)
} Phoneme;

// Phoneme sequence
typedef struct {
    Phoneme* phonemes;
    uint32_t count;
    uint32_t capacity;
} PhonemeSequence;

// Create/destroy phoneme sequence
PhonemeSequence* phoneme_sequence_create(uint32_t initial_capacity);
void phoneme_sequence_destroy(PhonemeSequence* seq);

// Add phoneme to sequence
bool phoneme_sequence_add(PhonemeSequence* seq, const char* phoneme, 
                          uint32_t duration_ms, uint8_t accent_type, uint8_t stress_level);

// Convert morpheme to phonemes
typedef struct PhonemeConverter PhonemeConverter;

// Create/destroy converter
PhonemeConverter* phoneme_converter_create(void);
void phoneme_converter_destroy(PhonemeConverter* converter);

// Convert MecabFullNode list to phoneme sequence
PhonemeSequence* phoneme_converter_convert(PhonemeConverter* converter, 
                                           MecabFullNode* nodes);

// Get phoneme string representation (for debugging)
const char* phoneme_sequence_to_string(PhonemeSequence* seq, char* buffer, size_t buffer_size);

// Japanese-specific phoneme mappings
typedef struct {
    const char* kana;      // Hiragana/Katakana
    const char* phoneme;   // Phoneme representation
} KanaPhonemeMap;

// Get phoneme for kana character
const char* get_phoneme_for_kana(const char* kana);

// Convert reading to phoneme string
bool convert_reading_to_phonemes(const char* reading, PhonemeSequence* seq);

#ifdef __cplusplus
}
#endif

#endif // PHONEME_CONVERTER_H