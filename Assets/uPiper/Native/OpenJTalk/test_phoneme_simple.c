#include <stdio.h>
#include <stdlib.h>
#include "src/openjtalk_phonemizer.h"

int main(int argc, char* argv[]) {
    const char* text = argc > 1 ? argv[1] : "こんにちは";
    
    printf("Testing phonemization for: '%s'\n", text);
    
    // Create phonemizer
    OpenJTalkPhonemizer* phonemizer = openjtalk_phonemizer_create();
    if (!phonemizer) {
        fprintf(stderr, "Failed to create phonemizer\n");
        return 1;
    }
    
    // Initialize with dictionary
    if (!openjtalk_phonemizer_initialize(phonemizer, "dictionary")) {
        fprintf(stderr, "Failed to initialize phonemizer\n");
        openjtalk_phonemizer_destroy(phonemizer);
        return 1;
    }
    
    // Phonemize
    int phoneme_ids[256];
    int count = openjtalk_phonemizer_get_ids(phonemizer, text, phoneme_ids, 256);
    
    if (count > 0) {
        printf("Phoneme count: %d\n", count);
        printf("Phoneme IDs: ");
        for (int i = 0; i < count; i++) {
            printf("%d ", phoneme_ids[i]);
        }
        printf("\n");
        
        printf("Phonemes: ");
        for (int i = 0; i < count; i++) {
            const char* phoneme = openjtalk_phonemizer_get_phoneme_string(phoneme_ids[i]);
            printf("%s ", phoneme);
        }
        printf("\n");
    } else {
        fprintf(stderr, "Phonemization failed\n");
    }
    
    // Clean up
    openjtalk_phonemizer_destroy(phonemizer);
    
    return 0;
}