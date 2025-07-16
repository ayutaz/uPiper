#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "../include/openjtalk_wrapper.h"
#include "../src/mecab_dict_loader.h"
#include "../src/mecab_full.h"

void test_mecab_full_parse(const char* dict_path, const char* text) {
    printf("\n=== Testing MecabFull with Viterbi ===\n");
    printf("Dictionary: %s\n", dict_path);
    printf("Text: %s\n", text);
    
    MecabFull* mecab = mecab_full_create(dict_path);
    if (!mecab) {
        fprintf(stderr, "Failed to create MecabFull\n");
        return;
    }
    
    printf("\nParsing...\n");
    MecabFullNode* result = mecab_full_parse(mecab, text);
    
    if (!result) {
        fprintf(stderr, "Failed to parse text\n");
        mecab_full_destroy(mecab);
        return;
    }
    
    printf("\nResults (Viterbi path):\n");
    int node_count = 0;
    MecabFullNode* node = result;
    
    while (node) {
        printf("\n[%d] ", node_count++);
        
        // Print surface
        printf("Surface: '");
        fwrite(node->surface, 1, node->length, stdout);
        printf("'");
        
        // Print position
        printf(" (pos: %d-%d)", node->begin_pos, node->end_pos);
        
        // Print features
        printf("\n    POS: %s", node->feature.pos);
        if (strlen(node->feature.pos_detail1) > 0) {
            printf(",%s", node->feature.pos_detail1);
        }
        if (strlen(node->feature.reading) > 0) {
            printf("\n    Reading: %s", node->feature.reading);
        }
        if (strlen(node->feature.pronunciation) > 0) {
            printf("\n    Pronunciation: %s", node->feature.pronunciation);
        }
        
        // Print cost info
        printf("\n    Cost: %d (lcAttr=%u, rcAttr=%u)", 
               node->cost, node->lcAttr, node->rcAttr);
        
        node = node->next;
    }
    
    printf("\n\n");
    mecab_full_free_nodes(mecab, result);
    mecab_full_destroy(mecab);
}

int main(int argc, char* argv[]) {
    if (argc < 2) {
        printf("Usage: %s <dict_path> [text]\n", argv[0]);
        return 1;
    }
    
    const char* dict_path = argv[1];
    const char* text = argc > 2 ? argv[2] : "今日は良い天気ですね。";
    
    test_mecab_full_parse(dict_path, text);
    
    // Test more examples
    const char* test_texts[] = {
        "東京都に住んでいます。",
        "日本語の音声合成システムを開発中です。",
        "すもももももももものうち",
        NULL
    };
    
    for (int i = 0; test_texts[i]; i++) {
        test_mecab_full_parse(dict_path, test_texts[i]);
    }
    
    return 0;
}