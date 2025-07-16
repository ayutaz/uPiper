#include "surface_index.h"
#include <stdlib.h>
#include <string.h>
#include <stdio.h>

// Simple hash function for UTF-8 strings
static uint32_t hash_utf8(const char* str) {
    uint32_t hash = 5381;
    const unsigned char* p = (const unsigned char*)str;
    
    while (*p) {
        hash = ((hash << 5) + hash) + *p;
        p++;
    }
    
    return hash;
}

// Create surface index
SurfaceIndex* surface_index_create(uint32_t expected_size) {
    SurfaceIndex* index = (SurfaceIndex*)calloc(1, sizeof(SurfaceIndex));
    if (!index) return NULL;
    
    // Choose prime number for bucket count
    index->bucket_count = expected_size * 2;
    if (index->bucket_count < 1009) index->bucket_count = 1009;
    
    index->buckets = (SurfaceEntry**)calloc(index->bucket_count, sizeof(SurfaceEntry*));
    if (!index->buckets) {
        free(index);
        return NULL;
    }
    
    return index;
}

// Destroy surface index
void surface_index_destroy(SurfaceIndex* index) {
    if (!index) return;
    
    // Free all entries
    for (uint32_t i = 0; i < index->bucket_count; i++) {
        SurfaceEntry* entry = index->buckets[i];
        while (entry) {
            SurfaceEntry* next = entry->next;
            free(entry->surface);
            free(entry->token_indices);
            free(entry);
            entry = next;
        }
    }
    
    free(index->buckets);
    free(index);
}

// Add surface form to index
bool surface_index_add(SurfaceIndex* index, const char* surface, uint32_t token_index) {
    if (!index || !surface) return false;
    
    uint32_t hash = hash_utf8(surface);
    uint32_t bucket = hash % index->bucket_count;
    
    // Search for existing entry
    SurfaceEntry* entry = index->buckets[bucket];
    while (entry) {
        if (strcmp(entry->surface, surface) == 0) {
            // Found existing entry - add token index
            if (entry->count >= entry->capacity) {
                // Expand array
                uint32_t new_capacity = entry->capacity * 2;
                if (new_capacity < 4) new_capacity = 4;
                
                uint32_t* new_indices = (uint32_t*)realloc(entry->token_indices,
                                                           new_capacity * sizeof(uint32_t));
                if (!new_indices) return false;
                
                entry->token_indices = new_indices;
                entry->capacity = new_capacity;
            }
            
            entry->token_indices[entry->count++] = token_index;
            return true;
        }
        entry = entry->next;
    }
    
    // Create new entry
    entry = (SurfaceEntry*)calloc(1, sizeof(SurfaceEntry));
    if (!entry) return false;
    
    entry->surface = strdup(surface);
    if (!entry->surface) {
        free(entry);
        return false;
    }
    
    entry->capacity = 4;
    entry->token_indices = (uint32_t*)malloc(entry->capacity * sizeof(uint32_t));
    if (!entry->token_indices) {
        free(entry->surface);
        free(entry);
        return false;
    }
    
    entry->token_indices[0] = token_index;
    entry->count = 1;
    
    // Add to bucket chain
    entry->next = index->buckets[bucket];
    index->buckets[bucket] = entry;
    index->entry_count++;
    
    return true;
}

// Lookup surface form
const uint32_t* surface_index_lookup(const SurfaceIndex* index, 
                                     const char* surface, 
                                     uint32_t* count) {
    if (!index || !surface || !count) {
        if (count) *count = 0;
        return NULL;
    }
    
    uint32_t hash = hash_utf8(surface);
    uint32_t bucket = hash % index->bucket_count;
    
    SurfaceEntry* entry = index->buckets[bucket];
    while (entry) {
        if (strcmp(entry->surface, surface) == 0) {
            *count = entry->count;
            return entry->token_indices;
        }
        entry = entry->next;
    }
    
    *count = 0;
    return NULL;
}

// UTF-8 helper functions
static int utf8_char_len(unsigned char c) {
    if (c < 0x80) return 1;
    if ((c & 0xE0) == 0xC0) return 2;
    if ((c & 0xF0) == 0xE0) return 3;
    if ((c & 0xF8) == 0xF0) return 4;
    return 1;
}

// Common prefix search
int surface_index_common_prefix_search(const SurfaceIndex* index,
                                       const char* text, size_t len,
                                       SurfaceMatch* results, int max_results) {
    if (!index || !text || !results || max_results <= 0) {
        return 0;
    }
    
    int result_count = 0;
    size_t pos = 0;
    
    // Try different prefix lengths
    while (pos < len && result_count < max_results) {
        // Find the end of current position
        size_t end = pos;
        int chars_counted = 0;
        
        // Try up to 20 characters (reasonable max word length)
        while (end < len && chars_counted < 20) {
            int char_len = utf8_char_len((unsigned char)text[end]);
            end += char_len;
            chars_counted++;
            
            // Create substring
            size_t substr_len = end - pos;
            char* substr = (char*)malloc(substr_len + 1);
            if (!substr) continue;
            
            memcpy(substr, text + pos, substr_len);
            substr[substr_len] = '\0';
            
            // Look up substring
            uint32_t count;
            const uint32_t* indices = surface_index_lookup(index, substr, &count);
            
            if (indices && count > 0) {
                // Found match
                if (result_count < max_results) {
                    results[result_count].surface = substr;  // Caller must free
                    results[result_count].indices = indices;
                    results[result_count].count = count;
                    results[result_count].length = substr_len;
                    result_count++;
                } else {
                    free(substr);
                }
            } else {
                free(substr);
            }
        }
        
        // Move to next character
        int char_len = utf8_char_len((unsigned char)text[pos]);
        pos += char_len;
    }
    
    return result_count;
}