#include <stdio.h>
#include <stdlib.h>
#include <stdint.h>
#include <string.h>
#include "../src/mecab_dict_loader.h"

void analyze_dictionary(const char* filename) {
    FILE* fp = fopen(filename, "rb");
    if (!fp) {
        perror("Failed to open file");
        return;
    }
    
    DictionaryHeader header;
    if (fread(&header, sizeof(header), 1, fp) != 1) {
        perror("Failed to read header");
        fclose(fp);
        return;
    }
    
    printf("\n=== Dictionary: %s ===\n", filename);
    printf("Header:\n");
    printf("  Magic: 0x%08X\n", header.magic);
    printf("  Version: %u\n", header.version);
    printf("  Type: %u\n", header.type);
    printf("  Lexsize: %u entries\n", header.lexsize);
    printf("  Left/Right size: %u/%u\n", header.lsize, header.rsize);
    printf("  Dictionary size: %u bytes\n", header.dsize);
    printf("  Token size: %u bytes\n", header.tsize);
    printf("  Feature size: %u bytes\n", header.fsize);
    printf("  Charset: %s\n", header.charset);
    
    // Calculate offsets
    size_t darts_offset = sizeof(DictionaryHeader);
    size_t token_offset = darts_offset + header.dsize;
    size_t feature_offset = token_offset + header.tsize;
    
    printf("\nData layout:\n");
    printf("  Header: 0x%08zX - 0x%08zX (%zu bytes)\n", 
           (size_t)0, sizeof(DictionaryHeader), sizeof(DictionaryHeader));
    printf("  Darts:  0x%08zX - 0x%08zX (%u bytes)\n", 
           darts_offset, token_offset, header.dsize);
    printf("  Tokens: 0x%08zX - 0x%08zX (%u bytes)\n", 
           token_offset, feature_offset, header.tsize);
    printf("  Features: 0x%08zX - 0x%08zX (%u bytes)\n", 
           feature_offset, feature_offset + header.fsize, header.fsize);
    
    // Calculate token structure size
    if (header.lexsize > 0 && header.tsize > 0) {
        size_t token_size = header.tsize / header.lexsize;
        printf("\nToken structure size: %zu bytes per token\n", token_size);
        printf("Expected Token struct size: %zu bytes\n", sizeof(Token));
        
        // Read first token
        fseek(fp, token_offset, SEEK_SET);
        Token first_token;
        if (fread(&first_token, sizeof(Token), 1, fp) == 1) {
            printf("\nFirst token:\n");
            printf("  lcAttr: %u\n", first_token.lcAttr);
            printf("  rcAttr: %u\n", first_token.rcAttr);
            printf("  posid: %u\n", first_token.posid);
            printf("  cost: %u\n", first_token.cost);
            printf("  wcost: %u\n", first_token.wcost);
            printf("  feature offset: %u\n", first_token.feature);
            
            // Try to read the feature string
            if (first_token.feature < header.fsize) {
                fseek(fp, feature_offset + first_token.feature, SEEK_SET);
                char feature[256];
                fgets(feature, sizeof(feature), fp);
                printf("  feature string: %s\n", feature);
            }
        }
    }
    
    // Analyze Darts structure
    printf("\nDarts analysis:\n");
    fseek(fp, darts_offset, SEEK_SET);
    
    // The Darts data appears to start with the actual trie array
    // Let's check the first few integers
    uint32_t darts_data[8];
    if (fread(darts_data, sizeof(uint32_t), 8, fp) == 8) {
        printf("  First 8 uint32s:\n");
        for (int i = 0; i < 8; i++) {
            printf("    [%d]: 0x%08X (%u)\n", i, darts_data[i], darts_data[i]);
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
    
    // Analyze system dictionary
    snprintf(filepath, sizeof(filepath), "%s/sys.dic", argv[1]);
    analyze_dictionary(filepath);
    
    // Analyze unknown word dictionary
    snprintf(filepath, sizeof(filepath), "%s/unk.dic", argv[1]);
    analyze_dictionary(filepath);
    
    return 0;
}