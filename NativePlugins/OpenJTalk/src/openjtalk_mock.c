#include "openjtalk_wrapper.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#define VERSION "3.0.0-mock"

// Mock implementation for CI builds
// This provides the same API but returns dummy data

const char* openjtalk_get_version(void) {
    return VERSION;
}

void* openjtalk_create(const char* dict_path) {
    if (!dict_path) {
        return NULL;
    }
    // Return a non-null dummy pointer
    return (void*)0x12345678;
}

void openjtalk_destroy(void* handle) {
    // No-op
}

PhonemeResult* openjtalk_phonemize(void* handle, const char* text) {
    if (!handle || !text) {
        return NULL;
    }
    
    PhonemeResult* result = (PhonemeResult*)calloc(1, sizeof(PhonemeResult));
    if (!result) {
        return NULL;
    }
    
    // Return mock phonemes for testing
    const char* mock_phonemes = "pau k o N n i ch i w a pau";
    result->phonemes = strdup(mock_phonemes);
    result->phoneme_count = 11; // Count of phonemes in mock data
    result->phoneme_ids = (int*)calloc(result->phoneme_count, sizeof(int));
    result->durations = (float*)calloc(result->phoneme_count, sizeof(float));
    
    if (!result->phonemes || !result->phoneme_ids || !result->durations) {
        openjtalk_free_result(result);
        return NULL;
    }
    
    // Fill with dummy data
    for (int i = 0; i < result->phoneme_count; i++) {
        result->phoneme_ids[i] = i + 1;
        result->durations[i] = 0.05f;
    }
    result->total_duration = result->phoneme_count * 0.05f;
    
    return result;
}

void openjtalk_free_result(PhonemeResult* result) {
    if (!result) return;
    
    if (result->phonemes) free(result->phonemes);
    if (result->phoneme_ids) free(result->phoneme_ids);
    if (result->durations) free(result->durations);
    free(result);
}

int openjtalk_get_last_error(void* handle) {
    return OPENJTALK_SUCCESS;
}

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

int openjtalk_set_option(void* handle, const char* key, const char* value) {
    if (!handle || !key || !value) {
        return OPENJTALK_ERROR_INVALID_INPUT;
    }
    // Mock accepts all options
    return OPENJTALK_SUCCESS;
}

const char* openjtalk_get_option(void* handle, const char* key) {
    if (!handle || !key) {
        return NULL;
    }
    // Return default values
    if (strcmp(key, "speech_rate") == 0) {
        return "1.00";
    } else if (strcmp(key, "pitch") == 0) {
        return "0.00";
    } else if (strcmp(key, "volume") == 0) {
        return "1.00";
    }
    return NULL;
}