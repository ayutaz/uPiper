#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>
#include <locale.h>
#include "openjtalk_wrapper.h"

#ifdef _WIN32
#include <windows.h>
#else
#include <sys/time.h>
#endif

// High-resolution timer
double get_time() {
#ifdef _WIN32
    LARGE_INTEGER freq, count;
    QueryPerformanceFrequency(&freq);
    QueryPerformanceCounter(&count);
    return (double)count.QuadPart / freq.QuadPart;
#else
    struct timeval tv;
    gettimeofday(&tv, NULL);
    return tv.tv_sec + tv.tv_usec / 1000000.0;
#endif
}

// Test sentences
const char* test_sentences[] = {
    // Short sentences
    "こんにちは",
    "ありがとうございます",
    "今日は良い天気です",
    
    // Medium sentences
    "日本語の音声合成システムは、最近とても進歩しています。",
    "東京都の天気予報によると、明日は晴れのち曇りになるでしょう。",
    "人工知能技術の発展により、様々な分野で革新的な変化が起きています。",
    
    // Long sentences
    "音声合成技術は、テキストを自然な音声に変換する技術であり、スマートフォンやスマートスピーカー、カーナビゲーションシステムなど、私たちの日常生活の様々な場面で活用されています。",
    "日本語の音声合成において最も重要な要素の一つは、漢字の読み方を正確に推定することです。同じ漢字でも文脈によって読み方が変わることがあるため、高度な言語処理技術が必要となります。",
    
    NULL
};

// Benchmark results
typedef struct {
    const char* sentence;
    int char_count;
    int phoneme_count;
    double processing_time_ms;
    double time_per_char_ms;
} BenchmarkResult;

void run_benchmark(void* handle, BenchmarkResult* results, int* result_count) {
    int count = 0;
    
    for (int i = 0; test_sentences[i] != NULL; i++) {
        const char* sentence = test_sentences[i];
        
        // Warm up
        PhonemeResult* warmup = openjtalk_phonemize(handle, sentence);
        if (warmup) openjtalk_free_result(warmup);
        
        // Measure processing time
        double total_time = 0;
        int iterations = 10;
        int phoneme_count = 0;
        
        for (int j = 0; j < iterations; j++) {
            double start = get_time();
            PhonemeResult* result = openjtalk_phonemize(handle, sentence);
            double end = get_time();
            
            if (result) {
                if (j == 0) {
                    phoneme_count = result->phoneme_count;
                }
                openjtalk_free_result(result);
                total_time += (end - start);
            }
        }
        
        // Store results
        results[count].sentence = sentence;
        results[count].char_count = strlen(sentence);
        results[count].phoneme_count = phoneme_count;
        results[count].processing_time_ms = (total_time / iterations) * 1000.0;
        results[count].time_per_char_ms = results[count].processing_time_ms / results[count].char_count;
        count++;
    }
    
    *result_count = count;
}

void print_results(BenchmarkResult* results, int count) {
    printf("\n=== Benchmark Results ===\n");
    printf("%-50s %10s %10s %12s %15s\n", 
           "Sentence", "Chars", "Phonemes", "Time (ms)", "ms/char");
    printf("%s\n", "================================================================================");
    
    double total_time = 0;
    int total_chars = 0;
    
    for (int i = 0; i < count; i++) {
        // Truncate sentence for display
        char display[51];
        strncpy(display, results[i].sentence, 47);
        display[47] = '\0';
        if (strlen(results[i].sentence) > 47) {
            strcat(display, "...");
        }
        
        printf("%-50s %10d %10d %12.3f %15.3f\n",
               display,
               results[i].char_count,
               results[i].phoneme_count,
               results[i].processing_time_ms,
               results[i].time_per_char_ms);
        
        total_time += results[i].processing_time_ms;
        total_chars += results[i].char_count;
    }
    
    printf("%s\n", "================================================================================");
    printf("Average processing time: %.3f ms\n", total_time / count);
    printf("Average time per character: %.3f ms\n", total_time / total_chars);
    
    // Check performance requirements
    printf("\n=== Performance Requirements Check ===\n");
    printf("Target: < 10ms per sentence\n");
    
    int passed = 0;
    int failed = 0;
    for (int i = 0; i < count; i++) {
        if (results[i].processing_time_ms < 10.0) {
            passed++;
        } else {
            failed++;
            printf("  FAILED: Sentence %d took %.3f ms\n", i + 1, results[i].processing_time_ms);
        }
    }
    
    printf("Result: %d/%d sentences meet the requirement\n", passed, count);
    if (failed == 0) {
        printf("✓ All sentences processed within 10ms!\n");
    }
}

int main(int argc, char* argv[]) {
    const char* dict_path = argc > 1 ? argv[1] : "../test_dictionary";
    
    setlocale(LC_ALL, "");
    
    printf("OpenJTalk Performance Benchmark\n");
    printf("Version: %s\n", openjtalk_get_version());
    printf("Dictionary: %s\n", dict_path);
    
    // Create instance
    void* handle = openjtalk_create(dict_path);
    if (!handle) {
        fprintf(stderr, "Failed to create OpenJTalk instance\n");
        return 1;
    }
    
    // Run benchmark
    BenchmarkResult results[20];
    int result_count = 0;
    
    printf("\nRunning benchmark (10 iterations per sentence)...\n");
    run_benchmark(handle, results, &result_count);
    
    // Print results
    print_results(results, result_count);
    
    // Memory usage info
    printf("\n=== Memory Usage ===\n");
    printf("Target: 20-30MB\n");
    printf("Note: Actual memory usage should be measured with external tools\n");
    
    // Cleanup
    openjtalk_destroy(handle);
    
    return 0;
}