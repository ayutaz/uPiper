#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "../src/mecab_dict_loader.h"

void test_char_categories(MecabFullDictionary* dict) {
    printf("Testing character categories:\n");
    
    // Test cases with expected categories
    struct {
        const char* text;
        const char* desc;
        uint32_t expected_category;
    } test_cases[] = {
        {"あ", "Hiragana", 6},
        {"ア", "Katakana", 7},
        {"漢", "Kanji", 2},
        {"A", "Alpha", 5},
        {"1", "Numeric", 4},
        {" ", "Space", 1},
        {"。", "Symbol", 3},
        {NULL, NULL, 0}
    };
    
    for (int i = 0; test_cases[i].text; i++) {
        // Decode UTF-8 to get codepoint
        const unsigned char* p = (const unsigned char*)test_cases[i].text;
        uint32_t codepoint = 0;
        
        if (*p < 0x80) {
            codepoint = *p;
        } else if ((*p & 0xE0) == 0xC0) {
            codepoint = ((*p & 0x1F) << 6) | (p[1] & 0x3F);
        } else if ((*p & 0xF0) == 0xE0) {
            codepoint = ((*p & 0x0F) << 12) | ((p[1] & 0x3F) << 6) | (p[2] & 0x3F);
        }
        
        uint32_t category = mecab_dict_get_char_category(dict, codepoint);
        printf("  '%s' (%s) -> category %u", 
               test_cases[i].text, test_cases[i].desc, category);
        
        if (category == test_cases[i].expected_category) {
            printf(" [OK]\n");
        } else {
            printf(" [EXPECTED %u]\n", test_cases[i].expected_category);
        }
    }
}

int main() {
    printf("=== Testing char.bin loading ===\n\n");
    
    // Set debug environment variable
    setenv("DEBUG_CHAR_BIN", "1", 1);
    
    // Load dictionary
    const char* dict_path = "../test_dictionary";
    MecabFullDictionary* dict = mecab_dict_load(dict_path);
    
    if (!dict) {
        fprintf(stderr, "Failed to load dictionary\n");
        return 1;
    }
    
    printf("\nDictionary loaded successfully\n");
    printf("Character definition count: %u\n", dict->char_def_count);
    printf("Character property loaded: %s\n\n", 
           dict->char_property ? "Yes" : "No");
    
    // Test character categories
    test_char_categories(dict);
    
    // Cleanup
    mecab_dict_free_full(dict);
    
    printf("\n=== Test completed ===\n");
    return 0;
}