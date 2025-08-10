/**
 * OpenJTalk WebAssembly Wrapper
 * This file wraps the actual OpenJTalk implementation
 */

#include <emscripten.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

// Include actual OpenJTalk headers
#include "mecab.h"
#include "njd.h"
#include "jpcommon.h"
#include "text2mecab.h"
#include "mecab2njd.h"
#include "njd_set_pronunciation.h"
#include "njd_set_digit.h"
#include "njd_set_accent_phrase.h"
#include "njd_set_accent_type.h"
#include "njd_set_unvoiced_vowel.h"
#include "njd_set_long_vowel.h"
#include "njd2jpcommon.h"

// Global OpenJTalk objects
static Mecab mecab_obj;
static NJD njd_obj;
static JPCommon jpcommon_obj;
static Mecab* g_mecab = NULL;
static NJD* g_njd = NULL;
static JPCommon* g_jpcommon = NULL;
static int g_initialized = 0;

/**
 * Initialize OpenJTalk
 */
EMSCRIPTEN_KEEPALIVE
int Open_JTalk_initialize() {
    printf("[OpenJTalk WASM] Initializing...\n");
    
    if (g_initialized) {
        printf("[OpenJTalk WASM] Already initialized\n");
        return 0;
    }
    
    // Initialize structures
    g_mecab = &mecab_obj;
    g_njd = &njd_obj;
    g_jpcommon = &jpcommon_obj;
    
    Mecab_initialize(g_mecab);
    NJD_initialize(g_njd);
    JPCommon_initialize(g_jpcommon);
    
    g_initialized = 1;
    printf("[OpenJTalk WASM] Initialized successfully\n");
    return 0;
}

/**
 * Load dictionary
 */
EMSCRIPTEN_KEEPALIVE
int Open_JTalk_load(const char* dict_path) {
    printf("[OpenJTalk WASM] Loading dictionary from: %s\n", dict_path ? dict_path : "NULL");
    
    if (!g_initialized || !g_mecab) {
        printf("[OpenJTalk WASM] Not initialized\n");
        return -1;
    }
    
    const char* path = dict_path ? dict_path : "/dict";
    
    if (Mecab_load(g_mecab, path) != TRUE) {
        printf("[OpenJTalk WASM] Failed to load dictionary from: %s\n", path);
        return -1;
    }
    
    printf("[OpenJTalk WASM] Dictionary loaded successfully\n");
    return 0;
}

/**
 * Convert text to phonemes
 */
EMSCRIPTEN_KEEPALIVE
int Open_JTalk_synthesis(const char* text, char* output, int output_size) {
    if (!g_initialized || !text || !output || output_size <= 0) {
        printf("[OpenJTalk WASM] Invalid parameters or not initialized\n");
        return -1;
    }
    
    printf("[OpenJTalk WASM] Processing text: %s\n", text);
    
    // Clear previous data
    Mecab_refresh(g_mecab);
    NJD_refresh(g_njd);
    JPCommon_refresh(g_jpcommon);
    
    // Convert text to MeCab format
    char buff[8192];
    text2mecab(buff, text);
    printf("[OpenJTalk WASM] After text2mecab: %s\n", buff);
    
    // MeCab analysis
    if (Mecab_analysis(g_mecab, buff) != TRUE) {
        printf("[OpenJTalk WASM] MeCab analysis failed\n");
        return -1;
    }
    
    // Get MeCab features
    int mecab_size = Mecab_get_size(g_mecab);
    char** mecab_features = Mecab_get_feature(g_mecab);
    
    if (mecab_size <= 0 || !mecab_features) {
        printf("[OpenJTalk WASM] No MeCab features found\n");
        return -1;
    }
    
    // Convert MeCab to NJD
    mecab2njd(g_njd, mecab_features, mecab_size);
    
    // NJD processing pipeline
    njd_set_pronunciation(g_njd);
    njd_set_digit(g_njd);
    njd_set_accent_phrase(g_njd);
    njd_set_accent_type(g_njd);
    njd_set_unvoiced_vowel(g_njd);
    njd_set_long_vowel(g_njd);
    
    // Convert NJD to JPCommon
    njd2jpcommon(g_jpcommon, g_njd);
    
    // Make label
    JPCommon_make_label(g_jpcommon);
    
    // Extract phonemes from labels
    int label_size = JPCommon_get_label_size(g_jpcommon);
    char** labels = JPCommon_get_label_feature(g_jpcommon);
    
    printf("[OpenJTalk WASM] Label size: %d\n", label_size);
    
    output[0] = '\0';
    int output_len = 0;
    
    for (int i = 0; i < label_size; i++) {
        if (!labels[i]) continue;
        
        // Extract phoneme from full-context label
        char* p1 = strchr(labels[i], '-');
        char* p2 = strchr(labels[i], '+');
        
        if (p1 && p2 && p1 < p2) {
            p1++; // Skip '-'
            int len = p2 - p1;
            
            // Skip silence at beginning and end
            if (strncmp(p1, "sil", len) == 0) {
                if (i == 0 || i == label_size - 1) {
                    if (output_len > 0) {
                        strcat(output, " ");
                        output_len++;
                    }
                    strcat(output, "pau");
                    output_len += 3;
                }
            } else {
                // Add phoneme
                if (output_len > 0) {
                    strcat(output, " ");
                    output_len++;
                }
                strncat(output, p1, len);
                output_len += len;
            }
            
            // Check buffer overflow
            if (output_len >= output_size - 10) {
                printf("[OpenJTalk WASM] Output buffer full\n");
                break;
            }
        }
    }
    
    printf("[OpenJTalk WASM] Phonemes: %s\n", output);
    return output_len;
}

/**
 * Clear resources
 */
EMSCRIPTEN_KEEPALIVE
void Open_JTalk_clear() {
    printf("[OpenJTalk WASM] Clearing resources...\n");
    
    if (g_jpcommon) {
        JPCommon_clear(g_jpcommon);
    }
    
    if (g_njd) {
        NJD_clear(g_njd);
    }
    
    if (g_mecab) {
        Mecab_clear(g_mecab);
    }
    
    g_initialized = 0;
    printf("[OpenJTalk WASM] Cleared\n");
}

/**
 * Helper functions
 */
EMSCRIPTEN_KEEPALIVE
void* allocate_memory(int size) {
    return malloc(size);
}

EMSCRIPTEN_KEEPALIVE
void free_memory(void* ptr) {
    if (ptr) free(ptr);
}

EMSCRIPTEN_KEEPALIVE
int get_string_length(const char* str) {
    return str ? strlen(str) : 0;
}

EMSCRIPTEN_KEEPALIVE
const char* get_version() {
    return "OpenJTalk WASM Full 1.11";
}