#include <stdio.h>
#include <stdlib.h>
#include "../src/mecab_full.h"

int main(int argc, char* argv[]) {
    const char* dict_path = argc > 1 ? argv[1] : "test_dictionary";
    const char* text = argc > 2 ? argv[2] : "花が咲く";
    
    printf("Testing MeCab parse for: \"%s\"\n", text);
    printf("Dictionary: %s\n\n", dict_path);
    fflush(stdout);
    
    // Create MeCab instance
    MecabFull* mecab = mecab_full_create(dict_path);
    if (!mecab) {
        fprintf(stderr, "Failed to create MeCab instance\n");
        return 1;
    }
    
    // Parse text
    MecabFullNode* nodes = mecab_full_parse(mecab, text);
    if (!nodes) {
        fprintf(stderr, "Parse failed: %s\n", mecab_full_get_error(mecab));
        mecab_full_destroy(mecab);
        return 1;
    }
    
    // Print all nodes
    printf("Parse results:\n");
    MecabFullNode* node = nodes;
    int count = 0;
    while (node) {
        if (node->length > 0) {
            printf("Node %d:\n", count++);
            printf("  Surface: %.*s\n", (int)node->length, node->surface);
            printf("  POS: %s\n", node->feature.pos);
            printf("  Reading: %s\n", node->feature.reading);
            printf("  Pronunciation: %s\n", node->feature.pronunciation);
            printf("  Base: %s\n", node->feature.base);
            printf("  Is UNK: %s\n", node->is_unk ? "yes" : "no");
        }
        node = node->next;
    }
    
    // Clean up
    mecab_full_free_nodes(mecab, nodes);
    mecab_full_destroy(mecab);
    
    return 0;
}