#include "mecab_light_impl.h"
#include "mecab_dict_minimal.h"
#include <stdlib.h>
#include <string.h>
#include <stdio.h>
#include <limits.h>

// FNV-1a hash function
uint32_t mecab_hash_string(const char* str, int length) {
    uint32_t hash = 2166136261u;
    for (int i = 0; i < length; i++) {
        hash ^= (uint8_t)str[i];
        hash *= 16777619u;
    }
    return hash;
}

// Get character type
CharType mecab_get_char_type(const char* str) {
    unsigned char c = (unsigned char)str[0];
    
    // ASCII
    if (c < 0x80) {
        if (c == ' ' || c == '\t' || c == '\n' || c == '\r') return CHAR_TYPE_SPACE;
        if (c >= '0' && c <= '9') return CHAR_TYPE_NUMBER;
        if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')) return CHAR_TYPE_ALPHA;
        return CHAR_TYPE_SYMBOL;
    }
    
    // UTF-8 multi-byte
    if ((c & 0xE0) == 0xE0) {  // 3-byte UTF-8
        unsigned char c2 = (unsigned char)str[1];
        unsigned char c3 = (unsigned char)str[2];
        
        // Hiragana: U+3040-U+309F (E3 81 80 - E3 82 9F)
        if (c == 0xE3 && c2 >= 0x81 && c2 <= 0x82) {
            return CHAR_TYPE_HIRAGANA;
        }
        
        // Katakana: U+30A0-U+30FF (E3 82 A0 - E3 83 BF)
        if (c == 0xE3 && c2 >= 0x82 && c2 <= 0x83) {
            return CHAR_TYPE_KATAKANA;
        }
        
        // CJK Ideographs (Kanji)
        if ((c >= 0xE4 && c <= 0xE9) || 
            (c == 0xE3 && c2 >= 0x90)) {
            return CHAR_TYPE_KANJI;
        }
    }
    
    return CHAR_TYPE_OTHER;
}

// Get UTF-8 character length
int mecab_get_char_length(const char* str) {
    unsigned char c = (unsigned char)str[0];
    if (c < 0x80) return 1;
    if ((c & 0xE0) == 0xC0) return 2;
    if ((c & 0xF0) == 0xE0) return 3;
    if ((c & 0xF8) == 0xF0) return 4;
    return 1;
}

// Create minimal dictionary from static data
MecabDictionary* mecab_dict_create_minimal(void) {
    MecabDictionary* dict = (MecabDictionary*)calloc(1, sizeof(MecabDictionary));
    if (!dict) return NULL;
    
    // Count entries
    int count = 0;
    while (minimal_dict[count].surface != NULL) {
        count++;
    }
    dict->entry_count = count;
    
    // Allocate entries
    dict->entries = (DictEntry*)calloc(count, sizeof(DictEntry));
    if (!dict->entries) {
        free(dict);
        return NULL;
    }
    
    // Calculate string table size
    size_t string_size = 0;
    for (int i = 0; i < count; i++) {
        string_size += strlen(minimal_dict[i].surface) + 1;
        string_size += strlen(minimal_dict[i].reading) + 1;
    }
    
    // Allocate string table
    dict->string_table = (char*)malloc(string_size);
    if (!dict->string_table) {
        free(dict->entries);
        free(dict);
        return NULL;
    }
    dict->string_table_size = string_size;
    
    // Fill dictionary
    size_t string_offset = 0;
    for (int i = 0; i < count; i++) {
        const MinimalDictEntry* src = &minimal_dict[i];
        DictEntry* dst = &dict->entries[i];
        
        // Calculate hash
        int surf_len = strlen(src->surface);
        dst->surface_hash = mecab_hash_string(src->surface, surf_len);
        
        // Copy IDs and costs
        dst->left_id = src->left_id;
        dst->right_id = src->right_id;
        dst->cost = src->cost;
        dst->pos_id = src->pos;
        
        // Store surface (actually storing reading for now)
        dst->reading_offset = string_offset;
        strcpy(&dict->string_table[string_offset], src->reading);
        string_offset += strlen(src->reading) + 1;
        
        // Store base form (same as surface for now)
        dst->base_offset = string_offset;
        strcpy(&dict->string_table[string_offset], src->surface);
        string_offset += strlen(src->surface) + 1;
    }
    
    // Create hash table for fast lookup
    dict->hash_table_size = count * 2;  // 50% load factor
    dict->hash_table = (uint32_t*)calloc(dict->hash_table_size, sizeof(uint32_t));
    if (!dict->hash_table) {
        free(dict->string_table);
        free(dict->entries);
        free(dict);
        return NULL;
    }
    
    // Fill hash table (simple linear probing)
    for (int i = 0; i < count; i++) {
        uint32_t hash = dict->entries[i].surface_hash;
        uint32_t idx = hash % dict->hash_table_size;
        
        while (dict->hash_table[idx] != 0) {
            idx = (idx + 1) % dict->hash_table_size;
        }
        dict->hash_table[idx] = i + 1;  // Store index + 1 (0 means empty)
    }
    
    return dict;
}

// Dictionary lookup
const DictEntry* mecab_dict_lookup(MecabDictionary* dict, 
                                   const char* surface, 
                                   int length) {
    if (!dict || !surface || length <= 0) return NULL;
    
    uint32_t hash = mecab_hash_string(surface, length);
    uint32_t idx = hash % dict->hash_table_size;
    
    // Linear probing
    while (dict->hash_table[idx] != 0) {
        int entry_idx = dict->hash_table[idx] - 1;
        const DictEntry* entry = &dict->entries[entry_idx];
        
        // Check if this is the entry we're looking for
        if (entry->surface_hash == hash) {
            // Verify actual string match
            const char* base = &dict->string_table[entry->base_offset];
            if (strncmp(base, surface, length) == 0 && base[length] == '\0') {
                return entry;
            }
        }
        
        idx = (idx + 1) % dict->hash_table_size;
    }
    
    return NULL;
}

// Get connection cost
int mecab_get_connection_cost(MecabDictionary* dict,
                              uint16_t left_id,
                              uint16_t right_id) {
    // Simplified: use category-based costs
    int left_cat = left_id / 100;
    int right_cat = right_id / 100;
    
    if (left_cat >= 10 || right_cat >= 10) return 1000;
    
    return connection_matrix[left_cat][right_cat];
}

// Get reading from dictionary
const char* mecab_get_reading(MecabDictionary* dict, const DictEntry* entry) {
    if (!dict || !entry) return NULL;
    return &dict->string_table[entry->reading_offset];
}

// Get base form from dictionary
const char* mecab_get_baseform(MecabDictionary* dict, const DictEntry* entry) {
    if (!dict || !entry) return NULL;
    return &dict->string_table[entry->base_offset];
}

// Get POS name
const char* mecab_get_pos_name(PosID pos_id) {
    static const char* pos_names[] = {
        "名詞", "動詞", "形容詞", "副詞", "助詞",
        "助動詞", "接続詞", "接頭詞", "接尾詞", "記号", "その他"
    };
    
    if (pos_id >= 0 && pos_id < POS_COUNT) {
        return pos_names[pos_id];
    }
    return "不明";
}

// Free dictionary
void mecab_dict_free(MecabDictionary* dict) {
    if (!dict) return;
    
    free(dict->entries);
    free(dict->string_table);
    free(dict->connections);
    free(dict->char_type_table);
    free(dict->hash_table);
    free(dict);
}

// Create implementation
struct MecabLightImpl* mecab_impl_create(MecabDictionary* dict) {
    struct MecabLightImpl* impl = (struct MecabLightImpl*)calloc(1, sizeof(struct MecabLightImpl));
    if (!impl) return NULL;
    
    impl->dict = dict;
    impl->nbest = 1;
    impl->theta = 0.0f;
    
    // Allocate node pool
    impl->node_pool_size = 10000;
    impl->node_pool = (LatticeNode*)calloc(impl->node_pool_size, sizeof(LatticeNode));
    if (!impl->node_pool) {
        free(impl);
        return NULL;
    }
    
    // Allocate position lists
    impl->max_length = 1024;
    impl->begin_node_list = (LatticeNode**)calloc(impl->max_length, sizeof(LatticeNode*));
    impl->end_node_list = (LatticeNode**)calloc(impl->max_length, sizeof(LatticeNode*));
    
    if (!impl->begin_node_list || !impl->end_node_list) {
        free(impl->node_pool);
        free(impl->begin_node_list);
        free(impl->end_node_list);
        free(impl);
        return NULL;
    }
    
    return impl;
}

// Destroy implementation
void mecab_impl_destroy(struct MecabLightImpl* impl) {
    if (!impl) return;
    
    free(impl->node_pool);
    free(impl->begin_node_list);
    free(impl->end_node_list);
    free(impl);
}

// Add lattice node
static LatticeNode* add_lattice_node(struct MecabLightImpl* impl,
                                     const char* surface,
                                     int surface_length,
                                     const DictEntry* entry,
                                     int begin_pos,
                                     int end_pos) {
    if (impl->node_pool_used >= impl->node_pool_size) {
        return NULL;  // Pool exhausted
    }
    
    LatticeNode* node = &impl->node_pool[impl->node_pool_used++];
    node->surface = surface;
    node->surface_length = surface_length;
    node->entry = entry;
    node->begin_pos = begin_pos;
    node->end_pos = end_pos;
    node->prev = NULL;
    node->cost = 0;
    node->next = NULL;
    
    // Add to begin list
    node->next = impl->begin_node_list[begin_pos];
    impl->begin_node_list[begin_pos] = node;
    
    return node;
}

// Parse input with Viterbi algorithm
LatticeNode* mecab_impl_parse(struct MecabLightImpl* impl, const char* input) {
    if (!impl || !input) return NULL;
    
    // Clear previous parse
    mecab_impl_clear_lattice(impl);
    
    int input_len = strlen(input);
    if (input_len >= impl->max_length) {
        return NULL;  // Input too long
    }
    
    // Add BOS node
    add_lattice_node(impl, "", 0, NULL, 0, 0);
    
    // Build lattice
    for (int pos = 0; pos < input_len; ) {
        int char_len = mecab_get_char_length(&input[pos]);
        CharType char_type = mecab_get_char_type(&input[pos]);
        
        // Try to find words starting at this position
        bool found_word = false;
        
        // Try different lengths
        for (int len = char_len; len <= input_len - pos && len <= 30; len += char_len) {
            const DictEntry* entry = mecab_dict_lookup(impl->dict, &input[pos], len);
            if (entry) {
                add_lattice_node(impl, &input[pos], len, entry, pos, pos + len);
                found_word = true;
            }
            
            // For single character, always add as unknown
            if (len == char_len && !found_word) {
                // Create unknown word entry
                add_lattice_node(impl, &input[pos], char_len, NULL, pos, pos + char_len);
                found_word = true;
            }
            
            // Don't extend beyond character type boundary
            if (pos + len < input_len) {
                CharType next_type = mecab_get_char_type(&input[pos + len]);
                if (next_type != char_type) break;
            }
        }
        
        pos += char_len;
    }
    
    // Add EOS node
    LatticeNode* eos = add_lattice_node(impl, "", 0, NULL, input_len, input_len);
    
    // Viterbi algorithm
    for (int pos = 0; pos <= input_len; pos++) {
        LatticeNode* node = impl->begin_node_list[pos];
        
        while (node) {
            if (pos == 0) {
                // BOS node
                node->cost = 0;
                node->prev = NULL;
            } else {
                // Find best previous node
                int best_cost = INT_MAX;
                LatticeNode* best_prev = NULL;
                
                // Check all nodes ending at current position
                for (int prev_pos = 0; prev_pos < pos; prev_pos++) {
                    LatticeNode* prev = impl->begin_node_list[prev_pos];
                    
                    while (prev) {
                        if (prev->end_pos == pos) {
                            int cost = prev->cost;
                            
                            // Add word cost
                            if (node->entry) {
                                cost += node->entry->cost;
                            } else {
                                // Unknown word penalty
                                cost += 5000 + node->surface_length * 200;
                            }
                            
                            // Add connection cost
                            if (prev->entry && node->entry) {
                                cost += mecab_get_connection_cost(impl->dict,
                                                                  prev->entry->right_id,
                                                                  node->entry->left_id);
                            } else {
                                cost += 1000;  // Default connection cost
                            }
                            
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
    
    // Backtrack from EOS to get best path
    LatticeNode* best_path = NULL;
    LatticeNode* current = eos;
    
    while (current && current->begin_pos > 0) {
        LatticeNode* prev = current->prev;
        if (prev) {
            current->prev = best_path;
            best_path = current;
        }
        current = prev;
    }
    
    return best_path;
}

// Clear lattice
void mecab_impl_clear_lattice(struct MecabLightImpl* impl) {
    if (!impl) return;
    
    impl->node_pool_used = 0;
    memset(impl->begin_node_list, 0, impl->max_length * sizeof(LatticeNode*));
    memset(impl->end_node_list, 0, impl->max_length * sizeof(LatticeNode*));
}

// Convert lattice to MecabNode list
MecabNode* mecab_impl_lattice_to_nodes(struct MecabLightImpl* impl, 
                                        LatticeNode* lattice) {
    if (!impl || !lattice) return NULL;
    
    MecabNode* head = NULL;
    MecabNode* tail = NULL;
    
    LatticeNode* current = lattice;
    while (current) {
        MecabNode* node = (MecabNode*)calloc(1, sizeof(MecabNode));
        if (!node) break;
        
        // Copy surface
        if (current->surface_length > 0) {
            memcpy(node->surface, current->surface, current->surface_length);
            node->surface[current->surface_length] = '\0';
        }
        
        // Set feature
        if (current->entry) {
            const char* reading = mecab_get_reading(impl->dict, current->entry);
            const char* pos_name = mecab_get_pos_name(current->entry->pos_id);
            
            strncpy(node->feature.pos, pos_name, sizeof(node->feature.pos) - 1);
            strncpy(node->feature.reading, reading, sizeof(node->feature.reading) - 1);
            strncpy(node->feature.pronunciation, reading, sizeof(node->feature.pronunciation) - 1);
        } else {
            // Unknown word
            strncpy(node->feature.pos, "不明", sizeof(node->feature.pos) - 1);
            strncpy(node->feature.reading, node->surface, sizeof(node->feature.reading) - 1);
            strncpy(node->feature.pronunciation, node->surface, sizeof(node->feature.pronunciation) - 1);
        }
        
        // Link nodes
        if (tail) {
            tail->next = node;
            node->prev = tail;
        } else {
            head = node;
        }
        tail = node;
        
        current = current->prev;  // Note: we reversed the list during backtracking
    }
    
    return head;
}