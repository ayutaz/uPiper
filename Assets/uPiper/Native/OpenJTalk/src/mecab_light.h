#ifndef MECAB_LIGHT_H
#define MECAB_LIGHT_H

#include <stdbool.h>
#include <stddef.h>

#ifdef __cplusplus
extern "C" {
#endif

// Lightweight Mecab implementation for phonemization only
// Compatible with pyopenjtalk output format

// Feature structure for morphological analysis
typedef struct {
    char surface[256];      // Surface form
    char pos[64];          // Part of speech
    char pos_detail1[64];  // POS detail 1
    char pos_detail2[64];  // POS detail 2
    char pos_detail3[64];  // POS detail 3
    char inflection[64];   // Inflection type
    char conjugation[64];  // Conjugation form
    char base_form[256];   // Base form
    char reading[256];     // Reading (katakana)
    char pronunciation[256]; // Pronunciation
} MecabFeature;

// Mecab node structure
typedef struct MecabNode {
    char surface[256];
    MecabFeature feature;
    int length;
    int rlength;
    int id;
    int char_type;
    int stat;
    struct MecabNode* next;
    struct MecabNode* prev;
} MecabNode;

// Mecab light context (opaque)
typedef struct _MecabLight MecabLight;

// Initialize/cleanup
MecabLight* mecab_light_create(const char* dic_path);
void mecab_light_destroy(MecabLight* mecab);

// Parse text
MecabNode* mecab_light_parse(MecabLight* mecab, const char* text);
void mecab_light_free_nodes(MecabLight* mecab, MecabNode* node);

// Utility functions
const char* mecab_light_get_error(MecabLight* mecab);
bool mecab_light_load_dictionary(MecabLight* mecab, const char* dic_path);

// Feature parsing
bool mecab_light_parse_feature(const char* feature_str, MecabFeature* feature);

#ifdef __cplusplus
}
#endif

#endif // MECAB_LIGHT_H