#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/stat.h>
#include "../src/mecab_dict_loader.h"

#define DIC_MAGIC_ID 0xE954A1B6

int main(int argc, char* argv[]) {
    if (argc < 2) {
        printf("Usage: %s <dict_path>\n", argv[0]);
        return 1;
    }
    
    const char* dict_path = argv[1];
    printf("Testing dictionary loading from: %s\n", dict_path);
    
    // Check if files exist
    char filepath[1024];
    struct stat st;
    
    snprintf(filepath, sizeof(filepath), "%s/sys.dic", dict_path);
    if (stat(filepath, &st) != 0) {
        printf("ERROR: Cannot find %s\n", filepath);
        return 1;
    }
    printf("Found sys.dic: %s (size: %lld bytes)\n", filepath, (long long)st.st_size);
    
    snprintf(filepath, sizeof(filepath), "%s/unk.dic", dict_path);
    if (stat(filepath, &st) != 0) {
        printf("ERROR: Cannot find %s\n", filepath);
        return 1;
    }
    printf("Found unk.dic: %s (size: %lld bytes)\n", filepath, (long long)st.st_size);
    
    // Try to load dictionary
    MecabFullDictionary* dict = mecab_dict_load(dict_path);
    if (!dict) {
        printf("Failed to load dictionary - checking individual components...\n");
        
        // Try to read sys.dic header manually
        snprintf(filepath, sizeof(filepath), "%s/sys.dic", dict_path);
        FILE* fp = fopen(filepath, "rb");
        if (!fp) {
            printf("Cannot open sys.dic\n");
            return 1;
        }
        
        DictionaryHeader header;
        fread(&header, sizeof(header), 1, fp);
        fclose(fp);
        
        printf("\nsys.dic header:\n");
        printf("  Magic: 0x%08X (expected: 0x%08X)\n", header.magic, DIC_MAGIC_ID);
        printf("  Version: %u\n", header.version);
        printf("  Type: %u\n", header.type);
        printf("  Lexsize: %u\n", header.lexsize);
        
        // Check magic number
        if (header.magic != DIC_MAGIC_ID) {
            printf("ERROR: Magic number mismatch!\n");
            printf("Actual bytes: %02x %02x %02x %02x\n",
                   ((uint8_t*)&header.magic)[0],
                   ((uint8_t*)&header.magic)[1],
                   ((uint8_t*)&header.magic)[2],
                   ((uint8_t*)&header.magic)[3]);
        }
        
        return 1;
    }
    
    printf("\nDictionary loaded successfully!\n");
    printf("System dictionary: %u entries\n", dict->sys_header.lexsize);
    printf("Unknown word dictionary: %u entries\n", dict->unk_header.lexsize);
    
    mecab_dict_free_full(dict);
    printf("Dictionary freed successfully\n");
    
    return 0;
}