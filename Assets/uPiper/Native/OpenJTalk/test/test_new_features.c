#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "../include/openjtalk_wrapper.h"

void test_long_text(void* handle) {
    printf("\n=== Testing Long Text (Lattice Resize) ===\n");
    
    // Create a very long text (> 4096 characters)
    char long_text[8192];
    strcpy(long_text, "これは");
    size_t current_len = strlen(long_text);
    const char* append_str = "とても長い文章の一部です。";
    size_t append_len = strlen(append_str);
    
    // Prevent buffer overflow
    for (int i = 0; i < 100 && current_len + append_len < 8000; i++) {
        strcat(long_text, append_str);
        current_len += append_len;
    }
    
    printf("Text length: %zu characters\n", strlen(long_text));
    
    PhonemeResult* result = openjtalk_phonemize(handle, long_text);
    if (result) {
        printf("✓ Long text processed successfully\n");
        printf("  Phoneme count: %d\n", result->phoneme_count);
        printf("  Total duration: %.2f seconds\n", result->total_duration);
    } else {
        printf("✗ Failed to process long text\n");
    }
}

void test_accent_patterns(void* handle) {
    printf("\n=== Testing Accent Patterns ===\n");
    
    const char* test_words[] = {
        "雨",      // アメ - should have accent on first mora
        "山",      // ヤマ - should have accent on second mora
        "桜",      // サクラ - should be flat
        "学校",    // ガッコウ - should be flat
        "先生",    // センセイ - should have accent on third mora
        NULL
    };
    
    for (int i = 0; test_words[i] != NULL; i++) {
        PhonemeResult* result = openjtalk_phonemize(handle, test_words[i]);
        if (result) {
            printf("Word: %s\n", test_words[i]);
            printf("  Phonemes: %s\n", result->phonemes);
            printf("  Count: %d\n", result->phoneme_count);
        }
    }
}

void test_options(void* handle) {
    printf("\n=== Testing Options ===\n");
    
    // Test getting options
    printf("Initial options:\n");
    printf("  use_accent: %s\n", openjtalk_get_option(handle, "use_accent"));
    printf("  use_duration: %s\n", openjtalk_get_option(handle, "use_duration"));
    printf("  speech_rate: %s\n", openjtalk_get_option(handle, "speech_rate"));
    
    // Test setting options
    openjtalk_set_option(handle, "use_accent", "false");
    openjtalk_set_option(handle, "speech_rate", "0.5");
    
    printf("\nAfter setting options:\n");
    printf("  use_accent: %s\n", openjtalk_get_option(handle, "use_accent"));
    printf("  speech_rate: %s\n", openjtalk_get_option(handle, "speech_rate"));
    
    // Test effect of speech rate
    const char* test_text = "こんにちは";
    
    printf("\nTesting speech rate effect on '%s':\n", test_text);
    
    // Normal speed
    openjtalk_set_option(handle, "speech_rate", "1.0");
    PhonemeResult* result1 = openjtalk_phonemize(handle, test_text);
    float duration1 = result1 ? result1->total_duration : 0;
    
    // Half speed
    openjtalk_set_option(handle, "speech_rate", "0.5");
    PhonemeResult* result2 = openjtalk_phonemize(handle, test_text);
    float duration2 = result2 ? result2->total_duration : 0;
    
    printf("  Normal speed (1.0x): %.2f seconds\n", duration1);
    printf("  Half speed (0.5x): %.2f seconds\n", duration2);
    printf("  Ratio: %.2fx\n", duration2 / duration1);
    
    // Reset options
    openjtalk_set_option(handle, "use_accent", "true");
    openjtalk_set_option(handle, "speech_rate", "1.0");
}

int main(int argc, char* argv[]) {
    const char* dict_path = argc > 1 ? argv[1] : "../test_dictionary";
    
    printf("OpenJTalk New Features Test\n");
    printf("Dictionary: %s\n", dict_path);
    
    void* handle = openjtalk_create(dict_path);
    if (!handle) {
        fprintf(stderr, "Failed to create OpenJTalk instance\n");
        return 1;
    }
    
    printf("Version: %s\n", openjtalk_get_version());
    
    // Run tests
    test_long_text(handle);
    test_accent_patterns(handle);
    test_options(handle);
    
    openjtalk_destroy(handle);
    
    printf("\n=== Test completed ===\n");
    return 0;
}