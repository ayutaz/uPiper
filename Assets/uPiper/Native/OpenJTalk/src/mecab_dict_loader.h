#ifndef MECAB_DICT_LOADER_H
#define MECAB_DICT_LOADER_H

#include <stdint.h>
#include <stdbool.h>
#include <stddef.h>

#ifdef __cplusplus
extern "C" {
#endif

// MeCab dictionary format structures (pyopenjtalk format)
typedef struct {
    uint32_t magic;        // Magic number for validation
    uint32_t version;      // Dictionary version
    uint32_t type;         // Dictionary type (0: system, 1: user, 2: unknown)
    uint32_t lexsize;      // Lexicon size
    uint32_t lsize;        // Left context ID size
    uint32_t rsize;        // Right context ID size
    uint32_t dsize;        // Dictionary size (Darts)
    uint32_t tsize;        // Token array size
    uint32_t fsize;        // Feature string pool size
    uint32_t reserved;     // Reserved for future use
    char charset[32];      // Character set encoding
} DictionaryHeader;

// Token structure in dictionary (16 bytes in pyopenjtalk format)
typedef struct {
    uint16_t lcAttr;       // Left context attribute
    uint16_t rcAttr;       // Right context attribute
    uint16_t posid;        // Part-of-speech ID
    int16_t wcost;         // Word cost (signed)
    uint32_t feature;      // Feature string offset
    uint32_t compound;     // Compound word info
} Token;

// Dictionary rewrite rule
typedef struct {
    const char* pattern;
    const char* replacement;
} RewriteRule;

// Character definition
typedef struct {
    uint32_t category_num;    // Number of categories
    char** category_names;    // Category names
    uint32_t char_num;        // Number of character mappings
    uint32_t* char_map;       // Character to category mapping
} CharDef;

// Matrix definition for connection costs
typedef struct {
    uint16_t lsize;           // Left context size
    uint16_t rsize;           // Right context size
    int16_t* matrix;          // Connection cost matrix
} Matrix;

// Forward declaration
typedef struct Darts Darts;

// Complete dictionary structure
typedef struct MecabFullDictionary {
    // Dictionary headers
    DictionaryHeader sys_header;
    DictionaryHeader unk_header;
    
    // Dictionary data
    void* sys_data;           // System dictionary data
    void* unk_data;           // Unknown word dictionary data
    size_t sys_size;          // System dictionary size
    size_t unk_size;          // Unknown word dictionary size
    
    // Features
    const char* feature_str;  // Feature string pool
    size_t feature_size;      // Feature string pool size
    
    // Double Array Trie for fast lookup
    Darts* sys_darts;         // System dictionary Darts
    Darts* unk_darts;         // Unknown word dictionary Darts
    
    // Character definition
    CharDef* char_def;
    
    // Connection cost matrix
    Matrix* matrix;
    
    // POS information
    uint32_t pos_num;         // Number of POS tags
    char** pos_names;         // POS tag names
    
    // Rewrite rules
    uint32_t rewrite_num;     // Number of rewrite rules
    RewriteRule* rewrite_rules;
    
    // Memory mapped files (if used)
    bool use_mmap;            // Whether using memory mapping
    void* mmap_addr[4];       // mmap addresses for cleanup
    size_t mmap_size[4];      // mmap sizes for cleanup
} MecabFullDictionary;

// Dictionary loading functions
MecabFullDictionary* mecab_dict_load(const char* dict_path);
MecabFullDictionary* mecab_dict_load_from_memory(
    const void* sys_dic, size_t sys_size,
    const void* unk_dic, size_t unk_size,
    const void* char_bin, size_t char_size,
    const void* matrix_bin, size_t matrix_size
);
void mecab_dict_free_full(MecabFullDictionary* dict);

// Dictionary access functions
const Token* mecab_dict_get_token(const MecabFullDictionary* dict, 
                                  uint32_t index, bool is_unk);
const char* mecab_dict_get_feature(const MecabFullDictionary* dict, 
                                   const Token* token);
int16_t mecab_dict_get_connection_cost(const MecabFullDictionary* dict,
                                       uint16_t left_id, uint16_t right_id);
uint32_t mecab_dict_get_char_category(const MecabFullDictionary* dict, 
                                      uint32_t codepoint);

// Dictionary search functions
typedef struct {
    const Token* token;
    uint32_t length;
    bool is_unk;
} DictMatch;

int mecab_dict_common_prefix_search(const MecabFullDictionary* dict,
                                    const char* text, size_t len,
                                    DictMatch* results, int max_results);

// Utility functions
bool mecab_dict_validate_header(const DictionaryHeader* header);
const char* mecab_dict_get_pos_name(const MecabFullDictionary* dict, uint16_t posid);

#ifdef __cplusplus
}
#endif

#endif // MECAB_DICT_LOADER_H