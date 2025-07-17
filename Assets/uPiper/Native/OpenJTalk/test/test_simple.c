#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "../include/openjtalk_wrapper.h"

int main() {
    printf("=== Simple OpenJTalk Test ===\n");
    
    // Initialize with test dictionary
    void* handle = openjtalk_create("test_dictionary");
    if (!handle) {
        printf("Failed to initialize with test_dictionary\n");
        // Try without dictionary
        handle = openjtalk_create(NULL);
        if (!handle) {
            printf("Failed to initialize without dictionary\n");
            return 1;
        }
        printf("Initialized without dictionary (light mode)\n");
    }
    
    // Test cases
    const char* test_texts[] = {
        "は",
        "です",
        "今日",
        "こんにちは",
        "今日は",
        NULL
    };
    
    for (int i = 0; test_texts[i]; i++) {
        printf("\nTest: \"%s\"\n", test_texts[i]);
        
        PhonemeResult* result = openjtalk_phonemize(handle, test_texts[i]);
        if (!result) {
            printf("  Error: Failed to phonemize\n");
            continue;
        }
        
        printf("  Count: %d\n", result->phoneme_count);
        printf("  Phonemes: %s\n", result->phonemes);
        printf("  IDs: ");
        for (int j = 0; j < result->phoneme_count; j++) {
            printf("%d ", result->phoneme_ids[j]);
        }
        printf("\n");
        
        openjtalk_free_result(result);
    }
    
    openjtalk_destroy(handle);
    return 0;
}