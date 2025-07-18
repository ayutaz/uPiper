#ifndef PHONEME_TIMING_H
#define PHONEME_TIMING_H

#include "openjtalk_phonemizer.h"
#include "accent_estimator.h"

#ifdef __cplusplus
extern "C" {
#endif

// Timing calculator structure
typedef struct PhonemeTimingCalculator PhonemeTimingCalculator;

// Create/destroy calculator
PhonemeTimingCalculator* phoneme_timing_create(void);
void phoneme_timing_destroy(PhonemeTimingCalculator* calculator);

// Calculate duration for a phoneme
float phoneme_timing_calculate_duration(PhonemeTimingCalculator* calculator,
                                        PhonemeID phoneme_id,
                                        PhonemeID prev_phoneme,
                                        PhonemeID next_phoneme,
                                        int position_in_mora,
                                        int mora_position,
                                        int total_moras,
                                        AccentInfo* accent_info);

// Calculate durations for a phoneme sequence
void phoneme_timing_calculate_sequence(PhonemeTimingCalculator* calculator,
                                       PhonemeInfo* phonemes,
                                       int phoneme_count,
                                       AccentInfo* accent_info);

// Get default duration for a phoneme type
float phoneme_timing_get_default_duration(PhonemeID phoneme_id);

#ifdef __cplusplus
}
#endif

#endif // PHONEME_TIMING_H