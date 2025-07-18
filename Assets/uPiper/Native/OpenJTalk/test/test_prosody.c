#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "../include/openjtalk_wrapper.h"

void test_prosody(void* openjtalk, const char* text) {
    printf("\n=== Testing prosody for: \"%s\" ===\n", text);
    
    PhonemeResult* result = openjtalk_phonemize(openjtalk, text);
    if (result) {
        printf("Phoneme count: %d\n", result->phoneme_count);
        printf("Total duration: %.3f seconds\n", result->total_duration);
        
        printf("\nDetailed phonemes with prosody:\n");
        printf("%-4s %-8s %-10s %-8s %-8s\n", "Idx", "Phoneme", "Duration", "Accent", "ID");
        printf("------------------------------------------------\n");
        
        // Split phonemes string to access individual phonemes
        char phonemes_copy[1024];
        strncpy(phonemes_copy, result->phonemes, sizeof(phonemes_copy) - 1);
        phonemes_copy[sizeof(phonemes_copy) - 1] = '\0';
        
        char* phoneme_array[100];
        int phoneme_idx = 0;
        char* token = strtok(phonemes_copy, " ");
        while (token && phoneme_idx < 100) {
            phoneme_array[phoneme_idx++] = token;
            token = strtok(NULL, " ");
        }
        
        for (int i = 0; i < result->phoneme_count; i++) {
            const char* accent_str = "";
            if (result->durations[i] > 0) {  // Use duration as proxy for accent type
                if (i > 0 && result->durations[i] > result->durations[i-1] * 1.05) {
                    accent_str = "HIGH";
                } else if (i > 0 && result->durations[i] < result->durations[i-1] * 0.95) {
                    accent_str = "LOW";
                } else {
                    accent_str = "-";
                }
            }
            
            printf("[%-2d] %-8s %6.3fs    %-8s %d\n", 
                   i,
                   i < phoneme_idx ? phoneme_array[i] : "?",
                   result->durations[i],
                   accent_str,
                   result->phoneme_ids[i]);
        }
        
        // Calculate average phoneme duration
        float avg_duration = result->total_duration / result->phoneme_count;
        printf("\nAverage phoneme duration: %.3fs\n", avg_duration);
        
        // Identify mora boundaries
        printf("\nMora boundaries (estimated):\n");
        int mora_count = 0;
        for (int i = 0; i < result->phoneme_count; i++) {
            // Vowels and N typically end moras
            int id = result->phoneme_ids[i];
            if ((id >= 2 && id <= 6) || id == 16) {  // Vowels or N
                mora_count++;
                printf("  Mora %d ends at phoneme %d\n", mora_count, i);
            }
        }
        
        openjtalk_free_result(result);
    } else {
        int error = openjtalk_get_last_error(openjtalk);
        printf("Error: %s\n", openjtalk_get_error_string(error));
    }
}

int main(int argc, char* argv[]) {
    printf("OpenJTalk Prosody Test\n");
    printf("Version: %s\n", openjtalk_get_version());
    
    const char* dict_path = argc > 1 ? argv[1] : "test_dictionary";
    
    // Create OpenJTalk instance
    void* openjtalk = openjtalk_create(dict_path);
    if (!openjtalk) {
        fprintf(stderr, "Failed to create OpenJTalk instance\n");
        return 1;
    }
    
    // Test various texts with different accent patterns
    const char* test_texts[] = {
        "雨",          // あめ (1型 - 頭高型)
        "飴",          // あめ (0型 - 平板型) 
        "橋",          // はし (0型)
        "箸",          // はし (1型)
        "花が咲く",    // Multiple words
        "ありがとうございます",  // Long phrase
        NULL
    };
    
    for (int i = 0; test_texts[i] != NULL; i++) {
        test_prosody(openjtalk, test_texts[i]);
    }
    
    // Clean up
    openjtalk_destroy(openjtalk);
    
    return 0;
}