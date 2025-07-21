#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>

int main(int argc, char* argv[]) {
    if (argc < 2) {
        printf("Usage: %s <dict_file>\n", argv[0]);
        return 1;
    }
    
    FILE* fp = fopen(argv[1], "rb");
    if (!fp) {
        perror("Failed to open file");
        return 1;
    }
    
    // Skip header (72 bytes)
    fseek(fp, 72, SEEK_SET);
    
    // Read the Darts data
    int32_t data[10000];
    size_t count = fread(data, sizeof(int32_t), 10000, fp);
    
    printf("Analyzing Darts structure in %s\n", argv[1]);
    printf("Read %zu int32 values\n\n", count);
    
    // Look for patterns
    printf("Looking for base/check pairs:\n");
    
    // Standard double-array format often has:
    // Even indices = base values
    // Odd indices = check values
    
    // Try character codes
    for (int c = 0x20; c < 0x7F; c++) {  // ASCII printable
        int32_t idx = 0 + c + 1;  // base[0] + c + 1
        if (idx < (int32_t)count && idx > 0) {
            if (data[idx] != 0) {
                printf("  base[0] + '%c'(%d) + 1 = %d, value = %d\n", 
                       c, c, idx, data[idx]);
            }
        }
    }
    
    // Try UTF-8 Japanese characters
    printf("\nTrying Japanese character codes:\n");
    
    // あ = 0xE38182 in UTF-8, but first byte is 0xE3
    uint8_t japanese_chars[] = {
        0xE3,  // First byte of most hiragana/katakana
        0x81,  // Second byte range for hiragana
        0x82,  // Third byte range
    };
    
    for (int i = 0; i < 3; i++) {
        int32_t idx = 0 + japanese_chars[i] + 1;
        if (idx < (int32_t)count && idx > 0 && data[idx] != 0) {
            printf("  base[0] + 0x%02X + 1 = %d, value = %d\n", 
                   japanese_chars[i], idx, data[idx]);
        }
    }
    
    // Check if there's a pattern in negative values
    printf("\nNegative values (potential token indices):\n");
    int neg_count = 0;
    for (size_t i = 0; i < count && neg_count < 20; i++) {
        if (data[i] < 0) {
            printf("  [%zu]: %d (token index: %d)\n", i, data[i], -data[i] - 1);
            neg_count++;
        }
    }
    
    // Check specific positions we know have data
    printf("\nKnown non-zero positions:\n");
    int known_positions[] = {0, 392, 393, 394, 395, 396, 397, 398, -1};
    for (int i = 0; known_positions[i] >= 0; i++) {
        int pos = known_positions[i];
        if (pos < (int)count) {
            printf("  [%d]: %d (0x%08X)\n", pos, data[pos], (uint32_t)data[pos]);
        }
    }
    
    // Try to trace a path through the trie
    printf("\nTrying to trace path from root:\n");
    int32_t node = 0;
    printf("  Node 0: base = %d\n", data[node]);
    
    // Try first byte of "今" (0xE4 0xBB 0x8A)
    uint8_t ima_bytes[] = {0xE4, 0xBB, 0x8A};
    for (int i = 0; i < 3; i++) {
        int32_t base = data[node];
        if (base < 0) {
            printf("  Terminal node with token %d\n", -base - 1);
            break;
        }
        
        int32_t next = base + ima_bytes[i] + 1;
        printf("  Trying byte 0x%02X: base(%d) + %d + 1 = %d\n", 
               ima_bytes[i], base, ima_bytes[i], next);
        
        if (next >= 0 && next < (int32_t)count) {
            printf("    data[%d] = %d\n", next, data[next]);
            if (data[next] == node) {
                printf("    Check passed! Moving to node %d\n", next);
                node = next;
            } else {
                printf("    Check failed (expected %d, got %d)\n", node, data[next]);
                break;
            }
        } else {
            printf("    Out of bounds\n");
            break;
        }
    }
    
    fclose(fp);
    return 0;
}