#include <stdio.h>
#include <stdlib.h>
#include "../src/mecab_full.h"

int main(int argc, char* argv[]) {
    printf("Accent Information Test\n");
    
    const char* dict_path = argc > 1 ? argv[1] : "test_dictionary";
    printf("Dictionary path: %s\n", dict_path);
    
    // Create MeCab instance
    MecabFull* mecab = mecab_full_create(dict_path);
    if (!mecab) {
        fprintf(stderr, "Failed to create MeCab instance\n");
        return 1;
    }
    
    // Test words with known accent patterns
    const char* test_words[] = {
        "東京",      // とうきょう (0型 - 平板型)
        "花",        // はな (2型 - 尾高型)
        "雨",        // あめ (1型 - 頭高型)
        "日本語",    // にほんご
        "ありがとう", // ありがとう
        NULL
    };
    
    for (int i = 0; test_words[i] != NULL; i++) {
        printf("\n=== Testing: %s ===\n", test_words[i]);
        
        MecabFullNode* nodes = mecab_full_parse(mecab, test_words[i]);
        if (nodes) {
            MecabFullNode* node = nodes;
            while (node) {
                printf("Surface: %.*s\n", (int)node->length, node->surface);
                printf("  POS: %s\n", node->feature.pos);
                printf("  Reading: %s\n", node->feature.reading);
                printf("  Pronunciation: %s\n", node->feature.pronunciation);
                
                // Show all feature fields
                printf("  Full feature: %s,%s,%s,%s,%s,%s,%s,%s,%s\n",
                       node->feature.pos,
                       node->feature.pos_detail1,
                       node->feature.pos_detail2,
                       node->feature.pos_detail3,
                       node->feature.cform,
                       node->feature.ctype,
                       node->feature.base,
                       node->feature.reading,
                       node->feature.pronunciation);
                
                node = node->next;
            }
            
            mecab_full_free_nodes(mecab, nodes);
        }
    }
    
    // Clean up
    mecab_full_destroy(mecab);
    
    return 0;
}