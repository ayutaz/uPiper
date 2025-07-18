#include <stdio.h>
#include <stdlib.h>
#include "../src/mecab_full.h"

int main(int argc, char* argv[]) {
    const char* dict_path = argc > 1 ? argv[1] : "test_dictionary";
    const char* text = argc > 2 ? argv[2] : "今日";
    
    printf("Viterbi test\n");
    printf("Dictionary: %s\n", dict_path);
    printf("Text: %s\n\n", text);
    fflush(stdout);
    
    // Create MeCab instance
    printf("Creating MeCab instance...\n");
    fflush(stdout);
    
    MecabFull* mecab = mecab_full_create(dict_path);
    if (!mecab) {
        fprintf(stderr, "Failed to create MeCab instance\n");
        return 1;
    }
    
    printf("MeCab created successfully\n");
    printf("Dictionary info: %s\n", mecab_full_get_dictionary_info(mecab));
    fflush(stdout);
    
    // Parse text
    printf("\nParsing text...\n");
    fflush(stdout);
    
    MecabFullNode* nodes = mecab_full_parse(mecab, text);
    if (!nodes) {
        fprintf(stderr, "Parse failed: %s\n", mecab_full_get_error(mecab));
        mecab_full_destroy(mecab);
        return 1;
    }
    
    printf("Parse succeeded!\n");
    
    // Print results
    MecabFullNode* node = nodes;
    int count = 0;
    while (node) {
        if (node->length > 0) {
            printf("Node %d: '%.*s' (%s)\n", count++, 
                   node->length, node->surface, node->feature.pos);
        }
        node = node->next;
    }
    
    // Clean up
    mecab_full_free_nodes(mecab, nodes);
    mecab_full_destroy(mecab);
    
    printf("\nTest completed\n");
    return 0;
}