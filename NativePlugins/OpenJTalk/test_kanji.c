#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <locale.h>
#include "openjtalk_wrapper.h"

int main(int argc, char* argv[]) {
    // Set locale for UTF-8
    setlocale(LC_ALL, "");
    
    // Set debug environment
    setenv("DEBUG_MECAB", "1", 1);
    setenv("UPIPER_DEBUG", "1", 1);
    
    printf("=== OpenJTalk Kanji Test ===\n\n");
    
    // Create instance
    void* handle = openjtalk_create("dictionary/");
    if (!handle) {
        fprintf(stderr, "Failed to create OpenJTalk instance\n");
        return 1;
    }
    
    printf("OpenJTalk initialized (version: %s)\n\n", openjtalk_get_version());
    
    // Test texts
    const char* test_texts[] = {
        "こんにちは",
        "今日",
        "天気",
        "今日はいい天気ですね",
        "私は学生です",
        NULL
    };
    
    for (int i = 0; test_texts[i]; i++) {
        printf("----------------------------------------\n");
        printf("Test %d: \"%s\"\n", i + 1, test_texts[i]);
        printf("----------------------------------------\n");
        
        PhonemeResult* result = openjtalk_phonemize(handle, test_texts[i]);
        
        if (result) {
            printf("Phoneme count: %d\n", result->phoneme_count);
            printf("Phoneme string: %s\n", result->phonemes);
            printf("Total duration: %.3f seconds\n", result->total_duration);
            
            // Print individual phonemes
            if (result->phonemes) {
                printf("\nIndividual phonemes:\n");
                char* phoneme_copy = strdup(result->phonemes);
                char* token = strtok(phoneme_copy, " ");
                int idx = 0;
                while (token) {
                    printf("  [%2d] '%s'", idx, token);
                    if (result->phoneme_ids && idx < result->phoneme_count) {
                        printf(" (ID: %d)", result->phoneme_ids[idx]);
                    }
                    if (result->durations && idx < result->phoneme_count) {
                        printf(" (duration: %.3f)", result->durations[idx]);
                    }
                    printf("\n");
                    token = strtok(NULL, " ");
                    idx++;
                }
                free(phoneme_copy);
            }
            
            openjtalk_free_result(result);
        } else {
            int error_code = openjtalk_get_last_error(handle);
            const char* error_msg = openjtalk_get_error_string(error_code);
            fprintf(stderr, "Failed to phonemize: %s (code: %d)\n", error_msg, error_code);
        }
        
        printf("\n");
    }
    
    // Cleanup
    openjtalk_destroy(handle);
    
    printf("=== Test Complete ===\n");
    return 0;
}