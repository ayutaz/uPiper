/**
 * OpenJTalk WebAssembly Implementation - Based on piper-plus approach
 * Using forward declarations like piper-plus to avoid header issues
 */

#include <emscripten.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

// BOOL type definition for OpenJTalk
typedef int BOOL;

// Forward declarations for Open JTalk structures
typedef struct _Mecab Mecab;
typedef struct _NJD NJD;
typedef struct _JPCommon JPCommon;

// Forward declarations for Open JTalk functions (correct signatures with BOOL)
BOOL Mecab_initialize(Mecab *m);
BOOL Mecab_load(Mecab *m, const char *dicdir);
BOOL Mecab_analysis(Mecab *m, const char *str);
int Mecab_get_size(Mecab *m);
char** Mecab_get_feature(Mecab *m);
BOOL Mecab_refresh(Mecab *m);
BOOL Mecab_clear(Mecab *m);

void NJD_initialize(NJD *njd);
void NJD_refresh(NJD *njd);
void NJD_clear(NJD *njd);

void JPCommon_initialize(JPCommon *jpcommon);
void JPCommon_refresh(JPCommon *jpcommon);
void JPCommon_clear(JPCommon *jpcommon);
void JPCommon_make_label(JPCommon *jpcommon);
int JPCommon_get_label_size(JPCommon *jpcommon);
char** JPCommon_get_label_feature(JPCommon *jpcommon);

void text2mecab(char *output, const char *input);
void mecab2njd(NJD *njd, char **features, int size);
void njd_set_pronunciation(NJD *njd);
void njd_set_digit(NJD *njd);
void njd_set_accent_phrase(NJD *njd);
void njd_set_accent_type(NJD *njd);
void njd_set_unvoiced_vowel(NJD *njd);
void njd_set_long_vowel(NJD *njd);
void njd2jpcommon(JPCommon *jpcommon, NJD *njd);

#define TRUE 1
#define FALSE 0

// Static buffers for structures (allocate enough space)
static char mecab_buf[16384];
static char njd_buf[16384];
static char jpcommon_buf[16384];
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
    
    Mecab *mecab = (Mecab*)mecab_buf;
    NJD *njd = (NJD*)njd_buf;
    JPCommon *jpcommon = (JPCommon*)jpcommon_buf;
    
    Mecab_initialize(mecab);
    NJD_initialize(njd);
    JPCommon_initialize(jpcommon);
    
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
    
    if (!g_initialized) {
        printf("[OpenJTalk WASM] Not initialized\n");
        return -1;
    }
    
    Mecab *mecab = (Mecab*)mecab_buf;
    const char* path = dict_path ? dict_path : "/dict";
    
    if (Mecab_load(mecab, path) != TRUE) {
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
    
    Mecab *mecab = (Mecab*)mecab_buf;
    NJD *njd = (NJD*)njd_buf;
    JPCommon *jpcommon = (JPCommon*)jpcommon_buf;
    
    // Clear previous data
    Mecab_refresh(mecab);
    NJD_refresh(njd);
    JPCommon_refresh(jpcommon);
    
    // Convert text to MeCab format
    char buff[8192];
    text2mecab(buff, text);
    printf("[OpenJTalk WASM] MeCab input: %s\n", buff);
    
    // MeCab analysis
    if (Mecab_analysis(mecab, buff) != TRUE) {
        printf("[OpenJTalk WASM] MeCab analysis failed\n");
        return -1;
    }
    
    // Get MeCab features
    int mecab_size = Mecab_get_size(mecab);
    char** mecab_features = Mecab_get_feature(mecab);
    
    printf("[OpenJTalk WASM] MeCab size: %d\n", mecab_size);
    
    if (mecab_size <= 0 || !mecab_features) {
        printf("[OpenJTalk WASM] No MeCab features found\n");
        return -1;
    }
    
    // Convert MeCab to NJD
    mecab2njd(njd, mecab_features, mecab_size);
    
    // NJD processing pipeline
    njd_set_pronunciation(njd);
    njd_set_digit(njd);
    njd_set_accent_phrase(njd);
    njd_set_accent_type(njd);
    njd_set_unvoiced_vowel(njd);
    njd_set_long_vowel(njd);
    
    // Convert NJD to JPCommon
    njd2jpcommon(jpcommon, njd);
    
    // Make label
    JPCommon_make_label(jpcommon);
    
    // Extract phonemes from labels
    int label_size = JPCommon_get_label_size(jpcommon);
    char** labels = JPCommon_get_label_feature(jpcommon);
    
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
    
    if (g_initialized) {
        Mecab *mecab = (Mecab*)mecab_buf;
        NJD *njd = (NJD*)njd_buf;
        JPCommon *jpcommon = (JPCommon*)jpcommon_buf;
        
        JPCommon_clear(jpcommon);
        NJD_clear(njd);
        Mecab_clear(mecab);
        g_initialized = 0;
    }
    
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
    return "OpenJTalk WASM Piper-based 1.11";
}