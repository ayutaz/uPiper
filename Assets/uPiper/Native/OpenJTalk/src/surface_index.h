#ifndef SURFACE_INDEX_H
#define SURFACE_INDEX_H

#include <stdint.h>
#include <stdbool.h>
#include <stddef.h>

#ifdef __cplusplus
extern "C" {
#endif

// Surface form index entry
typedef struct SurfaceEntry {
    char* surface;              // Surface form (UTF-8)
    uint32_t* token_indices;    // Array of token indices
    uint32_t count;            // Number of tokens
    uint32_t capacity;         // Capacity of token_indices array
    struct SurfaceEntry* next; // Hash collision chain
} SurfaceEntry;

// Surface form index (hash table)
typedef struct SurfaceIndex {
    SurfaceEntry** buckets;    // Hash table buckets
    uint32_t bucket_count;     // Number of buckets
    uint32_t entry_count;      // Total number of entries
} SurfaceIndex;

// Create/destroy surface index
SurfaceIndex* surface_index_create(uint32_t expected_size);
void surface_index_destroy(SurfaceIndex* index);

// Add surface form to index
bool surface_index_add(SurfaceIndex* index, const char* surface, uint32_t token_index);

// Lookup surface form
const uint32_t* surface_index_lookup(const SurfaceIndex* index, 
                                     const char* surface, 
                                     uint32_t* count);

// Common prefix search
typedef struct {
    const char* surface;
    const uint32_t* indices;
    uint32_t count;
    uint32_t length;
} SurfaceMatch;

int surface_index_common_prefix_search(const SurfaceIndex* index,
                                       const char* text, size_t len,
                                       SurfaceMatch* results, int max_results);

#ifdef __cplusplus
}
#endif

#endif // SURFACE_INDEX_H