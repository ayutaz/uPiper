#ifndef MECAB_DARTS_H
#define MECAB_DARTS_H

#include <stdint.h>
#include <stddef.h>
#include <stdbool.h>

#ifdef __cplusplus
extern "C" {
#endif

// Double Array Trie structure for fast dictionary lookup
typedef struct Darts {
    int32_t* array;      // Double array
    size_t size;         // Array size
    uint32_t* lengths;   // Length array for each entry
    uint32_t* values;    // Value array (token indices)
    size_t entry_num;    // Number of entries
} Darts;

// Search result
typedef struct {
    size_t length;       // Matched length
    uint32_t value;      // Token index
} DartsResult;

// Load Darts from dictionary data
Darts* darts_load(const void* data, size_t size);
void darts_free(Darts* darts);

// Common prefix search
int darts_common_prefix_search(const Darts* darts,
                               const char* key, size_t len,
                               DartsResult* results, int max_results);

// Exact match search
uint32_t darts_exact_match_search(const Darts* darts,
                                  const char* key, size_t len);

#ifdef __cplusplus
}
#endif

#endif // MECAB_DARTS_H