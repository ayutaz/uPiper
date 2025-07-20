#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "../include/openjtalk_wrapper.h"

void test_phonemize(void* handle, const char* text) {
    printf("\n=== Testing: %s ===\n", text);
    
    PhonemeResult* result = openjtalk_phonemize(handle, text);
    if (result) {
        printf("Phonemes: %s\n", result->phonemes);
        printf("Phoneme count: %d\n", result->phoneme_count);
        printf("Total duration: %.2f seconds\n", result->total_duration);
        
        openjtalk_free_result(result);
    } else {
        int error = openjtalk_get_last_error(handle);
        printf("Error: %s (code: %d)\n", openjtalk_get_error_string(error), error);
    }
}

int main() {
    printf("OpenJTalk Full Version Test\n");
    printf("Version: %s\n", openjtalk_get_version());
    
    // Get dictionary path from environment or use default
    const char* dict_path = getenv("OPENJTALK_DICT");
    if (!dict_path) {
        dict_path = "../naist_jdic/open_jtalk_dic_utf_8-1.11";
    }
    
    printf("Dictionary path: %s\n", dict_path);
    
    // Create OpenJTalk instance
    void* handle = openjtalk_create(dict_path);
    if (!handle) {
        fprintf(stderr, "Failed to create OpenJTalk instance\n");
        return 1;
    }
    
    // Test cases
    test_phonemize(handle, "こんにちは");
    test_phonemize(handle, "今日はいい天気ですね");
    test_phonemize(handle, "日本語の音声合成");
    test_phonemize(handle, "OpenJTalkを使った音素変換");
    test_phonemize(handle, "123");
    test_phonemize(handle, "テスト");
    
    // Cleanup
    openjtalk_destroy(handle);
    
    return 0;
}