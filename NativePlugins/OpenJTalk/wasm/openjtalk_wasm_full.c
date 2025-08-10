/**
 * OpenJTalk Full WebAssembly Implementation
 * 完全な辞書ベース実装（Windows/Android版と同等）
 * 
 * This implementation uses the full OpenJTalk library with MeCab dictionary
 * for proper Japanese text-to-phoneme conversion.
 */

#include <emscripten.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

// Forward declarations for OpenJTalk functions
// Since we're linking with the libraries, we just need the declarations

// MeCab structures and functions
typedef void Mecab;  // Opaque pointer
extern void Mecab_initialize(Mecab* m);
extern void Mecab_clear(Mecab* m);
extern void Mecab_refresh(Mecab* m);
extern int Mecab_load(Mecab* m, const char* dicdir);
extern int Mecab_analysis(Mecab* m, const char* str);
extern int Mecab_get_size(Mecab* m);
extern char** Mecab_get_feature(Mecab* m);
extern Mecab* Mecab_new();
extern void Mecab_delete(Mecab* m);

// NJD structures and functions
typedef void NJD;  // Opaque pointer
extern void NJD_initialize(NJD* njd);
extern void NJD_clear(NJD* njd);
extern void NJD_refresh(NJD* njd);
extern NJD* NJD_new();
extern void NJD_delete(NJD* njd);

// JPCommon structures and functions
typedef void JPCommon;  // Opaque pointer
extern void JPCommon_initialize(JPCommon* jpcommon);
extern void JPCommon_clear(JPCommon* jpcommon);
extern void JPCommon_refresh(JPCommon* jpcommon);
extern void JPCommon_make_label(JPCommon* jpcommon);
extern int JPCommon_get_label_size(JPCommon* jpcommon);
extern char** JPCommon_get_label_feature(JPCommon* jpcommon);
extern JPCommon* JPCommon_new();
extern void JPCommon_delete(JPCommon* jpcommon);

// Conversion functions
extern void text2mecab(char* output, const char* input);
extern void mecab2njd(NJD* njd, char** feature, int size);
extern void njd_set_pronunciation(NJD* njd);
extern void njd_set_digit(NJD* njd);
extern void njd_set_accent_phrase(NJD* njd);
extern void njd_set_accent_type(NJD* njd);
extern void njd_set_unvoiced_vowel(NJD* njd);
extern void njd_set_long_vowel(NJD* njd);
extern void njd2jpcommon(JPCommon* jpcommon, NJD* njd);

#define TRUE 1
#define FALSE 0

// グローバル状態
static Mecab* g_mecab = NULL;
static NJD* g_njd = NULL;
static JPCommon* g_jpcommon = NULL;
static int g_initialized = 0;
static char* g_dict_path = NULL;

/**
 * Initialize OpenJTalk
 * @return 0 on success, -1 on error
 */
EMSCRIPTEN_KEEPALIVE
int Open_JTalk_initialize() {
    printf("[OpenJTalk WASM] Initializing...\n");
    
    if (g_initialized) {
        printf("[OpenJTalk WASM] Already initialized\n");
        return 0;
    }
    
    // Create Mecab
    g_mecab = Mecab_new();
    if (!g_mecab) {
        printf("[OpenJTalk WASM] Failed to create Mecab\n");
        return -1;
    }
    Mecab_initialize(g_mecab);
    
    // Create NJD
    g_njd = NJD_new();
    if (!g_njd) {
        printf("[OpenJTalk WASM] Failed to create NJD\n");
        Mecab_delete(g_mecab);
        g_mecab = NULL;
        return -1;
    }
    NJD_initialize(g_njd);
    
    // Create JPCommon
    g_jpcommon = JPCommon_new();
    if (!g_jpcommon) {
        printf("[OpenJTalk WASM] Failed to create JPCommon\n");
        NJD_delete(g_njd);
        g_njd = NULL;
        Mecab_delete(g_mecab);
        g_mecab = NULL;
        return -1;
    }
    JPCommon_initialize(g_jpcommon);
    
    g_initialized = 1;
    printf("[OpenJTalk WASM] Initialized successfully\n");
    return 0;
}

/**
 * Load dictionary
 * @param dict_path Path to dictionary directory
 * @return 0 on success, -1 on error
 */
EMSCRIPTEN_KEEPALIVE
int Open_JTalk_load(const char* dict_path) {
    printf("[OpenJTalk WASM] Loading dictionary from: %s\n", dict_path ? dict_path : "NULL");
    
    if (!g_initialized || !g_mecab) {
        printf("[OpenJTalk WASM] Not initialized\n");
        return -1;
    }
    
    // Use default path if not provided
    const char* path = dict_path ? dict_path : "/dict";
    
    // Store dictionary path
    if (g_dict_path) {
        free(g_dict_path);
    }
    g_dict_path = strdup(path);
    
    // Load MeCab dictionary
    if (Mecab_load(g_mecab, path) != TRUE) {
        printf("[OpenJTalk WASM] Failed to load dictionary from: %s\n", path);
        return -1;
    }
    
    printf("[OpenJTalk WASM] Dictionary loaded successfully from: %s\n", path);
    return 0;
}

/**
 * Convert text to phonemes (main function)
 * @param text Input Japanese text (UTF-8)
 * @param output Output buffer for phonemes
 * @param output_size Size of output buffer
 * @return Number of characters written, or -1 on error
 */
EMSCRIPTEN_KEEPALIVE
int Open_JTalk_synthesis(const char* text, char* output, int output_size) {
    if (!g_initialized || !text || !output || output_size <= 0) {
        printf("[OpenJTalk WASM] Invalid parameters or not initialized\n");
        return -1;
    }
    
    if (!g_mecab || !g_njd || !g_jpcommon) {
        printf("[OpenJTalk WASM] Components not initialized\n");
        return -1;
    }
    
    printf("[OpenJTalk WASM] Processing text: %s (length: %zu)\n", text, strlen(text));
    
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
    printf("[OpenJTalk WASM] MeCab analysis succeeded\n");
    
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
 * Clear and free resources
 */
EMSCRIPTEN_KEEPALIVE
void Open_JTalk_clear() {
    printf("[OpenJTalk WASM] Clearing resources...\n");
    
    if (g_jpcommon) {
        JPCommon_clear(g_jpcommon);
        JPCommon_delete(g_jpcommon);
        g_jpcommon = NULL;
    }
    
    if (g_njd) {
        NJD_clear(g_njd);
        NJD_delete(g_njd);
        g_njd = NULL;
    }
    
    if (g_mecab) {
        Mecab_clear(g_mecab);
        Mecab_delete(g_mecab);
        g_mecab = NULL;
    }
    
    if (g_dict_path) {
        free(g_dict_path);
        g_dict_path = NULL;
    }
    
    g_initialized = 0;
    printf("[OpenJTalk WASM] Cleared\n");
}

/**
 * Helper function to allocate memory from JavaScript
 */
EMSCRIPTEN_KEEPALIVE
void* allocate_memory(int size) {
    return malloc(size);
}

/**
 * Helper function to free memory from JavaScript
 */
EMSCRIPTEN_KEEPALIVE
void free_memory(void* ptr) {
    if (ptr) {
        free(ptr);
    }
}

/**
 * Helper function to get string length
 */
EMSCRIPTEN_KEEPALIVE
int get_string_length(const char* str) {
    return str ? strlen(str) : 0;
}

/**
 * Get version information
 */
EMSCRIPTEN_KEEPALIVE
const char* get_version() {
    return "OpenJTalk WASM Full 1.11";
}

/**
 * Test function for debugging
 */
EMSCRIPTEN_KEEPALIVE
int test_function() {
    printf("[OpenJTalk WASM] Test function called\n");
    return 42;
}