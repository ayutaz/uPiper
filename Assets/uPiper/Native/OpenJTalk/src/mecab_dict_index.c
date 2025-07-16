#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "mecab_dict_loader.h"

// Build a surface form to token index mapping
// This should be done once when loading the dictionary
typedef struct {
    char* surface;
    uint32_t* indices;
    uint32_t count;
    uint32_t capacity;
} SurfaceIndex;

typedef struct {
    SurfaceIndex* entries;
    uint32_t size;
    uint32_t capacity;
} SurfaceMap;

// Simple hash function
static uint32_t hash_string(const char* str) {
    uint32_t hash = 5381;
    int c;
    while ((c = *str++)) {
        hash = ((hash << 5) + hash) + c;
    }
    return hash;
}

// Extract surface form from feature string
static const char* extract_surface(const char* feature, char* buffer, size_t bufsize) {
    // Feature format: POS1,POS2,...,surface,reading,pronunciation,...
    const char* p = feature;
    int comma_count = 0;
    
    while (*p && comma_count < 6) {
        if (*p == ',') comma_count++;
        p++;
    }
    
    if (comma_count < 6 || *p == '\0') return NULL;
    
    const char* start = p;
    const char* end = strchr(p, ',');
    if (!end) return NULL;
    
    size_t len = end - start;
    if (len >= bufsize) len = bufsize - 1;
    
    strncpy(buffer, start, len);
    buffer[len] = '\0';
    
    return buffer;
}

// Build surface index for dictionary
SurfaceMap* mecab_dict_build_surface_index(const MecabFullDictionary* dict) {
    if (!dict) return NULL;
    
    SurfaceMap* map = (SurfaceMap*)calloc(1, sizeof(SurfaceMap));
    if (!map) return NULL;
    
    // Initialize with reasonable size
    map->capacity = dict->sys_header.lexsize / 2;  // Assume some duplicates
    map->entries = (SurfaceIndex*)calloc(map->capacity, sizeof(SurfaceIndex));
    if (!map->entries) {
        free(map);
        return NULL;
    }
    
    // Get token array
    size_t token_offset = sizeof(DictionaryHeader) + dict->sys_header.dsize;
    const Token* tokens = (const Token*)((uint8_t*)dict->sys_data + token_offset);
    
    char surface_buffer[256];
    
    // Build index
    for (uint32_t i = 0; i < dict->sys_header.lexsize; i++) {
        const Token* token = &tokens[i];
        const char* feature = mecab_dict_get_feature(dict, token);
        if (!feature) continue;
        
        const char* surface = extract_surface(feature, surface_buffer, sizeof(surface_buffer));
        if (!surface || *surface == '\0') continue;
        
        // Hash to find bucket
        uint32_t hash = hash_string(surface);
        uint32_t bucket = hash % map->capacity;
        
        // Linear probing to find entry
        while (map->entries[bucket].surface != NULL) {
            if (strcmp(map->entries[bucket].surface, surface) == 0) {
                // Found existing entry - add index
                if (map->entries[bucket].count >= map->entries[bucket].capacity) {
                    uint32_t new_cap = map->entries[bucket].capacity * 2;
                    if (new_cap == 0) new_cap = 4;
                    uint32_t* new_indices = (uint32_t*)realloc(map->entries[bucket].indices,
                                                                new_cap * sizeof(uint32_t));
                    if (!new_indices) continue;
                    map->entries[bucket].indices = new_indices;
                    map->entries[bucket].capacity = new_cap;
                }
                map->entries[bucket].indices[map->entries[bucket].count++] = i;
                break;
            }
            bucket = (bucket + 1) % map->capacity;
        }
        
        // New entry
        if (map->entries[bucket].surface == NULL) {
            map->entries[bucket].surface = strdup(surface);
            map->entries[bucket].indices = (uint32_t*)malloc(4 * sizeof(uint32_t));
            map->entries[bucket].capacity = 4;
            map->entries[bucket].count = 1;
            map->entries[bucket].indices[0] = i;
            map->size++;
        }
    }
    
    return map;
}

// Lookup surface form
const uint32_t* mecab_dict_lookup_surface(const SurfaceMap* map, const char* surface, uint32_t* count) {
    if (!map || !surface || !count) return NULL;
    
    uint32_t hash = hash_string(surface);
    uint32_t bucket = hash % map->capacity;
    
    while (map->entries[bucket].surface != NULL) {
        if (strcmp(map->entries[bucket].surface, surface) == 0) {
            *count = map->entries[bucket].count;
            return map->entries[bucket].indices;
        }
        bucket = (bucket + 1) % map->capacity;
    }
    
    *count = 0;
    return NULL;
}

// Free surface index
void mecab_dict_free_surface_index(SurfaceMap* map) {
    if (!map) return;
    
    for (uint32_t i = 0; i < map->capacity; i++) {
        if (map->entries[i].surface) {
            free(map->entries[i].surface);
            free(map->entries[i].indices);
        }
    }
    
    free(map->entries);
    free(map);
}