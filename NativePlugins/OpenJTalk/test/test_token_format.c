#include <stdio.h>
#include <stdlib.h>
#include <stdint.h>
#include <string.h>

// Try different token layouts to match 16 bytes
typedef struct {
    uint16_t lcAttr;
    uint16_t rcAttr;
    uint16_t posid;
    int16_t wcost;
    uint32_t feature;
    uint32_t compound;
} Token16A;

typedef struct {
    uint32_t attrs;     // Combined lcAttr and rcAttr
    uint16_t posid;
    uint16_t wcost;
    uint32_t feature;
    uint32_t compound;
} Token16B;

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
    // Skip Darts data (23216752 bytes for sys.dic)
    fseek(fp, 23216752, SEEK_CUR);
    
    // Now at token array
    uint8_t token_data[16];
    
    printf("First 10 tokens (raw bytes):\n");
    for (int i = 0; i < 10; i++) {
        if (fread(token_data, 16, 1, fp) != 1) break;
        
        printf("\nToken %d:\n", i);
        printf("  Raw: ");
        for (int j = 0; j < 16; j++) {
            printf("%02x ", token_data[j]);
        }
        printf("\n");
        
        // Interpret as different structures
        Token16A* a = (Token16A*)token_data;
        Token16B* b = (Token16B*)token_data;
        
        printf("  Layout A: lcAttr=%u rcAttr=%u posid=%u wcost=%d feature=%u compound=%u\n",
               a->lcAttr, a->rcAttr, a->posid, a->wcost, a->feature, a->compound);
        printf("  Layout B: attrs=0x%08x posid=%u wcost=%u feature=%u compound=%u\n",
               b->attrs, b->posid, b->wcost, b->feature, b->compound);
        
        // Extract feature string if possible
        if (a->feature < 67257353) {  // Feature size from earlier
            long saved_pos = ftell(fp);
            fseek(fp, 72 + 23216752 + 12625920 + a->feature, SEEK_SET);
            char feature[256];
            if (fgets(feature, sizeof(feature), fp)) {
                // Remove newline
                char* nl = strchr(feature, '\n');
                if (nl) *nl = '\0';
                printf("  Feature: %s\n", feature);
            }
            fseek(fp, saved_pos, SEEK_SET);
        }
    }
    
    fclose(fp);
    
    printf("\nStructure sizes:\n");
    printf("  Token16A: %zu bytes\n", sizeof(Token16A));
    printf("  Token16B: %zu bytes\n", sizeof(Token16B));
    
    return 0;
}