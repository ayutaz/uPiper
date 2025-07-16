#include <stdio.h>
#include <stdlib.h>
#include <time.h>
#include "../include/openjtalk_wrapper.h"

int main(int argc, char* argv[]) {
    printf("Dictionary Load Performance Test\n");
    
    const char* dict_path = argc > 1 ? argv[1] : "dictionary";
    printf("Dictionary path: %s\n", dict_path);
    
    // Measure dictionary load time
    clock_t start = clock();
    
    printf("Creating OpenJTalk instance...\n");
    void* openjtalk = openjtalk_create(dict_path);
    
    clock_t end = clock();
    double cpu_time_used = ((double) (end - start)) / CLOCKS_PER_SEC;
    
    if (!openjtalk) {
        fprintf(stderr, "Failed to create OpenJTalk instance\n");
        return 1;
    }
    
    printf("Dictionary loaded successfully in %.2f seconds\n", cpu_time_used);
    
    // Check dictionary size
    const char* dict_size = openjtalk_get_option(openjtalk, "dictionary_size");
    if (dict_size) {
        printf("Dictionary size: %s entries\n", dict_size);
    }
    
    // Clean up
    openjtalk_destroy(openjtalk);
    
    return 0;
}