#include "openjtalk_wrapper.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdbool.h>
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
    
    // Count phonemes
    int phoneme_count = 0;
    int label_size = JPCommon_get_label_size(jpcommon);
    
    // Pre-count to allocate arrays
    char** labels = JPCommon_get_label_feature(jpcommon);
    for (int i = 0; i < label_size; i++) {
        if (labels && labels[i] && strstr(labels[i], "-") && strstr(labels[i], "+")) {
            phoneme_count++;
        }
    }
    
    // Allocate arrays
    result->phoneme_count = phoneme_count;
    result->phonemes = (char*)calloc(phoneme_count * 8 + 1, sizeof(char)); // Max phoneme length
    result->phoneme_ids = (int*)calloc(phoneme_count, sizeof(int));
    result->durations = (float*)calloc(phoneme_count, sizeof(float));
    
    if (!result->phonemes || !result->phoneme_ids || !result->durations) {
        openjtalk_free_result(result);
        return NULL;
    }
    
    // Extract phonemes from labels
    char* phoneme_ptr = result->phonemes;
    int phoneme_idx = 0;
    
    for (int i = 0; i < label_size && phoneme_idx < phoneme_count; i++) {
        if (!labels || !labels[i]) continue;
        char* label = labels[i];
        
        // Parse phoneme from label (format: xx^xx-phoneme+xx=xx/...)
        char* p_start = strchr(label, '-');
        char* p_end = strchr(label, '+');
        
        if (p_start && p_end && p_start < p_end) {
            p_start++; // Skip '-'
            size_t len = p_end - p_start;
            
            // Handle special phonemes
            if (strncmp(p_start, "sil", len) == 0) {
                strcpy(phoneme_ptr, "pau");
                result->phoneme_ids[phoneme_idx] = 0; // Silence ID
                phoneme_ptr += 3;
                *phoneme_ptr++ = ' ';
            } else if (strncmp(p_start, "pau", len) == 0) {
                strcpy(phoneme_ptr, "pau");
                result->phoneme_ids[phoneme_idx] = 0; // Pause ID
                phoneme_ptr += 3;
                *phoneme_ptr++ = ' ';
            } else {
                // Copy phoneme
                strncpy(phoneme_ptr, p_start, len);
                phoneme_ptr += len;
                *phoneme_ptr++ = ' ';
                
                // Map to ID (simplified mapping)
                result->phoneme_ids[phoneme_idx] = 1; // Default non-silence ID
            }
            
            // Set default duration
            result->durations[phoneme_idx] = 0.05f;
            phoneme_idx++;
        }
    }
    
    // Remove trailing space
    if (phoneme_ptr > result->phonemes) {
        *(phoneme_ptr - 1) = '\0';
    }
    
    // Calculate total duration
    result->total_duration = 0.0f;
    for (int i = 0; i < result->phoneme_count; i++) {
        result->total_duration += result->durations[i];
    }
    
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
    
    // Clear previous data
    NJD_clear(ctx->njd);
    JPCommon_clear(ctx->jpcommon);
    
    // Convert text to Mecab input
    char mecab_text[8192];
    text2mecab(mecab_text, text);
    
    // Mecab analysis
    if (Mecab_analysis(ctx->mecab, mecab_text) != TRUE) {
        ctx->last_error = OPENJTALK_ERROR_PHONEMIZATION_FAILED;
        DEBUG_LOG("Mecab analysis failed");
        return NULL;
    }
    
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