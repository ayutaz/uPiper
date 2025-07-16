#include "openjtalk_wrapper.h"
#include "openjtalk_phonemizer.h"
#include <stdlib.h>
#include <string.h>
#include <stdio.h>

#define VERSION "2.0.0-light"

// Version information
const char* openjtalk_get_version(void) {
    return VERSION;
}

// Create phonemizer instance
void* openjtalk_create(const char* dict_path) {
    OpenJTalkPhonemizer* phonemizer = openjtalk_phonemizer_create();
    if (!phonemizer) {
        return NULL;
    }
    
    // Initialize with dictionary (optional for light version)
    if (dict_path && !openjtalk_phonemizer_initialize(phonemizer, dict_path)) {
        openjtalk_phonemizer_destroy(phonemizer);
        return NULL;
    }
    
    return phonemizer;
}

// Destroy phonemizer instance
void openjtalk_destroy(void* handle) {
    if (handle) {
        openjtalk_phonemizer_destroy((OpenJTalkPhonemizer*)handle);
    }
}

// Text to phonemes conversion
int openjtalk_text_to_phonemes(void* handle, const char* text, int* phoneme_ids, int max_phonemes) {
    if (!handle || !text || !phoneme_ids || max_phonemes <= 0) {
        return -1;
    }
    
    OpenJTalkPhonemizer* phonemizer = (OpenJTalkPhonemizer*)handle;
    return openjtalk_phonemizer_get_ids(phonemizer, text, phoneme_ids, max_phonemes);
}

// Text to phonemes with details
int openjtalk_text_to_phonemes_ex(void* handle, const char* text, 
                                   PhonemeData* phonemes, int max_phonemes) {
    if (!handle || !text || !phonemes || max_phonemes <= 0) {
        return -1;
    }
    
    OpenJTalkPhonemizer* phonemizer = (OpenJTalkPhonemizer*)handle;
    
    // Allocate temporary storage
    PhonemeInfo* temp_info = (PhonemeInfo*)calloc(max_phonemes, sizeof(PhonemeInfo));
    if (!temp_info) {
        return -1;
    }
    
    // Get phonemes with full information
    int count = openjtalk_phonemizer_phonemize(phonemizer, text, temp_info, max_phonemes);
    
    if (count > 0) {
        // Convert to output format
        for (int i = 0; i < count; i++) {
            phonemes[i].phoneme_id = (int)temp_info[i].id;
            phonemes[i].accent_type = temp_info[i].accent_type;
            phonemes[i].mora_position = temp_info[i].mora_position;
            phonemes[i].duration_ms = (int)(temp_info[i].duration * 1000.0f);
            
            // Get phoneme string
            const char* phoneme_str = openjtalk_phonemizer_get_phoneme_string(temp_info[i].id);
            strncpy(phonemes[i].phoneme_str, phoneme_str, sizeof(phonemes[i].phoneme_str) - 1);
            phonemes[i].phoneme_str[sizeof(phonemes[i].phoneme_str) - 1] = '\0';
        }
    }
    
    free(temp_info);
    return count;
}

// Get last error message
const char* openjtalk_get_last_error(void* handle) {
    if (!handle) {
        return "Invalid handle";
    }
    
    OpenJTalkPhonemizer* phonemizer = (OpenJTalkPhonemizer*)handle;
    return openjtalk_phonemizer_get_error(phonemizer);
}

// Set options
int openjtalk_set_option(void* handle, const char* key, const char* value) {
    if (!handle || !key || !value) {
        return -1;
    }
    
    OpenJTalkPhonemizer* phonemizer = (OpenJTalkPhonemizer*)handle;
    return openjtalk_phonemizer_set_option(phonemizer, key, value) ? 0 : -1;
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

// Check if initialized
int openjtalk_is_initialized(void* handle) {
    return handle != NULL ? 1 : 0;
}

// Get phoneme count for text (without actual conversion)
int openjtalk_get_phoneme_count(void* handle, const char* text) {
    if (!handle || !text) {
        return -1;
    }
    
    // Rough estimate: 2 phonemes per character + silence
    size_t len = strlen(text);
    return (int)(len * 2 + 2);  // Start and end silence
}

// Clear internal buffers
void openjtalk_clear(void* handle) {
    // Light implementation doesn't need explicit clearing
    // Memory pool is reset on each phonemization
}