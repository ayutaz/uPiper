#include <stdio.h>
#include <stdlib.h>
#include <stdint.h>
#include <string.h>

// Analyze the Darts structure in the dictionary
void analyze_darts(const char* filename) {
    FILE* fp = fopen(filename, "rb");
    if (!fp) {
        perror("Failed to open file");
        return;
    }
    
    // Skip header
    fseek(fp, 72, SEEK_SET);
    
    // Read some data to analyze
    uint32_t data[256];
    size_t count = fread(data, sizeof(uint32_t), 256, fp);
    
    printf("\n=== Analyzing Darts in %s ===\n", filename);
    
    // Look for patterns
    int non_zero = 0;
    uint32_t max_val = 0;
    for (size_t i = 0; i < count; i++) {
        if (data[i] != 0) {
            non_zero++;
            if (data[i] > max_val) max_val = data[i];
            if (non_zero <= 10) {
                printf("  data[%zu] = 0x%08X (%u)\n", i, data[i], data[i]);
            }
        }
    }
    
    printf("\nFirst %zu uint32s: %d non-zero, max value: 0x%08X\n", 
           count, non_zero, max_val);
    
    // The Darts structure in pyopenjtalk seems to directly store the trie array
    // without a separate header. Let's check if we can find any string data
    
    // Try to find the start of actual data by looking for specific patterns
    fseek(fp, 72, SEEK_SET);  // Back to start of Darts
    
    // In a Double Array Trie, we typically have:
    // - base array: contains base values for state transitions
    // - check array: contains parent state checks
    // These might be interleaved or separate
    
    // Let's look for the token index values that would be stored in the trie
    printf("\nLooking for token indices in first 1000 entries:\n");
    for (int i = 0; i < 1000; i++) {
        uint32_t val;
        if (fread(&val, sizeof(uint32_t), 1, fp) != 1) break;
        
        // Token indices should be less than lexsize (789120 for sys.dic)
        if (val > 0 && val < 789120) {
            // This could be a token index
            if (i < 100) {  // Show first few
                printf("  Position %d: value %u (possible token index)\n", i, val);
            }
        }
    }
    
    fclose(fp);
}

// Check the actual trie structure more carefully
void check_trie_structure(const char* filename) {
    FILE* fp = fopen(filename, "rb");
    if (!fp) return;
    
    // Read dictionary header
    struct {
        uint32_t magic;
        uint32_t version;
        uint32_t type;
        uint32_t lexsize;
        uint32_t lsize;
        uint32_t rsize;
        uint32_t dsize;
        uint32_t tsize;
        uint32_t fsize;
        uint32_t reserved;
        char charset[32];
    } header;
    
    fread(&header, sizeof(header), 1, fp);
    
    printf("\n=== Trie structure analysis for %s ===\n", filename);
    printf("Dictionary has %u entries\n", header.lexsize);
    printf("Darts size: %u bytes (%u uint32s)\n", header.dsize, header.dsize / 4);
    
    // The Darts might have a simple format:
    // For each state, we have base and check values
    // Or it might store the array directly
    
    fseek(fp, 72, SEEK_SET);
    
    // Read and analyze structure
    uint32_t entry[2];
    printf("\nFirst 20 entries (as pairs):\n");
    for (int i = 0; i < 20; i++) {
        if (fread(entry, sizeof(uint32_t), 2, fp) != 2) break;
        printf("  [%d]: base=0x%08X check=0x%08X\n", i, entry[0], entry[1]);
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
    analyze_darts(filepath);
    check_trie_structure(filepath);
    
    snprintf(filepath, sizeof(filepath), "%s/unk.dic", argv[1]);
    analyze_darts(filepath);
    
    return 0;
}