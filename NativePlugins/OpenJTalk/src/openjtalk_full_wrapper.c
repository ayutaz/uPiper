#include "openjtalk_wrapper.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdbool.h>

#ifdef _WIN32
#define strdup _strdup
#endif

// Android NDK might not have strdup
#ifdef ANDROID
static char* strdup(const char* s) {
    if (!s) return NULL;
    size_t len = strlen(s) + 1;
    char* dup = (char*)malloc(len);
    if (dup) {
        memcpy(dup, s, len);
    }
    return dup;
}
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

#define VERSION "3.0.0-full"

// Debug logging
#ifdef ENABLE_DEBUG_LOG
#define DEBUG_LOG(fmt, ...) fprintf(stderr, "[OpenJTalk] " fmt "\n", ##__VA_ARGS__)
#else
#define DEBUG_LOG(fmt, ...)
#endif

// OpenJTalk context structure
typedef struct {
    Mecab* mecab;
    NJD* njd;
    JPCommon* jpcommon;
    char* dict_path;
    int last_error;
    bool initialized;
    // Options storage
    double speech_rate;
    double pitch;
    double volume;
} OpenJTalkContext;

// Version information
const char* openjtalk_get_version(void) {
    return VERSION;
}

// Create phonemizer instance
void* openjtalk_create(const char* dict_path) {
    if (!dict_path) {
        return NULL;
    }
    
    OpenJTalkContext* ctx = (OpenJTalkContext*)calloc(1, sizeof(OpenJTalkContext));
    if (!ctx) {
        return NULL;
    }
    
    // Initialize default options
    ctx->speech_rate = 1.0;
    ctx->pitch = 0.0;
    ctx->volume = 1.0;
    
    // Store dictionary path
    ctx->dict_path = strdup(dict_path);
    if (!ctx->dict_path) {
        free(ctx);
        return NULL;
    }
    
    // Initialize Mecab
    ctx->mecab = (Mecab*)calloc(1, sizeof(Mecab));
    if (!ctx->mecab) {
        free(ctx->dict_path);
        free(ctx);
        return NULL;
    }
    
    if (Mecab_initialize(ctx->mecab) != TRUE) {
        free(ctx->mecab);
        free(ctx->dict_path);
        free(ctx);
        return NULL;
    }
    
    if (Mecab_load(ctx->mecab, ctx->dict_path) != TRUE) {
        Mecab_clear(ctx->mecab);
        free(ctx->mecab);
        free(ctx->dict_path);
        free(ctx);
        return NULL;
    }
    
    // Initialize NJD
    ctx->njd = (NJD*)calloc(1, sizeof(NJD));
    if (!ctx->njd) {
        Mecab_clear(ctx->mecab);
        free(ctx->mecab);
        free(ctx->dict_path);
        free(ctx);
        return NULL;
    }
    NJD_initialize(ctx->njd);
    
    // Initialize JPCommon
    ctx->jpcommon = (JPCommon*)calloc(1, sizeof(JPCommon));
    if (!ctx->jpcommon) {
        NJD_clear(ctx->njd);
        free(ctx->njd);
        Mecab_clear(ctx->mecab);
        free(ctx->mecab);
        free(ctx->dict_path);
        free(ctx);
        return NULL;
    }
    JPCommon_initialize(ctx->jpcommon);
    
    ctx->initialized = true;
    ctx->last_error = OPENJTALK_SUCCESS;
    
    DEBUG_LOG("OpenJTalk initialized with dictionary: %s", dict_path);
    
    return ctx;
}

// Destroy phonemizer instance
void openjtalk_destroy(void* handle) {
    if (!handle) {
        return;
    }
    
    OpenJTalkContext* ctx = (OpenJTalkContext*)handle;
    
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

// Convert JPCommon labels to phonemes
static PhonemeResult* labels_to_phonemes(OpenJTalkContext* ctx, JPCommon* jpcommon) {
    PhonemeResult* result = (PhonemeResult*)calloc(1, sizeof(PhonemeResult));
    if (!result) {
        return NULL;
    }
    
    // Get label data from JPCommon
    int label_size = JPCommon_get_label_size(jpcommon);
    char** label_feature = JPCommon_get_label_feature(jpcommon);
    
    if (label_size <= 0 || !label_feature) {
        free(result);
        return NULL;
    }
    
    // First pass: count phonemes and build phoneme string
    char phoneme_buffer[8192] = {0};
    char* buf_ptr = phoneme_buffer;
    int phoneme_count = 0;
    
    for (int i = 0; i < label_size; i++) {
        if (!label_feature[i]) continue;
        
        DEBUG_LOG("Label[%d]: %s", i, label_feature[i]);
        
        // Extract phoneme from full-context label
        // Full-context label format: xx^xx-phoneme+xx=xx/A:...
        // We need to extract the phoneme between '-' and '+'
        char* phoneme_start = strchr(label_feature[i], '-');
        char* phoneme_end = strchr(label_feature[i], '+');
        
        if (phoneme_start && phoneme_end && phoneme_start < phoneme_end) {
            phoneme_start++; // Skip the '-' character
            int phoneme_len = (int)(phoneme_end - phoneme_start);
            
            // Extract phoneme to a temp buffer for logging
            char phoneme_tmp[32] = {0};
            strncpy(phoneme_tmp, phoneme_start, (phoneme_len < 31) ? phoneme_len : 31);
            DEBUG_LOG("  Extracted phoneme: '%s' (len=%d)", phoneme_tmp, phoneme_len);
            
            // Handle silence phonemes
            if (strncmp(phoneme_start, "sil", phoneme_len) == 0) {
                // Only add pau at the beginning and end
                if (i == 0 || i == label_size - 1) {
                    if (buf_ptr != phoneme_buffer) {
                        *buf_ptr++ = ' ';
                    }
                    strcpy(buf_ptr, "pau");
                    buf_ptr += 3;
                    phoneme_count++;
                    DEBUG_LOG("  Added 'pau' for silence");
                }
            } else {
                // Add the phoneme
                if (buf_ptr != phoneme_buffer) {
                    *buf_ptr++ = ' ';
                }
                strncpy(buf_ptr, phoneme_start, phoneme_len);
                buf_ptr += phoneme_len;
                phoneme_count++;
            }
        } else {
            DEBUG_LOG("  WARNING: Could not extract phoneme from label");
        }
    }
    
    // Null-terminate the phoneme string
    *buf_ptr = '\0';
    
    DEBUG_LOG("Extracted phonemes: %s (count: %d)", phoneme_buffer, phoneme_count);
    
    // Log first 10 phonemes individually for debugging
    char* debug_ptr = phoneme_buffer;
    for (int i = 0; i < 10 && i < phoneme_count; i++) {
        char phoneme[32] = {0};
        sscanf(debug_ptr, "%s", phoneme);
        DEBUG_LOG("  Phoneme[%d]: '%s'", i, phoneme);
        debug_ptr = strchr(debug_ptr, ' ');
        if (debug_ptr) debug_ptr++;
        else break;
    }
    
#ifdef _WIN32
    // Additional Windows-specific debugging
    DEBUG_LOG("Windows-specific debugging:");
    DEBUG_LOG("  Phoneme buffer length: %zu", strlen(phoneme_buffer));
    unsigned int checksum = 0;
    for (const char* p = phoneme_buffer; *p; p++) {
        checksum = checksum * 31 + (unsigned char)*p;
    }
    DEBUG_LOG("  C checksum: %u", checksum);
    
    // Check for common Windows-specific issues
    if (phoneme_count > 0 && strlen(phoneme_buffer) > phoneme_count * 10) {
        DEBUG_LOG("  WARNING: Suspiciously long phoneme buffer for phoneme count");
    }
#endif
    
    // Allocate result arrays
    result->phoneme_count = phoneme_count;
    result->phonemes = strdup(phoneme_buffer);
    result->phoneme_ids = (int*)calloc(phoneme_count, sizeof(int));
    result->durations = (float*)calloc(phoneme_count, sizeof(float));
    
    // Add a simple checksum for debugging (outside Windows block)
#ifndef _WIN32
    unsigned int checksum = 0;
    for (char* p = phoneme_buffer; *p; p++) {
        checksum = checksum * 31 + (unsigned char)*p;
    }
    DEBUG_LOG("Phoneme buffer checksum: %u", checksum);
#endif
    
    if (!result->phonemes || !result->phoneme_ids || !result->durations) {
        openjtalk_free_result(result);
        return NULL;
    }
    
    // IMPORTANT: Phoneme Duration Design Decision (Phase 1.10)
    // ======================================================
    // We intentionally use fixed 50ms durations for all phonemes in this implementation.
    // 
    // Why this is OK:
    // 1. Piper uses VITS neural model with built-in Duration Predictor
    // 2. The VITS model automatically re-estimates phoneme durations during inference
    // 3. Our fixed durations are only used as rough guidance, not final timing
    // 4. The important part is correct phoneme sequence, not precise timing
    //
    // Traditional OpenJTalk approach (NOT used here):
    // - Would use HTS Engine to estimate durations statistically
    // - Requires acoustic models and additional complexity
    // - Not necessary for neural TTS like Piper
    //
    // If you need precise phoneme timing for other purposes:
    // - Consider integrating HTS Engine for duration estimation
    // - Or implement rule-based duration estimation
    // - Typical Japanese phoneme durations: consonants 30-50ms, vowels 60-100ms, 
    //   long vowels 150-200ms, geminate consonants 100-150ms
    //
    // Reference: https://github.com/rhasspy/piper uses VITS architecture
    // which handles timing internally via Duration Predictor module
    
    // Set default values for phoneme IDs and durations
    for (int i = 0; i < phoneme_count; i++) {
        result->phoneme_ids[i] = 1; // Default non-silence ID
        result->durations[i] = 0.05f; // Default 50ms duration (see above comment)
    }
    
    // Calculate total duration (rough estimate only)
    result->total_duration = phoneme_count * 0.05f;
    
    return result;
}

// Phonemize text
PhonemeResult* openjtalk_phonemize(void* handle, const char* text) {
    if (!handle || !text) {
        return NULL;
    }
    
    OpenJTalkContext* ctx = (OpenJTalkContext*)handle;
    
    if (!ctx->initialized) {
        ctx->last_error = OPENJTALK_ERROR_INITIALIZATION_FAILED;
        return NULL;
    }
    
    DEBUG_LOG("Phonemizing text: %s", text);
    DEBUG_LOG("Text length: %zu", strlen(text));
    
#ifdef _WIN32
    // Windows-specific input debugging
    DEBUG_LOG("Windows input debugging:");
    DEBUG_LOG("  First 10 bytes (hex):");
    const unsigned char* bytes = (const unsigned char*)text;
    for (int i = 0; i < 10 && i < strlen(text); i++) {
        fprintf(stderr, "  [%d] 0x%02X '%c'\n", i, bytes[i], 
                (bytes[i] >= 32 && bytes[i] < 127) ? bytes[i] : '?');
    }
#endif
    
    // Clear previous data
    NJD_clear(ctx->njd);
    JPCommon_clear(ctx->jpcommon);
    
    // Convert text to Mecab input
    char mecab_text[8192];
    text2mecab(mecab_text, text);
    
    DEBUG_LOG("After text2mecab: %s", mecab_text);
    DEBUG_LOG("Mecab text length: %zu", strlen(mecab_text));
    
    // Mecab analysis
    if (Mecab_analysis(ctx->mecab, mecab_text) != TRUE) {
        ctx->last_error = OPENJTALK_ERROR_PHONEMIZATION_FAILED;
        DEBUG_LOG("Mecab analysis failed");
        return NULL;
    }
    
    DEBUG_LOG("Mecab analysis succeeded");
    
    // Convert Mecab to NJD
    mecab2njd(ctx->njd, Mecab_get_feature(ctx->mecab), Mecab_get_size(ctx->mecab));
    
    // NJD processing pipeline
    njd_set_pronunciation(ctx->njd);
    njd_set_digit(ctx->njd);
    njd_set_accent_phrase(ctx->njd);
    njd_set_accent_type(ctx->njd);
    njd_set_unvoiced_vowel(ctx->njd);
    njd_set_long_vowel(ctx->njd);
    
    // Convert NJD to JPCommon
    njd2jpcommon(ctx->jpcommon, ctx->njd);
    
    // Make label
    JPCommon_make_label(ctx->jpcommon);
    
    // Convert labels to phonemes
    PhonemeResult* result = labels_to_phonemes(ctx, ctx->jpcommon);
    
    if (!result) {
        ctx->last_error = OPENJTALK_ERROR_MEMORY_ALLOCATION;
        return NULL;
    }
    
    DEBUG_LOG("Phonemization complete: %d phonemes", result->phoneme_count);
    
    ctx->last_error = OPENJTALK_SUCCESS;
    return result;
}

// Free phoneme result
void openjtalk_free_result(PhonemeResult* result) {
    if (!result) return;
    
    if (result->phonemes) free(result->phonemes);
    if (result->phoneme_ids) free(result->phoneme_ids);
    if (result->durations) free(result->durations);
    free(result);
}

// Get last error
int openjtalk_get_last_error(void* handle) {
    if (!handle) {
        return OPENJTALK_ERROR_INVALID_HANDLE;
    }
    
    OpenJTalkContext* ctx = (OpenJTalkContext*)handle;
    return ctx->last_error;
}

// Get error string
const char* openjtalk_get_error_string(int error_code) {
    switch (error_code) {
        case OPENJTALK_SUCCESS:
            return "Success";
        case OPENJTALK_ERROR_INVALID_HANDLE:
            return "Invalid handle";
        case OPENJTALK_ERROR_INVALID_INPUT:
            return "Invalid input";
        case OPENJTALK_ERROR_INITIALIZATION_FAILED:
            return "Initialization failed";
        case OPENJTALK_ERROR_DICTIONARY_NOT_FOUND:
            return "Dictionary not found";
        case OPENJTALK_ERROR_MEMORY_ALLOCATION:
            return "Memory allocation failed";
        case OPENJTALK_ERROR_PHONEMIZATION_FAILED:
            return "Phonemization failed";
        case OPENJTALK_ERROR_PROCESSING:
            return "Processing error";
        case OPENJTALK_ERROR_INVALID_OPTION:
            return "Invalid option";
        case OPENJTALK_ERROR_INVALID_DICTIONARY:
            return "Invalid dictionary";
        case OPENJTALK_ERROR_INVALID_UTF8:
            return "Invalid UTF-8";
        default:
            return "Unknown error";
    }
}

// Set option
int openjtalk_set_option(void* handle, const char* key, const char* value) {
    if (!handle || !key || !value) {
        return OPENJTALK_ERROR_INVALID_INPUT;
    }
    
    OpenJTalkContext* ctx = (OpenJTalkContext*)handle;
    
    // Parse and set options
    if (strcmp(key, "speech_rate") == 0) {
        double rate = atof(value);
        if (rate > 0.0 && rate <= 10.0) {
            ctx->speech_rate = rate;
            return OPENJTALK_SUCCESS;
        }
        return OPENJTALK_ERROR_INVALID_INPUT;
    }
    else if (strcmp(key, "pitch") == 0) {
        double pitch = atof(value);
        if (pitch >= -20.0 && pitch <= 20.0) {
            ctx->pitch = pitch;
            return OPENJTALK_SUCCESS;
        }
        return OPENJTALK_ERROR_INVALID_INPUT;
    }
    else if (strcmp(key, "volume") == 0) {
        double volume = atof(value);
        if (volume >= 0.0 && volume <= 2.0) {
            ctx->volume = volume;
            return OPENJTALK_SUCCESS;
        }
        return OPENJTALK_ERROR_INVALID_INPUT;
    }
    
    // Unknown option
    return OPENJTALK_ERROR_INVALID_INPUT;
}

// Get option
const char* openjtalk_get_option(void* handle, const char* key) {
    if (!handle || !key) {
        return NULL;
    }
    
    OpenJTalkContext* ctx = (OpenJTalkContext*)handle;
    static char buffer[32];  // Static buffer for return value
    
    // Return formatted option values
    if (strcmp(key, "speech_rate") == 0) {
        snprintf(buffer, sizeof(buffer), "%.2f", ctx->speech_rate);
        return buffer;
    }
    else if (strcmp(key, "pitch") == 0) {
        snprintf(buffer, sizeof(buffer), "%.2f", ctx->pitch);
        return buffer;
    }
    else if (strcmp(key, "volume") == 0) {
        snprintf(buffer, sizeof(buffer), "%.2f", ctx->volume);
        return buffer;
    }
    
    // Unknown option
    return NULL;
}

// ============================================================================
// UTF-8 Optimized Functions for Android Performance
// ============================================================================

// Initialize with UTF-8 byte array (avoids string marshalling overhead)
void* openjtalk_initialize_utf8(const unsigned char* dict_path_utf8, int path_length) {
    if (!dict_path_utf8 || path_length <= 0) {
        DEBUG_LOG("Invalid UTF-8 dictionary path");
        return NULL;
    }
    
    // Create null-terminated string from UTF-8 bytes
    char* dict_path = (char*)malloc(path_length + 1);
    if (!dict_path) {
        DEBUG_LOG("Failed to allocate memory for path");
        return NULL;
    }
    
    memcpy(dict_path, dict_path_utf8, path_length);
    dict_path[path_length] = '\0';
    
    // Call regular create function
    void* handle = openjtalk_create(dict_path);
    
    free(dict_path);
    return handle;
}

// Alias for compatibility
void* openjtalk_initialize(const char* dict_path) {
    return openjtalk_create(dict_path);
}

// Analyze with UTF-8 byte array (avoids string marshalling overhead)
char* openjtalk_analyze_utf8(void* handle, const unsigned char* text_utf8, int text_length) {
    if (!handle || !text_utf8 || text_length <= 0) {
        DEBUG_LOG("Invalid parameters for UTF-8 analyze");
        return NULL;
    }
    
    // Create null-terminated string from UTF-8 bytes
    char* text = (char*)malloc(text_length + 1);
    if (!text) {
        DEBUG_LOG("Failed to allocate memory for text");
        return NULL;
    }
    
    memcpy(text, text_utf8, text_length);
    text[text_length] = '\0';
    
    // Call phonemize and convert to string format
    PhonemeResult* phoneme_result = openjtalk_phonemize(handle, text);
    free(text);
    
    if (!phoneme_result) {
        return NULL;
    }
    
    // Convert PhonemeResult to string format for compatibility
    // Format: "phoneme1 phoneme2 phoneme3..."
    char* result = strdup(phoneme_result->phonemes);
    openjtalk_free_result(phoneme_result);
    
    return result;
}

// Simple analyze function for compatibility
char* openjtalk_analyze(void* handle, const char* text) {
    PhonemeResult* phoneme_result = openjtalk_phonemize(handle, text);
    if (!phoneme_result) {
        return NULL;
    }
    
    char* result = strdup(phoneme_result->phonemes);
    openjtalk_free_result(phoneme_result);
    
    return result;
}

// Free string result (for legacy analyze function)
void openjtalk_free_string(char* result) {
    if (result) {
        free(result);
    }
}

// Finalize (alias for destroy)
void openjtalk_finalize(void* handle) {
    openjtalk_destroy(handle);
}