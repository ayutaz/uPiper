#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <fcntl.h>
#include <sys/stat.h>
#include <sys/mman.h>
#include <unistd.h>
#include "../src/mecab_dict_loader.h"

int main(int argc, char* argv[]) {
    if (argc < 2) {
        printf("Usage: %s <dict_path>\n", argv[0]);
        return 1;
    }
    
    const char* dict_path = argv[1];
    char filepath[1024];
    
    // Step 1: Try to mmap sys.dic
    snprintf(filepath, sizeof(filepath), "%s/sys.dic", dict_path);
    printf("Step 1: Memory mapping %s\n", filepath);
    
    int fd = open(filepath, O_RDONLY);
    if (fd < 0) {
        perror("open failed");
        return 1;
    }
    
    struct stat st;
    if (fstat(fd, &st) < 0) {
        perror("fstat failed");
        close(fd);
        return 1;
    }
    
    void* addr = mmap(NULL, st.st_size, PROT_READ, MAP_PRIVATE, fd, 0);
    close(fd);
    
    if (addr == MAP_FAILED) {
        perror("mmap failed");
        return 1;
    }
    
    printf("  Successfully mapped %lld bytes\n", (long long)st.st_size);
    
    // Step 2: Check header
    DictionaryHeader* header = (DictionaryHeader*)addr;
    printf("\nStep 2: Checking header\n");
    printf("  Magic: 0x%08X\n", header->magic);
    printf("  Version: %u\n", header->version);
    printf("  Type: %u\n", header->type);
    printf("  Lexsize: %u\n", header->lexsize);
    printf("  Charset: %.32s\n", header->charset);
    
    // Step 3: Try to create Darts
    printf("\nStep 3: Creating Darts from dictionary data\n");
    const uint8_t* darts_data = (const uint8_t*)addr + sizeof(DictionaryHeader);
    printf("  Darts data starts at offset: %zu\n", sizeof(DictionaryHeader));
    printf("  Darts size: %u bytes\n", header->dsize);
    
    // Show first few values
    int32_t* darts_array = (int32_t*)darts_data;
    printf("  First 10 values: ");
    for (int i = 0; i < 10; i++) {
        printf("%d ", darts_array[i]);
    }
    printf("\n");
    
    munmap(addr, st.st_size);
    
    // Now try the actual load function
    printf("\nStep 4: Calling mecab_dict_load()\n");
    MecabFullDictionary* dict = mecab_dict_load(dict_path);
    if (!dict) {
        printf("  FAILED to load dictionary\n");
        return 1;
    }
    
    printf("  SUCCESS! Dictionary loaded\n");
    mecab_dict_free_full(dict);
    
    return 0;
}