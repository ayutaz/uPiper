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

// Count UTF-8 characters in string with byte length
static size_t utf8_strlen_n(const char* str, size_t n) {
    size_t count = 0;
    size_t pos = 0;
    while (pos < n && str[pos]) {
        int len = utf8_char_len((unsigned char)str[pos]);
        pos += len;
        count++;
    }
    return count;
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
    // Store positions in node
    node->begin_pos = begin_pos;
    node->end_pos = end_pos;
    
    // Add to begin list
    node->next = mecab->lattice->begin_node_list[begin_pos];
    mecab->lattice->begin_node_list[begin_pos] = node;
    
    // Add to end list
    node->enext = mecab->lattice->end_node_list[end_pos];
    mecab->lattice->end_node_list[end_pos] = node;
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
    size_t char_count = 0;
    
    // Count UTF-8 characters
    const char* p = text;
    while (*p) {
        int len = utf8_char_len((unsigned char)*p);
        p += len;
        char_count++;
    }
    
    if (char_count >= mecab->lattice->capacity) {
        // Resize lattice
        // TODO: Implement lattice resizing
        return false;
    }
    
    mecab->lattice->length = char_count;
    
    // Add BOS node
    MecabFullNode* bos = get_node(mecab);
    if (!bos) return false;
    bos->surface = "";
    bos->length = 0;
    bos->lcAttr = 0;
    bos->rcAttr = 0;
    bos->cost = 0;
    // Add using the standard function which sets begin_pos and end_pos
    add_node_to_lattice(mecab, bos, 0, 0);
    
    // Build lattice for each position
    size_t char_pos = 0;  // Character position (for lattice)
    for (size_t pos = 0; pos < text_len; ) {
        if (getenv("DEBUG_MECAB")) {
            printf("DEBUG: build_lattice byte_pos=%zu, char_pos=%zu, text_len=%zu\n", pos, char_pos, text_len);
        }
        
        int char_len = utf8_char_len((unsigned char)text[pos]);
        
        // Safety check to prevent infinite loop
        if (char_len <= 0) {
            if (getenv("DEBUG_MECAB")) {
                printf("ERROR: Invalid UTF-8 character at position %zu\n", pos);
            }
            char_len = 1;  // Skip invalid byte
        }
        
        // Decode UTF-8 to codepoint
        uint32_t codepoint = 0;
        if (char_len == 1) {
            codepoint = (unsigned char)text[pos];
        } else if (char_len == 2) {
            codepoint = ((text[pos] & 0x1F) << 6) | (text[pos+1] & 0x3F);
        } else if (char_len == 3) {
            codepoint = ((text[pos] & 0x0F) << 12) | ((text[pos+1] & 0x3F) << 6) | (text[pos+2] & 0x3F);
        } else if (char_len == 4) {
            codepoint = ((text[pos] & 0x07) << 18) | ((text[pos+1] & 0x3F) << 12) | 
                        ((text[pos+2] & 0x3F) << 6) | (text[pos+3] & 0x3F);
        }
        
        // Get character type
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
            
            // Calculate character positions
            size_t start_char_pos = char_pos;
            size_t end_char_pos = char_pos + utf8_strlen_n(text + pos, node->length);
            add_node_to_lattice(mecab, node, start_char_pos, end_char_pos);
        }
        
        // If no matches found, create unknown word node
        if (match_count == 0) {
            // Get unknown word tokens for this character type
            DictMatch unk_matches[16];
            int unk_count = mecab_dict_get_unknown_tokens(mecab->dict, char_type, unk_matches, 16);
            
            if (unk_count > 0) {
                // Add unknown word nodes from dictionary
                for (int i = 0; i < unk_count && i < 3; i++) {
                    MecabFullNode* unk_node = get_node(mecab);
                    if (unk_node) {
                        unk_node->surface = text + pos;
                        unk_node->length = char_len;
                        unk_node->is_unk = true;
                        unk_node->char_type = char_type;
                        unk_node->lcAttr = unk_matches[i].token->lcAttr;
                        unk_node->rcAttr = unk_matches[i].token->rcAttr;
                        unk_node->posid = unk_matches[i].token->posid;
                        unk_node->cost = unk_matches[i].token->wcost;
                        
                        // Parse feature string
                        const char* feature = mecab_dict_get_feature(mecab->dict, unk_matches[i].token);
                        if (feature) {
                            parse_feature_internal(&unk_node->feature, feature);
                        }
                        
                        add_node_to_lattice(mecab, unk_node, char_pos, char_pos + 1);
                    }
                }
            } else {
                // Fallback: create default unknown word node
                MecabFullNode* unk_node = get_node(mecab);
                if (unk_node) {
                    unk_node->surface = text + pos;
                    unk_node->length = char_len;
                    unk_node->is_unk = true;
                    unk_node->char_type = char_type;
                    unk_node->cost = 10000;  // Unknown word penalty
                    
                    // Set default unknown word features
                    strncpy(unk_node->feature.pos, "名詞", sizeof(unk_node->feature.pos) - 1);
                    strncpy(unk_node->feature.pos_detail1, "一般", sizeof(unk_node->feature.pos_detail1) - 1);
                    
                    add_node_to_lattice(mecab, unk_node, char_pos, char_pos + 1);
                }
            }
        }
        
        pos += char_len;
        char_pos++;
    }
    
    // Add EOS node
    MecabFullNode* eos = get_node(mecab);
    if (!eos) return false;
    eos->surface = "";
    eos->length = 0;
    eos->lcAttr = 0;
    eos->rcAttr = 0;
    eos->cost = 0;
    add_node_to_lattice(mecab, eos, char_count, char_count);
    
    return true;
}

// Viterbi algorithm
static MecabFullNode* viterbi(MecabFull* mecab) {
    size_t len = mecab->lattice->length;
    
    // Forward pass
    for (size_t pos = 0; pos <= len; pos++) {
        MecabFullNode* node = mecab->lattice->begin_node_list[pos];
        
        if (getenv("DEBUG_MECAB")) {
            printf("DEBUG: Viterbi at position %zu\n", pos);
            MecabFullNode* n = mecab->lattice->begin_node_list[pos];
            int count = 0;
            while (n) {
                printf("  begin_node[%d]: surface='%.*s', begin=%d, end=%d\n", 
                       count++, n->length, n->surface, n->begin_pos, n->end_pos);
                n = n->next;
            }
        }
        
        while (node) {
            if (pos == 0 && node->length == 0) {
                // BOS node
                node->cost = 0;
                node->prev = NULL;
            } else {
                // Find best previous node
                int best_cost = INT_MAX;
                MecabFullNode* best_prev = NULL;
                
                // Check all nodes ending at current position
                MecabFullNode* prev = mecab->lattice->end_node_list[node->begin_pos];
                
                if (getenv("DEBUG_MECAB") && pos <= 1) {
                    printf("    Looking for prev nodes ending at %d for node '%.*s'\n", 
                           node->begin_pos, node->length, node->surface);
                    MecabFullNode* p = prev;
                    while (p) {
                        printf("      prev candidate: '%.*s' end_pos=%d\n", 
                               p->length, p->surface, p->end_pos);
                        p = p->enext;
                    }
                }
                
                while (prev) {
                    if (prev->end_pos == node->begin_pos && prev != node) {
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
                    prev = prev->enext;
                }
                
                node->cost = best_cost;
                node->prev = best_prev;
                
                // Sanity check: prevent self-reference
                if (node->prev == node) {
                    if (getenv("DEBUG_MECAB")) {
                        printf("  ERROR: Node self-reference detected! Setting prev to NULL\n");
                    }
                    node->prev = NULL;
                }
                
                if (getenv("DEBUG_MECAB")) {
                    if (node->length == 0 && pos > 0) {  // EOS node
                        printf("  EOS node: cost=%d, prev=%p, best_prev=%p\n", 
                               node->cost, (void*)node->prev, (void*)best_prev);
                    }
                    if (best_prev == NULL && pos > 0) {
                        printf("  WARNING: No prev node found for '%.*s' at pos %zu\n",
                               node->length, node->surface, pos);
                    }
                }
            }
            
            node = node->next;
        }
    }
    
    // Find EOS node
    MecabFullNode* eos = mecab->lattice->begin_node_list[len];
    if (getenv("DEBUG_MECAB")) {
        printf("DEBUG: Looking for EOS at position %zu\n", len);
        MecabFullNode* n = eos;
        int count = 0;
        while (n) {
            printf("  Node %d: length=%d, cost=%d, prev=%p\n", count++, n->length, n->cost, (void*)n->prev);
            n = n->next;
        }
    }
    
    while (eos && eos->length != 0) {
        eos = eos->next;
    }
    
    if (!eos) {
        if (getenv("DEBUG_MECAB")) {
            printf("ERROR: No EOS node found\n");
        }
        return NULL;
    }
    
    if (!eos->prev) {
        if (getenv("DEBUG_MECAB")) {
            printf("ERROR: EOS node has no previous node\n");
            // Dump end_node_list at EOS position
            printf("  Nodes ending at position %zu:\n", len);
            MecabFullNode* end_node = mecab->lattice->end_node_list[len];
            while (end_node) {
                printf("    - '%.*s' (begin=%d, end=%d)\n", 
                       end_node->length, end_node->surface, 
                       end_node->begin_pos, end_node->end_pos);
                end_node = end_node->enext;
            }
        }
        return NULL;
    }
    
    // Build result path
    MecabFullNode* path[1000];
    int path_len = 0;
    
    if (getenv("DEBUG_MECAB")) {
        printf("Building path from EOS\n");
        printf("  EOS->prev = %p\n", (void*)eos->prev);
    }
    
    MecabFullNode* current = eos->prev;
    int iterations = 0;
    while (current) {
        if (getenv("DEBUG_MECAB")) {
            printf("  Iteration %d: '%.*s' (length=%d, prev=%p)\n", iterations, 
                   current->length, current->surface, current->length, (void*)current->prev);
        }
        
        // Skip BOS node in the result path
        if (current->length > 0) {
            path[path_len++] = current;
        }
        
        // Check for circular reference or too many iterations
        iterations++;
        if (iterations > 1000) {
            if (getenv("DEBUG_MECAB")) {
                printf("ERROR: Too many iterations (%d), possible circular reference\n", iterations);
            }
            break;
        }
        
        // Safety check for path length
        if (path_len >= 1000) {
            if (getenv("DEBUG_MECAB")) {
                printf("ERROR: Path too long, breaking\n");
            }
            break;
        }
        
        current = current->prev;
    }
    
    if (getenv("DEBUG_MECAB")) {
        printf("  Total path length: %d\n", path_len);
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
    
    if (getenv("DEBUG_MECAB")) {
        if (result) {
            printf("  Result path created, first node: '%.*s'\n", 
                   result->length, result->surface);
        } else {
            printf("  Result path is NULL\n");
        }
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