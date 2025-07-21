#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "../src/mecab_dict_loader.h"
#include "../src/mecab_darts.h"

void analyze_trie_structure(MecabFullDictionary* dict) {
    if (!dict || !dict->sys_darts) {
        printf("No dictionary or Darts loaded\n");
        return;
    }
    
    printf("Darts structure analysis:\n");
    printf("  Size: %zu entries\n", dict->sys_darts->size);
    
    // Look for non-zero entries in the first 1000 positions
    printf("\nNon-zero entries in first 1000 positions:\n");
    int non_zero = 0;
    for (size_t i = 0; i < 1000 && i < dict->sys_darts->size; i++) {
        if (dict->sys_darts->array[i] != 0) {
            if (non_zero < 20) {
                printf("  [%zu]: %d (0x%08x)\n", i, dict->sys_darts->array[i], 
                       (uint32_t)dict->sys_darts->array[i]);
            }
            non_zero++;
        }
    }
    printf("  Total non-zero in first 1000: %d\n", non_zero);
    
    // The pyopenjtalk dictionary might store the surface forms differently
    // Let's check the token array to understand the structure
    printf("\nAnalyzing token structure:\n");
    size_t token_offset = sizeof(DictionaryHeader) + dict->sys_header.dsize;
    const Token* tokens = (const Token*)((uint8_t*)dict->sys_data + token_offset);
    
    // Show first few tokens
    for (int i = 0; i < 5 && i < (int)dict->sys_header.lexsize; i++) {
        const Token* token = &tokens[i];
        const char* feature = mecab_dict_get_feature(dict, token);
        
        printf("\nToken %d:\n", i);
        printf("  lcAttr: %u, rcAttr: %u, posid: %u, wcost: %d\n",
               token->lcAttr, token->rcAttr, token->posid, token->wcost);
        printf("  Feature offset: %u\n", token->feature);
        
        if (feature) {
            // Parse feature to extract surface form
            char surface[256] = {0};
            const char* comma = strchr(feature, ',');
            if (comma) {
                // In pyopenjtalk format, surface might be in the feature string
                const char* surface_start = feature;
                while (surface_start < comma && *surface_start != '\0') {
                    surface_start++;
                }
                // Actually, let's just show the full feature
                printf("  Feature: %.100s...\n", feature);
            }
        }
    }
    
    // The dictionary might be using a different indexing scheme
    // Let's try to understand how words are indexed
    printf("\nChecking for surface forms in features:\n");
    
    // Skip to a random position to see different tokens
    for (int i = 1000; i < 1010 && i < (int)dict->sys_header.lexsize; i++) {
        const Token* token = &tokens[i];
        const char* feature = mecab_dict_get_feature(dict, token);
        if (feature) {
            // Show just the beginning of the feature
            char preview[100];
            strncpy(preview, feature, 99);
            preview[99] = '\0';
            
            // Replace commas with spaces for easier reading
            for (char* p = preview; *p; p++) {
                if (*p == ',') *p = ' ';
            }
            
            printf("  Token %d: %s\n", i, preview);
        }
    }
}

int main(int argc, char* argv[]) {
    if (argc < 2) {
        printf("Usage: %s <dict_path>\n", argv[0]);
        return 1;
    }
    
    MecabFullDictionary* dict = mecab_dict_load(argv[1]);
    if (!dict) {
        fprintf(stderr, "Failed to load dictionary\n");
        return 1;
    }
    
    analyze_trie_structure(dict);
    
    mecab_dict_free_full(dict);
    return 0;
}