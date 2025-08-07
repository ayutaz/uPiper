#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <emscripten.h>

// Correct OpenJTalk headers
#include <jpcommon.h>
#include <mecab.h>
#include <njd.h>
#include <text2mecab.h>
#include <mecab2njd.h>
#include <njd_set_pronunciation.h>
#include <njd_set_digit.h>
#include <njd_set_accent_phrase.h>
#include <njd_set_accent_type.h>
#include <njd_set_unvoiced_vowel.h>
#include <njd_set_long_vowel.h>
#include <njd2jpcommon.h>

// Global instances
static Mecab mecab;
static NJD njd;
static JPCommon jpcommon;
static int initialized = 0;

// Initialize OpenJTalk with dictionary path
EMSCRIPTEN_KEEPALIVE
int openjtalk_initialize(const char* dict_path) {
    if (initialized) {
        return 1;
    }
    
    // Initialize MeCab
    if (!Mecab_initialize(&mecab)) {
        printf("[OpenJTalk] Failed to initialize MeCab\n");
        return -1;
    }
    
    // Load dictionary
    if (!Mecab_load(&mecab, dict_path)) {
        printf("[OpenJTalk] Failed to load dictionary from: %s\n", dict_path);
        Mecab_clear(&mecab);
        return -2;
    }
    
    // Initialize NJD
    NJD_initialize(&njd);
    
    // Initialize JPCommon
    JPCommon_initialize(&jpcommon);
    
    initialized = 1;
    printf("[OpenJTalk] Initialized successfully\n");
    return 0;
}

// Convert text to phonemes
EMSCRIPTEN_KEEPALIVE
char* openjtalk_text_to_phonemes(const char* text) {
    if (!initialized) {
        return strdup("ERROR: Not initialized");
    }
    
    char buff[8192];
    text2mecab(buff, text);
    
    // Clear previous data
    NJD_clear(&njd);
    JPCommon_clear(&jpcommon);
    
    // Process through MeCab
    if (!Mecab_analysis(&mecab, buff)) {
        return strdup("ERROR: MeCab analysis failed");
    }
    
    // Convert to NJD
    mecab2njd(&njd, Mecab_get_feature(&mecab), Mecab_get_size(&mecab));
    
    // Apply NJD processing
    njd_set_pronunciation(&njd);
    njd_set_digit(&njd);
    njd_set_accent_phrase(&njd);
    njd_set_accent_type(&njd);
    njd_set_unvoiced_vowel(&njd);
    njd_set_long_vowel(&njd);
    
    // Convert to JPCommon
    njd2jpcommon(&jpcommon, &njd);
    
    // Extract phonemes
    char* result = (char*)malloc(8192);
    memset(result, 0, 8192);
    
    int total_time = 0;
    const int frame_period = 50000; // 50ms in microseconds
    
    strcat(result, "0 ");
    strcat(result, "50000 sil\n");
    total_time = 50000;
    
    // Get phoneme sequence
    for (int i = 0; i < JPCommon_get_label_size(&jpcommon); i++) {
        char* label = JPCommon_get_label_feature(&jpcommon, i);
        
        // Extract phoneme from label
        char phoneme[32];
        char* p = strstr(label, "-");
        if (p && *(p+1) != '\0') {
            char* q = strstr(p+1, "+");
            if (q) {
                int len = q - (p+1);
                if (len > 0 && len < 31) {
                    strncpy(phoneme, p+1, len);
                    phoneme[len] = '\0';
                    
                    // Add to result
                    char line[256];
                    sprintf(line, "%d %d %s\n", total_time, total_time + frame_period, phoneme);
                    strcat(result, line);
                    total_time += frame_period;
                }
            }
        }
    }
    
    // Add final silence
    char line[256];
    sprintf(line, "%d %d sil\n", total_time, total_time + 50000);
    strcat(result, line);
    
    return result;
}

// Get phoneme array (JSON format)
EMSCRIPTEN_KEEPALIVE
char* openjtalk_get_phonemes_json(const char* text) {
    if (!initialized) {
        return strdup("{\"success\":false,\"error\":\"Not initialized\",\"phonemes\":[]}");
    }
    
    char buff[8192];
    text2mecab(buff, text);
    
    // Clear previous data
    NJD_clear(&njd);
    JPCommon_clear(&jpcommon);
    
    // Process through MeCab
    if (!Mecab_analysis(&mecab, buff)) {
        return strdup("{\"success\":false,\"error\":\"MeCab analysis failed\",\"phonemes\":[]}");
    }
    
    // Convert to NJD
    mecab2njd(&njd, Mecab_get_feature(&mecab), Mecab_get_size(&mecab));
    
    // Apply NJD processing
    njd_set_pronunciation(&njd);
    njd_set_digit(&njd);
    njd_set_accent_phrase(&njd);
    njd_set_accent_type(&njd);
    njd_set_unvoiced_vowel(&njd);
    njd_set_long_vowel(&njd);
    
    // Convert to JPCommon
    njd2jpcommon(&jpcommon, &njd);
    
    // Build JSON result
    char* result = (char*)malloc(16384);
    strcpy(result, "{\"success\":true,\"phonemes\":[\"^\",");
    
    // Get phoneme sequence
    for (int i = 0; i < JPCommon_get_label_size(&jpcommon); i++) {
        char* label = JPCommon_get_label_feature(&jpcommon, i);
        
        // Extract phoneme from label
        char* p = strstr(label, "-");
        if (p && *(p+1) != '\0') {
            char* q = strstr(p+1, "+");
            if (q) {
                int len = q - (p+1);
                if (len > 0 && len < 31) {
                    char phoneme[32];
                    strncpy(phoneme, p+1, len);
                    phoneme[len] = '\0';
                    
                    // Handle multi-character phonemes
                    if (strcmp(phoneme, "cl") == 0) {
                        strcat(result, "\"\ue002\",");
                    } else if (strcmp(phoneme, "ch") == 0) {
                        strcat(result, "\"\ue001\",");
                    } else if (strcmp(phoneme, "ts") == 0) {
                        strcat(result, "\"\ue00f\",");
                    } else if (strcmp(phoneme, "sh") == 0) {
                        strcat(result, "\"\ue00c\",");
                    } else if (strcmp(phoneme, "ky") == 0) {
                        strcat(result, "\"\ue006\",");
                    } else if (strcmp(phoneme, "gy") == 0) {
                        strcat(result, "\"\ue004\",");
                    } else if (strcmp(phoneme, "ny") == 0) {
                        strcat(result, "\"\ue009\",");
                    } else if (strcmp(phoneme, "hy") == 0) {
                        strcat(result, "\"\ue005\",");
                    } else if (strcmp(phoneme, "by") == 0) {
                        strcat(result, "\"\ue000\",");
                    } else if (strcmp(phoneme, "py") == 0) {
                        strcat(result, "\"\ue00a\",");
                    } else if (strcmp(phoneme, "my") == 0) {
                        strcat(result, "\"\ue008\",");
                    } else if (strcmp(phoneme, "ry") == 0) {
                        strcat(result, "\"\ue00b\",");
                    } else if (strcmp(phoneme, "dy") == 0) {
                        strcat(result, "\"\ue003\",");
                    } else if (strcmp(phoneme, "ty") == 0) {
                        strcat(result, "\"\ue00e\",");
                    } else if (strcmp(phoneme, "sy") == 0) {
                        strcat(result, "\"\ue00d\",");
                    } else if (strcmp(phoneme, "zy") == 0) {
                        strcat(result, "\"\ue010\",");
                    } else {
                        strcat(result, "\"");
                        strcat(result, phoneme);
                        strcat(result, "\",");
                    }
                }
            }
        }
    }
    
    strcat(result, "\"$\"]}");
    
    return result;
}

// Free allocated string
EMSCRIPTEN_KEEPALIVE
void openjtalk_free_string(char* str) {
    if (str) {
        free(str);
    }
}

// Cleanup
EMSCRIPTEN_KEEPALIVE
void openjtalk_cleanup() {
    if (initialized) {
        JPCommon_clear(&jpcommon);
        NJD_clear(&njd);
        Mecab_clear(&mecab);
        initialized = 0;
    }
}

// Test function
EMSCRIPTEN_KEEPALIVE
int openjtalk_test(const char* text) {
    printf("[OpenJTalk] Test called with: %s\n", text);
    return strlen(text);
}