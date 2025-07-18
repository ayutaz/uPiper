#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "../src/mecab_dict_loader.h"
#include "../src/mecab_darts.h"

void test_lookup(MecabFullDictionary* dict, const char* word) {
    printf("\nLooking up: '%s' (length: %zu)\n", word, strlen(word));
    
    DictMatch matches[10];
    int count = mecab_dict_common_prefix_search(dict, word, strlen(word), matches, 10);
    
    printf("Found %d matches:\n", count);
    for (int i = 0; i < count; i++) {
        printf("  Match %d: length=%zu, is_unk=%d\n", 
               i, matches[i].length, matches[i].is_unk);
        
        if (matches[i].token) {
            const char* feature = mecab_dict_get_feature(dict, matches[i].token);
            printf("    Token: lcAttr=%u, rcAttr=%u, posid=%u, wcost=%d\n",
                   matches[i].token->lcAttr, matches[i].token->rcAttr,
                   matches[i].token->posid, matches[i].token->wcost);
            printf("    Feature: %s\n", feature ? feature : "(null)");
        }
    }
    
    // Also test direct Darts search
    if (dict->sys_darts) {
        printf("\nDirect Darts search:\n");
        DartsResult darts_results[10];
        int darts_count = darts_common_prefix_search(dict->sys_darts, word, strlen(word),
                                                     darts_results, 10);
        printf("  Found %d results\n", darts_count);
        for (int i = 0; i < darts_count; i++) {
            printf("    Result %d: length=%zu, value=%u\n",
                   i, darts_results[i].length, darts_results[i].value);
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
    
    printf("Dictionary loaded: %u entries\n", dict->sys_header.lexsize);
    
    // Test some common words
    test_lookup(dict, "今日");
    test_lookup(dict, "は");
    test_lookup(dict, "です");
    test_lookup(dict, "日本");
    test_lookup(dict, "東京");
    
    // Test single characters
    test_lookup(dict, "今");
    test_lookup(dict, "日");
    
    mecab_dict_free_full(dict);
    
    return 0;
}