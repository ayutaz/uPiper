#include "openjtalk_phonemizer.h"
#include "phoneme_mapping.h"
#include "kanji_mapping.h"
#include "mecab_light.h"
#include "mecab_full.h"
#include "memory_pool.h"
#include <stdlib.h>
#include <string.h>
#include <stdio.h>
#include <ctype.h>

struct OpenJTalkPhonemizer {
    MecabLight* mecab_light;
    MecabFull* mecab_full;
    MemoryPool* memory_pool;
    char error_message[256];
    
    // Options
    bool use_accent;
    bool use_duration;
    bool use_full_dict;  // Use full dictionary if available
};

// UTF-8 handling
static int utf8_char_len(unsigned char c) {
    if (c < 0x80) return 1;
    if ((c & 0xE0) == 0xC0) return 2;
    if ((c & 0xF0) == 0xE0) return 3;
    if ((c & 0xF8) == 0xF0) return 4;
    return 1;
}

static bool utf8_get_char(const char* str, char* out, int* bytes_read) {
    int len = utf8_char_len((unsigned char)str[0]);
    memcpy(out, str, len);
    out[len] = '\0';
    *bytes_read = len;
    return true;
}

// Convert mora to phonemes using mapping table
static int mora_to_phonemes(const char* mora, char* phonemes, int max_len) {
    for (int i = 0; mora_rules[i].mora != NULL; i++) {
        if (strcmp(mora, mora_rules[i].mora) == 0) {
            strncpy(phonemes, mora_rules[i].phonemes, max_len - 1);
            phonemes[max_len - 1] = '\0';
            return strlen(phonemes);
        }
    }
    
    // Unknown mora - try to handle as ASCII
    if (strlen(mora) == 1 && isascii(mora[0])) {
        // Keep ASCII characters as-is (for debugging)
        phonemes[0] = '\0';
        return 0;
    }
    
    // Default to silence for unknown characters
    strncpy(phonemes, "sil", max_len - 1);
    phonemes[max_len - 1] = '\0';
    return 3;
}

// Parse phoneme string to IDs
static int parse_phoneme_string(const char* phoneme_str, PhonemeInfo* phonemes, int max_phonemes) {
    char buffer[256];
    strncpy(buffer, phoneme_str, sizeof(buffer) - 1);
    buffer[sizeof(buffer) - 1] = '\0';
    
    int count = 0;
    char* token = strtok(buffer, " ");
    
    while (token != NULL && count < max_phonemes) {
        PhonemeID id = phoneme_str_to_id(token);
        
        phonemes[count].id = id;
        phonemes[count].accent_type = 0;  // Default no accent
        phonemes[count].mora_position = count;
        phonemes[count].duration = 0.0f;
        
        count++;
        token = strtok(NULL, " ");
    }
    
    return count;
}

// Simple text normalization
static void normalize_text(const char* input, char* output, size_t output_size) {
    size_t out_pos = 0;
    size_t in_pos = 0;
    size_t in_len = strlen(input);
    
    while (in_pos < in_len && out_pos < output_size - 1) {
        unsigned char c = (unsigned char)input[in_pos];
        
        // Handle ASCII
        if (c < 0x80) {
            if (isspace(c)) {
                // Normalize whitespace
                if (out_pos > 0 && output[out_pos - 1] != ' ') {
                    output[out_pos++] = ' ';
                }
                in_pos++;
            } else if (isprint(c)) {
                // Keep printable ASCII
                output[out_pos++] = c;
                in_pos++;
            } else {
                // Skip control characters
                in_pos++;
            }
        } else {
            // Copy UTF-8 characters
            int char_len = utf8_char_len(c);
            if (out_pos + char_len < output_size - 1) {
                memcpy(&output[out_pos], &input[in_pos], char_len);
                out_pos += char_len;
                in_pos += char_len;
            } else {
                break;
            }
        }
    }
    
    // Remove trailing space
    if (out_pos > 0 && output[out_pos - 1] == ' ') {
        out_pos--;
    }
    
    output[out_pos] = '\0';
}

// Create phonemizer
OpenJTalkPhonemizer* openjtalk_phonemizer_create(void) {
    OpenJTalkPhonemizer* phonemizer = (OpenJTalkPhonemizer*)calloc(1, sizeof(OpenJTalkPhonemizer));
    if (!phonemizer) {
        return NULL;
    }
    
    phonemizer->memory_pool = memory_pool_create(1024 * 1024);  // 1MB pool
    if (!phonemizer->memory_pool) {
        free(phonemizer);
        return NULL;
    }
    
    phonemizer->use_accent = false;  // Simplified version
    phonemizer->use_duration = false;
    
    return phonemizer;
}

// Destroy phonemizer
void openjtalk_phonemizer_destroy(OpenJTalkPhonemizer* phonemizer) {
    if (!phonemizer) return;
    
    if (phonemizer->mecab_light) {
        mecab_light_destroy(phonemizer->mecab_light);
    }
    
    if (phonemizer->mecab_full) {
        mecab_full_destroy(phonemizer->mecab_full);
    }
    
    if (phonemizer->memory_pool) {
        memory_pool_destroy(phonemizer->memory_pool);
    }
    
    free(phonemizer);
}

// Initialize with dictionary
bool openjtalk_phonemizer_initialize(OpenJTalkPhonemizer* phonemizer, const char* dic_path) {
    if (!phonemizer) {
        return false;
    }
    
    // Check if dictionary path exists and contains full dictionary
    if (dic_path) {
        char sys_dic_path[1024];
        snprintf(sys_dic_path, sizeof(sys_dic_path), "%s/sys.dic", dic_path);
        
        FILE* fp = fopen(sys_dic_path, "rb");
        if (fp) {
            fclose(fp);
            // Full dictionary exists, use MecabFull
            phonemizer->mecab_full = mecab_full_create(dic_path);
            if (phonemizer->mecab_full) {
                phonemizer->use_full_dict = true;
                return true;
            }
        }
    }
    
    // Fall back to lightweight Mecab
    phonemizer->mecab_light = mecab_light_create(dic_path);
    if (!phonemizer->mecab_light) {
        snprintf(phonemizer->error_message, sizeof(phonemizer->error_message),
                 "Failed to create Mecab instance");
        return false;
    }
    
    phonemizer->use_full_dict = false;
    return true;
}

// Main phonemization function
int openjtalk_phonemizer_phonemize(OpenJTalkPhonemizer* phonemizer,
                                    const char* text,
                                    PhonemeInfo* phonemes,
                                    int max_phonemes) {
    if (!phonemizer || !text || !phonemes || max_phonemes <= 0) {
        return -1;
    }
    
    if (getenv("DEBUG_MECAB")) {
        printf("\n=== Phonemize: '%s' ===\n", text);
        printf("Using full dict: %s\n", phonemizer->use_full_dict ? "YES" : "NO");
    }
    
    // Reset memory pool
    memory_pool_reset(phonemizer->memory_pool);
    
    // Normalize text
    char normalized[2048];
    normalize_text(text, normalized, sizeof(normalized));
    
    int phoneme_count = 0;
    
    // Add initial silence
    if (phoneme_count < max_phonemes) {
        phonemes[phoneme_count].id = PHONEME_SIL;
        phonemes[phoneme_count].accent_type = 0;
        phonemes[phoneme_count].mora_position = 0;
        phonemes[phoneme_count].duration = 0.1f;
        phoneme_count++;
    }
    
    // Use Mecab for morphological analysis if available
    if (phonemizer->use_full_dict && phonemizer->mecab_full) {
        if (getenv("DEBUG_MECAB")) {
            printf("Using MecabFull to parse: '%s'\n", normalized);
        }
        MecabFullNode* nodes = mecab_full_parse(phonemizer->mecab_full, normalized);
        if (nodes) {
            if (getenv("DEBUG_MECAB")) {
                printf("MecabFull parse successful\n");
            }
            MecabFullNode* current = nodes;
            
            while (current && phoneme_count < max_phonemes - 1) {
                // Get reading from Mecab
                const char* reading = current->feature.reading;
                if (reading[0] == '\0' || strcmp(reading, "*") == 0) {
                    reading = current->surface;  // Fallback to surface
                }
                
                // Convert reading to phonemes
                size_t reading_pos = 0;
                size_t reading_len = strlen(reading);
                
                while (reading_pos < reading_len && phoneme_count < max_phonemes - 1) {
                    char current_char[5] = {0};
                    int char_len;
                    utf8_get_char(&reading[reading_pos], current_char, &char_len);
                    
                    // Convert character to phonemes
                    char phoneme_str[64];
                    mora_to_phonemes(current_char, phoneme_str, sizeof(phoneme_str));
                    
                    if (strlen(phoneme_str) > 0) {
                        PhonemeInfo temp_phonemes[10];
                        int temp_count = parse_phoneme_string(phoneme_str, temp_phonemes, 10);
                        
                        for (int j = 0; j < temp_count && phoneme_count < max_phonemes - 1; j++) {
                            phonemes[phoneme_count++] = temp_phonemes[j];
                        }
                    }
                    
                    reading_pos += char_len;
                }
                
                // Add pause between words if needed
                if (current->next && phoneme_count < max_phonemes - 1) {
                    // Check if we need a pause
                    if (strcmp(current->feature.pos, "記号") == 0) {
                        phonemes[phoneme_count].id = PHONEME_PAU;
                        phonemes[phoneme_count].accent_type = 0;
                        phonemes[phoneme_count].mora_position = phoneme_count;
                        phonemes[phoneme_count].duration = 0.2f;
                        phoneme_count++;
                    }
                }
                
                current = current->next;
            }
            
            mecab_full_free_nodes(phonemizer->mecab_full, nodes);
        } else {
            // Fallback to simple conversion if Mecab fails
            goto simple_conversion;
        }
    } else if (phonemizer->mecab_light) {
        if (getenv("DEBUG_MECAB")) {
            printf("Using MecabLight to parse: '%s'\n", normalized);
        }
        MecabNode* nodes = mecab_light_parse(phonemizer->mecab_light, normalized);
        if (nodes) {
            if (getenv("DEBUG_MECAB")) {
                printf("MecabLight parse successful\n");
            }
            MecabNode* current = nodes;
            
            while (current && phoneme_count < max_phonemes - 1) {
                // Get reading from Mecab
                const char* reading = current->feature.reading;
                if (reading[0] == '\0') {
                    reading = current->surface;  // Fallback to surface
                }
                
                // Convert reading to phonemes
                size_t reading_pos = 0;
                size_t reading_len = strlen(reading);
                
                while (reading_pos < reading_len && phoneme_count < max_phonemes - 1) {
                    char current_char[5] = {0};
                    int char_len;
                    utf8_get_char(&reading[reading_pos], current_char, &char_len);
                    
                    // Convert character to phonemes
                    char phoneme_str[64];
                    mora_to_phonemes(current_char, phoneme_str, sizeof(phoneme_str));
                    
                    if (strlen(phoneme_str) > 0) {
                        PhonemeInfo temp_phonemes[10];
                        int temp_count = parse_phoneme_string(phoneme_str, temp_phonemes, 10);
                        
                        for (int j = 0; j < temp_count && phoneme_count < max_phonemes - 1; j++) {
                            phonemes[phoneme_count++] = temp_phonemes[j];
                        }
                    }
                    
                    reading_pos += char_len;
                }
                
                // Add pause between words if needed
                if (current->next && phoneme_count < max_phonemes - 1) {
                    // Check if we need a pause
                    if (strcmp(current->feature.pos, "記号") == 0) {
                        phonemes[phoneme_count].id = PHONEME_PAU;
                        phonemes[phoneme_count].accent_type = 0;
                        phonemes[phoneme_count].mora_position = phoneme_count;
                        phonemes[phoneme_count].duration = 0.2f;
                        phoneme_count++;
                    }
                }
                
                current = current->next;
            }
            
            mecab_light_free_nodes(phonemizer->mecab_light, nodes);
        } else {
            // Fallback to simple conversion if Mecab fails
            goto simple_conversion;
        }
    } else {
        // Simple conversion without Mecab
        simple_conversion:
        {
        size_t text_pos = 0;
        size_t text_len = strlen(normalized);
        
        while (text_pos < text_len && phoneme_count < max_phonemes - 1) {
            char current_char[5] = {0};
            int char_len;
        
        // Get UTF-8 character
        utf8_get_char(&normalized[text_pos], current_char, &char_len);
        
        // Try two-character combinations first (for 拗音)
        if (text_pos + char_len < text_len) {
            char next_char[5] = {0};
            int next_len;
            utf8_get_char(&normalized[text_pos + char_len], next_char, &next_len);
            
            char combined[10];
            snprintf(combined, sizeof(combined), "%s%s", current_char, next_char);
            
            // Check if this is a valid mora combination
            bool found_combined = false;
            for (int i = 0; mora_rules[i].mora != NULL; i++) {
                if (strcmp(mora_rules[i].mora, combined) == 0) {
                    // Found combination
                    char phoneme_str[64];
                    mora_to_phonemes(combined, phoneme_str, sizeof(phoneme_str));
                    
                    PhonemeInfo temp_phonemes[10];
                    int temp_count = parse_phoneme_string(phoneme_str, temp_phonemes, 10);
                    
                    for (int j = 0; j < temp_count && phoneme_count < max_phonemes - 1; j++) {
                        phonemes[phoneme_count++] = temp_phonemes[j];
                    }
                    
                    text_pos += char_len + next_len;
                    found_combined = true;
                    break;
                }
            }
            
            if (found_combined) {
                continue;
            }
        }
        
        // Single character conversion
        if (strcmp(current_char, " ") == 0 || strcmp(current_char, "　") == 0) {
            // Add short pause for spaces
            if (phoneme_count > 0 && phonemes[phoneme_count - 1].id != PHONEME_PAU) {
                phonemes[phoneme_count].id = PHONEME_PAU;
                phonemes[phoneme_count].accent_type = 0;
                phonemes[phoneme_count].mora_position = phoneme_count;
                phonemes[phoneme_count].duration = 0.2f;
                phoneme_count++;
            }
        } else {
            // Convert mora to phonemes
            char phoneme_str[64];
            mora_to_phonemes(current_char, phoneme_str, sizeof(phoneme_str));
            
            if (strlen(phoneme_str) > 0) {
                PhonemeInfo temp_phonemes[10];
                int temp_count = parse_phoneme_string(phoneme_str, temp_phonemes, 10);
                
                for (int j = 0; j < temp_count && phoneme_count < max_phonemes - 1; j++) {
                    phonemes[phoneme_count++] = temp_phonemes[j];
                }
            }
        }
        
            text_pos += char_len;
        }
        }
    }
    
    // Add final silence
    if (phoneme_count < max_phonemes) {
        phonemes[phoneme_count].id = PHONEME_SIL;
        phonemes[phoneme_count].accent_type = 0;
        phonemes[phoneme_count].mora_position = phoneme_count;
        phonemes[phoneme_count].duration = 0.1f;
        phoneme_count++;
    }
    
    return phoneme_count;
}

// Simple ID-only version
int openjtalk_phonemizer_get_ids(OpenJTalkPhonemizer* phonemizer,
                                  const char* text,
                                  int* phoneme_ids,
                                  int max_phonemes) {
    if (!phonemizer || !text || !phoneme_ids || max_phonemes <= 0) {
        return -1;
    }
    
    // Use full phonemization and extract IDs
    PhonemeInfo* temp_phonemes = (PhonemeInfo*)memory_pool_alloc(
        phonemizer->memory_pool, 
        sizeof(PhonemeInfo) * max_phonemes
    );
    
    if (!temp_phonemes) {
        return -1;
    }
    
    int count = openjtalk_phonemizer_phonemize(phonemizer, text, temp_phonemes, max_phonemes);
    
    if (count > 0) {
        for (int i = 0; i < count; i++) {
            phoneme_ids[i] = (int)temp_phonemes[i].id;
        }
    }
    
    return count;
}

// Get phoneme string representation
const char* openjtalk_phonemizer_get_phoneme_string(PhonemeID id) {
    return phoneme_id_to_str(id);
}

// Error handling
const char* openjtalk_phonemizer_get_error(OpenJTalkPhonemizer* phonemizer) {
    if (!phonemizer) {
        return "Invalid phonemizer";
    }
    return phonemizer->error_message;
}

// Set options
bool openjtalk_phonemizer_set_option(OpenJTalkPhonemizer* phonemizer,
                                      const char* key,
                                      const char* value) {
    if (!phonemizer || !key || !value) {
        return false;
    }
    
    if (strcmp(key, "use_accent") == 0) {
        phonemizer->use_accent = (strcmp(value, "true") == 0);
        return true;
    } else if (strcmp(key, "use_duration") == 0) {
        phonemizer->use_duration = (strcmp(value, "true") == 0);
        return true;
    }
    
    snprintf(phonemizer->error_message, sizeof(phonemizer->error_message),
             "Unknown option: %s", key);
    return false;
}