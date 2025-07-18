#include "openjtalk_wrapper.h"
#include "phonemizer_internal.h"
#include "memory_pool.h"
#include "error_handling.h"
#include <stdlib.h>
#include <string.h>
#include <stdio.h>

#define VERSION "2.0.0-full"

// Extended phonemizer structure with options
typedef struct {
    OpenJTalkPhonemizer base;
    
    // Options
    bool use_accent;
    bool use_duration;
    float speech_rate;
    bool auto_pad_silence;
    int phoneme_alignment_mode;
} OpenJTalkPhonemizerExt;

// Version information
const char* openjtalk_get_version(void) {
    return VERSION;
}

// Create phonemizer instance
void* openjtalk_create(const char* dict_path) {
    if (!dict_path) {
        return NULL;
    }
    
    OpenJTalkPhonemizerExt* phonemizer = (OpenJTalkPhonemizerExt*)calloc(1, sizeof(OpenJTalkPhonemizerExt));
    if (!phonemizer) {
        return NULL;
    }
    
    // Create memory pool
    phonemizer->base.memory_pool = memory_pool_create(1024 * 1024); // 1MB initial size
    if (!phonemizer->base.memory_pool) {
        free(phonemizer);
        return NULL;
    }
    
    // Initialize phonemizer
    if (!phonemizer_init(&phonemizer->base, dict_path)) {
        memory_pool_destroy(phonemizer->base.memory_pool);
        free(phonemizer);
        return NULL;
    }
    
    // Set default options
    phonemizer->use_accent = true;
    phonemizer->use_duration = true;
    phonemizer->speech_rate = 1.0f;
    phonemizer->auto_pad_silence = true;
    phonemizer->phoneme_alignment_mode = 0;
    
    phonemizer->base.initialized = true;
    return phonemizer;
}

// Destroy phonemizer instance
void openjtalk_destroy(void* handle) {
    if (!handle) {
        return;
    }
    
    OpenJTalkPhonemizerExt* phonemizer = (OpenJTalkPhonemizerExt*)handle;
    
    if (phonemizer->base.initialized) {
        phonemizer_cleanup(&phonemizer->base);
    }
    
    if (phonemizer->base.memory_pool) {
        memory_pool_destroy(phonemizer->base.memory_pool);
    }
    
    free(phonemizer);
}

// Phonemize text with options support
PhonemeResult* openjtalk_phonemize(void* handle, const char* text) {
    if (!handle) {
        return NULL;
    }
    
    OpenJTalkPhonemizerExt* phonemizer = (OpenJTalkPhonemizerExt*)handle;
    
    if (!phonemizer->base.initialized) {
        set_last_error(&phonemizer->base, OPENJTALK_ERROR_INITIALIZATION_FAILED);
        return NULL;
    }
    
    if (!text || strlen(text) == 0) {
        set_last_error(&phonemizer->base, OPENJTALK_ERROR_INVALID_INPUT);
        return NULL;
    }
    
    // Process with phonemizer
    PhonemeResult* result = phonemizer_process(&phonemizer->base, text);
    if (!result) {
        return NULL;
    }
    
    // Apply options
    if (phonemizer->speech_rate != 1.0f && result->durations) {
        // Adjust durations based on speech rate
        for (int i = 0; i < result->phoneme_count; i++) {
            result->durations[i] /= phonemizer->speech_rate;
        }
        result->total_duration /= phonemizer->speech_rate;
    }
    
    if (phonemizer->auto_pad_silence) {
        // Add silence padding at beginning and end
        // This would require modifying the result, skipping for now
    }
    
    if (!phonemizer->use_accent) {
        // Clear accent information if disabled
        // This would be handled in phoneme_converter
    }
    
    return result;
}

// Free phoneme result
void openjtalk_free_result(PhonemeResult* result) {
    // Results are allocated from memory pool, so we don't free individual results
    // The memory pool will be reset or destroyed later
    (void)result;
}

// Get last error
int openjtalk_get_last_error(void* handle) {
    if (!handle) {
        return OPENJTALK_ERROR_INVALID_HANDLE;
    }
    
    OpenJTalkPhonemizerExt* phonemizer = (OpenJTalkPhonemizerExt*)handle;
    return phonemizer->base.last_error;
}

// Get error string
const char* openjtalk_get_error_string(int error_code) {
    return get_error_string(error_code);
}

// Set option - full implementation
int openjtalk_set_option(void* handle, const char* key, const char* value) {
    if (!handle || !key || !value) {
        return OPENJTALK_ERROR_INVALID_INPUT;
    }
    
    OpenJTalkPhonemizerExt* phonemizer = (OpenJTalkPhonemizerExt*)handle;
    
    // Handle different options
    if (strcmp(key, "use_accent") == 0) {
        phonemizer->use_accent = (strcmp(value, "true") == 0 || strcmp(value, "1") == 0);
    } else if (strcmp(key, "use_duration") == 0) {
        phonemizer->use_duration = (strcmp(value, "true") == 0 || strcmp(value, "1") == 0);
    } else if (strcmp(key, "speech_rate") == 0) {
        float rate = atof(value);
        if (rate > 0.1f && rate < 10.0f) {
            phonemizer->speech_rate = rate;
        } else {
            set_last_error(&phonemizer->base, OPENJTALK_ERROR_INVALID_OPTION);
            return OPENJTALK_ERROR_INVALID_OPTION;
        }
    } else if (strcmp(key, "auto_pad_silence") == 0) {
        phonemizer->auto_pad_silence = (strcmp(value, "true") == 0 || strcmp(value, "1") == 0);
    } else if (strcmp(key, "phoneme_alignment_mode") == 0) {
        int mode = atoi(value);
        if (mode >= 0 && mode <= 2) {
            phonemizer->phoneme_alignment_mode = mode;
        } else {
            set_last_error(&phonemizer->base, OPENJTALK_ERROR_INVALID_OPTION);
            return OPENJTALK_ERROR_INVALID_OPTION;
        }
    } else {
        set_last_error(&phonemizer->base, OPENJTALK_ERROR_INVALID_OPTION);
        return OPENJTALK_ERROR_INVALID_OPTION;
    }
    
    set_last_error(&phonemizer->base, OPENJTALK_SUCCESS);
    return OPENJTALK_SUCCESS;
}

// Get option - full implementation
const char* openjtalk_get_option(void* handle, const char* key) {
    if (!handle || !key) {
        return NULL;
    }
    
    OpenJTalkPhonemizerExt* phonemizer = (OpenJTalkPhonemizerExt*)handle;
    static char buffer[32];
    
    if (strcmp(key, "use_accent") == 0) {
        return phonemizer->use_accent ? "true" : "false";
    } else if (strcmp(key, "use_duration") == 0) {
        return phonemizer->use_duration ? "true" : "false";
    } else if (strcmp(key, "speech_rate") == 0) {
        snprintf(buffer, sizeof(buffer), "%.2f", phonemizer->speech_rate);
        return buffer;
    } else if (strcmp(key, "auto_pad_silence") == 0) {
        return phonemizer->auto_pad_silence ? "true" : "false";
    } else if (strcmp(key, "phoneme_alignment_mode") == 0) {
        snprintf(buffer, sizeof(buffer), "%d", phonemizer->phoneme_alignment_mode);
        return buffer;
    }
    
    return NULL;
}