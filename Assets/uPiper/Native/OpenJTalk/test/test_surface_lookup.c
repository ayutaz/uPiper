#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "../src/mecab_dict_loader.h"
#include "../src/surface_index.h"

int main(int argc, char* argv[]) {
    if (argc < 2) {
        printf("Usage: %s <dict_path>\n", argv[0]);
        return 1;
    }
    
    printf("Loading dictionary...\n");
    MecabFullDictionary* dict = mecab_dict_load(argv[1]);
    if (!dict) {
        fprintf(stderr, "Failed to load dictionary\n");
        return 1;
    }
    
    printf("Dictionary loaded successfully\n");
    printf("Has surface index: %s\n", dict->surface_index ? "YES" : "NO");
    
    if (dict->surface_index) {
        SurfaceIndex* index = (SurfaceIndex*)dict->surface_index;
        printf("Surface index entries: %u\n", index->entry_count);
        
        // Test specific lookups
        const char* test_words[] = {"今日", "は", "です", "日本", "東京", NULL};
        
        for (int i = 0; test_words[i]; i++) {
            printf("\nLooking up '%s':\n", test_words[i]);
            
            uint32_t count;
            const uint32_t* indices = surface_index_lookup(index, test_words[i], &count);
            
            if (indices && count > 0) {
                printf("  Found %u tokens\n", count);
                for (uint32_t j = 0; j < count && j < 3; j++) {
                    const Token* token = mecab_dict_get_token(dict, indices[j], false);
                    const char* feature = mecab_dict_get_feature(dict, token);
                    printf("  Token %u: %s\n", indices[j], feature);
                }
            } else {
                printf("  Not found\n");
            }
        }
        
        // Test common prefix search
        printf("\n\nTesting common prefix search:\n");
        const char* test_text = "今日は良い天気です";
        printf("Text: %s\n", test_text);
        
        SurfaceMatch matches[10];
        int match_count = surface_index_common_prefix_search(
            index, test_text, strlen(test_text), matches, 10
        );
        
        printf("Found %d matches:\n", match_count);
        for (int i = 0; i < match_count; i++) {
            printf("  Match %d: '%s' (length=%u, %u tokens)\n", 
                   i, matches[i].surface, matches[i].length, matches[i].count);
            free((char*)matches[i].surface);
        }
    }
    
    mecab_dict_free_full(dict);
    return 0;
}