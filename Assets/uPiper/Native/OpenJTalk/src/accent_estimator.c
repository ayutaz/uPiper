#include "accent_estimator.h"
#include <stdlib.h>
#include <string.h>
#include <stdio.h>

struct AccentEstimator {
    // Simple rule-based estimator for now
    // Could be extended with dictionary or ML model
    int dummy;  // Placeholder
};

// Create estimator
AccentEstimator* accent_estimator_create(void) {
    AccentEstimator* estimator = (AccentEstimator*)calloc(1, sizeof(AccentEstimator));
    return estimator;
}

// Destroy estimator
void accent_estimator_destroy(AccentEstimator* estimator) {
    if (estimator) {
        free(estimator);
    }
}

// Count moras in katakana reading
int accent_estimator_count_moras(const char* reading) {
    if (!reading) return 0;
    
    int mora_count = 0;
    size_t len = strlen(reading);
    
    for (size_t i = 0; i < len; ) {
        unsigned char c = (unsigned char)reading[i];
        
        // Skip non-katakana characters
        if (c < 0x80) {
            i++;
            continue;
        }
        
        // UTF-8 handling
        int char_len = 1;
        if ((c & 0xE0) == 0xC0) char_len = 2;
        else if ((c & 0xF0) == 0xE0) char_len = 3;
        else if ((c & 0xF8) == 0xF0) char_len = 4;
        
        if (i + char_len > len) break;
        
        // Check for katakana
        if (char_len == 3) {
            // Katakana range: U+30A0 - U+30FF
            if (c == 0xE3 && (unsigned char)reading[i+1] == 0x83) {
                unsigned char c3 = (unsigned char)reading[i+2];
                
                // Small kana (ャュョァィゥェォ) don't count as separate moras
                if (c3 == 0xA3 || c3 == 0xA5 || c3 == 0xA7 ||  // ャュョ
                    c3 == 0xA1 || c3 == 0xA3 || c3 == 0xA5 ||  // ァィゥ
                    c3 == 0xA7 || c3 == 0xA9) {                // ェォ
                    // Don't count
                } else if (c3 == 0xBC) {  // ー (long vowel mark)
                    mora_count++;
                } else if (c3 >= 0xA0 && c3 <= 0xBF) {
                    mora_count++;
                }
            } else if (c == 0xE3 && (unsigned char)reading[i+1] == 0x82) {
                unsigned char c3 = (unsigned char)reading[i+2];
                if (c3 >= 0xA1 && c3 <= 0xBF) {
                    mora_count++;
                }
            }
        }
        
        i += char_len;
    }
    
    return mora_count;
}

// Simple rule-based accent estimation
AccentInfo accent_estimator_estimate(AccentEstimator* estimator,
                                     const char* surface,
                                     const char* reading,
                                     const char* pos,
                                     const char* pos_detail) {
    AccentInfo info = {0};
    
    if (!reading || !pos) {
        return info;
    }
    
    // Count moras
    info.mora_count = accent_estimator_count_moras(reading);
    
    // Check if particle
    if (strcmp(pos, "助詞") == 0) {
        info.is_particle = true;
        info.accent_position = 0;  // Particles are usually unaccented
        info.type = ACCENT_TYPE_FLAT;
        return info;
    }
    
    // Simple rules based on POS and mora count
    if (strcmp(pos, "動詞") == 0) {
        // Verbs: often accent on the second-to-last mora
        if (info.mora_count >= 2) {
            info.accent_position = info.mora_count - 1;
            info.type = ACCENT_TYPE_MIDDLE;
        }
    } else if (strcmp(pos, "形容詞") == 0) {
        // I-adjectives: often accent on the second-to-last mora
        if (info.mora_count >= 2) {
            info.accent_position = info.mora_count - 1;
            info.type = ACCENT_TYPE_MIDDLE;
        }
    } else if (strcmp(pos, "名詞") == 0) {
        // Nouns: various patterns
        if (pos_detail && strcmp(pos_detail, "固有名詞") == 0) {
            // Proper nouns: often flat
            info.accent_position = 0;
            info.type = ACCENT_TYPE_FLAT;
        } else if (info.mora_count == 1) {
            // Single mora nouns: often accented
            info.accent_position = 1;
            info.type = ACCENT_TYPE_HEAD_HIGH;
        } else if (info.mora_count == 2) {
            // Two mora nouns: 50/50 chance
            // Simple heuristic: if ends with ン, likely flat
            if (strstr(reading, "ン") == reading + strlen(reading) - 3) {
                info.accent_position = 0;
                info.type = ACCENT_TYPE_FLAT;
            } else {
                info.accent_position = 1;
                info.type = ACCENT_TYPE_HEAD_HIGH;
            }
        } else {
            // Longer nouns: often accent on second or third mora
            info.accent_position = 2;
            info.type = ACCENT_TYPE_MIDDLE;
        }
    } else if (strcmp(pos, "副詞") == 0) {
        // Adverbs: often flat
        info.accent_position = 0;
        info.type = ACCENT_TYPE_FLAT;
    } else {
        // Default: flat accent
        info.accent_position = 0;
        info.type = ACCENT_TYPE_FLAT;
    }
    
    // Ensure accent position is valid
    if (info.accent_position > info.mora_count) {
        info.accent_position = info.mora_count;
    }
    
    return info;
}

// Get accent type name
const char* accent_estimator_get_type_name(AccentType type) {
    switch (type) {
        case ACCENT_TYPE_FLAT:
            return "平板型";
        case ACCENT_TYPE_HEAD_HIGH:
            return "頭高型";
        case ACCENT_TYPE_MIDDLE:
            return "中高型";
        case ACCENT_TYPE_TAIL_HIGH:
            return "尾高型";
        default:
            return "不明";
    }
}