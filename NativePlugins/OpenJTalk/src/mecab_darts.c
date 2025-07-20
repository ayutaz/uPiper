#include "mecab_darts.h"
#include <stdlib.h>
#include <string.h>

// Load Darts from dictionary data
// The pyopenjtalk dictionary format stores the Darts array directly
// without a separate header. The size is determined by the dictionary header.
Darts* darts_load(const void* data, size_t size) {
    if (!data || size < sizeof(uint32_t)) {
        return NULL;
    }
    
    Darts* darts = (Darts*)calloc(1, sizeof(Darts));
    if (!darts) {
        return NULL;
    }
    
    // The entire data is the Darts array
    darts->array = (int32_t*)data;
    darts->size = size / sizeof(int32_t);
    
    // For pyopenjtalk format, we don't have separate lengths/values arrays
    // The token indices are stored directly in the trie
    darts->lengths = NULL;
    darts->values = NULL;
    darts->entry_num = 0;
    
    return darts;
}

// Free Darts
void darts_free(Darts* darts) {
    // Data is memory-mapped or part of dictionary, so don't free arrays
    free(darts);
}

// Common prefix search implementation for pyopenjtalk Darts format
int darts_common_prefix_search(const Darts* darts,
                               const char* key, size_t len,
                               DartsResult* results, int max_results) {
    if (!darts || !key || !results || max_results <= 0 || darts->size < 2) {
        return 0;
    }
    
    int result_count = 0;
    int32_t node_pos = 0;
    size_t key_pos = 0;
    
    // Standard double-array trie format
    // base[s] + c = t
    // check[t] = s
    
    while (key_pos < len && result_count < max_results) {
        uint8_t c = (uint8_t)key[key_pos];
        int32_t base = darts->array[node_pos];
        
        // Negative base indicates a terminal node with value
        if (base < 0) {
            // This is a match - extract the token index
            if (result_count < max_results) {
                results[result_count].length = key_pos;
                results[result_count].value = -base - 1;  // Convert to token index
                result_count++;
            }
        }
        
        // Try to continue matching
        if (base >= 0) {
            int32_t next = base + c + 1;  // +1 because index 0 is reserved
            
            // Bounds check
            if (next >= 0 && next < (int32_t)darts->size) {
                int32_t check = darts->array[next];
                
                if (check == node_pos) {
                    // Valid transition
                    node_pos = next;
                    key_pos++;
                } else {
                    // No valid transition
                    break;
                }
            } else {
                // Out of bounds
                break;
            }
        } else {
            // Terminal node but continue searching for longer matches
            int32_t next = (-base - 1) + c + 1;
            if (next >= 0 && next < (int32_t)darts->size && darts->array[next] == node_pos) {
                node_pos = next;
                key_pos++;
            } else {
                break;
            }
        }
    }
    
    // Check final node
    if (key_pos == len && node_pos > 0) {
        int32_t base = darts->array[node_pos];
        if (base < 0 && result_count < max_results) {
            results[result_count].length = key_pos;
            results[result_count].value = -base - 1;
            result_count++;
        }
    }
    
    return result_count;
}

// Exact match search
uint32_t darts_exact_match_search(const Darts* darts,
                                  const char* key, size_t len) {
    if (!darts || !key || darts->size < 2) {
        return (uint32_t)-1;
    }
    
    int32_t node_pos = 0;
    size_t key_pos = 0;
    
    // Traverse the trie
    while (key_pos < len) {
        uint8_t c = (uint8_t)key[key_pos];
        int32_t base = darts->array[node_pos];
        
        if (base >= 0) {
            int32_t next = base + c + 1;
            
            // Bounds check
            if (next >= 0 && next < (int32_t)darts->size) {
                int32_t check = darts->array[next];
                
                if (check == node_pos) {
                    // Valid transition
                    node_pos = next;
                    key_pos++;
                } else {
                    // No valid transition
                    return (uint32_t)-1;
                }
            } else {
                // Out of bounds
                return (uint32_t)-1;
            }
        } else {
            // Terminal node - can't continue
            return (uint32_t)-1;
        }
    }
    
    // Check if final node has a value
    if (key_pos == len && node_pos > 0) {
        int32_t base = darts->array[node_pos];
        if (base < 0) {
            return (uint32_t)(-base - 1);
        }
    }
    
    return (uint32_t)-1;
}