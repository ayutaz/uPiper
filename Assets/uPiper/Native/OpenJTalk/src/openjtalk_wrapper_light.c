#include "openjtalk_wrapper.h"
#include "openjtalk_phonemizer.h"
#include <stdlib.h>
#include <string.h>
#include <stdio.h>

#define VERSION "2.0.0-light"

// Internal structure to hold phonemizer and error state
typedef struct {
    OpenJTalkPhonemizer* phonemizer;
    int last_error;
    PhonemeResult last_result;
    char* phoneme_buffer;
    int* phoneme_id_buffer;
    float* duration_buffer;
    size_t buffer_size;
} OpenJTalkHandle;

// Version information
const char* openjtalk_get_version(void) {
    return VERSION;
}

// Create phonemizer instance
void* openjtalk_create(const char* dict_path) {
    OpenJTalkHandle* handle = (OpenJTalkHandle*)calloc(1, sizeof(OpenJTalkHandle));
    if (!handle) {
        return NULL;
    }
    
    handle->phonemizer = openjtalk_phonemizer_create();
    if (!handle->phonemizer) {
        free(handle);
        return NULL;
    }
    
    // Initialize phonemizer (dictionary path is optional)
    if (!openjtalk_phonemizer_initialize(handle->phonemizer, dict_path)) {
        handle->last_error = OPENJTALK_ERROR_INITIALIZATION_FAILED;
        openjtalk_phonemizer_destroy(handle->phonemizer);
        free(handle);
        return NULL;
    }
    
    // Allocate initial buffers
    handle->buffer_size = 1024;
    handle->phoneme_buffer = (char*)calloc(handle->buffer_size, sizeof(char));
    handle->phoneme_id_buffer = (int*)calloc(handle->buffer_size, sizeof(int));
    handle->duration_buffer = (float*)calloc(handle->buffer_size, sizeof(float));
    
    if (!handle->phoneme_buffer || !handle->phoneme_id_buffer || !handle->duration_buffer) {
        handle->last_error = OPENJTALK_ERROR_MEMORY_ALLOCATION;
        openjtalk_destroy(handle);
        return NULL;
    }
    
    handle->last_error = OPENJTALK_SUCCESS;
    return handle;
}

// Destroy phonemizer instance
void openjtalk_destroy(void* handle) {
    if (!handle) return;
    
    OpenJTalkHandle* h = (OpenJTalkHandle*)handle;
    
    if (h->phonemizer) {
        openjtalk_phonemizer_destroy(h->phonemizer);
    }
    
    free(h->phoneme_buffer);
    free(h->phoneme_id_buffer);
    free(h->duration_buffer);
    free(h);
}

// Phonemization
PhonemeResult* openjtalk_phonemize(void* handle, const char* text) {
    if (!handle) {
        return NULL;
    }
    
    OpenJTalkHandle* h = (OpenJTalkHandle*)handle;
    h->last_error = OPENJTALK_SUCCESS;
    
    if (!text) {
        h->last_error = OPENJTALK_ERROR_INVALID_INPUT;
        return NULL;
    }
    
    // Get phoneme IDs
    int count = openjtalk_phonemizer_get_ids(h->phonemizer, text, 
                                              h->phoneme_id_buffer, 
                                              (int)h->buffer_size);
    
    if (count < 0) {
        h->last_error = OPENJTALK_ERROR_PHONEMIZATION_FAILED;
        return NULL;
    }
    
    // Build phoneme string
    h->phoneme_buffer[0] = '\0';
    size_t offset = 0;
    
    for (int i = 0; i < count; i++) {
        const char* phoneme_str = openjtalk_phonemizer_get_phoneme_string(
            (PhonemeID)h->phoneme_id_buffer[i]
        );
        
        if (i > 0) {
            // Add space separator
            if (offset < h->buffer_size - 1) {
                h->phoneme_buffer[offset++] = ' ';
            }
        }
        
        size_t len = strlen(phoneme_str);
        if (offset + len < h->buffer_size - 1) {
            strcpy(&h->phoneme_buffer[offset], phoneme_str);
            offset += len;
        }
        
        // Simple duration estimation (100ms per phoneme)
        h->duration_buffer[i] = 0.1f;
    }
    
    h->phoneme_buffer[offset] = '\0';
    
    // Fill result structure
    h->last_result.phonemes = h->phoneme_buffer;
    h->last_result.phoneme_ids = h->phoneme_id_buffer;
    h->last_result.phoneme_count = count;
    h->last_result.durations = h->duration_buffer;
    h->last_result.total_duration = count * 0.1f;  // Simple estimation
    
    return &h->last_result;
}

// Free result (no-op for this implementation as we use internal buffers)
void openjtalk_free_result(PhonemeResult* result) {
    // Nothing to free - using internal buffers
}

// Error handling
int openjtalk_get_last_error(void* handle) {
    if (!handle) {
        return OPENJTALK_ERROR_INVALID_HANDLE;
    }
    
    OpenJTalkHandle* h = (OpenJTalkHandle*)handle;
    return h->last_error;
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
        case OPENJTALK_ERROR_MEMORY_ALLOCATION:
            return "Memory allocation failed";
        case OPENJTALK_ERROR_DICTIONARY_NOT_FOUND:
            return "Dictionary not found";
        case OPENJTALK_ERROR_INITIALIZATION_FAILED:
            return "Initialization failed";
        case OPENJTALK_ERROR_PHONEMIZATION_FAILED:
            return "Phonemization failed";
        default:
            return "Unknown error";
    }
}

// Set options
int openjtalk_set_option(void* handle, const char* key, const char* value) {
    if (!handle || !key || !value) {
        return OPENJTALK_ERROR_INVALID_INPUT;
    }
    
    OpenJTalkHandle* h = (OpenJTalkHandle*)handle;
    
    if (openjtalk_phonemizer_set_option(h->phonemizer, key, value)) {
        h->last_error = OPENJTALK_SUCCESS;
        return OPENJTALK_SUCCESS;
    } else {
        h->last_error = OPENJTALK_ERROR_INVALID_INPUT;
        return OPENJTALK_ERROR_INVALID_INPUT;
    }
}

// Get option
const char* openjtalk_get_option(void* handle, const char* key) {
    if (!handle || !key) {
        return NULL;
    }
    
    // For now, return default values
    if (strcmp(key, "use_accent") == 0) {
        return "false";
    } else if (strcmp(key, "use_duration") == 0) {
        return "false";
    } else if (strcmp(key, "version") == 0) {
        return VERSION;
    }
    
    return NULL;
}