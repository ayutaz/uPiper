#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "../src/mecab_light.h"
#include "../src/openjtalk_phonemizer.h"

void test_mecab_parse(const char* text) {
    printf("\n=== Testing: \"%s\" ===\n", text);
    
    // Create Mecab instance
    MecabLight* mecab = mecab_light_create(NULL);
    if (!mecab) {
        printf("Failed to create MecabLight\n");
        return;
    }
    
    // Parse text
    MecabNode* nodes = mecab_light_parse(mecab, text);
    if (!nodes) {
        printf("Failed to parse text\n");
        mecab_light_destroy(mecab);
        return;
    }
    
    // Print nodes
    printf("Mecab nodes:\n");
    MecabNode* current = nodes;
    int count = 0;
    while (current) {
        printf("  [%d] surface='%s', pos='%s', reading='%s'\n",
               count++,
               current->surface,
               current->feature.pos,
               current->feature.reading);
        current = current->next;
    }
    
    // Test phonemizer
    printf("\nPhonemizer output:\n");
    OpenJTalkPhonemizer* phonemizer = openjtalk_phonemizer_create();
    if (phonemizer && openjtalk_phonemizer_initialize(phonemizer, NULL)) {
        PhonemeInfo phonemes[256];
        int phoneme_count = openjtalk_phonemizer_phonemize(phonemizer, text, phonemes, 256);
        
        printf("  Phoneme count: %d\n", phoneme_count);
        printf("  Phonemes: ");
        for (int i = 0; i < phoneme_count; i++) {
            const char* phoneme_str = openjtalk_phonemizer_get_phoneme_string(phonemes[i].id);
            printf("%s ", phoneme_str);
        }
        printf("\n");
        
        openjtalk_phonemizer_destroy(phonemizer);
    }
    
    // Clean up
    mecab_light_free_nodes(mecab, nodes);
    mecab_light_destroy(mecab);
}

int main() {
    printf("=== Mecab Integration Test ===\n");
    
    test_mecab_parse("今日");
    test_mecab_parse("今日は");
    test_mecab_parse("今日は良い天気です");
    
    return 0;
}