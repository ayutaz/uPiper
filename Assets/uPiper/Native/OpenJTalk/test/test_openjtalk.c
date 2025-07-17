#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <locale.h>
#include "openjtalk_wrapper.h"

#define TEST_PASSED "\033[32mPASSED\033[0m"
#define TEST_FAILED "\033[31mFAILED\033[0m"

// Test counter
static int tests_passed = 0;
static int tests_failed = 0;

// Test macros
#define TEST_START(name) printf("\n[TEST] %s\n", name)
#define TEST_ASSERT(condition, message) \
    do { \
        if (condition) { \
            printf("  ✓ %s\n", message); \
            tests_passed++; \
        } else { \
            printf("  ✗ %s\n", message); \
            tests_failed++; \
        } \
    } while(0)

// Test version information
void test_version() {
    TEST_START("Version Information");
    
    const char* version = openjtalk_get_version();
    TEST_ASSERT(version != NULL, "Version string is not NULL");
    TEST_ASSERT(strlen(version) > 0, "Version string is not empty");
    printf("  Version: %s\n", version);
}

// Test initialization and cleanup
void test_init_cleanup() {
    TEST_START("Initialization and Cleanup");
    
    // Test with NULL dictionary path
    void* handle1 = openjtalk_create(NULL);
    TEST_ASSERT(handle1 == NULL, "Creating with NULL dictionary path returns NULL");
    
    // Test with valid dictionary path
    void* handle2 = openjtalk_create("../test_dictionary");
    TEST_ASSERT(handle2 != NULL, "Creating with valid path returns handle");
    
    if (handle2) {
        // Test cleanup
        openjtalk_destroy(handle2);
        TEST_ASSERT(1, "Destroy completed without crash");
    }
    
    // Test double destroy (should not crash)
    openjtalk_destroy(NULL);
    TEST_ASSERT(1, "Destroy with NULL handle doesn't crash");
}

// Test error handling
void test_error_handling() {
    TEST_START("Error Handling");
    
    // Test error strings
    const char* err_str;
    
    err_str = openjtalk_get_error_string(OPENJTALK_SUCCESS);
    TEST_ASSERT(strcmp(err_str, "Success") == 0, "Success error string");
    
    err_str = openjtalk_get_error_string(OPENJTALK_ERROR_INVALID_HANDLE);
    TEST_ASSERT(strstr(err_str, "handle") != NULL, "Invalid handle error string");
    
    err_str = openjtalk_get_error_string(-999);
    TEST_ASSERT(strstr(err_str, "Unknown") != NULL, "Unknown error string");
    
    // Test last error
    int error = openjtalk_get_last_error(NULL);
    TEST_ASSERT(error == OPENJTALK_ERROR_INVALID_HANDLE, "Get last error with NULL handle");
}

// Test phonemization
void test_phonemization() {
    TEST_START("Phonemization");
    
    void* handle = openjtalk_create("../test_dictionary");
    TEST_ASSERT(handle != NULL, "Create handle for phonemization");
    
    if (!handle) return;
    
    // Test NULL text
    PhonemeResult* result1 = openjtalk_phonemize(handle, NULL);
    TEST_ASSERT(result1 == NULL, "Phonemize NULL text returns NULL");
    
    // Test empty text
    PhonemeResult* result2 = openjtalk_phonemize(handle, "");
    TEST_ASSERT(result2 == NULL, "Phonemize empty text returns NULL");
    
    // Test Japanese text
    PhonemeResult* result3 = openjtalk_phonemize(handle, "こんにちは");
    TEST_ASSERT(result3 != NULL, "Phonemize Japanese text returns result");
    
    if (result3) {
        TEST_ASSERT(result3->phonemes != NULL, "Phonemes string is not NULL");
        TEST_ASSERT(result3->phoneme_count > 0, "Phoneme count is positive");
        TEST_ASSERT(result3->phoneme_ids != NULL, "Phoneme IDs array is not NULL");
        TEST_ASSERT(result3->durations != NULL, "Durations array is not NULL");
        TEST_ASSERT(result3->total_duration > 0, "Total duration is positive");
        
        printf("  Phonemes: %s\n", result3->phonemes);
        printf("  Count: %d\n", result3->phoneme_count);
        printf("  Total duration: %.2f seconds\n", result3->total_duration);
        
        openjtalk_free_result(result3);
    }
    
    // Test mixed text
    PhonemeResult* result4 = openjtalk_phonemize(handle, "Hello, 世界!");
    TEST_ASSERT(result4 != NULL, "Phonemize mixed text returns result");
    
    if (result4) {
        printf("  Mixed text phonemes: %s\n", result4->phonemes);
        openjtalk_free_result(result4);
    }
    
    // Test with hiragana
    PhonemeResult* result5 = openjtalk_phonemize(handle, "あいうえお");
    TEST_ASSERT(result5 != NULL, "Phonemize hiragana returns result");
    
    if (result5) {
        printf("  Hiragana phonemes: %s\n", result5->phonemes);
        // Test dictionary has limited vocabulary, so just check it has phonemes
        TEST_ASSERT(result5->phoneme_count > 0, "Hiragana has phonemes");
        openjtalk_free_result(result5);
    }
    
    openjtalk_destroy(handle);
}

// Test options
void test_options() {
    TEST_START("Options");
    
    void* handle = openjtalk_create("../test_dictionary");
    TEST_ASSERT(handle != NULL, "Create handle for options test");
    
    if (!handle) return;
    
    // Test set option
    int result = openjtalk_set_option(handle, "use_accent", "true");
    TEST_ASSERT(result == OPENJTALK_SUCCESS, "Set option returns success");
    
    // Test get option
    const char* value = openjtalk_get_option(handle, "use_accent");
    TEST_ASSERT(value != NULL && strcmp(value, "true") == 0, "Get option returns correct value");
    
    // Test invalid parameters
    result = openjtalk_set_option(NULL, "key", "value");
    TEST_ASSERT(result == OPENJTALK_ERROR_INVALID_INPUT, "Set option with NULL handle");
    
    result = openjtalk_set_option(handle, NULL, "value");
    TEST_ASSERT(result == OPENJTALK_ERROR_INVALID_INPUT, "Set option with NULL key");
    
    openjtalk_destroy(handle);
}

// Test memory management
void test_memory_management() {
    TEST_START("Memory Management");
    
    void* handle = openjtalk_create("../test_dictionary");
    TEST_ASSERT(handle != NULL, "Create handle for memory test");
    
    if (!handle) return;
    
    // Perform multiple phonemizations
    const char* test_texts[] = {
        "テスト",
        "これは長い日本語のテキストです。",
        "123456789",
        "ABC",
        "あいうえおかきくけこ"
    };
    
    for (int i = 0; i < 5; i++) {
        PhonemeResult* result = openjtalk_phonemize(handle, test_texts[i]);
        if (result) {
            openjtalk_free_result(result);
        }
    }
    
    TEST_ASSERT(1, "Multiple phonemizations completed without crash");
    
    openjtalk_destroy(handle);
}

// Main test runner
int main(int argc, char* argv[]) {
    printf("========================================\n");
    printf("OpenJTalk Wrapper Test Suite\n");
    printf("========================================\n");
    
    // Set locale for Unicode support
    setlocale(LC_ALL, "");
    
    // Run tests
    test_version();
    test_init_cleanup();
    test_error_handling();
    test_phonemization();
    test_options();
    test_memory_management();
    
    // Summary
    printf("\n========================================\n");
    printf("Test Summary:\n");
    printf("  Passed: %d\n", tests_passed);
    printf("  Failed: %d\n", tests_failed);
    printf("  Total:  %d\n", tests_passed + tests_failed);
    
    if (tests_failed == 0) {
        printf("\nAll tests " TEST_PASSED "!\n");
    } else {
        printf("\nSome tests " TEST_FAILED "!\n");
    }
    
    printf("========================================\n");
    
    return tests_failed > 0 ? 1 : 0;
}