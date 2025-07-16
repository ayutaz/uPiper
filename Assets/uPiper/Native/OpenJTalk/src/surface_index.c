#include "surface_index.h"
#include <stdlib.h>
#include <string.h>
#include <stdio.h>

// UTF-8 utilities
static int utf8_char_len(unsigned char c) {
    if (c < 0x80) return 1;
    if ((c & 0xE0) == 0xC0) return 2;
    if ((c & 0xF0) == 0xE0) return 3;
    if ((c & 0xF8) == 0xF0) return 4;
    return 1;
}

// Hash function for UTF-8 strings
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
    
    // Calculate bucket count (next prime after expected_size / 0.75)
    index->bucket_count = expected_size * 4 / 3;
    if (index->bucket_count < 1024) index->bucket_count = 1024;
    
    // Make it prime-ish
    index->bucket_count |= 1;
    
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
                // Resize array
                uint32_t new_capacity = entry->capacity * 2;
                if (new_capacity < 8) new_capacity = 8;
                
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
    
    // Add to hash chain
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

// Common prefix search
int surface_index_common_prefix_search(const SurfaceIndex* index,
                                       const char* text, size_t text_len,
                                       SurfaceMatch* results, int max_results) {
    if (!index || !text || !results || max_results <= 0) return 0;
    
    int result_count = 0;
    size_t pos = 0;
    
    // Try all prefix lengths from the beginning
    while (pos < text_len && result_count < max_results) {
        // Move to next character boundary
        int char_len = utf8_char_len((unsigned char)text[pos]);
        pos += char_len;
        
        // Extract substring from beginning
        char* substr = (char*)malloc(pos + 1);
        if (!substr) break;
        
        memcpy(substr, text, pos);
        substr[pos] = '\0';
        
        // Look up in index
        uint32_t count;
        const uint32_t* indices = surface_index_lookup(index, substr, &count);
        
        if (indices && count > 0) {
            // Check for duplicates
            bool is_duplicate = false;
            for (int i = 0; i < result_count; i++) {
                if (results[i].length == pos && 
                    memcmp(results[i].surface, substr, pos) == 0) {
                    is_duplicate = true;
                    break;
                }
            }
            
            if (!is_duplicate) {
                results[result_count].surface = substr;  // Caller must free
                results[result_count].indices = indices;
                results[result_count].count = count;
                results[result_count].length = pos;
                result_count++;
            } else {
                free(substr);
            }
        } else {
            free(substr);
        }
        
        // Don't try too long prefixes
        if (pos > 100) break;
    }
    
    // Also try from each position
    for (size_t start_pos = 1; start_pos < text_len && result_count < max_results; ) {
        size_t pos = start_pos;
        
        // Try different lengths from this position
        for (int len = 1; len <= 10 && pos < text_len && result_count < max_results; len++) {
            // Move to next character boundary
            int char_len = utf8_char_len((unsigned char)text[pos]);
            pos += char_len;
            
            if (pos > text_len) break;
            
            // Extract substring
            size_t substr_len = pos - start_pos;
            char* substr = (char*)malloc(substr_len + 1);
            if (!substr) break;
            
            memcpy(substr, text + start_pos, substr_len);
            substr[substr_len] = '\0';
            
            // Look up in index
            uint32_t count;
            const uint32_t* indices = surface_index_lookup(index, substr, &count);
            
            if (indices && count > 0) {
                // Store result with adjusted indices
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

// Extract surface form from feature string
bool extract_surface_from_feature(const char* feature, char* surface, size_t surface_size) {
    if (!feature || !surface || surface_size == 0) {
        return false;
    }
    
    // Feature format: "品詞,品詞細分類1,...,表層形,読み,発音"
    // We need to extract the 表層形 (surface form) field
    
    // Skip to the 7th comma-separated field (0-indexed: field 6)
    const char* p = feature;
    int field_count = 0;
    
    while (*p && field_count < 6) {
        if (*p == ',') {
            field_count++;
        }
        p++;
    }
    
    if (field_count < 6 || !*p) {
        // Not enough fields, use first field as fallback
        const char* end = strchr(feature, ',');
        if (!end) {
            strncpy(surface, feature, surface_size - 1);
        } else {
            size_t len = end - feature;
            if (len >= surface_size) len = surface_size - 1;
            strncpy(surface, feature, len);
            surface[len] = '\0';
        }
        surface[surface_size - 1] = '\0';
        return true;
    }
    
    // Extract the surface form field
    const char* start = p;
    const char* end = strchr(p, ',');
    
    if (!end) {
        // Last field
        strncpy(surface, start, surface_size - 1);
    } else {
        size_t len = end - start;
        if (len >= surface_size) len = surface_size - 1;
        strncpy(surface, start, len);
        surface[len] = '\0';
    }
    
    surface[surface_size - 1] = '\0';
    
    // If surface is "*", it means it's not available
    if (strcmp(surface, "*") == 0) {
        return false;
    }
    
    return strlen(surface) > 0;
}

// Free surface index - alias for destroy
void surface_index_free(SurfaceIndex* index) {
    surface_index_destroy(index);
}