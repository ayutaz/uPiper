#include <stdio.h>
#include <stdlib.h>
#include <stdint.h>
#include <string.h>

// Dictionary header structure from pyopenjtalk
typedef struct {
    uint32_t magic;
    uint32_t version;
    uint32_t dict_type;
    uint32_t lexsize;
    uint32_t lsize;
    uint32_t rsize;
    uint32_t dsize;
    uint32_t tsize;
    uint32_t fsize;
    uint32_t reserved;
    char charset[32];
} DictHeader;

void print_hex(const uint8_t* data, size_t size) {
    for (size_t i = 0; i < size; i++) {
        if (i % 16 == 0) printf("\n%08zx: ", i);
        printf("%02x ", data[i]);
    }
    printf("\n");
}

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
    
    // Read header
    DictHeader header;
    if (fread(&header, sizeof(header), 1, fp) != 1) {
        perror("Failed to read header");
        fclose(fp);
        return 1;
    }
    
    printf("Dictionary Header:\n");
    printf("  Magic: 0x%08X\n", header.magic);
    printf("  Version: %u\n", header.version);
    printf("  Type: %u\n", header.dict_type);
    printf("  Lexsize: %u entries\n", header.lexsize);
    printf("  Left size: %u\n", header.lsize);
    printf("  Right size: %u\n", header.rsize);
    printf("  Data size: %u bytes\n", header.dsize);
    printf("  Token size: %u bytes\n", header.tsize);
    printf("  Feature size: %u bytes\n", header.fsize);
    printf("  Charset: %s\n", header.charset);
    
    // Show first few bytes after header
    printf("\nFirst 256 bytes after header:\n");
    uint8_t data[256];
    size_t read = fread(data, 1, 256, fp);
    print_hex(data, read);
    
    // Seek to where Darts data might be
    fseek(fp, sizeof(header), SEEK_SET);
    
    // Read potential Darts header
    uint32_t darts_data[4];
    if (fread(darts_data, sizeof(uint32_t), 4, fp) == 4) {
        printf("\nPotential Darts header at offset %zu:\n", sizeof(header));
        printf("  Field 0: 0x%08X (%u)\n", darts_data[0], darts_data[0]);
        printf("  Field 1: 0x%08X (%u)\n", darts_data[1], darts_data[1]);
        printf("  Field 2: 0x%08X (%u)\n", darts_data[2], darts_data[2]);
        printf("  Field 3: 0x%08X (%u)\n", darts_data[3], darts_data[3]);
    }
    
    fclose(fp);
    return 0;
}