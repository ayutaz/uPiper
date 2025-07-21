#include "include/openjtalk_wrapper.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <locale.h>

#ifdef _WIN32
#include <windows.h>
#endif

void print_hex(const char* str) {
    printf("HEX: ");
    for (const unsigned char* p = (const unsigned char*)str; *p; p++) {
        printf("%02X ", *p);
    }
    printf("\n");
}

void test_phonemization(void* phonemizer, const char* text) {
    printf("\n========================================\n");
    printf("Testing: '%s'\n", text);
    printf("Length: %zu\n", strlen(text));
    print_hex(text);
    
    PhonemeResult* result = openjtalk_phonemize(phonemizer, text);
    
    if (result) {
        printf("Success!\n");
        printf("Phoneme count: %d\n", result->phoneme_count);
        printf("Phonemes: %s\n", result->phonemes);
        print_hex(result->phonemes);
        
        // Print individual phonemes
        printf("Individual phonemes:\n");
        char* phoneme_copy = strdup(result->phonemes);
        char* token = strtok(phoneme_copy, " ");
        int i = 0;
        while (token != NULL) {
            printf("  [%d] '%s'", i++, token);
            print_hex(token);
            token = strtok(NULL, " ");
        }
        free(phoneme_copy);
        
        printf("Total duration: %.3f\n", result->total_duration);
        
        openjtalk_free_result(result);
    } else {
        int error = openjtalk_get_last_error(phonemizer);
        const char* error_str = openjtalk_get_error_string(error);
        printf("Error: %s (code: %d)\n", error_str, error);
    }
}

int main(int argc, char* argv[]) {
    // Set locale for proper UTF-8 handling
#ifdef _WIN32
    SetConsoleOutputCP(CP_UTF8);
    SetConsoleCP(CP_UTF8);
#endif
    setlocale(LC_ALL, "");
    
    // Get dictionary path from command line or use default
    const char* dict_path = (argc > 1) ? argv[1] : "dictionary";
    
    printf("OpenJTalk Windows Debug Test\n");
    printf("Dictionary: %s\n", dict_path);
    printf("Version: %s\n", openjtalk_get_version());
    
    // Create phonemizer
    void* phonemizer = openjtalk_create(dict_path);
    if (!phonemizer) {
        fprintf(stderr, "Failed to create phonemizer\n");
        return 1;
    }
    
    // Test cases focusing on "今日" (kyou)
    test_phonemization(phonemizer, "今日");
    test_phonemization(phonemizer, "きょう");  // Hiragana version
    test_phonemization(phonemizer, "キョウ");  // Katakana version
    test_phonemization(phonemizer, "今日は");
    test_phonemization(phonemizer, "今日は良い天気です");
    
    // Test other potentially problematic words
    test_phonemization(phonemizer, "東京");  // Tokyo
    test_phonemization(phonemizer, "京都");  // Kyoto
    test_phonemization(phonemizer, "教育");  // Education (kyouiku)
    
    // Simple test cases
    test_phonemization(phonemizer, "あ");
    test_phonemization(phonemizer, "こんにちは");
    
    // Cleanup
    openjtalk_destroy(phonemizer);
    
    printf("\n========================================\n");
    printf("Test complete.\n");
    
    return 0;
}