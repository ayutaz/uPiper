#include "../include/openjtalk_wrapper.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

void test_kanji_phonemization(const char* text) {
    printf("\n=== Testing: %s ===\n", text);
    
    // Enable debug output
    setenv("DEBUG_MECAB", "1", 1);
    
    void* phonemizer = openjtalk_create("../dictionary");
    if (!phonemizer) {
        printf("Failed to create phonemizer\n");
        return;
    }
    
    PhonemeResult* result = openjtalk_phonemize(phonemizer, text);
    
    if (result) {
        printf("Phoneme count: %d\n", result->phoneme_count);
        printf("Phonemes: %s\n", result->phonemes);
        printf("Phoneme IDs: ");
        for (int i = 0; i < result->phoneme_count; i++) {
            printf("%d ", result->phoneme_ids[i]);
        }
        printf("\n");
        
        openjtalk_free_result(result);
    } else {
        const char* error = openjtalk_get_error_string(openjtalk_get_last_error(phonemizer));
        printf("Error: %s\n", error);
    }
    
    openjtalk_destroy(phonemizer);
}

int main() {
    // Test various kanji inputs
    test_kanji_phonemization("世界");  // "sekai" - world
    test_kanji_phonemization("日本");  // "nihon" - Japan
    test_kanji_phonemization("漢字");  // "kanji" - kanji
    test_kanji_phonemization("音声");  // "onsei" - voice
    test_kanji_phonemization("合成");  // "gousei" - synthesis
    
    // Test mixed content
    test_kanji_phonemization("こんにちは世界");  // "konnichiwa sekai"
    test_kanji_phonemization("日本語です");      // "nihongo desu"
    
    return 0;
}