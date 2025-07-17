#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "../src/mecab_light.h"
#include "../src/mecab_light_impl.h"

void debug_viterbi(const char* text) {
    printf("\n=== Viterbi Debug: \"%s\" ===\n", text);
    
    // Create minimal dictionary
    MecabDictionary* dict = mecab_dict_create_minimal();
    if (!dict) {
        printf("Failed to create dictionary\n");
        return;
    }
    
    // Create implementation
    struct MecabLightImpl* impl = mecab_impl_create(dict);
    if (!impl) {
        mecab_dict_free(dict);
        printf("Failed to create implementation\n");
        return;
    }
    
    // Parse text
    LatticeNode* lattice = mecab_impl_parse(impl, text);
    if (!lattice) {
        printf("Failed to parse text\n");
        mecab_impl_destroy(impl);
        mecab_dict_free(dict);
        return;
    }
    
    // Print lattice nodes
    printf("Lattice nodes:\n");
    LatticeNode* current = lattice;
    int count = 0;
    while (current) {
        printf("  [%d] pos:%d-%d surface:'%.*s' cost:%d\n", 
               count++,
               current->begin_pos,
               current->end_pos,
               current->surface_length,
               current->surface,
               current->cost);
        
        // Check if we have an entry
        if (current->entry) {
            const char* reading = mecab_get_reading(dict, current->entry);
            const char* pos = mecab_get_pos_name(current->entry->pos_id);
            printf("      -> reading:'%s' pos:'%s'\n", reading, pos);
        }
        
        current = current->prev;  // Following the backtrack chain
    }
    
    // Clean up
    mecab_impl_destroy(impl);
    mecab_dict_free(dict);
}

int main() {
    printf("=== Viterbi Algorithm Debug ===\n");
    
    debug_viterbi("今日");
    debug_viterbi("は");
    debug_viterbi("今日は");
    debug_viterbi("今日は良い天気です");
    
    return 0;
}