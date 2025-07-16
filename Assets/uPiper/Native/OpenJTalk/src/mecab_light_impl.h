#ifndef MECAB_LIGHT_IMPL_H
#define MECAB_LIGHT_IMPL_H

#include <stdint.h>
#include <stdbool.h>
#include <stddef.h>
#include "mecab_light.h"

#ifdef __cplusplus
extern "C" {
#endif

// Lightweight Mecab implementation
// Minimal dictionary format for Japanese morphological analysis

// Dictionary entry format (binary)
typedef struct {
    uint32_t surface_hash;      // Hash of surface form
    uint16_t left_id;          // Left context ID
    uint16_t right_id;         // Right context ID
    uint16_t cost;             // Word cost
    uint16_t pos_id;           // Part of speech ID
    uint16_t reading_offset;   // Offset to reading in string table
    uint16_t base_offset;      // Offset to base form in string table
} DictEntry;

// Connection matrix entry
typedef struct {
    uint16_t left_id;
    uint16_t right_id;
    int16_t cost;
} ConnectionEntry;

// Character type definitions
typedef enum {
    CHAR_TYPE_SPACE,
    CHAR_TYPE_KANJI,
    CHAR_TYPE_HIRAGANA,
    CHAR_TYPE_KATAKANA,
    CHAR_TYPE_NUMBER,
    CHAR_TYPE_ALPHA,
    CHAR_TYPE_SYMBOL,
    CHAR_TYPE_OTHER
} CharType;

// Part of speech IDs (simplified)
typedef enum {
    POS_NOUN = 0,           // 名詞
    POS_VERB,              // 動詞
    POS_ADJECTIVE,         // 形容詞
    POS_ADVERB,            // 副詞
    POS_PARTICLE,          // 助詞
    POS_AUXILIARY_VERB,    // 助動詞
    POS_CONJUNCTION,       // 接続詞
    POS_PREFIX,            // 接頭詞
    POS_SUFFIX,            // 接尾詞
    POS_SYMBOL,            // 記号
    POS_OTHER,             // その他
    POS_COUNT
} PosID;

// Dictionary structure
typedef struct {
    // Dictionary data
    DictEntry* entries;
    size_t entry_count;
    
    // String table for readings and base forms
    char* string_table;
    size_t string_table_size;
    
    // Connection cost matrix
    ConnectionEntry* connections;
    size_t connection_count;
    
    // Character type table
    uint8_t* char_type_table;
    
    // Hash table for fast lookup
    uint32_t* hash_table;
    size_t hash_table_size;
} MecabDictionary;

// Lattice node for Viterbi algorithm
typedef struct LatticeNode {
    const char* surface;        // Surface string
    int surface_length;         // Byte length
    const DictEntry* entry;     // Dictionary entry
    int begin_pos;             // Begin position in input
    int end_pos;               // End position in input
    
    struct LatticeNode* prev;   // Best previous node
    int cost;                   // Accumulated cost
    
    struct LatticeNode* next;   // Next in position list
} LatticeNode;

// Mecab light implementation
struct MecabLightImpl {
    MecabDictionary* dict;
    
    // Node allocator
    LatticeNode* node_pool;
    size_t node_pool_size;
    size_t node_pool_used;
    
    // Work buffers
    LatticeNode** begin_node_list;  // Nodes beginning at each position
    LatticeNode** end_node_list;    // Nodes ending at each position
    size_t max_length;
    
    // Settings
    int nbest;                      // N-best results
    float theta;                    // Pruning threshold
};

// Dictionary loading
MecabDictionary* mecab_dict_load(const void* data, size_t size);
MecabDictionary* mecab_dict_load_file(const char* path);
void mecab_dict_free(MecabDictionary* dict);

// Dictionary lookup
const DictEntry* mecab_dict_lookup(MecabDictionary* dict, 
                                   const char* surface, 
                                   int length);

// Connection cost
int mecab_get_connection_cost(MecabDictionary* dict,
                              uint16_t left_id,
                              uint16_t right_id);

// Character type detection
CharType mecab_get_char_type(const char* str);
int mecab_get_char_length(const char* str);

// Implementation functions
struct MecabLightImpl* mecab_impl_create(MecabDictionary* dict);
void mecab_impl_destroy(struct MecabLightImpl* impl);

// Analysis
LatticeNode* mecab_impl_parse(struct MecabLightImpl* impl, const char* input);
void mecab_impl_clear_lattice(struct MecabLightImpl* impl);

// Result conversion
MecabNode* mecab_impl_lattice_to_nodes(struct MecabLightImpl* impl, 
                                        LatticeNode* lattice);

// Utility
uint32_t mecab_hash_string(const char* str, int length);
const char* mecab_get_reading(MecabDictionary* dict, const DictEntry* entry);
const char* mecab_get_baseform(MecabDictionary* dict, const DictEntry* entry);
const char* mecab_get_pos_name(PosID pos_id);

// Create minimal dictionary
MecabDictionary* mecab_dict_create_minimal(void);

#ifdef __cplusplus
}
#endif

#endif // MECAB_LIGHT_IMPL_H