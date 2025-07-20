#include "phonemizer_internal.h"
#include "mecab_full.h"
#include "phoneme_converter.h"
#include "memory_pool.h"
#include "error_handling.h"
#include <string.h>
#include <stdlib.h>
#include <ctype.h>

// Initialize phonemizer with full MeCab and phoneme conversion
bool phonemizer_init(OpenJTalkPhonemizer* phonemizer, const char* dict_path) {
    if (!phonemizer || !dict_path) {
        return false;
    }
    
    // Store dictionary path
    phonemizer->dict_path = strdup(dict_path);
    if (!phonemizer->dict_path) {
        set_last_error(phonemizer, OPENJTALK_ERROR_MEMORY_ALLOCATION);
        return false;
    }
    
    // Initialize MeCab with full dictionary
    phonemizer->mecab = mecab_full_create(dict_path);
    if (!phonemizer->mecab) {
        free(phonemizer->dict_path);
        phonemizer->dict_path = NULL;
        set_last_error(phonemizer, OPENJTALK_ERROR_INVALID_DICTIONARY);
        return false;
    }
    
    // Initialize phoneme converter
    phonemizer->njd = phoneme_converter_create();
    if (!phonemizer->njd) {
        mecab_full_destroy((MecabFull*)phonemizer->mecab);
        free(phonemizer->dict_path);
        phonemizer->dict_path = NULL;
        set_last_error(phonemizer, OPENJTALK_ERROR_MEMORY_ALLOCATION);
        return false;
    }
    
    // JPCommon is integrated into phoneme_converter, so we don't need separate initialization
    phonemizer->jpcommon = phonemizer->njd;  // Use same instance
    phonemizer->last_error = OPENJTALK_SUCCESS;
    phonemizer->initialized = true;
    
    return true;
}

// Cleanup phonemizer
void phonemizer_cleanup(OpenJTalkPhonemizer* phonemizer) {
    if (!phonemizer) {
        return;
    }
    
    // Free dictionary path
    if (phonemizer->dict_path) {
        free(phonemizer->dict_path);
        phonemizer->dict_path = NULL;
    }
    
    // Cleanup MeCab
    if (phonemizer->mecab) {
        mecab_full_destroy((MecabFull*)phonemizer->mecab);
        phonemizer->mecab = NULL;
    }
    
    // Cleanup phoneme converter
    if (phonemizer->njd) {
        phoneme_converter_destroy((PhonemeConverter*)phonemizer->njd);
        phonemizer->njd = NULL;
    }
    
    phonemizer->jpcommon = NULL;
    phonemizer->initialized = false;
}

// Process text to phonemes using full implementation
PhonemeResult* phonemizer_process(OpenJTalkPhonemizer* phonemizer, const char* text) {
    if (!phonemizer || !text || !phonemizer->initialized) {
        return NULL;
    }
    
    MemoryPool* pool = (MemoryPool*)phonemizer->memory_pool;
    
    // Allocate result structure
    PhonemeResult* result = (PhonemeResult*)memory_pool_alloc(pool, sizeof(PhonemeResult));
    if (!result) {
        set_last_error(phonemizer, OPENJTALK_ERROR_MEMORY_ALLOCATION);
        return NULL;
    }
    
    // Parse text with MeCab
    MecabFullNode* mecab_nodes = mecab_full_parse((MecabFull*)phonemizer->mecab, text);
    if (!mecab_nodes) {
        set_last_error(phonemizer, OPENJTALK_ERROR_INVALID_INPUT);
        return NULL;
    }
    
    // Convert to phonemes
    PhonemeSequence* phoneme_seq = phoneme_converter_convert(
        (PhonemeConverter*)phonemizer->njd, 
        mecab_nodes
    );
    
    if (!phoneme_seq) {
        mecab_full_free_nodes((MecabFull*)phonemizer->mecab, mecab_nodes);
        set_last_error(phonemizer, OPENJTALK_ERROR_PROCESSING);
        return NULL;
    }
    
    // Build phoneme string and arrays
    char phoneme_buffer[4096] = {0};
    int buffer_pos = 0;
    
    result->phoneme_count = phoneme_seq->count;
    result->phoneme_ids = (int*)memory_pool_alloc(pool, sizeof(int) * phoneme_seq->count);
    result->durations = (float*)memory_pool_alloc(pool, sizeof(float) * phoneme_seq->count);
    
    if (!result->phoneme_ids || !result->durations) {
        phoneme_sequence_destroy(phoneme_seq);
        mecab_full_free_nodes((MecabFull*)phonemizer->mecab, mecab_nodes);
        set_last_error(phonemizer, OPENJTALK_ERROR_MEMORY_ALLOCATION);
        return NULL;
    }
    
    // Copy phoneme data
    result->total_duration = 0.0f;
    for (int i = 0; i < phoneme_seq->count; i++) {
        Phoneme* phoneme = &phoneme_seq->phonemes[i];
        
        // Add phoneme name to string
        if (buffer_pos > 0) {
            phoneme_buffer[buffer_pos++] = ' ';
        }
        int len = strlen(phoneme->phoneme);
        memcpy(phoneme_buffer + buffer_pos, phoneme->phoneme, len);
        buffer_pos += len;
        
        // Copy ID and duration
        result->phoneme_ids[i] = i; // Use index as ID for now
        result->durations[i] = phoneme->duration_ms / 1000.0f; // Convert ms to seconds
        result->total_duration += result->durations[i];
    }
    
    // Copy phoneme string
    result->phonemes = memory_pool_strdup(pool, phoneme_buffer);
    
    // Cleanup
    phoneme_sequence_destroy(phoneme_seq);
    mecab_full_free_nodes((MecabFull*)phonemizer->mecab, mecab_nodes);
    
    set_last_error(phonemizer, OPENJTALK_SUCCESS);
    return result;
}

// Get phoneme mappings (for compatibility)
const PhonemeMapping* get_phoneme_mappings(void) {
    // This is now handled by phoneme_mapping.h
    return NULL;
}

int get_phoneme_mapping_count(void) {
    // This is now handled by phoneme_mapping.h
    return 0;
}