#include <stdio.h>
#include <stdlib.h>
#include <stdint.h>
#include <string.h>
#include <stdbool.h>

// Test different interpretations of the Darts format
typedef struct {
    int32_t* array;
    size_t size;
} SimpleDarts;

// Standard double-array format
bool test_standard_format(SimpleDarts* darts, const char* key) {
    printf("\n--- Testing standard double-array format for '%s' ---\n", key);
    
    int32_t node = 0;
    size_t pos = 0;
    size_t len = strlen(key);
    
    while (pos < len) {
        uint8_t c = (uint8_t)key[pos];
        int32_t base = darts->array[node];
        
        if (base >= 0) {
            int32_t next = base + c;
            if (next >= 0 && next < (int32_t)darts->size) {
                int32_t check = darts->array[next];
                printf("  pos=%zu, char='%c'(0x%02x), node=%d, base=%d, next=%d, check=%d\n",
                       pos, c, c, node, base, next, check);
                
                if (check == node) {
                    // Valid transition
                    node = next;
                    pos++;
                } else {
                    printf("  Check failed: expected %d, got %d\n", node, check);
                    return false;
                }
            } else {
                printf("  Next index out of bounds: %d\n", next);
                return false;
            }
        } else {
            printf("  Terminal node with value: %d\n", -base - 1);
            return true;
        }
    }
    
    return false;
}

// Mecab-specific format (with interleaved base/check)
bool test_mecab_format(SimpleDarts* darts, const char* key) {
    printf("\n--- Testing MeCab format for '%s' ---\n", key);
    
    // In MeCab format, even indices are base, odd indices are check
    int32_t node = 0;
    size_t pos = 0;
    size_t len = strlen(key);
    
    while (pos < len) {
        uint8_t c = (uint8_t)key[pos];
        
        // Get base value (even index)
        int32_t base_idx = node * 2;
        if (base_idx >= (int32_t)darts->size) {
            printf("  Base index out of bounds: %d\n", base_idx);
            return false;
        }
        
        int32_t base = darts->array[base_idx];
        int32_t next = base + c;
        
        if (next >= 0 && next * 2 + 1 < (int32_t)darts->size) {
            int32_t check = darts->array[next * 2 + 1];
            printf("  pos=%zu, char='%c', node=%d, base=%d, next=%d, check=%d\n",
                   pos, c, node, base, next, check);
            
            if (check == node) {
                node = next;
                pos++;
            } else {
                printf("  Check failed\n");
                return false;
            }
        } else {
            return false;
        }
    }
    
    return true;
}

// Analyze actual dictionary data
void analyze_dict_darts(const char* filename) {
    FILE* fp = fopen(filename, "rb");
    if (!fp) {
        perror("Failed to open file");
        return;
    }
    
    // Skip header
    fseek(fp, 72, SEEK_SET);
    
    // Read first part of Darts
    int32_t data[1024];
    size_t count = fread(data, sizeof(int32_t), 1024, fp);
    
    printf("\n=== Analyzing %s ===\n", filename);
    printf("Non-zero values in first %zu entries:\n", count);
    
    int non_zero = 0;
    for (size_t i = 0; i < count && non_zero < 20; i++) {
        if (data[i] != 0) {
            printf("  [%zu]: %d (0x%08x)\n", i, data[i], (uint32_t)data[i]);
            non_zero++;
        }
    }
    
    // Try to find patterns
    printf("\nLooking for base/check patterns:\n");
    for (size_t i = 0; i < count - 256; i++) {
        if (data[i] > 0 && data[i] < 1000) {
            // This could be a base value
            int32_t base = data[i];
            bool found_valid = false;
            
            // Check if any character transition is valid
            for (int c = 0; c < 256; c++) {
                int32_t next = base + c;
                if (next > 0 && next < (int32_t)count) {
                    if (data[next] == (int32_t)i) {
                        printf("  Found valid transition: node=%zu, base=%d, char=%d, next=%d\n",
                               i, base, c, next);
                        found_valid = true;
                        break;
                    }
                }
            }
            
            if (found_valid) break;
        }
    }
    
    fclose(fp);
}

int main(int argc, char* argv[]) {
    if (argc < 2) {
        printf("Usage: %s <dict_path>\n", argv[0]);
        return 1;
    }
    
    char filepath[1024];
    snprintf(filepath, sizeof(filepath), "%s/sys.dic", argv[1]);
    
    analyze_dict_darts(filepath);
    
    // Also analyze unk.dic which is smaller
    snprintf(filepath, sizeof(filepath), "%s/unk.dic", argv[1]);
    analyze_dict_darts(filepath);
    
    return 0;
}