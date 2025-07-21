#include "openjtalk_wrapper.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#ifdef _WIN32
#define strdup _strdup
#endif

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

// Enable debug logging
#define ENABLE_DEBUG_LOG 1
#define DEBUG_LOG(fmt, ...) fprintf(stderr, "[OpenJTalk Debug] " fmt "\n", ##__VA_ARGS__)

typedef struct {
    Mecab* mecab;
    NJD* njd;
    JPCommon* jpcommon;
    char* dict_path;
    int last_error;
} OpenJTalkDebugContext;

void* openjtalk_debug_create(const char* dict_path) {
    if (!dict_path) {
        DEBUG_LOG("Error: dict_path is NULL");
        return NULL;
    }
    
    DEBUG_LOG("Creating OpenJTalk debug instance with dictionary: %s", dict_path);
    
    OpenJTalkDebugContext* ctx = (OpenJTalkDebugContext*)calloc(1, sizeof(OpenJTalkDebugContext));
    if (!ctx) return NULL;
    
    ctx->dict_path = strdup(dict_path);
    
    // Initialize components
    ctx->mecab = (Mecab*)calloc(1, sizeof(Mecab));
    ctx->njd = (NJD*)calloc(1, sizeof(NJD));
    ctx->jpcommon = (JPCommon*)calloc(1, sizeof(JPCommon));
    
    if (!ctx->mecab || !ctx->njd || !ctx->jpcommon) {
        DEBUG_LOG("Error: Failed to allocate memory for components");
        free(ctx);
        return NULL;
    }
    
    // Initialize Mecab
    if (Mecab_initialize(ctx->mecab) != TRUE) {
        DEBUG_LOG("Error: Failed to initialize Mecab");
        free(ctx);
        return NULL;
    }
    
    if (Mecab_load(ctx->mecab, ctx->dict_path) != TRUE) {
        DEBUG_LOG("Error: Failed to load Mecab dictionary from %s", ctx->dict_path);
        free(ctx);
        return NULL;
    }
    
    NJD_initialize(ctx->njd);
    JPCommon_initialize(ctx->jpcommon);
    
    DEBUG_LOG("Successfully created OpenJTalk debug instance");
    return ctx;
}

void openjtalk_debug_analyze(void* handle, const char* text) {
    if (!handle || !text) return;
    
    OpenJTalkDebugContext* ctx = (OpenJTalkDebugContext*)handle;
    
    DEBUG_LOG("========================================");
    DEBUG_LOG("Analyzing text: '%s'", text);
    DEBUG_LOG("Text length: %zu", strlen(text));
    
    // Show UTF-8 bytes
    DEBUG_LOG("UTF-8 bytes:");
    const unsigned char* bytes = (const unsigned char*)text;
    for (size_t i = 0; i < strlen(text); i++) {
        fprintf(stderr, "%02X ", bytes[i]);
    }
    fprintf(stderr, "\n");
    
    // Clear previous data
    NJD_clear(ctx->njd);
    JPCommon_clear(ctx->jpcommon);
    
    // Convert text to Mecab input
    char mecab_text[8192];
    text2mecab(mecab_text, text);
    DEBUG_LOG("After text2mecab: '%s'", mecab_text);
    DEBUG_LOG("Mecab text length: %zu", strlen(mecab_text));
    
    // Show mecab_text bytes
    DEBUG_LOG("Mecab text bytes:");
    bytes = (const unsigned char*)mecab_text;
    for (size_t i = 0; i < strlen(mecab_text) && i < 100; i++) {
        fprintf(stderr, "%02X ", bytes[i]);
    }
    fprintf(stderr, "\n");
    
    // Mecab analysis
    if (Mecab_analysis(ctx->mecab, mecab_text) != TRUE) {
        DEBUG_LOG("Error: Mecab analysis failed");
        return;
    }
    
    DEBUG_LOG("Mecab analysis succeeded");
    
    // Show Mecab results
    int mecab_size = Mecab_get_size(ctx->mecab);
    char** mecab_features = Mecab_get_feature(ctx->mecab);
    
    DEBUG_LOG("Mecab results: %d nodes", mecab_size);
    for (int i = 0; i < mecab_size && i < 20; i++) {
        if (mecab_features && mecab_features[i]) {
            DEBUG_LOG("  Node[%d]: %s", i, mecab_features[i]);
        }
    }
    
    // Convert Mecab to NJD
    mecab2njd(ctx->njd, mecab_features, mecab_size);
    
    // NJD processing
    njd_set_pronunciation(ctx->njd);
    njd_set_digit(ctx->njd);
    njd_set_accent_phrase(ctx->njd);
    njd_set_accent_type(ctx->njd);
    njd_set_unvoiced_vowel(ctx->njd);
    njd_set_long_vowel(ctx->njd);
    
    // Convert to JPCommon
    njd2jpcommon(ctx->jpcommon, ctx->njd);
    JPCommon_make_label(ctx->jpcommon);
    
    // Show phoneme results
    int label_size = JPCommon_get_label_size(ctx->jpcommon);
    char** labels = JPCommon_get_label_feature(ctx->jpcommon);
    
    DEBUG_LOG("Label results: %d labels", label_size);
    for (int i = 0; i < label_size && i < 20; i++) {
        if (labels && labels[i]) {
            DEBUG_LOG("  Label[%d]: %s", i, labels[i]);
            
            // Extract phoneme
            char* phoneme_start = strchr(labels[i], '-');
            char* phoneme_end = strchr(labels[i], '+');
            if (phoneme_start && phoneme_end && phoneme_start < phoneme_end) {
                phoneme_start++;
                int len = (int)(phoneme_end - phoneme_start);
                char phoneme[32] = {0};
                strncpy(phoneme, phoneme_start, (len < 31) ? len : 31);
                DEBUG_LOG("    -> Phoneme: '%s'", phoneme);
            }
        }
    }
    
    DEBUG_LOG("========================================\n");
}

void openjtalk_debug_destroy(void* handle) {
    if (!handle) return;
    
    OpenJTalkDebugContext* ctx = (OpenJTalkDebugContext*)handle;
    
    if (ctx->jpcommon) {
        JPCommon_clear(ctx->jpcommon);
        free(ctx->jpcommon);
    }
    if (ctx->njd) {
        NJD_clear(ctx->njd);
        free(ctx->njd);
    }
    if (ctx->mecab) {
        Mecab_clear(ctx->mecab);
        free(ctx->mecab);
    }
    if (ctx->dict_path) {
        free(ctx->dict_path);
    }
    
    free(ctx);
}

// Test program
int main(int argc, char* argv[]) {
    if (argc < 2) {
        fprintf(stderr, "Usage: %s <dictionary_path> [text]\n", argv[0]);
        return 1;
    }
    
    const char* dict_path = argv[1];
    const char* test_text = (argc > 2) ? argv[2] : "今日はいい天気ですね";
    
    void* handle = openjtalk_debug_create(dict_path);
    if (!handle) {
        fprintf(stderr, "Failed to create OpenJTalk instance\n");
        return 1;
    }
    
    // Test various texts
    const char* test_cases[] = {
        "今日はいい天気ですね",
        "今日は",
        "いい天気",
        "天気ですね",
        "こんにちは",
        "日本語",
        NULL
    };
    
    if (argc > 2) {
        // Use command line text
        openjtalk_debug_analyze(handle, test_text);
    } else {
        // Test all cases
        for (int i = 0; test_cases[i]; i++) {
            openjtalk_debug_analyze(handle, test_cases[i]);
        }
    }
    
    openjtalk_debug_destroy(handle);
    return 0;
}