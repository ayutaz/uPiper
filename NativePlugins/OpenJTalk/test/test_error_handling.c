#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "../include/openjtalk_wrapper.h"
#include "../src/utf8_utils.h"

// Test UTF-8 validation
void test_utf8_validation() {
    printf("=== UTF-8 Validation Tests ===\n");
    
    // Valid UTF-8 strings
    const char* valid_strings[] = {
        "Hello World",                    // ASCII
        "こんにちは",                     // Japanese
        "Привет мир",                    // Russian
        "你好世界",                       // Chinese
        "🌍🌎🌏",                        // Emoji (4-byte UTF-8)
        "café",                          // Latin with accent
        "",                              // Empty string
        NULL
    };
    
    // Invalid UTF-8 sequences
    struct {
        const char* data;
        size_t len;
        const char* description;
    } invalid_strings[] = {
        {"\x80", 1, "Invalid start byte (continuation byte)"},
        {"\xC0\x80", 2, "Overlong encoding of NULL"},
        {"\xE0\x80\x80", 3, "Invalid continuation bytes"},
        {"\xF5\x80\x80\x80", 4, "Out of range (> U+10FFFF)"},
        {"\xED\xA0\x80", 3, "UTF-16 surrogate (U+D800)"},
        {"\xC2", 1, "Incomplete 2-byte sequence"},
        {"\xE0\xA0", 2, "Incomplete 3-byte sequence"},
        {"\xF0\x90\x80", 3, "Incomplete 4-byte sequence"},
        {"Hello\xFFWorld", 11, "Invalid byte in middle"},
        {NULL, 0, NULL}
    };
    
    printf("\nTesting valid UTF-8 strings:\n");
    for (int i = 0; valid_strings[i]; i++) {
        int result = utf8_validate(valid_strings[i], strlen(valid_strings[i]));
        printf("  %-30s: %s\n", valid_strings[i], result ? "PASS" : "FAIL");
        if (!result) {
            printf("    ERROR: Valid UTF-8 string failed validation\n");
        }
    }
    
    printf("\nTesting invalid UTF-8 sequences:\n");
    for (int i = 0; invalid_strings[i].data; i++) {
        int result = utf8_validate(invalid_strings[i].data, invalid_strings[i].len);
        printf("  %-30s: %s\n", 
               invalid_strings[i].description, 
               !result ? "PASS" : "FAIL");
        if (result) {
            printf("    ERROR: Invalid UTF-8 sequence passed validation\n");
        }
    }
}

// Test OpenJTalk error handling
void test_openjtalk_errors() {
    printf("\n=== OpenJTalk Error Handling Tests ===\n");
    
    // Test with invalid UTF-8
    void* handle = openjtalk_create("test_dictionary");
    if (!handle) {
        printf("Failed to create OpenJTalk instance - trying with test dictionary\n");
        return;
    }
    
    // Test invalid UTF-8 input
    printf("\nTesting phonemization with invalid UTF-8:\n");
    const char* invalid_utf8 = "Hello\xFF\xFEWorld";
    PhonemeResult* result = openjtalk_phonemize(handle, invalid_utf8);
    
    if (result) {
        printf("  ERROR: Phonemization succeeded with invalid UTF-8\n");
        openjtalk_free_result(result);
    } else {
        int error = openjtalk_get_last_error(handle);
        const char* error_str = openjtalk_get_error_string(error);
        printf("  Expected error occurred: %s (code: %d)\n", error_str, error);
        
        if (error != OPENJTALK_ERROR_INVALID_UTF8) {
            printf("  WARNING: Expected INVALID_UTF8 error, got %d\n", error);
        }
    }
    
    // Test NULL input
    printf("\nTesting phonemization with NULL input:\n");
    result = openjtalk_phonemize(handle, NULL);
    if (result) {
        printf("  ERROR: Phonemization succeeded with NULL input\n");
        openjtalk_free_result(result);
    } else {
        int error = openjtalk_get_last_error(handle);
        const char* error_str = openjtalk_get_error_string(error);
        printf("  Expected error occurred: %s (code: %d)\n", error_str, error);
    }
    
    // Test empty string
    printf("\nTesting phonemization with empty string:\n");
    result = openjtalk_phonemize(handle, "");
    if (result) {
        printf("  Phonemization succeeded with empty string\n");
        printf("  Phoneme count: %d\n", result->phoneme_count);
        openjtalk_free_result(result);
    } else {
        int error = openjtalk_get_last_error(handle);
        const char* error_str = openjtalk_get_error_string(error);
        printf("  Error occurred: %s (code: %d)\n", error_str, error);
    }
    
    openjtalk_destroy(handle);
}

// Test memory allocation fallback
void test_memory_fallback() {
    printf("\n=== Memory Allocation Fallback Tests ===\n");
    printf("Note: This test requires manual memory pressure simulation\n");
    
    void* handle = openjtalk_create("test_dictionary");
    if (!handle) {
        printf("Failed to create OpenJTalk instance\n");
        return;
    }
    
    // Test with very long text to stress memory allocation
    char long_text[8192];
    for (int i = 0; i < 8191; i++) {
        long_text[i] = 'a' + (i % 26);  // Use ASCII characters
    }
    long_text[8191] = '\0';
    
    printf("Testing with long text (%zu bytes)...\n", strlen(long_text));
    PhonemeResult* result = openjtalk_phonemize(handle, long_text);
    
    if (result) {
        printf("  Phonemization succeeded\n");
        printf("  Phoneme count: %d\n", result->phoneme_count);
        openjtalk_free_result(result);
    } else {
        int error = openjtalk_get_last_error(handle);
        const char* error_str = openjtalk_get_error_string(error);
        printf("  Error occurred: %s (code: %d)\n", error_str, error);
    }
    
    openjtalk_destroy(handle);
}

// Test dictionary error handling
void test_dictionary_errors() {
    printf("\n=== Dictionary Error Handling Tests ===\n");
    
    // Test with non-existent dictionary
    printf("\nTesting with non-existent dictionary:\n");
    void* handle = openjtalk_create("/non/existent/path");
    if (handle) {
        printf("  WARNING: OpenJTalk created with invalid dictionary path\n");
        openjtalk_destroy(handle);
    } else {
        printf("  Expected: Failed to create with invalid dictionary path\n");
    }
    
    // Test with invalid dictionary file
    printf("\nTesting with invalid dictionary file:\n");
    // This would require creating a corrupted dictionary file for testing
    printf("  Skipped: Requires test fixture with corrupted dictionary\n");
}

int main(int argc, char* argv[]) {
    printf("OpenJTalk Error Handling Test Suite\n");
    printf("===================================\n\n");
    
    test_utf8_validation();
    test_openjtalk_errors();
    test_memory_fallback();
    test_dictionary_errors();
    
    printf("\n=== Test Suite Complete ===\n");
    return 0;
}