#include "mecab_full.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <limits.h>

// UTF-8 utilities
static int utf8_char_len(unsigned char c) {
    if (c < 0x80) return 1;
    if ((c & 0xE0) == 0xC0) return 2;
    if ((c & 0xF0) == 0xE0) return 3;
    if ((c & 0xF8) == 0xF0) return 4;
    return 1;
}

// Parse feature string into MecabFullFeature structure
static void parse_feature_internal(MecabFullFeature* feature, const char* feature_str) {
    if (!feature || !feature_str) return;
    
    // Clear feature
    memset(feature, 0, sizeof(MecabFullFeature));
    
    // Parse CSV format: POS,detail1,detail2,detail3,detail4,detail5,surface,reading,pronunciation
    char buffer[1024];
    strncpy(buffer, feature_str, sizeof(buffer) - 1);
    buffer[sizeof(buffer) - 1] = '\0';
    
    char* fields[16];
    int field_count = 0;
    
    char* token = strtok(buffer, ",");
    while (token && field_count < 16) {
        fields[field_count++] = token;
        token = strtok(NULL, ",");
    }
    
    // Copy fields
    if (field_count > 0 && strcmp(fields[0], "*") != 0) {
        strncpy(feature->pos, fields[0], sizeof(feature->pos) - 1);
    }
    if (field_count > 1 && strcmp(fields[1], "*") != 0) {
        strncpy(feature->pos_detail1, fields[1], sizeof(feature->pos_detail1) - 1);
    }
    if (field_count > 2 && strcmp(fields[2], "*") != 0) {
        strncpy(feature->pos_detail2, fields[2], sizeof(feature->pos_detail2) - 1);
    }
    if (field_count > 3 && strcmp(fields[3], "*") != 0) {
        strncpy(feature->pos_detail3, fields[3], sizeof(feature->pos_detail3) - 1);
    }
    if (field_count > 4 && strcmp(fields[4], "*") != 0) {
        strncpy(feature->ctype, fields[4], sizeof(feature->ctype) - 1);
    }
    if (field_count > 5 && strcmp(fields[5], "*") != 0) {
        strncpy(feature->cform, fields[5], sizeof(feature->cform) - 1);
    }
    if (field_count > 6 && strcmp(fields[6], "*") != 0) {
        strncpy(feature->base, fields[6], sizeof(feature->base) - 1);
    }
    if (field_count > 7 && strcmp(fields[7], "*") != 0) {
        strncpy(feature->reading, fields[7], sizeof(feature->reading) - 1);
    }
    if (field_count > 8 && strcmp(fields[8], "*") != 0) {
        strncpy(feature->pronunciation, fields[8], sizeof(feature->pronunciation) - 1);
    }
}

// Create lattice
static Lattice* lattice_create(size_t capacity) {
    Lattice* lattice = (Lattice*)calloc(1, sizeof(Lattice));
    if (!lattice) return NULL;
    
    lattice->capacity = capacity;
    lattice->begin_node_list = (MecabFullNode**)calloc(capacity, sizeof(MecabFullNode*));
    lattice->end_node_list = (MecabFullNode**)calloc(capacity, sizeof(MecabFullNode*));
    
    if (!lattice->begin_node_list || !lattice->end_node_list) {
        free(lattice->begin_node_list);
        free(lattice->end_node_list);
        free(lattice);
        return NULL;
    }
    
    return lattice;
}

// Free lattice
static void lattice_free(Lattice* lattice) {
    if (!lattice) return;
    free(lattice->begin_node_list);
    free(lattice->end_node_list);
    free(lattice);
}

// Clear lattice
static void lattice_clear(Lattice* lattice) {
    if (!lattice) return;
    memset(lattice->begin_node_list, 0, lattice->capacity * sizeof(MecabFullNode*));
    memset(lattice->end_node_list, 0, lattice->capacity * sizeof(MecabFullNode*));
    lattice->length = 0;
}

// Create MecabFull instance
MecabFull* mecab_full_create(const char* dict_path) {
    MecabFull* mecab = (MecabFull*)calloc(1, sizeof(MecabFull));
    if (!mecab) return NULL;
    
    // Load dictionary
    mecab->dict = mecab_dict_load(dict_path);
    if (!mecab->dict) {
        snprintf(mecab->error_message, sizeof(mecab->error_message),
                 "Failed to load dictionary from: %s", dict_path);
        free(mecab);
        return NULL;
    }
    
    // Create lattice
    mecab->lattice = lattice_create(4096);  // Initial capacity
    if (!mecab->lattice) {
        mecab_dict_free_full(mecab->dict);
        free(mecab);
        return NULL;
    }
    
    // Allocate node pool
    mecab->node_pool_size = 10000;
    mecab->node_pool = (MecabFullNode*)calloc(mecab->node_pool_size, sizeof(MecabFullNode));
    if (!mecab->node_pool) {
        lattice_free(mecab->lattice);
        mecab_dict_free_full(mecab->dict);
        free(mecab);
        return NULL;
    }
    
    // Set default options
    mecab->nbest = 1;
    mecab->theta = 0.0f;
    mecab->all_morphs = false;
    mecab->partial = false;
    
    return mecab;
}

// Destroy MecabFull instance
void mecab_full_destroy(MecabFull* mecab) {
    if (!mecab) return;
    
    free(mecab->node_pool);
    lattice_free(mecab->lattice);
    mecab_dict_free_full(mecab->dict);
    free(mecab);
}

// Get node from pool
static MecabFullNode* get_node(MecabFull* mecab) {
    if (mecab->node_pool_used >= mecab->node_pool_size) {
        // Reallocate pool
        size_t new_size = mecab->node_pool_size * 2;
        MecabFullNode* new_pool = (MecabFullNode*)realloc(mecab->node_pool, 
                                                          new_size * sizeof(MecabFullNode));
        if (!new_pool) return NULL;
        
        mecab->node_pool = new_pool;
        mecab->node_pool_size = new_size;
    }
    
    MecabFullNode* node = &mecab->node_pool[mecab->node_pool_used++];
    memset(node, 0, sizeof(MecabFullNode));
    return node;
}

// Add node to lattice
static void add_node_to_lattice(MecabFull* mecab, MecabFullNode* node, 
                                int begin_pos, int end_pos) {
    // Add to begin list
    node->next = mecab->lattice->begin_node_list[begin_pos];
    mecab->lattice->begin_node_list[begin_pos] = node;
    
    // Add to end list
    MecabFullNode** end_list = &mecab->lattice->end_node_list[end_pos];
    while (*end_list) {
        end_list = &(*end_list)->prev;
    }
    *end_list = node;
}

// Parse feature string
bool mecab_full_parse_feature(const char* feature_str, MecabFullFeature* feature) {
    if (!feature_str || !feature) return false;
    
    memset(feature, 0, sizeof(MecabFullFeature));
    
    // Feature format: "POS,detail1,detail2,detail3,cform,ctype,base,reading,pronunciation"
    char buffer[1024];
    strncpy(buffer, feature_str, sizeof(buffer) - 1);
    
    char* fields[9] = {NULL};
    int field_count = 0;
    
    char* token = strtok(buffer, ",");
    while (token && field_count < 9) {
        fields[field_count++] = token;
        token = strtok(NULL, ",");
    }
    
    // Copy fields
    if (field_count > 0 && fields[0]) strncpy(feature->pos, fields[0], sizeof(feature->pos) - 1);
    if (field_count > 1 && fields[1]) strncpy(feature->pos_detail1, fields[1], sizeof(feature->pos_detail1) - 1);
    if (field_count > 2 && fields[2]) strncpy(feature->pos_detail2, fields[2], sizeof(feature->pos_detail2) - 1);
    if (field_count > 3 && fields[3]) strncpy(feature->pos_detail3, fields[3], sizeof(feature->pos_detail3) - 1);
    if (field_count > 4 && fields[4]) strncpy(feature->cform, fields[4], sizeof(feature->cform) - 1);
    if (field_count > 5 && fields[5]) strncpy(feature->ctype, fields[5], sizeof(feature->ctype) - 1);
    if (field_count > 6 && fields[6]) strncpy(feature->base, fields[6], sizeof(feature->base) - 1);
    if (field_count > 7 && fields[7]) strncpy(feature->reading, fields[7], sizeof(feature->reading) - 1);
    if (field_count > 8 && fields[8]) strncpy(feature->pronunciation, fields[8], sizeof(feature->pronunciation) - 1);
    
    return true;
}

// Build lattice
static bool build_lattice(MecabFull* mecab, const char* text) {
    lattice_clear(mecab->lattice);
    mecab->node_pool_used = 0;
    
    size_t text_len = strlen(text);
    if (text_len >= mecab->lattice->capacity) {
        // Resize lattice
        // TODO: Implement lattice resizing
        return false;
    }
    
    mecab->lattice->length = text_len;
    
    // Add BOS node
    MecabFullNode* bos = get_node(mecab);
    if (!bos) return false;
    bos->surface = "";
    bos->length = 0;
    bos->lcAttr = 0;
    bos->rcAttr = 0;
    bos->cost = 0;
    add_node_to_lattice(mecab, bos, 0, 0);
    
    // Build lattice for each position
    for (size_t pos = 0; pos < text_len; ) {
        int char_len = utf8_char_len((unsigned char)text[pos]);
        
        // Get character type
        uint32_t codepoint = 0;
        // TODO: Properly decode UTF-8 to codepoint
        uint32_t char_type = mecab_dict_get_char_category(mecab->dict, codepoint);
        
        // Common prefix search in dictionary
        DictMatch dict_matches[256];
        int match_count = mecab_dict_common_prefix_search(
            mecab->dict, 
            text + pos, 
            text_len - pos, 
            dict_matches, 
            256
        );
        
        // Add dictionary matches to lattice
        for (int i = 0; i < match_count; i++) {
            MecabFullNode* node = get_node(mecab);
            if (!node) continue;
            
            node->surface = text + pos;
            node->length = dict_matches[i].length;
            node->is_unk = dict_matches[i].is_unk;
            node->lcAttr = dict_matches[i].token->lcAttr;
            node->rcAttr = dict_matches[i].token->rcAttr;
            node->posid = dict_matches[i].token->posid;
            node->cost = dict_matches[i].token->wcost;
            
            // Parse feature string
            const char* feature = mecab_dict_get_feature(mecab->dict, dict_matches[i].token);
            if (feature) {
                parse_feature_internal(&node->feature, feature);
            }
            
            add_node_to_lattice(mecab, node, pos, pos + node->length);
        }
        
        // If no matches found, create unknown word node
        if (match_count == 0) {
                MecabFullNode* unk_node = get_node(mecab);
                if (unk_node) {
                    unk_node->surface = text + pos;
                    unk_node->length = char_len;
                    unk_node->is_unk = true;
                    unk_node->char_type = char_type;
                    unk_node->cost = 10000;  // Unknown word penalty
                    
                    // Set unknown word features
                    // TODO: Use proper unknown word processing
                    strncpy(unk_node->feature.pos, "名詞", sizeof(unk_node->feature.pos) - 1);
                    strncpy(unk_node->feature.pos_detail1, "一般", sizeof(unk_node->feature.pos_detail1) - 1);
                    
                    add_node_to_lattice(mecab, unk_node, pos, pos + char_len);
                }
            }
        
        pos += char_len;
    }
    
    // Add EOS node
    MecabFullNode* eos = get_node(mecab);
    if (!eos) return false;
    eos->surface = "";
    eos->length = 0;
    eos->lcAttr = 0;
    eos->rcAttr = 0;
    eos->cost = 0;
    add_node_to_lattice(mecab, eos, text_len, text_len);
    
    return true;
}

// Viterbi algorithm
static MecabFullNode* viterbi(MecabFull* mecab) {
    size_t len = mecab->lattice->length;
    
    // Forward pass
    for (size_t pos = 0; pos <= len; pos++) {
        MecabFullNode* node = mecab->lattice->begin_node_list[pos];
        
        while (node) {
            if (pos == 0) {
                // BOS node
                node->cost = 0;
                node->prev = NULL;
            } else {
                // Find best previous node
                int best_cost = INT_MAX;
                MecabFullNode* best_prev = NULL;
                
                // Check all nodes ending at current position
                for (size_t prev_pos = 0; prev_pos < pos; prev_pos++) {
                    MecabFullNode* prev = mecab->lattice->begin_node_list[prev_pos];
                    
                    while (prev) {
                        // Check if prev node connects to current position
                        if (prev_pos + prev->length == pos) {
                            // Calculate cost
                            int cost = prev->cost + node->cost;
                            
                            // Add connection cost
                            int conn_cost = mecab_dict_get_connection_cost(
                                mecab->dict, prev->rcAttr, node->lcAttr);
                            cost += conn_cost;
                            
                            if (cost < best_cost) {
                                best_cost = cost;
                                best_prev = prev;
                            }
                        }
                        prev = prev->next;
                    }
                }
                
                node->cost = best_cost;
                node->prev = best_prev;
            }
            
            node = node->next;
        }
    }
    
    // Find EOS node
    MecabFullNode* eos = mecab->lattice->begin_node_list[len];
    while (eos && eos->length != 0) {
        eos = eos->next;
    }
    
    if (!eos || !eos->prev) {
        return NULL;
    }
    
    // Build result path
    MecabFullNode* path[1000];
    int path_len = 0;
    
    MecabFullNode* current = eos->prev;
    while (current && current->length > 0) {
        path[path_len++] = current;
        current = current->prev;
    }
    
    // Reverse path and link nodes
    MecabFullNode* result = NULL;
    MecabFullNode* tail = NULL;
    
    for (int i = path_len - 1; i >= 0; i--) {
        MecabFullNode* node = path[i];
        node->prev = tail;
        node->next = NULL;
        
        if (tail) {
            tail->next = node;
        } else {
            result = node;
        }
        tail = node;
    }
    
    return result;
}

// Main parse function
MecabFullNode* mecab_full_parse(MecabFull* mecab, const char* text) {
    if (!mecab || !text) {
        if (mecab) {
            snprintf(mecab->error_message, sizeof(mecab->error_message),
                     "Invalid input");
        }
        return NULL;
    }
    
    // Build lattice
    if (!build_lattice(mecab, text)) {
        snprintf(mecab->error_message, sizeof(mecab->error_message),
                 "Failed to build lattice");
        return NULL;
    }
    
    // Run Viterbi
    MecabFullNode* result = viterbi(mecab);
    if (!result) {
        snprintf(mecab->error_message, sizeof(mecab->error_message),
                 "Failed to find path");
        return NULL;
    }
    
    return result;
}

// Free nodes
void mecab_full_free_nodes(MecabFull* mecab, MecabFullNode* node) {
    // Nodes are allocated from pool, so nothing to free individually
    // Just reset the pool on next parse
}

// Get error message
const char* mecab_full_get_error(MecabFull* mecab) {
    if (!mecab) return "Invalid MecabFull instance";
    return mecab->error_message;
}

// Get dictionary info
const char* mecab_full_get_dictionary_info(MecabFull* mecab) {
    if (!mecab || !mecab->dict) return "No dictionary loaded";
    
    static char info[256];
    snprintf(info, sizeof(info), 
             "Dictionary: lexsize=%u, lsize=%u, rsize=%u",
             mecab->dict->sys_header.lexsize,
             mecab->dict->sys_header.lsize,
             mecab->dict->sys_header.rsize);
    return info;
}

// Get dictionary size
int mecab_full_get_dictionary_size(MecabFull* mecab) {
    if (!mecab || !mecab->dict) return 0;
    return mecab->dict->sys_header.lexsize;
}