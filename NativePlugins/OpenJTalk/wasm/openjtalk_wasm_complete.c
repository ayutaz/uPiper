/**
 * OpenJTalk Complete WebAssembly Implementation
 * Based on piper-plus implementation approach
 */

#include <emscripten.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

// Forward declarations with proper structure hints
typedef struct _Mecab {
    void *feature;
    int size;
    void *model;
    void *lattice;
    void *mecab;
    char padding[8192]; // Ensure enough space
} Mecab;

typedef struct _NJD {
    void *head;
    void *tail;
    char padding[4096]; // Ensure enough space
} NJD;

typedef struct _JPCommon {
    void *label;
    int label_size;
    char padding[4096]; // Ensure enough space
} JPCommon;

// Function declarations
extern void Mecab_initialize(Mecab *m);
extern void Mecab_clear(Mecab *m);
extern void Mecab_refresh(Mecab *m);
extern int Mecab_load(Mecab *m, const char *dicdir);
extern int Mecab_analysis(Mecab *m, const char *str);
extern int Mecab_get_size(Mecab *m);
extern char** Mecab_get_feature(Mecab *m);

extern void NJD_initialize(NJD *njd);
extern void NJD_clear(NJD *njd);
extern void NJD_refresh(NJD *njd);

extern void JPCommon_initialize(JPCommon *jpcommon);
extern void JPCommon_clear(JPCommon *jpcommon);
extern void JPCommon_refresh(JPCommon *jpcommon);
extern void JPCommon_make_label(JPCommon *jpcommon);
extern int JPCommon_get_label_size(JPCommon *jpcommon);
extern char** JPCommon_get_label_feature(JPCommon *jpcommon);

extern void text2mecab(char *output, const char *input);
extern void mecab2njd(NJD *njd, char **features, int size);
extern void njd_set_pronunciation(NJD *njd);
extern void njd_set_digit(NJD *njd);
extern void njd_set_accent_phrase(NJD *njd);
extern void njd_set_accent_type(NJD *njd);
extern void njd_set_unvoiced_vowel(NJD *njd);
extern void njd_set_long_vowel(NJD *njd);
extern void njd2jpcommon(JPCommon *jpcommon, NJD *njd);

#define TRUE 1
#define FALSE 0

// Static buffers for structures
static Mecab mecab_instance;
static NJD njd_instance;
static JPCommon jpcommon_instance;
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
    memset(&mecab_instance, 0, sizeof(Mecab));
    memset(&njd_instance, 0, sizeof(NJD));
    memset(&jpcommon_instance, 0, sizeof(JPCommon));
    
    Mecab_initialize(&mecab_instance);
    NJD_initialize(&njd_instance);
    JPCommon_initialize(&jpcommon_instance);
    
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
    
    const char* path = dict_path ? dict_path : "/dict";
    
    if (Mecab_load(&mecab_instance, path) != TRUE) {
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
    Mecab_refresh(&mecab_instance);
    NJD_refresh(&njd_instance);
    JPCommon_refresh(&jpcommon_instance);
    
    // Convert text to MeCab format
    char buff[8192];
    text2mecab(buff, text);
    printf("[OpenJTalk WASM] After text2mecab: %s\n", buff);
    
    // MeCab analysis
    if (Mecab_analysis(&mecab_instance, buff) != TRUE) {
        printf("[OpenJTalk WASM] MeCab analysis failed\n");
        return -1;
    }
    printf("[OpenJTalk WASM] MeCab analysis succeeded\n");
    
    // Get MeCab features
    int mecab_size = Mecab_get_size(&mecab_instance);
    char** mecab_features = Mecab_get_feature(&mecab_instance);
    
    if (mecab_size <= 0 || !mecab_features) {
        printf("[OpenJTalk WASM] No MeCab features found\n");
        return -1;
    }
    printf("[OpenJTalk WASM] MeCab size: %d\n", mecab_size);
    
    // Convert MeCab to NJD
    mecab2njd(&njd_instance, mecab_features, mecab_size);
    
    // NJD processing pipeline
    njd_set_pronunciation(&njd_instance);
    njd_set_digit(&njd_instance);
    njd_set_accent_phrase(&njd_instance);
    njd_set_accent_type(&njd_instance);
    njd_set_unvoiced_vowel(&njd_instance);
    njd_set_long_vowel(&njd_instance);
    
    // Convert NJD to JPCommon
    njd2jpcommon(&jpcommon_instance, &njd_instance);
    
    // Make label
    JPCommon_make_label(&jpcommon_instance);
    
    // Extract phonemes from labels
    int label_size = JPCommon_get_label_size(&jpcommon_instance);
    char** labels = JPCommon_get_label_feature(&jpcommon_instance);
    
    printf("[OpenJTalk WASM] Label size: %d\n", label_size);
    
    output[0] = '\0';
    int output_len = 0;
    
    for (int i = 0; i < label_size; i++) {
        if (!labels[i]) continue;
        
        // Debug: print first few labels
        if (i < 5) {
            printf("[OpenJTalk WASM] Label[%d]: %s\n", i, labels[i]);
        }
        
        // Extract phoneme from full-context label
        // Format: xx^xx-phoneme+xx=xx/A:...
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
    printf("[OpenJTalk WASM] Phoneme count: %d characters\n", output_len);
    
    return output_len;
}

/**
 * Clear resources
 */
EMSCRIPTEN_KEEPALIVE
void Open_JTalk_clear() {
    printf("[OpenJTalk WASM] Clearing resources...\n");
    
    if (g_initialized) {
        JPCommon_clear(&jpcommon_instance);
        NJD_clear(&njd_instance);
        Mecab_clear(&mecab_instance);
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
    return "OpenJTalk WASM Complete 1.11";
}

EMSCRIPTEN_KEEPALIVE
int test_function() {
    printf("[OpenJTalk WASM] Test function called\n");
    return 42;
}