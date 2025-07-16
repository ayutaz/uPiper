#include "openjtalk_wrapper.h"
#include "mecab_full.h"
#include "phoneme_converter.h"
#include "openjtalk_phonemizer.h"  // For PhonemeID
#include <stdlib.h>
#include <string.h>
#include <stdio.h>

#define VERSION "2.0.0-full"

// Internal structure to hold MeCab instance and state
typedef struct {
    MecabFull* mecab;
    PhonemeConverter* converter;
    int last_error;
    PhonemeResult last_result;
    char* phoneme_buffer;
    int* phoneme_id_buffer;
    float* duration_buffer;
    size_t buffer_size;
    PhonemeSequence* last_sequence;
} OpenJTalkHandle;

// Phoneme ID mapping
typedef struct {
    const char* phoneme;
    PhonemeID id;
} PhonemeIDMap;

static const PhonemeIDMap phoneme_id_map[] = {
    {"pau", PHONEME_PAU},
    {"cl", PHONEME_CL},
    {"a", PHONEME_A}, {"i", PHONEME_I}, {"u", PHONEME_U}, {"e", PHONEME_E}, {"o", PHONEME_O},
    {"k", PHONEME_K}, {"g", PHONEME_G},
    {"s", PHONEME_S}, {"z", PHONEME_Z},
    {"t", PHONEME_T}, {"d", PHONEME_D},
    {"n", PHONEME_N}, {"N", PHONEME_N},
    {"h", PHONEME_H}, {"b", PHONEME_B}, {"p", PHONEME_P},
    {"m", PHONEME_M}, {"y", PHONEME_Y}, {"r", PHONEME_R}, {"w", PHONEME_W},
    {"f", PHONEME_F}, {"v", PHONEME_V},
    {"j", PHONEME_J}, {"q", PHONEME_Q},
    {"R", PHONEME_R},  // Long vowel
    {NULL, PHONEME_UNKNOWN}
};

// Get phoneme ID from string
static PhonemeID get_phoneme_id(const char* phoneme) {
    for (int i = 0; phoneme_id_map[i].phoneme != NULL; i++) {
        if (strcmp(phoneme_id_map[i].phoneme, phoneme) == 0) {
            return phoneme_id_map[i].id;
        }
    }
    return PHONEME_UNKNOWN;
}

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
    
    // Use dictionary path or default
    const char* dict_dir = dict_path ? dict_path : "dictionary";
    
    // Create MeCab instance
    handle->mecab = mecab_full_create(dict_dir);
    if (!handle->mecab) {
        handle->last_error = OPENJTALK_ERROR_DICTIONARY_NOT_FOUND;
        free(handle);
        return NULL;
    }
    
    // Create phoneme converter
    handle->converter = phoneme_converter_create();
    if (!handle->converter) {
        handle->last_error = OPENJTALK_ERROR_INITIALIZATION_FAILED;
        mecab_full_destroy(handle->mecab);
        free(handle);
        return NULL;
    }
    
    // Allocate initial buffers
    handle->buffer_size = 4096;
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
    
    if (h->last_sequence) {
        phoneme_sequence_destroy(h->last_sequence);
    }
    
    if (h->converter) {
        phoneme_converter_destroy(h->converter);
    }
    
    if (h->mecab) {
        mecab_full_destroy(h->mecab);
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
    
    // Parse text with MeCab
    MecabFullNode* nodes = mecab_full_parse(h->mecab, text);
    if (!nodes) {
        h->last_error = OPENJTALK_ERROR_PHONEMIZATION_FAILED;
        return NULL;
    }
    
    // Free previous sequence
    if (h->last_sequence) {
        phoneme_sequence_destroy(h->last_sequence);
        h->last_sequence = NULL;
    }
    
    // Convert to phonemes
    h->last_sequence = phoneme_converter_convert(h->converter, nodes);
    mecab_full_free_nodes(h->mecab, nodes);
    
    if (!h->last_sequence) {
        h->last_error = OPENJTALK_ERROR_PHONEMIZATION_FAILED;
        return NULL;
    }
    
    // Ensure buffer is large enough
    if (h->last_sequence->count > h->buffer_size) {
        h->buffer_size = h->last_sequence->count * 2;
        
        int* new_id_buffer = (int*)realloc(h->phoneme_id_buffer, 
                                           h->buffer_size * sizeof(int));
        float* new_duration_buffer = (float*)realloc(h->duration_buffer, 
                                                     h->buffer_size * sizeof(float));
        
        if (!new_id_buffer || !new_duration_buffer) {
            h->last_error = OPENJTALK_ERROR_MEMORY_ALLOCATION;
            return NULL;
        }
        
        h->phoneme_id_buffer = new_id_buffer;
        h->duration_buffer = new_duration_buffer;
    }
    
    // Convert to result format
    float total_duration = 0.0f;
    
    for (uint32_t i = 0; i < h->last_sequence->count; i++) {
        Phoneme* p = &h->last_sequence->phonemes[i];
        h->phoneme_id_buffer[i] = get_phoneme_id(p->phoneme);
        h->duration_buffer[i] = p->duration_ms / 1000.0f;  // Convert to seconds
        total_duration += h->duration_buffer[i];
    }
    
    // Build phoneme string
    phoneme_sequence_to_string(h->last_sequence, h->phoneme_buffer, h->buffer_size);
    
    // Fill result structure
    h->last_result.phonemes = h->phoneme_buffer;
    h->last_result.phoneme_ids = h->phoneme_id_buffer;
    h->last_result.phoneme_count = h->last_sequence->count;
    h->last_result.durations = h->duration_buffer;
    h->last_result.total_duration = total_duration;
    
    return &h->last_result;
}

// Free result (no-op for this implementation)
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
    
    // TODO: Implement option setting
    if (strcmp(key, "use_accent") == 0) {
        // Enable/disable accent processing
        h->last_error = OPENJTALK_SUCCESS;
        return OPENJTALK_SUCCESS;
    } else if (strcmp(key, "use_duration") == 0) {
        // Enable/disable duration estimation
        h->last_error = OPENJTALK_SUCCESS;
        return OPENJTALK_SUCCESS;
    }
    
    h->last_error = OPENJTALK_ERROR_INVALID_INPUT;
    return OPENJTALK_ERROR_INVALID_INPUT;
}

// Get option
const char* openjtalk_get_option(void* handle, const char* key) {
    if (!handle || !key) {
        return NULL;
    }
    
    OpenJTalkHandle* h = (OpenJTalkHandle*)handle;
    
    if (strcmp(key, "use_accent") == 0) {
        return "true";
    } else if (strcmp(key, "use_duration") == 0) {
        return "true";
    } else if (strcmp(key, "version") == 0) {
        return VERSION;
    } else if (strcmp(key, "dictionary_size") == 0) {
        static char size_str[32];
        snprintf(size_str, sizeof(size_str), "%d", 
                 mecab_full_get_dictionary_size(h->mecab));
        return size_str;
    }
    
    return NULL;
}