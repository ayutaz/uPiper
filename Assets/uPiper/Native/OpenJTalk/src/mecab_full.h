#ifndef MECAB_FULL_H
#define MECAB_FULL_H

#include "mecab_dict_loader.h"
#include <stdbool.h>
#include <stddef.h>

#ifdef __cplusplus
extern "C" {
#endif

// Forward declarations
typedef struct MecabFull MecabFull;
typedef struct MecabFullNode MecabFullNode;

// Node feature structure
typedef struct {
    char pos[32];               // Part of speech
    char pos_detail1[32];       // POS detail 1
    char pos_detail2[32];       // POS detail 2
    char pos_detail3[32];       // POS detail 3
    char cform[32];             // Conjugation form
    char ctype[32];             // Conjugation type
    char base[64];              // Base form
    char reading[64];           // Reading (katakana)
    char pronunciation[64];     // Pronunciation
} MecabFullFeature;

// Node structure
struct MecabFullNode {
    const char* surface;        // Surface string
    int length;                 // Length in bytes
    MecabFullFeature feature;   // Feature information
    int cost;                   // Cost
    MecabFullNode* next;        // Next node in begin list
    MecabFullNode* enext;       // Next node in end list
    MecabFullNode* prev;        // Previous node in best path
    
    // Internal use
    uint16_t lcAttr;            // Left context attribute
    uint16_t rcAttr;            // Right context attribute
    uint16_t posid;             // POS ID
    uint16_t char_type;         // Character type
    uint32_t token_idx;         // Token index in dictionary
    bool is_unk;                // Is unknown word
    int begin_pos;              // Begin position in text
    int end_pos;                // End position in text
};

// Lattice structure for Viterbi
typedef struct {
    MecabFullNode** begin_node_list;  // Nodes starting at each position
    MecabFullNode** end_node_list;    // Nodes ending at each position
    size_t length;                    // Text length in bytes
    size_t capacity;                  // Allocated capacity
} Lattice;

// Main MecabFull structure
struct MecabFull {
    MecabFullDictionary* dict;        // Dictionary
    Lattice* lattice;                 // Lattice for analysis
    
    // Node pool for memory efficiency
    MecabFullNode* node_pool;
    size_t node_pool_size;
    size_t node_pool_used;
    
    // Options
    int nbest;                        // N-best output
    float theta;                      // Pruning threshold
    bool all_morphs;                  // Output all morphemes
    bool partial;                     // Partial analysis
    
    // Error handling
    char error_message[256];
};

// API functions
MecabFull* mecab_full_create(const char* dict_path);
void mecab_full_destroy(MecabFull* mecab);

// Analysis functions
MecabFullNode* mecab_full_parse(MecabFull* mecab, const char* text);
MecabFullNode* mecab_full_parse_nbest(MecabFull* mecab, const char* text, int n);
void mecab_full_free_nodes(MecabFull* mecab, MecabFullNode* node);

// Feature parsing
bool mecab_full_parse_feature(const char* feature_str, MecabFullFeature* feature);

// Dictionary access
const char* mecab_full_get_dictionary_info(MecabFull* mecab);
int mecab_full_get_dictionary_size(MecabFull* mecab);

// Error handling
const char* mecab_full_get_error(MecabFull* mecab);

// Options
void mecab_full_set_theta(MecabFull* mecab, float theta);
void mecab_full_set_all_morphs(MecabFull* mecab, bool all_morphs);
void mecab_full_set_partial(MecabFull* mecab, bool partial);

#ifdef __cplusplus
}
#endif

#endif // MECAB_FULL_H