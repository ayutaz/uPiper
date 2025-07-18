#include <stdio.h>
#include <stdlib.h>
#include "../src/mecab_dict_loader.h"

int main(int argc, char* argv[]) {
    if (argc < 2) {
        printf("Usage: %s <dict_path>\n", argv[0]);
        return 1;
    }
    
    printf("Testing dictionary loading from: %s\n", argv[1]);
    
    MecabFullDictionary* dict = mecab_dict_load(argv[1]);
    if (!dict) {
        printf("Failed to load dictionary\n");
        return 1;
    }
    
    printf("Dictionary loaded successfully!\n");
    printf("System dictionary: %u entries\n", dict->sys_header.lexsize);
    printf("Unknown word dictionary: %u entries\n", dict->unk_header.lexsize);
    
    mecab_dict_free_full(dict);
    printf("Dictionary freed successfully\n");
    
    return 0;
}