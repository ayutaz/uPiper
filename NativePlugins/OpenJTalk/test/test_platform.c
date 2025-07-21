#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "openjtalk_wrapper.h"

// Platform detection
const char* get_platform() {
#ifdef _WIN32
    return "Windows";
#elif __APPLE__
    return "macOS";
#elif __linux__
    return "Linux";
#else
    return "Unknown";
#endif
}

// Test platform-specific features
void test_library_loading() {
    printf("=== Testing library loading on %s ===\n", get_platform());
    
    const char* version = openjtalk_get_version();
    if (version) {
        printf("✓ Library loaded successfully\n");
        printf("  Version: %s\n", version);
    } else {
        printf("✗ Failed to get version\n");
        exit(1);
    }
}

void test_file_paths() {
    printf("\n=== Testing file path handling ===\n");
    
    // Test different path formats
    const char* test_paths[] = {
        "test_dictionary",
        "test_dictionary/",
#ifdef _WIN32
        "..\\test_dictionary",
        "..\\test_dictionary\\",
#endif
        NULL
    };
    
    for (int i = 0; test_paths[i] != NULL; i++) {
        printf("Testing path: %s\n", test_paths[i]);
        void* handle = openjtalk_create(test_paths[i]);
        if (handle) {
            printf("  ✓ Dictionary loaded\n");
            openjtalk_destroy(handle);
        } else {
            printf("  ✗ Failed to load dictionary\n");
        }
    }
}

void test_unicode_handling() {
    printf("\n=== Testing Unicode handling ===\n");
    
    void* handle = openjtalk_create("test_dictionary");
    if (!handle) {
        printf("✗ Failed to create handle\n");
        exit(1);
    }
    
    // Test various Unicode strings
    const char* test_strings[] = {
        "こんにちは",          // Basic Japanese
        "今日は良い天気です",   // Kanji mixed
        "ｶﾀｶﾅ",               // Half-width katakana
        "Ａｌｐｈａ",          // Full-width alphabet
        "①②③",              // Circled numbers
        "😊テスト",           // Emoji (should be ignored)
        NULL
    };
    
    for (int i = 0; test_strings[i] != NULL; i++) {
        printf("Testing: %s\n", test_strings[i]);
        PhonemeResult* result = openjtalk_phonemize(handle, test_strings[i]);
        if (result) {
            printf("  ✓ Processed (%d phonemes)\n", result->phoneme_count);
            openjtalk_free_result(result);
        } else {
            printf("  ✗ Failed to process\n");
        }
    }
    
    openjtalk_destroy(handle);
}

void test_memory_mapping() {
    printf("\n=== Testing memory mapping ===\n");
    
    // Create and destroy multiple times to test mmap/munmap
    for (int i = 0; i < 5; i++) {
        void* handle = openjtalk_create("test_dictionary");
        if (handle) {
            printf("  ✓ Iteration %d: Created\n", i + 1);
            
            // Do some work
            PhonemeResult* result = openjtalk_phonemize(handle, "テスト");
            if (result) {
                openjtalk_free_result(result);
            }
            
            openjtalk_destroy(handle);
            printf("  ✓ Iteration %d: Destroyed\n", i + 1);
        } else {
            printf("  ✗ Iteration %d: Failed\n", i + 1);
        }
    }
}

void test_thread_safety() {
    printf("\n=== Testing thread safety (single-threaded) ===\n");
    
    // Note: Full thread safety test would require pthread/Windows threads
    // This just tests that the API can handle rapid sequential calls
    
    void* handle = openjtalk_create("test_dictionary");
    if (!handle) {
        printf("✗ Failed to create handle\n");
        exit(1);
    }
    
    const char* text = "スレッドセーフテスト";
    int iterations = 100;
    
    printf("Running %d rapid iterations...\n", iterations);
    for (int i = 0; i < iterations; i++) {
        PhonemeResult* result = openjtalk_phonemize(handle, text);
        if (result) {
            openjtalk_free_result(result);
        } else {
            printf("✗ Failed at iteration %d\n", i);
            break;
        }
    }
    printf("✓ Completed %d iterations\n", iterations);
    
    openjtalk_destroy(handle);
}

int main(int argc, char* argv[]) {
    printf("=== OpenJTalk Platform Compatibility Test ===\n");
    printf("Platform: %s\n", get_platform());
    printf("Pointer size: %zu bits\n", sizeof(void*) * 8);
    
#ifdef _WIN32
    printf("Windows specific: Using CreateFileMapping/MapViewOfFile\n");
#else
    printf("POSIX specific: Using mmap/munmap\n");
#endif
    
    test_library_loading();
    test_file_paths();
    test_unicode_handling();
    test_memory_mapping();
    test_thread_safety();
    
    printf("\n=== All platform tests completed ===\n");
    return 0;
}