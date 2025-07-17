#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "../src/mecab_light.h"
#include "../src/openjtalk_phonemizer.h"

void test_mecab_parse(const char* text) {
    printf("\n=== Testing Mecab Parse: \"%s\" ===\n", text);
    
    MecabLight* mecab = mecab_light_create(NULL);
    if (!mecab) {
        printf("Failed to create MecabLight\n");
        return;
    }
    
    MecabNode* nodes = mecab_light_parse(mecab, text);
    if (!nodes) {
        printf("Failed to parse text: %s\n", mecab_light_get_error(mecab));
        mecab_light_destroy(mecab);
        return;
    }
    
    printf("Parsed nodes:\n");
    MecabNode* current = nodes;
    int count = 0;
    while (current) {
        printf("  [%d] Surface: '%s', POS: %s, Reading: %s\n", 
               count++, 
               current->surface,
               current->feature.pos,
               current->feature.reading);
        current = current->next;
    }
    
    mecab_light_free_nodes(mecab, nodes);
    mecab_light_destroy(mecab);
}

void test_phonemizer(const char* text) {
    printf("\n=== Testing Phonemizer: \"%s\" ===\n", text);
    
    OpenJTalkPhonemizer* phonemizer = openjtalk_phonemizer_create();
    if (!phonemizer) {
        printf("Failed to create phonemizer\n");
        return;
    }
    
    if (!openjtalk_phonemizer_initialize(phonemizer, NULL)) {
        printf("Failed to initialize phonemizer\n");
        openjtalk_phonemizer_destroy(phonemizer);
        return;
    }
    
    PhonemeInfo phonemes[256];
    int count = openjtalk_phonemizer_phonemize(phonemizer, text, phonemes, 256);
    
    printf("Phoneme count: %d\n", count);
    printf("Phonemes: ");
    for (int i = 0; i < count; i++) {
        const char* phoneme_str = openjtalk_phonemizer_get_phoneme_string(phonemes[i].id);
        printf("%s ", phoneme_str);
    }
    printf("\n");
    
    openjtalk_phonemizer_destroy(phonemizer);
}

int main() {
    printf("=== Mecab Light Debug Test ===\n");
    
    // Test basic words
    test_mecab_parse("今日");
    test_mecab_parse("日本語");
    test_mecab_parse("東京");
    test_mecab_parse("今日は良い天気です");
    
    // Test phonemizer
    test_phonemizer("今日");
    test_phonemizer("日本語");
    test_phonemizer("東京");
    test_phonemizer("今日は良い天気です");
    
    return 0;
}