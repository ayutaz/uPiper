#include <stdio.h>
#include <stdlib.h>
#include <time.h>
#include "../src/mecab_full.h"

int main(int argc, char* argv[]) {
    printf("MeCab Full Simple Test\n");
    
    const char* dict_path = argc > 1 ? argv[1] : "dictionary";
    printf("Dictionary path: %s\n", dict_path);
    
    // Create MeCab instance
    MecabFull* mecab = mecab_full_create(dict_path);
    if (!mecab) {
        fprintf(stderr, "Failed to create MeCab instance\n");
        return 1;
    }
    
    printf("MeCab created successfully\n");
    
    // Test parsing
    const char* text = "こんにちは";
    printf("\nParsing: \"%s\"\n", text);
    
    clock_t start = clock();
    MecabFullNode* nodes = mecab_full_parse(mecab, text);
    clock_t end = clock();
    
    double parse_time = ((double) (end - start)) / CLOCKS_PER_SEC;
    printf("Parse time: %.4f seconds\n", parse_time);
    
    if (nodes) {
        printf("Parse successful!\n");
        
        MecabFullNode* node = nodes;
        int count = 0;
        while (node) {
            printf("  [%d] surface: %s, pos: %s\n", 
                   count, node->surface, node->feature.pos);
            node = node->next;
            count++;
        }
        
        mecab_full_free_nodes(mecab, nodes);
    } else {
        printf("Parse failed!\n");
    }
    
    // Clean up
    mecab_full_destroy(mecab);
    
    return 0;
}