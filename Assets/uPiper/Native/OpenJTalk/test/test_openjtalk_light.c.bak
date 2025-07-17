#include "openjtalk_wrapper.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <locale.h>

// ANSI color codes
#define COLOR_GREEN "\033[0;32m"
#define COLOR_RED "\033[0;31m"
#define COLOR_YELLOW "\033[0;33m"
#define COLOR_RESET "\033[0m"

// Test cases
typedef struct {
    const char* text;
    const char* description;
    int expected_min_phonemes;
} TestCase;

static TestCase test_cases[] = {
    {"こんにちは", "Basic hiragana greeting", 10},
    {"ありがとう", "Basic hiragana thanks", 10},
    {"おはようございます", "Polite morning greeting", 15},
    {"テスト", "Katakana test", 6},
    {"日本語", "Kanji (will be passed through)", 6},
    {"Hello", "English text", 5},
    {"こんにちは、世界！", "Mixed with punctuation", 12},
    {"きょうは いい てんき です", "Sentence with spaces", 20},
    {"しゃしん", "Palatalized sound", 6},
    {"がっこう", "Voiced + long vowel", 8},
    {"", "Empty string", 2},  // Just silence markers
    {NULL, NULL, 0}
};

// Phoneme ID to string mapping (subset)
const char* phoneme_names[] = {
    "pau", "sil", "a", "i", "u", "e", "o",
    "k", "g", "s", "sh", "z", "t", "ch", "ts", "d", "n",
    "h", "f", "b", "p", "m", "y", "r", "w",
    "ny", "hy", "my", "ry", "gy", "by", "py",
    "cl", "v", "q", "j", "dy", "ty"
};

void print_test_header(const char* test_name) {
    printf("\n" COLOR_YELLOW "=== %s ===" COLOR_RESET "\n", test_name);
}

void print_result(int passed) {
    if (passed) {
        printf(COLOR_GREEN "[PASS]" COLOR_RESET "\n");
    } else {
        printf(COLOR_RED "[FAIL]" COLOR_RESET "\n");
    }
}

int test_basic_phonemization() {
    print_test_header("Basic Phonemization Tests");
    
    void* phonemizer = openjtalk_create(NULL);
    if (!phonemizer) {
        printf("Failed to create phonemizer\n");
        return 0;
    }
    
    int passed = 0;
    int total = 0;
    
    for (int i = 0; test_cases[i].text != NULL; i++) {
        printf("\nTest %d: %s - \"%s\"\n", i + 1, test_cases[i].description, test_cases[i].text);
        
        int phoneme_ids[256];
        int count = openjtalk_text_to_phonemes(phonemizer, test_cases[i].text, phoneme_ids, 256);
        
        if (count < 0) {
            printf("  Error: phonemization failed\n");
            print_result(0);
        } else {
            printf("  Phoneme count: %d (expected min: %d)\n", count, test_cases[i].expected_min_phonemes);
            printf("  Phonemes: ");
            
            for (int j = 0; j < count; j++) {
                if (phoneme_ids[j] >= 0 && phoneme_ids[j] < 38) {
                    printf("%s ", phoneme_names[phoneme_ids[j]]);
                } else {
                    printf("(%d) ", phoneme_ids[j]);
                }
            }
            printf("\n");
            
            int test_passed = (count >= test_cases[i].expected_min_phonemes);
            print_result(test_passed);
            
            if (test_passed) passed++;
        }
        total++;
    }
    
    openjtalk_destroy(phonemizer);
    
    printf("\n" COLOR_YELLOW "Summary: %d/%d tests passed" COLOR_RESET "\n", passed, total);
    return passed == total;
}

int test_extended_phonemization() {
    print_test_header("Extended Phonemization Tests");
    
    void* phonemizer = openjtalk_create(NULL);
    if (!phonemizer) {
        printf("Failed to create phonemizer\n");
        return 0;
    }
    
    const char* test_text = "きょうは、いいてんきですね。";
    PhonemeData phonemes[256];
    
    int count = openjtalk_text_to_phonemes_ex(phonemizer, test_text, phonemes, 256);
    
    if (count < 0) {
        printf("Extended phonemization failed\n");
        openjtalk_destroy(phonemizer);
        return 0;
    }
    
    printf("Text: \"%s\"\n", test_text);
    printf("Phoneme count: %d\n\n", count);
    printf("ID | Phoneme | Accent | Mora | Duration\n");
    printf("---|---------|--------|------|----------\n");
    
    for (int i = 0; i < count; i++) {
        printf("%2d | %-7s | %6d | %4d | %5dms\n",
               phonemes[i].phoneme_id,
               phonemes[i].phoneme_str,
               phonemes[i].accent_type,
               phonemes[i].mora_position,
               phonemes[i].duration_ms);
    }
    
    openjtalk_destroy(phonemizer);
    print_result(1);
    return 1;
}

int test_error_handling() {
    print_test_header("Error Handling Tests");
    
    int passed = 0;
    int total = 0;
    
    // Test 1: NULL handle
    printf("Test 1: NULL handle... ");
    int ids[10];
    int result = openjtalk_text_to_phonemes(NULL, "test", ids, 10);
    if (result == -1) {
        passed++;
        print_result(1);
    } else {
        print_result(0);
    }
    total++;
    
    // Test 2: NULL text
    void* phonemizer = openjtalk_create(NULL);
    printf("Test 2: NULL text... ");
    result = openjtalk_text_to_phonemes(phonemizer, NULL, ids, 10);
    if (result == -1) {
        passed++;
        print_result(1);
    } else {
        print_result(0);
    }
    total++;
    
    // Test 3: Zero buffer size
    printf("Test 3: Zero buffer size... ");
    result = openjtalk_text_to_phonemes(phonemizer, "test", ids, 0);
    if (result == -1) {
        passed++;
        print_result(1);
    } else {
        print_result(0);
    }
    total++;
    
    openjtalk_destroy(phonemizer);
    
    printf("\n" COLOR_YELLOW "Error handling: %d/%d tests passed" COLOR_RESET "\n", passed, total);
    return passed == total;
}

int test_options() {
    print_test_header("Options Tests");
    
    void* phonemizer = openjtalk_create(NULL);
    if (!phonemizer) {
        printf("Failed to create phonemizer\n");
        return 0;
    }
    
    // Test setting options
    printf("Setting use_accent = true... ");
    int result = openjtalk_set_option(phonemizer, "use_accent", "true");
    print_result(result == 0);
    
    printf("Setting use_duration = true... ");
    result = openjtalk_set_option(phonemizer, "use_duration", "true");
    print_result(result == 0);
    
    printf("Setting invalid option... ");
    result = openjtalk_set_option(phonemizer, "invalid_option", "value");
    print_result(result == -1);  // Should fail
    
    // Test getting options
    printf("Getting version... ");
    const char* version = openjtalk_get_option(phonemizer, "version");
    if (version != NULL) {
        printf("%s ", version);
        print_result(1);
    } else {
        print_result(0);
    }
    
    openjtalk_destroy(phonemizer);
    return 1;
}

int test_memory_stress() {
    print_test_header("Memory Stress Test");
    
    void* phonemizer = openjtalk_create(NULL);
    if (!phonemizer) {
        printf("Failed to create phonemizer\n");
        return 0;
    }
    
    const char* long_text = "これは、メモリーストレステストのための、とても長い日本語の文章です。"
                           "このテストでは、大量のテキストを処理して、メモリーリークがないか確認します。"
                           "音素化処理が正しく動作し、メモリーが適切に管理されているかをチェックします。";
    
    printf("Processing long text %d times...\n", 100);
    
    for (int i = 0; i < 100; i++) {
        int phoneme_ids[1024];
        int count = openjtalk_text_to_phonemes(phonemizer, long_text, phoneme_ids, 1024);
        
        if (count < 0) {
            printf("Failed at iteration %d\n", i);
            openjtalk_destroy(phonemizer);
            return 0;
        }
        
        if (i % 10 == 0) {
            printf(".");
            fflush(stdout);
        }
    }
    
    printf("\n");
    openjtalk_destroy(phonemizer);
    print_result(1);
    return 1;
}

int main(int argc, char* argv[]) {
    // Set locale for UTF-8 handling
    setlocale(LC_ALL, "");
    
    printf(COLOR_YELLOW "OpenJTalk Light Implementation Test Suite" COLOR_RESET "\n");
    printf("Version: %s\n", openjtalk_get_version());
    
    int total_passed = 0;
    int total_tests = 5;
    
    // Run all tests
    if (test_basic_phonemization()) total_passed++;
    if (test_extended_phonemization()) total_passed++;
    if (test_error_handling()) total_passed++;
    if (test_options()) total_passed++;
    if (test_memory_stress()) total_passed++;
    
    // Final summary
    printf("\n" COLOR_YELLOW "========================================" COLOR_RESET "\n");
    printf(COLOR_YELLOW "FINAL RESULTS: %d/%d test suites passed" COLOR_RESET "\n", total_passed, total_tests);
    
    if (total_passed == total_tests) {
        printf(COLOR_GREEN "ALL TESTS PASSED!" COLOR_RESET "\n");
        return 0;
    } else {
        printf(COLOR_RED "SOME TESTS FAILED!" COLOR_RESET "\n");
        return 1;
    }
}