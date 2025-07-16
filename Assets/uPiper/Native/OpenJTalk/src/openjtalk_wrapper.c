#include "openjtalk_wrapper.h"
#include "phonemizer_internal.h"
#include "memory_pool.h"
#include "error_handling.h"
#include <stdlib.h>
#include <string.h>
#include <stdio.h>

#define VERSION "1.0.0-stub"

// Version information
const char* openjtalk_get_version(void) {
    return VERSION;
}

// Create phonemizer instance
void* openjtalk_create(const char* dict_path) {
    if (!dict_path) {
        return NULL;
    }
    
    OpenJTalkPhonemizer* phonemizer = (OpenJTalkPhonemizer*)calloc(1, sizeof(OpenJTalkPhonemizer));
    if (!phonemizer) {
        return NULL;
    }
    
    // Create memory pool
    phonemizer->memory_pool = memory_pool_create(1024 * 1024); // 1MB initial size
    if (!phonemizer->memory_pool) {
        free(phonemizer);
        return NULL;
    }
    
    // Initialize phonemizer
    if (!phonemizer_init(phonemizer, dict_path)) {
        memory_pool_destroy(phonemizer->memory_pool);
        free(phonemizer);
        return NULL;
    }
    
    phonemizer->initialized = true;
    return phonemizer;
}

// Destroy phonemizer instance
void openjtalk_destroy(void* handle) {
    if (!handle) {
        return;
    }
    
    OpenJTalkPhonemizer* phonemizer = (OpenJTalkPhonemizer*)handle;
    
    if (phonemizer->initialized) {
        phonemizer_cleanup(phonemizer);
    }
    
    if (phonemizer->memory_pool) {
        memory_pool_destroy(phonemizer->memory_pool);
    }
    
    free(phonemizer);
}

// Phonemize text
PhonemeResult* openjtalk_phonemize(void* handle, const char* text) {
    if (!handle) {
        return NULL;
    }
    
    OpenJTalkPhonemizer* phonemizer = (OpenJTalkPhonemizer*)handle;
    
    if (!phonemizer->initialized) {
        set_last_error(phonemizer, OPENJTALK_ERROR_INITIALIZATION_FAILED);
        return NULL;
    }
    
    if (!text || strlen(text) == 0) {
        set_last_error(phonemizer, OPENJTALK_ERROR_INVALID_INPUT);
        return NULL;
    }
    
    return phonemizer_process(phonemizer, text);
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
    
    OpenJTalkPhonemizer* phonemizer = (OpenJTalkPhonemizer*)handle;
    return phonemizer->last_error;
}

// Get error string
const char* openjtalk_get_error_string(int error_code) {
    return get_error_string(error_code);
}

// Set option
int openjtalk_set_option(void* handle, const char* key, const char* value) {
    if (!handle || !key || !value) {
        return OPENJTALK_ERROR_INVALID_INPUT;
    }
    
    // Stub implementation - options will be implemented later
    OpenJTalkPhonemizer* phonemizer = (OpenJTalkPhonemizer*)handle;
    set_last_error(phonemizer, OPENJTALK_SUCCESS);
    return OPENJTALK_SUCCESS;
}

// Get option
const char* openjtalk_get_option(void* handle, const char* key) {
    if (!handle || !key) {
        return NULL;
    }
    
    // Stub implementation
    return NULL;
}