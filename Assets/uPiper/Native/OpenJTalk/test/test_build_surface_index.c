#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "../src/mecab_dict_loader.h"

// Extract surface form from feature string
const char* extract_surface(const char* feature) {
    static char surface[256];
    
    // Feature format: POS1,POS2,...,surface,reading,pronunciation,...
    // Count 6 commas to get to surface
    const char* p = feature;
    int comma_count = 0;
    
    while (*p && comma_count < 6) {
        if (*p == ',') comma_count++;
        p++;
    }
    
    if (comma_count < 6 || *p == '\0') return NULL;
    
    // Extract until next comma
    const char* start = p;
    const char* end = strchr(p, ',');
    if (!end) return NULL;
    
    size_t len = end - start;
    if (len >= sizeof(surface)) len = sizeof(surface) - 1;
    
    strncpy(surface, start, len);
    surface[len] = '\0';
    
    return surface;
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
    
    printf("Building surface form index...\n");
    
    // Get token array
    size_t token_offset = sizeof(DictionaryHeader) + dict->sys_header.dsize;
    const Token* tokens = (const Token*)((uint8_t*)dict->sys_data + token_offset);
    
    // Build a simple hash table of surface forms
    typedef struct {
        char surface[64];
        uint32_t token_index;
    } SurfaceEntry;
    
    // Show first 20 surface forms and their indices
    printf("\nFirst 20 surface forms:\n");
    for (uint32_t i = 0; i < 20 && i < dict->sys_header.lexsize; i++) {
        const Token* token = &tokens[i];
        const char* feature = mecab_dict_get_feature(dict, token);
        if (feature) {
            const char* surface = extract_surface(feature);
            if (surface) {
                printf("  Token %u: '%s' (feature: %.50s...)\n", i, surface, feature);
            }
        }
    }
    
    // Now let's see if we can find specific words in the token array
    const char* test_words[] = {"今日", "は", "です", "日本", "東京", NULL};
    
    printf("\nSearching for test words in token array:\n");
    for (int w = 0; test_words[w]; w++) {
        const char* target = test_words[w];
        printf("\nSearching for '%s':\n", target);
        
        int found_count = 0;
        for (uint32_t i = 0; i < dict->sys_header.lexsize && found_count < 5; i++) {
            const Token* token = &tokens[i];
            const char* feature = mecab_dict_get_feature(dict, token);
            if (feature) {
                const char* surface = extract_surface(feature);
                if (surface && strcmp(surface, target) == 0) {
                    printf("  Found at token %u: %s\n", i, feature);
                    found_count++;
                }
            }
        }
        
        if (found_count == 0) {
            printf("  Not found in dictionary\n");
        }
    }
    
    mecab_dict_free_full(dict);
    return 0;
}