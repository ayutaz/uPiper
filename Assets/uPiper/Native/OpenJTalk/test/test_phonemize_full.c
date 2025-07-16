#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "../include/openjtalk_wrapper.h"
#include "../src/openjtalk_phonemizer.h"

void test_phonemize(void* openjtalk, const char* text) {
    printf("\n=== Testing: \"%s\" ===\n", text);
    
    PhonemeResult* result = openjtalk_phonemize(openjtalk, text);
    if (result) {
        printf("Phoneme count: %d\n", result->phoneme_count);
        printf("Total duration: %.2f seconds\n", result->total_duration);
        printf("Phonemes: %s\n", result->phonemes);
        
        printf("\nDetailed phonemes:\n");
        for (int i = 0; i < result->phoneme_count; i++) {
            const char* phoneme_str = openjtalk_phonemizer_get_phoneme_string(
                (PhonemeID)result->phoneme_ids[i]
            );
            printf("  [%d] %s (ID: %d, Duration: %.3fs)\n", 
                   i, phoneme_str, result->phoneme_ids[i], result->durations[i]);
        }
    } else {
        int error = openjtalk_get_last_error(openjtalk);
        printf("Error: %s\n", openjtalk_get_error_string(error));
    }
}

void print_dictionary_info(void* openjtalk) {
    const char* dict_size = openjtalk_get_option(openjtalk, "dictionary_size");
    if (dict_size) {
        printf("Dictionary size: %s entries\n", dict_size);
    }
}

int main(int argc, char* argv[]) {
    printf("OpenJTalk Full Phonemization Test\n");
    printf("Version: %s\n", openjtalk_get_version());
    
    const char* dict_path = argc > 1 ? argv[1] : "dictionary";
    printf("Dictionary path: %s\n", dict_path);
    
    // Create OpenJTalk instance
    void* openjtalk = openjtalk_create(dict_path);
    if (!openjtalk) {
        fprintf(stderr, "Failed to create OpenJTalk instance\n");
        return 1;
    }
    
    print_dictionary_info(openjtalk);
    
    // Test various texts
    const char* test_texts[] = {
        "こんにちは",
        "今日は良い天気です",
        "日本語の音声合成システム",
        "東京都に住んでいます",
        "すもももももももものうち",
        "音声合成は面白い技術です。",
        "OpenJTalkを使って音素変換を行います。",
        NULL
    };
    
    for (int i = 0; test_texts[i] != NULL; i++) {
        test_phonemize(openjtalk, test_texts[i]);
    }
    
    // Test with user input if provided
    if (argc > 2) {
        printf("\n=== User input test ===\n");
        for (int i = 2; i < argc; i++) {
            test_phonemize(openjtalk, argv[i]);
        }
    }
    
    // Clean up
    openjtalk_destroy(openjtalk);
    
    printf("\nTest completed successfully.\n");
    return 0;
}