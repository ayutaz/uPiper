#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "../src/mecab_dict_loader.h"
#include "../src/mecab_full.h"
#include "../include/openjtalk_wrapper.h"

void test_dict_loading(const char* dict_path) {
    printf("\n=== Dictionary Loading Test ===\n");
    printf("Dict path: %s\n", dict_path);
    
    MecabFullDictionary* dict = mecab_dict_load(dict_path);
    if (!dict) {
        printf("Failed to load dictionary\n");
        return;
    }
    
    printf("System dictionary:\n");
    printf("  Magic: 0x%08X\n", dict->sys_header.magic);
    printf("  Version: %u\n", dict->sys_header.version);
    printf("  Lexsize: %u entries\n", dict->sys_header.lexsize);
    printf("  Size: %.2f MB\n", dict->sys_size / (1024.0 * 1024.0));
    
    printf("Unknown word dictionary:\n");
    printf("  Lexsize: %u entries\n", dict->unk_header.lexsize);
    printf("  Size: %.2f KB\n", dict->unk_size / 1024.0);
    
    mecab_dict_free_full(dict);
    printf("Dictionary freed successfully\n");
}

void test_mecab_full(const char* dict_path) {
    printf("\n=== MecabFull Test ===\n");
    
    MecabFull* mecab = mecab_full_create(dict_path);
    if (!mecab) {
        printf("Failed to create MecabFull: %s\n", mecab_full_get_error(NULL));
        return;
    }
    
    printf("MecabFull created successfully\n");
    printf("Dictionary info: %s\n", mecab_full_get_dictionary_info(mecab));
    
    // Test texts
    const char* test_texts[] = {
        "今日は良い天気です",
        "日本語の音声合成システム",
        "東京都渋谷区の天気予報",
        "機械学習による自然言語処理",
        NULL
    };
    
    for (int i = 0; test_texts[i]; i++) {
        printf("\n--- Test %d: \"%s\" ---\n", i + 1, test_texts[i]);
        
        MecabFullNode* nodes = mecab_full_parse(mecab, test_texts[i]);
        if (!nodes) {
            printf("Failed to parse: %s\n", mecab_full_get_error(mecab));
            continue;
        }
        
        MecabFullNode* current = nodes;
        int node_count = 0;
        while (current) {
            printf("[%d] Surface: '%.*s'\n", 
                   node_count++, current->length, current->surface);
            printf("    POS: %s\n", current->feature.pos);
            printf("    Reading: %s\n", current->feature.reading);
            printf("    Pronunciation: %s\n", current->feature.pronunciation);
            printf("    Base form: %s\n", current->feature.base);
            
            current = current->next;
        }
        
        mecab_full_free_nodes(mecab, nodes);
    }
    
    mecab_full_destroy(mecab);
    printf("\n\nMecabFull destroyed successfully\n");
}

void test_openjtalk_wrapper(const char* dict_path) {
    printf("\n=== OpenJTalk Wrapper Test (Full Dictionary) ===\n");
    
    void* handle = openjtalk_create(dict_path);
    if (!handle) {
        printf("Failed to create OpenJTalk\n");
        return;
    }
    
    printf("OpenJTalk created with dictionary: %s\n", dict_path);
    
    const char* test_texts[] = {
        "今日は良い天気ですね",
        "日本語TTSのテスト",
        "漢字も平仮名もカタカナも読めます",
        NULL
    };
    
    for (int i = 0; test_texts[i]; i++) {
        printf("\nTest: \"%s\"\n", test_texts[i]);
        
        PhonemeResult* result = openjtalk_phonemize(handle, test_texts[i]);
        if (!result) {
            printf("  Failed to phonemize\n");
            continue;
        }
        
        printf("  Phoneme count: %d\n", result->phoneme_count);
        printf("  Phonemes: %s\n", result->phonemes);
        printf("  Total duration: %.2f seconds\n", result->total_duration);
        
        openjtalk_free_result(result);
    }
    
    openjtalk_destroy(handle);
    printf("\nOpenJTalk destroyed successfully\n");
}

int main(int argc, char* argv[]) {
    const char* dict_path = argc > 1 ? argv[1] : "test_dictionary";
    
    printf("=== Full Dictionary Test Suite ===\n");
    printf("Using dictionary path: %s\n", dict_path);
    
    // Test dictionary loading
    test_dict_loading(dict_path);
    
    // Test MecabFull
    test_mecab_full(dict_path);
    
    // Test OpenJTalk wrapper
    test_openjtalk_wrapper(dict_path);
    
    return 0;
}