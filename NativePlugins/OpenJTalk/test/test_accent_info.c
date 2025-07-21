#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "../include/openjtalk_wrapper.h"

int main(int argc, char* argv[]) {
    printf("Accent Information Test\n");
    
    const char* dict_path = argc > 1 ? argv[1] : "test_dictionary";
    printf("Dictionary path: %s\n", dict_path);
    
    // Create OpenJTalk instance
    void* handle = openjtalk_create(dict_path);
    if (!handle) {
        fprintf(stderr, "Failed to create OpenJTalk instance\n");
        return 1;
    }
    
    // Test words with known accent patterns
    const char* test_words[] = {
        "東京",      // とうきょう (0型 - 平板型)
        "花",        // はな (2型 - 尾高型)
        "雨",        // あめ (1型 - 頭高型)
        "日本語",    // にほんご
        "ありがとう", // ありがとう
        NULL
    };
    
    for (int i = 0; test_words[i] != NULL; i++) {
        printf("\n=== Testing: %s ===\n", test_words[i]);
        
        PhonemeResult* result = openjtalk_phonemize(handle, test_words[i]);
        if (result) {
            printf("Phonemes: %s\n", result->phonemes);
            printf("Phoneme count: %d\n", result->phoneme_count);
            printf("Total duration: %.3f seconds\n", result->total_duration);
            
            // Show individual phoneme details
            if (result->durations) {
                printf("Phoneme durations:\n");
                // Create a copy to avoid modifying the original
                char phonemes_copy[1024];
                strncpy(phonemes_copy, result->phonemes, sizeof(phonemes_copy) - 1);
                phonemes_copy[sizeof(phonemes_copy) - 1] = '\0';
                
                int j = 0;
                char* phoneme = strtok(phonemes_copy, " ");
                while (phoneme && j < result->phoneme_count) {
                    printf("  %s: %.3fs\n", phoneme, result->durations[j]);
                    phoneme = strtok(NULL, " ");
                    j++;
                }
            }
            
            openjtalk_free_result(result);
        } else {
            printf("Failed to phonemize: %s\n", test_words[i]);
        }
    }
    
    // Clean up
    openjtalk_destroy(handle);
    
    return 0;
}