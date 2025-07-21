#include <stdio.h>
#include <stdlib.h>
#include "../include/openjtalk_wrapper.h"

int main(int argc, char* argv[]) {
    printf("Simple OpenJTalk Full Test\n");
    printf("Version: %s\n", openjtalk_get_version());
    
    const char* dict_path = argc > 1 ? argv[1] : "test_dictionary";
    printf("Dictionary path: %s\n", dict_path);
    
    // Create OpenJTalk instance
    printf("\nCreating OpenJTalk instance...\n");
    void* openjtalk = openjtalk_create(dict_path);
    if (!openjtalk) {
        fprintf(stderr, "Failed to create OpenJTalk instance\n");
        return 1;
    }
    
    printf("OpenJTalk instance created successfully!\n");
    
    // Test simple text
    const char* text = "こんにちは";
    printf("\nTesting phonemization for: \"%s\"\n", text);
    
    PhonemeResult* result = openjtalk_phonemize(openjtalk, text);
    if (result) {
        printf("Success!\n");
        printf("  Phoneme count: %d\n", result->phoneme_count);
        printf("  Phonemes: %s\n", result->phonemes);
        printf("  Phoneme IDs:");
        for (int i = 0; i < result->phoneme_count; i++) {
            printf(" %d", result->phoneme_ids[i]);
        }
        printf("\n");
    } else {
        int error = openjtalk_get_last_error(openjtalk);
        printf("Error: %s\n", openjtalk_get_error_string(error));
    }
    
    // Clean up
    openjtalk_destroy(openjtalk);
    printf("\nTest completed.\n");
    
    return 0;
}