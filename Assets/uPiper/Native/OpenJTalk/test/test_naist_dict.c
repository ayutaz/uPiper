#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "../include/openjtalk_wrapper.h"

int main(int argc, char* argv[]) {
    printf("=== NAIST Dictionary Test ===\n");
    
    const char* dict_path = NULL;
    if (argc > 1) {
        dict_path = argv[1];
        printf("Using dictionary: %s\n", dict_path);
    } else {
        dict_path = "../naist_jdic/open_jtalk_dic_utf_8-1.11";
        printf("Using default NAIST dictionary: %s\n", dict_path);
    }
    
    // Initialize with NAIST dictionary
    void* handle = openjtalk_create(dict_path);
    if (!handle) {
        int error = openjtalk_get_last_error(NULL);
        const char* error_msg = openjtalk_get_error_string(error);
        printf("Failed to initialize: %s\n", error_msg);
        return 1;
    }
    
    printf("Successfully initialized with NAIST dictionary\n");
    
    // Test cases
    const char* test_texts[] = {
        "こんにちは",
        "今日はいい天気ですね",
        "私は学生です",
        "日本語の発音テスト",
        NULL
    };
    
    for (int i = 0; test_texts[i]; i++) {
        printf("\n================\n");
        printf("Test %d: \"%s\"\n", i+1, test_texts[i]);
        printf("================\n");
        
        PhonemeResult* result = openjtalk_phonemize(handle, test_texts[i]);
        if (!result) {
            int error = openjtalk_get_last_error(handle);
            const char* error_msg = openjtalk_get_error_string(error);
            printf("  Error: %s\n", error_msg);
            continue;
        }
        
        printf("  Phoneme count: %d\n", result->phoneme_count);
        printf("  Phonemes: %s\n", result->phonemes);
        printf("  IDs: ");
        for (int j = 0; j < result->phoneme_count && j < 20; j++) {
            printf("%d ", result->phoneme_ids[j]);
            if (j == 19 && result->phoneme_count > 20) {
                printf("... (%d more)", result->phoneme_count - 20);
            }
        }
        printf("\n");
        
        if (result->durations && result->phoneme_count > 0) {
            printf("  Durations: ");
            for (int j = 0; j < result->phoneme_count && j < 10; j++) {
                printf("%.3f ", result->durations[j]);
                if (j == 9 && result->phoneme_count > 10) {
                    printf("... (%d more)", result->phoneme_count - 10);
                }
            }
            printf("\n");
        }
        
        // Pitches not available in current implementation
        
        printf("  Total duration: %.3f\n", result->total_duration);
        
        openjtalk_free_result(result);
    }
    
    printf("\n=== Test Complete ===\n");
    openjtalk_destroy(handle);
    return 0;
}