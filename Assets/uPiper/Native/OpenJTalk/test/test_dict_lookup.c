#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "../src/mecab_light_impl.h"
#include "../src/mecab_dict_minimal.h"

int main() {
    printf("=== Dictionary Lookup Test ===\n");
    
    // Create dictionary
    MecabDictionary* dict = mecab_dict_create_minimal();
    if (!dict) {
        printf("Failed to create dictionary\n");
        return 1;
    }
    
    printf("Dictionary created with %d entries\n", dict->entry_count);
    
    // Test lookups
    const char* test_words[] = {
        "は", "が", "です", "今日", "明日", "日本語", NULL
    };
    
    for (int i = 0; test_words[i]; i++) {
        const char* word = test_words[i];
        int len = strlen(word);
        
        printf("\nLooking up: '%s' (len=%d)\n", word, len);
        
        const DictEntry* entry = mecab_dict_lookup(dict, word, len);
        if (entry) {
            const char* reading = mecab_get_reading(dict, entry);
            const char* base = mecab_get_baseform(dict, entry);
            const char* pos = mecab_get_pos_name(entry->pos_id);
            printf("  Found: base='%s', reading='%s', pos='%s'\n", base, reading, pos);
        } else {
            printf("  Not found\n");
        }
    }
    
    // Check dictionary contents
    printf("\n=== First 10 dictionary entries ===\n");
    for (int i = 0; i < 10 && i < dict->entry_count; i++) {
        const DictEntry* entry = &dict->entries[i];
        const char* base = mecab_get_baseform(dict, entry);
        const char* reading = mecab_get_reading(dict, entry);
        printf("[%d] base='%s', reading='%s'\n", i, base, reading);
    }
    
    mecab_dict_free(dict);
    return 0;
}