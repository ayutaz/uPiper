#ifndef ACCENT_ESTIMATOR_H
#define ACCENT_ESTIMATOR_H

#include <stdint.h>
#include <stdbool.h>

#ifdef __cplusplus
extern "C" {
#endif

// Accent types in Japanese
typedef enum {
    ACCENT_TYPE_FLAT = 0,      // 平板型 (no accent)
    ACCENT_TYPE_HEAD_HIGH = 1, // 頭高型 (accent on first mora)
    ACCENT_TYPE_MIDDLE = 2,    // 中高型 (accent in middle)
    ACCENT_TYPE_TAIL_HIGH = 3  // 尾高型 (accent on last mora)
} AccentType;

// Accent information structure
typedef struct {
    int accent_position;    // Position of accent nucleus (0 = no accent)
    int mora_count;        // Number of moras in the word
    AccentType type;       // Accent type
    bool is_particle;      // Is this a particle?
    bool is_compound;      // Is this part of a compound word?
} AccentInfo;

// Accent estimator
typedef struct AccentEstimator AccentEstimator;

// Create/destroy estimator
AccentEstimator* accent_estimator_create(void);
void accent_estimator_destroy(AccentEstimator* estimator);

// Estimate accent for a word
AccentInfo accent_estimator_estimate(AccentEstimator* estimator,
                                     const char* surface,
                                     const char* reading,
                                     const char* pos,
                                     const char* pos_detail);

// Count moras in a reading
int accent_estimator_count_moras(const char* reading);

// Get accent type name
const char* accent_estimator_get_type_name(AccentType type);

#ifdef __cplusplus
}
#endif

#endif // ACCENT_ESTIMATOR_H