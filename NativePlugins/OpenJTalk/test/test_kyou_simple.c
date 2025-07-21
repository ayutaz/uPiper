#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "../include/openjtalk_wrapper.h"

void print_hex(const char* str) {
    printf("HEX: ");
    for (int i = 0; str[i] != '\0'; i++) {
        printf("%02X ", (unsigned char)str[i]);
    }
    printf("\n");
}

int main(int argc, char* argv[]) {
    if (argc != 2) {
        printf("Usage: %s <dictionary_path>\n", argv[0]);
        return 1;
    }
    
    const char* dict_path = argv[1];
    
    // Initialize OpenJTalk
    printf("Initializing OpenJTalk with dictionary: %s\n", dict_path);
    void* handle = openjtalk_create(dict_path);
    if (!handle) {
        printf("ERROR: Failed to create OpenJTalk instance\n");
        return 1;
    }
    
    printf("OpenJTalk version: %s\n", openjtalk_get_version());
    
    // Test cases
    const char* test_texts[] = {
        "今日",           // kyou (kanji)
        "きょう",         // kyou (hiragana)
        "今日は",         // kyou wa
        "今日はいい天気", // kyou wa ii tenki
        "本",             // hon (simple kanji)
        "日本",           // nihon/nippon
        NULL
    };
    
    for (int i = 0; test_texts[i] != NULL; i++) {
        printf("\n=== Testing: %s ===\n", test_texts[i]);
        print_hex(test_texts[i]);
        
        PhonemeResult result = openjtalk_phonemize(handle, test_texts[i]);
        
        if (result.success) {
            printf("Success! Phoneme count: %d\n", result.phoneme_count);
            printf("Phonemes: %s\n", result.phonemes);
            
            // Print individual phonemes
            printf("Individual phonemes:\n");
            char* phoneme_copy = strdup(result.phonemes);
            char* token = strtok(phoneme_copy, " ");
            int idx = 0;
            while (token != NULL) {
                printf("  [%d] '%s'\n", idx++, token);
                token = strtok(NULL, " ");
            }
            free(phoneme_copy);
            
            // Check for specific patterns
            if (strstr(test_texts[i], "今日") != NULL) {
                if (strstr(result.phonemes, "k y o") != NULL || 
                    strstr(result.phonemes, "ky o") != NULL) {
                    printf("✓ '今日' correctly phonemized\n");
                } else {
                    printf("✗ '今日' NOT found in phonemes!\n");
                }
            }
            
            openjtalk_free_result(&result);
        } else {
            printf("ERROR: Phonemization failed\n");
        }
    }
    
    // Cleanup
    openjtalk_destroy(handle);
    printf("\n=== Test completed ===\n");
    
    return 0;
}