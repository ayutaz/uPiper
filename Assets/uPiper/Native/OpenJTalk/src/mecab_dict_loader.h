#ifndef MECAB_DICT_LOADER_H
#define MECAB_DICT_LOADER_H

#include <stdint.h>
#include <stdbool.h>
#include <stddef.h>

// Magic numbers for dictionary files
#define MAGIC_ID 0xE954A1B6
#define UNK_MAGIC_ID 0xEF71994D

#ifdef __cplusplus
extern "C" {
#endif

// MeCab dictionary format structures (pyopenjtalk format)
// Use compiler-specific packing
#ifdef _MSC_VER
#pragma pack(push, 1)
#endif
typedef struct
#ifdef __GNUC__
__attribute__((packed))
#endif
{
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
#ifdef _MSC_VER
#pragma pack(pop)
#endif

// Token structure in dictionary (16 bytes in pyopenjtalk format)
#ifdef _MSC_VER
#pragma pack(push, 1)
#endif
typedef struct
#ifdef __GNUC__
__attribute__((packed))
#endif
{
    uint16_t lcAttr;       // Left context attribute
    uint16_t rcAttr;       // Right context attribute
    uint16_t posid;        // Part-of-speech ID
    int16_t wcost;         // Word cost (signed)
    uint32_t feature;      // Feature string offset
    uint32_t compound;     // Compound word info
} Token;
#ifdef _MSC_VER
#pragma pack(pop)
#endif

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
struct Darts;

// Character map structure
typedef struct {
    uint32_t code;
    uint32_t category;
} CharMap;

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
    
    // Token arrays
    const Token* sys_tokens;  // System dictionary tokens
    const Token* unk_tokens;  // Unknown word tokens
    
    // Features
    const char* sys_features; // System dictionary features
    const char* unk_features; // Unknown word features
    const char* feature_str;  // Feature string pool
    size_t feature_size;      // Feature string pool size
    
    // Double Array Trie for fast lookup
    struct Darts* sys_darts;  // System dictionary Darts
    struct Darts* unk_darts;  // Unknown word dictionary Darts
    
    // Character definition
    CharDef* char_def;
    uint32_t char_def_count;
    CharMap* char_map;
    uint32_t char_map_count;
    uint8_t* char_property;
    
    // Connection cost matrix
    Matrix* matrix;
    int16_t* matrix_data;     // Matrix data
    uint16_t matrix_lsize;    // Left size
    uint16_t matrix_rsize;    // Right size
    
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
    
    // Surface form index (built at load time)
    void* surface_index;      // Hash table for surface form lookup
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
// Dictionary search functions
typedef struct {
    const Token* token;
    uint32_t length;
    bool is_unk;
} DictMatch;

uint32_t mecab_dict_get_char_category(const MecabFullDictionary* dict, 
                                      uint32_t codepoint);

// Unknown word processing
int mecab_dict_get_unknown_tokens(const MecabFullDictionary* dict, uint32_t char_type,
                                  DictMatch* matches, int max_matches);

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