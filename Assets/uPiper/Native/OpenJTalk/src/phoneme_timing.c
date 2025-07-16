#include "phoneme_timing.h"
#include <stdlib.h>
#include <string.h>
#include <math.h>

struct PhonemeTimingCalculator {
    float speech_rate;  // 1.0 = normal, 0.5 = slow, 2.0 = fast
};

// Default durations for different phoneme types (in seconds)
static const float DEFAULT_DURATIONS[] = {
    // Special
    0.200f,  // PHONEME_PAU (pause)
    0.100f,  // PHONEME_SIL (silence)
    
    // Vowels (longer duration)
    0.080f,  // PHONEME_A
    0.075f,  // PHONEME_I
    0.080f,  // PHONEME_U
    0.075f,  // PHONEME_E
    0.080f,  // PHONEME_O
    
    // Consonants (shorter duration)
    0.050f,  // PHONEME_K
    0.045f,  // PHONEME_G
    0.055f,  // PHONEME_S
    0.060f,  // PHONEME_SH
    0.050f,  // PHONEME_Z
    0.045f,  // PHONEME_T
    0.055f,  // PHONEME_CH
    0.060f,  // PHONEME_TS
    0.045f,  // PHONEME_D
    0.065f,  // PHONEME_N
    0.050f,  // PHONEME_H
    0.055f,  // PHONEME_F
    0.045f,  // PHONEME_B
    0.045f,  // PHONEME_P
    0.065f,  // PHONEME_M
    0.055f,  // PHONEME_Y
    0.060f,  // PHONEME_R
    0.055f,  // PHONEME_W
    
    // Special moras
    0.070f,  // PHONEME_NY
    0.070f,  // PHONEME_HY
    0.070f,  // PHONEME_MY
    0.070f,  // PHONEME_RY
    0.070f,  // PHONEME_GY
    0.070f,  // PHONEME_BY
    0.070f,  // PHONEME_PY
    
    // Others
    0.040f,  // PHONEME_CL (closure)
    0.055f,  // PHONEME_V
    0.040f,  // PHONEME_Q (glottal stop)
    0.050f,  // PHONEME_J
    0.055f,  // PHONEME_DY
    0.055f,  // PHONEME_TY
};

// Create calculator
PhonemeTimingCalculator* phoneme_timing_create(void) {
    PhonemeTimingCalculator* calc = (PhonemeTimingCalculator*)calloc(1, sizeof(PhonemeTimingCalculator));
    if (calc) {
        calc->speech_rate = 1.0f;
    }
    return calc;
}

// Destroy calculator
void phoneme_timing_destroy(PhonemeTimingCalculator* calculator) {
    if (calculator) {
        free(calculator);
    }
}

// Get default duration
float phoneme_timing_get_default_duration(PhonemeID phoneme_id) {
    if (phoneme_id >= 0 && phoneme_id < PHONEME_COUNT) {
        return DEFAULT_DURATIONS[phoneme_id];
    }
    return 0.050f;  // Default fallback
}

// Calculate duration for a single phoneme
float phoneme_timing_calculate_duration(PhonemeTimingCalculator* calculator,
                                        PhonemeID phoneme_id,
                                        PhonemeID prev_phoneme,
                                        PhonemeID next_phoneme,
                                        int position_in_mora,
                                        int mora_position,
                                        int total_moras,
                                        AccentInfo* accent_info) {
    if (!calculator) return 0.050f;
    
    // Start with base duration
    float duration = phoneme_timing_get_default_duration(phoneme_id);
    
    // Apply speech rate
    duration /= calculator->speech_rate;
    
    // Vowel lengthening rules
    if (phoneme_id >= PHONEME_A && phoneme_id <= PHONEME_O) {
        // Vowels are longer at phrase end
        if (mora_position == total_moras - 1) {
            duration *= 1.2f;
        }
        
        // Accented vowels are slightly longer
        if (accent_info && accent_info->accent_position == mora_position + 1) {
            duration *= 1.1f;
        }
    }
    
    // Consonant adjustments
    if (phoneme_id >= PHONEME_K && phoneme_id <= PHONEME_W) {
        // Consonants before vowels
        if (next_phoneme >= PHONEME_A && next_phoneme <= PHONEME_O) {
            duration *= 0.9f;  // Slightly shorter
        }
        
        // Geminate consonants (doubled)
        if (prev_phoneme == PHONEME_CL) {
            duration *= 1.5f;  // Longer for geminate
        }
    }
    
    // Pause adjustments
    if (phoneme_id == PHONEME_PAU) {
        // Shorter pause within utterance
        if (mora_position > 0 && mora_position < total_moras - 1) {
            duration *= 0.5f;
        }
    }
    
    // N sound adjustments
    if (phoneme_id == PHONEME_N) {
        // Moraic N is longer
        if (position_in_mora == 0) {
            duration *= 1.3f;
        }
    }
    
    return duration;
}

// Calculate durations for a sequence
void phoneme_timing_calculate_sequence(PhonemeTimingCalculator* calculator,
                                       PhonemeInfo* phonemes,
                                       int phoneme_count,
                                       AccentInfo* accent_info) {
    if (!calculator || !phonemes || phoneme_count <= 0) return;
    
    // First pass: assign mora positions
    int current_mora = 0;
    for (int i = 0; i < phoneme_count; i++) {
        phonemes[i].mora_position = current_mora;
        
        // Check if this phoneme ends a mora
        PhonemeID id = phonemes[i].id;
        if (id >= PHONEME_A && id <= PHONEME_O) {
            // Vowel ends a mora
            current_mora++;
        } else if (id == PHONEME_N && i < phoneme_count - 1 && 
                   phonemes[i+1].id != PHONEME_Y) {
            // Moraic N
            current_mora++;
        } else if (id == PHONEME_PAU || id == PHONEME_SIL) {
            // Don't count pauses as moras
        }
    }
    
    int total_moras = current_mora;
    
    // Second pass: calculate durations
    for (int i = 0; i < phoneme_count; i++) {
        PhonemeID prev = (i > 0) ? phonemes[i-1].id : PHONEME_SIL;
        PhonemeID next = (i < phoneme_count - 1) ? phonemes[i+1].id : PHONEME_SIL;
        
        float duration = phoneme_timing_calculate_duration(
            calculator,
            phonemes[i].id,
            prev,
            next,
            0,  // position_in_mora (simplified)
            phonemes[i].mora_position,
            total_moras,
            accent_info
        );
        
        phonemes[i].duration = duration;
    }
    
    // Apply accent information
    if (accent_info && accent_info->accent_position > 0) {
        for (int i = 0; i < phoneme_count; i++) {
            if (phonemes[i].mora_position == accent_info->accent_position - 1) {
                phonemes[i].accent_type = 1;  // High pitch
            } else if (phonemes[i].mora_position >= accent_info->accent_position) {
                phonemes[i].accent_type = -1;  // Low pitch
            } else {
                phonemes[i].accent_type = 0;  // Normal pitch
            }
        }
    }
}