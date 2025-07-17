#include "mecab_dict_loader.h"
#include "mecab_darts.h"
#include "surface_index.h"
#include "debug_log.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/mman.h>
#include <sys/stat.h>
#include <fcntl.h>
#include <unistd.h>

// Load dictionary
MecabFullDictionary* mecab_dict_load(const char* dict_path) {
    MecabFullDictionary* dict = (MecabFullDictionary*)calloc(1, sizeof(MecabFullDictionary));
    if (!dict) return NULL;
    
    char path_buffer[512];
    
    // Load system dictionary
    snprintf(path_buffer, sizeof(path_buffer), "%s/sys.dic", dict_path);
    FILE* sys_file = fopen(path_buffer, "rb");
    if (!sys_file) {
        LOG_ERROR("Failed to open sys.dic: %s\n", path_buffer);
        free(dict);
        return NULL;
    }
    
    // Read header
    if (fread(&dict->sys_header, sizeof(DictionaryHeader), 1, sys_file) != 1) {
        LOG_ERROR("Failed to read sys.dic header\n");
        fclose(sys_file);
        free(dict);
        return NULL;
    }
    
    // Validate magic number
    if (dict->sys_header.magic != MAGIC_ID) {
        LOG_ERROR("Invalid magic number in sys.dic: 0x%X (expected 0x%X)\n", 
                dict->sys_header.magic, MAGIC_ID);
        fclose(sys_file);
        free(dict);
        return NULL;
    }
    
    // Get file size
    fseek(sys_file, 0, SEEK_END);
    size_t file_size = ftell(sys_file);
    fseek(sys_file, 0, SEEK_SET);
    
    // Memory map the file
    int fd = fileno(sys_file);
    void* mapped = mmap(NULL, file_size, PROT_READ, MAP_PRIVATE, fd, 0);
    if (mapped == MAP_FAILED) {
        fprintf(stderr, "Failed to mmap sys.dic\n");
        fclose(sys_file);
        free(dict);
        return NULL;
    }
    
    dict->sys_data = mapped;
    dict->sys_size = file_size;
    fclose(sys_file);
    
    // Set up pointers
    const uint8_t* data = (const uint8_t*)dict->sys_data;
    size_t offset = sizeof(DictionaryHeader);
    
    // Skip Darts data
    offset += dict->sys_header.dsize;
    
    // Tokens
    dict->sys_tokens = (const Token*)(data + offset);
    offset += dict->sys_header.tsize;
    
    // Features
    dict->sys_features = (const char*)(data + offset);
    
    // Load unknown word dictionary
    snprintf(path_buffer, sizeof(path_buffer), "%s/unk.dic", dict_path);
    FILE* unk_file = fopen(path_buffer, "rb");
    if (!unk_file) {
        fprintf(stderr, "Failed to open unk.dic: %s\n", path_buffer);
        munmap(dict->sys_data, dict->sys_size);
        free(dict);
        return NULL;
    }
    
    // Read unk header
    if (fread(&dict->unk_header, sizeof(DictionaryHeader), 1, unk_file) != 1) {
        fprintf(stderr, "Failed to read unk.dic header\n");
        fclose(unk_file);
        munmap(dict->sys_data, dict->sys_size);
        free(dict);
        return NULL;
    }
    
    // Validate unk magic number
    if (dict->unk_header.magic != UNK_MAGIC_ID && dict->unk_header.magic != MAGIC_ID) {
        fprintf(stderr, "Invalid magic number in unk.dic: 0x%X\n", dict->unk_header.magic);
        fclose(unk_file);
        munmap(dict->sys_data, dict->sys_size);
        free(dict);
        return NULL;
    }
    
    // Get unk file size
    fseek(unk_file, 0, SEEK_END);
    size_t unk_file_size = ftell(unk_file);
    fseek(unk_file, 0, SEEK_SET);
    
    // Memory map unk file
    int unk_fd = fileno(unk_file);
    void* unk_mapped = mmap(NULL, unk_file_size, PROT_READ, MAP_PRIVATE, unk_fd, 0);
    if (unk_mapped == MAP_FAILED) {
        fprintf(stderr, "Failed to mmap unk.dic\n");
        fclose(unk_file);
        munmap(dict->sys_data, dict->sys_size);
        free(dict);
        return NULL;
    }
    
    dict->unk_data = unk_mapped;
    dict->unk_size = unk_file_size;
    fclose(unk_file);
    
    // Set up unk pointers
    const uint8_t* unk_data = (const uint8_t*)dict->unk_data;
    size_t unk_offset = sizeof(DictionaryHeader);
    
    // Skip unk Darts data
    unk_offset += dict->unk_header.dsize;
    
    // Unk tokens
    dict->unk_tokens = (const Token*)(unk_data + unk_offset);
    unk_offset += dict->unk_header.tsize;
    
    // Unk features
    dict->unk_features = (const char*)(unk_data + unk_offset);
    
    // Load matrix
    snprintf(path_buffer, sizeof(path_buffer), "%s/matrix.bin", dict_path);
    FILE* matrix_file = fopen(path_buffer, "rb");
    if (!matrix_file) {
        fprintf(stderr, "Failed to open matrix.bin: %s\n", path_buffer);
        munmap(dict->sys_data, dict->sys_size);
        munmap(dict->unk_data, dict->unk_size);
        free(dict);
        return NULL;
    }
    
    // Read matrix header
    uint16_t lsize, rsize;
    if (fread(&lsize, sizeof(uint16_t), 1, matrix_file) != 1 ||
        fread(&rsize, sizeof(uint16_t), 1, matrix_file) != 1) {
        fprintf(stderr, "Failed to read matrix header\n");
        fclose(matrix_file);
        munmap(dict->sys_data, dict->sys_size);
        munmap(dict->unk_data, dict->unk_size);
        free(dict);
        return NULL;
    }
    
    dict->matrix_lsize = lsize;
    dict->matrix_rsize = rsize;
    
    // Allocate and read matrix
    size_t matrix_size = (size_t)lsize * rsize * sizeof(int16_t);
    dict->matrix_data = (int16_t*)malloc(matrix_size);
    if (!dict->matrix_data || fread(dict->matrix_data, matrix_size, 1, matrix_file) != 1) {
        fprintf(stderr, "Failed to read matrix data\n");
        fclose(matrix_file);
        free(dict->matrix_data);
        munmap(dict->sys_data, dict->sys_size);
        munmap(dict->unk_data, dict->unk_size);
        free(dict);
        return NULL;
    }
    
    fclose(matrix_file);
    
    // Load char.bin for character categories
    snprintf(path_buffer, sizeof(path_buffer), "%s/char.bin", dict_path);
    FILE* char_file = fopen(path_buffer, "rb");
    if (!char_file) {
        fprintf(stderr, "Warning: Failed to open char.bin: %s\n", path_buffer);
        // Continue without char.bin - will use default categories
    } else {
        // Read category count
        uint32_t category_count;
        if (fread(&category_count, sizeof(uint32_t), 1, char_file) != 1) {
            fprintf(stderr, "Failed to read char.bin header\n");
            fclose(char_file);
        } else {
            dict->char_def_count = category_count;
            
            // Skip category names (we don't need them for now)
            fseek(char_file, category_count * 32, SEEK_CUR);
            
            // Allocate character property array (65536 entries for BMP)
            dict->char_property = (uint8_t*)calloc(65536, sizeof(uint8_t));
            if (dict->char_property) {
                // Read character properties
                // Note: char.bin contains 65535 entries (0-65534), not 65536
                uint32_t* char_info = (uint32_t*)malloc(65536 * sizeof(uint32_t));
                if (!char_info) {
                    fprintf(stderr, "Failed to allocate memory for char info\n");
                    free(dict->char_property);
                    dict->char_property = NULL;
                } else {
                    size_t items_read = fread(char_info, sizeof(uint32_t), 65535, char_file);
                    if (items_read == 65535) {
                        // Extract default_type from each CharInfo
                        // CharInfo format: type(18) | default_type(8) | length(4) | group(1) | invoke(1)
                        // The lower 8 bits contain category as bit flags (1 << category_id)
                        for (int i = 0; i < 65535; i++) {
                            uint32_t flags = char_info[i] & 0xFF;
                            // Convert bit flag to category index
                            uint8_t category = 0;
                            if (flags) {
                                // Find which bit is set
                                for (int bit = 0; bit < 8; bit++) {
                                    if (flags & (1 << bit)) {
                                        category = bit;
                                        break;
                                    }
                                }
                            }
                            dict->char_property[i] = category;
                        }
                        // Set default for the last entry (U+FFFF)
                        dict->char_property[65535] = 0;
                        
                        if (getenv("DEBUG_CHAR_BIN")) {
                            printf("Loaded character categories from char.bin (%zu entries)\n", items_read);
                        }
                    } else {
                        fprintf(stderr, "Failed to read character properties from char.bin (read %zu items, expected 65535)\n", items_read);
                        free(dict->char_property);
                        dict->char_property = NULL;
                    }
                    free(char_info);
                }
            }
            fclose(char_file);
        }
    }
    
    // Build surface form index instead of using Darts
    if (getenv("DEBUG_SURFACE_INDEX")) {
        printf("Building surface form index...\n");
    }
    dict->surface_index = surface_index_create(dict->sys_header.lexsize * 2);
    if (dict->surface_index) {
        SurfaceIndex* index = (SurfaceIndex*)dict->surface_index;
        
        // Add all tokens to surface index
        for (uint32_t i = 0; i < dict->sys_header.lexsize; i++) {
            const Token* token = &dict->sys_tokens[i];
            const char* feature = dict->sys_features + token->feature;
            
            // Extract surface form from feature string
            char surface[256];
            if (extract_surface_from_feature(feature, surface, sizeof(surface))) {
                surface_index_add(index, surface, i);
            }
            
            // Progress indicator
            if (getenv("DEBUG_SURFACE_INDEX") && (i + 1) % 100000 == 0) {
                printf("  Indexed %u/%u tokens", i + 1, dict->sys_header.lexsize);
                fflush(stdout);
            }
        }
        
        if (getenv("DEBUG_SURFACE_INDEX")) {
            printf("\nIndexed %u surface forms from %u tokens\n", 
                   index->entry_count, dict->sys_header.lexsize);
        }
    }
    
    // Also load unk Darts for unknown word processing
    dict->unk_darts = darts_load((const uint8_t*)dict->unk_data + sizeof(DictionaryHeader),
                                 dict->unk_header.dsize);
    
    return dict;
}

// Free dictionary
void mecab_dict_free_minimal(MecabFullDictionary* dict) {
    if (!dict) return;
    
    // Only free non-const allocations
    free(dict->matrix_data);
    free(dict->char_property);
    free(dict->char_map);
    free(dict->char_def);
    
    darts_free(dict->sys_darts);
    darts_free(dict->unk_darts);
    
    free(dict);
}

// Free full dictionary (with mmap)
void mecab_dict_free_full(MecabFullDictionary* dict) {
    if (!dict) return;
    
    if (dict->sys_data) {
        munmap((void*)dict->sys_data, dict->sys_size);
    }
    if (dict->unk_data) {
        munmap((void*)dict->unk_data, dict->unk_size);
    }
    
    free(dict->matrix_data);
    free(dict->char_property);
    free(dict->char_map);
    free(dict->char_def);
    
    darts_free(dict->sys_darts);
    darts_free(dict->unk_darts);
    
    if (dict->surface_index) {
        surface_index_destroy((SurfaceIndex*)dict->surface_index);
    }
    
    free(dict);
}

// Get token by index
const Token* mecab_dict_get_token(const MecabFullDictionary* dict, 
                                  uint32_t index, bool is_unk) {
    if (!dict) return NULL;
    
    if (is_unk) {
        if (index >= dict->unk_header.lexsize) return NULL;
        return &dict->unk_tokens[index];
    } else {
        if (index >= dict->sys_header.lexsize) return NULL;
        return &dict->sys_tokens[index];
    }
}

// Get feature string
const char* mecab_dict_get_feature(const MecabFullDictionary* dict, 
                                   const Token* token) {
    if (!dict || !token) return NULL;
    
    // Determine which dictionary the token belongs to
    if (token >= dict->sys_tokens && 
        token < dict->sys_tokens + dict->sys_header.lexsize) {
        return dict->sys_features + token->feature;
    } else if (token >= dict->unk_tokens && 
               token < dict->unk_tokens + dict->unk_header.lexsize) {
        return dict->unk_features + token->feature;
    }
    
    return NULL;
}

// Get connection cost
int16_t mecab_dict_get_connection_cost(const MecabFullDictionary* dict,
                                       uint16_t left_id, uint16_t right_id) {
    if (!dict || !dict->matrix_data) return 0;
    
    if (left_id >= dict->matrix_lsize || right_id >= dict->matrix_rsize) {
        return 0;
    }
    
    return dict->matrix_data[left_id * dict->matrix_rsize + right_id];
}

// Dictionary lookup - using surface index
int mecab_dict_common_prefix_search(const MecabFullDictionary* dict, 
                                    const char* str, size_t len,
                                    DictMatch* results, int max_results) {
    if (!dict || !str || !results || max_results <= 0) return 0;
    
    if (dict->surface_index) {
        // Use surface index for lookup
        SurfaceIndex* index = (SurfaceIndex*)dict->surface_index;
        SurfaceMatch matches[32];
        int match_count = surface_index_common_prefix_search(index, str, len, matches, 32);
        
        int total_results = 0;
        for (int i = 0; i < match_count && total_results < max_results; i++) {
            uint32_t count;
            const uint32_t* indices = surface_index_lookup(index, matches[i].surface, &count);
            
            if (indices && count > 0) {
                // Add first few matches
                for (uint32_t j = 0; j < count && total_results < max_results; j++) {
                    results[total_results].token = mecab_dict_get_token(dict, indices[j], false);
                    results[total_results].length = matches[i].length;
                    results[total_results].is_unk = false;
                    total_results++;
                }
            }
            
            // Free allocated surface string
            free((char*)matches[i].surface);
        }
        
        return total_results;
    }
    
    // Fallback to Darts if available
    if (dict->sys_darts) {
        DartsResult darts_results[256];
        int count = darts_common_prefix_search(dict->sys_darts, str, len, 
                                               darts_results, 256);
        
        int total_results = 0;
        for (int i = 0; i < count && total_results < max_results; i++) {
            results[total_results].token = mecab_dict_get_token(dict, darts_results[i].value, false);
            results[total_results].length = darts_results[i].length;
            results[total_results].is_unk = false;
            total_results++;
        }
        
        return total_results;
    }
    
    return 0;
}

// Get character category
uint32_t mecab_dict_get_char_category(const MecabFullDictionary* dict, 
                                      uint32_t codepoint) {
    if (!dict) return 0;
    
    // If char_property is loaded, use it for BMP characters
    if (dict->char_property && codepoint < 65536) {
        return dict->char_property[codepoint];
    }
    
    // For characters outside BMP or if char.bin not loaded,
    // use simple heuristics
    if (codepoint >= 0x3040 && codepoint <= 0x309F) {
        return 6;  // HIRAGANA
    } else if (codepoint >= 0x30A0 && codepoint <= 0x30FF) {
        return 7;  // KATAKANA
    } else if ((codepoint >= 0x4E00 && codepoint <= 0x9FFF) ||
               (codepoint >= 0x3400 && codepoint <= 0x4DBF)) {
        return 2;  // KANJI
    } else if ((codepoint >= 0x41 && codepoint <= 0x5A) ||
               (codepoint >= 0x61 && codepoint <= 0x7A)) {
        return 5;  // ALPHA
    } else if (codepoint >= 0x30 && codepoint <= 0x39) {
        return 4;  // NUMERIC
    } else if (codepoint == 0x20 || codepoint == 0x09 || 
               codepoint == 0x0A || codepoint == 0x0D) {
        return 1;  // SPACE
    } else {
        return 0;  // DEFAULT
    }
}

// Unknown word processing
int mecab_dict_lookup_unknown(const MecabFullDictionary* dict,
                              const char* str, size_t len,
                              DictMatch* results, int max_results) {
    if (!dict || !str || !results || max_results <= 0) return 0;
    
    int total_results = 0;
    
    // Process character by character for unknown words
    size_t pos = 0;
    while (pos < len && total_results < max_results) {
        // Get character length
        unsigned char c = (unsigned char)str[pos];
        int char_len = 1;
        if (c >= 0x80) {
            if ((c & 0xE0) == 0xC0) char_len = 2;
            else if ((c & 0xF0) == 0xE0) char_len = 3;
            else if ((c & 0xF8) == 0xF0) char_len = 4;
        }
        
        // Decode UTF-8 to codepoint
        uint32_t codepoint = 0;
        if (char_len == 1) {
            codepoint = c;
        } else if (char_len == 2) {
            codepoint = ((c & 0x1F) << 6) | (str[pos+1] & 0x3F);
        } else if (char_len == 3) {
            codepoint = ((c & 0x0F) << 12) | ((str[pos+1] & 0x3F) << 6) | (str[pos+2] & 0x3F);
        } else if (char_len == 4) {
            codepoint = ((c & 0x07) << 18) | ((str[pos+1] & 0x3F) << 12) | 
                        ((str[pos+2] & 0x3F) << 6) | (str[pos+3] & 0x3F);
        }
        
        // Get character category
        uint32_t char_category = mecab_dict_get_char_category(dict, codepoint);
        
        // Search unknown word templates for this character type
        char key[16];
        snprintf(key, sizeof(key), "%u", char_category);
        
        DartsResult darts_results[64];
        int count = darts_common_prefix_search(dict->unk_darts, key, strlen(key), 
                                               darts_results, 64);
        
        for (int i = 0; i < count && total_results < max_results; i++) {
            results[total_results].token = mecab_dict_get_token(dict, darts_results[i].value, true);
            results[total_results].length = 1;  // Unknown words are processed character by character
            results[total_results].is_unk = true;
            total_results++;
        }
    }
    
    return total_results;
}

// Get unknown word tokens for a character type
int mecab_dict_get_unknown_tokens(const MecabFullDictionary* dict, uint32_t char_type,
                                  DictMatch* matches, int max_matches) {
    if (!dict || !dict->unk_tokens || !matches || max_matches <= 0) {
        return 0;
    }
    
    int count = 0;
    
    // Character type names for searching
    const char* char_type_names[] = {
        "DEFAULT",    // 0
        "SPACE",      // 1
        "KANJI",      // 2
        "SYMBOL",     // 3
        "NUMERIC",    // 4
        "ALPHA",      // 5
        "HIRAGANA",   // 6
        "KATAKANA",   // 7
        "KANJINUMERIC", // 8
        "GREEK",      // 9
        "CYRILLIC"    // 10
    };
    
    const char* type_name = NULL;
    if (char_type < sizeof(char_type_names) / sizeof(char_type_names[0])) {
        type_name = char_type_names[char_type];
    }
    
    // Search for matching unknown tokens by character type
    for (uint32_t i = 0; i < dict->unk_header.lexsize && count < max_matches; i++) {
        const Token* token = &dict->unk_tokens[i];
        const char* feature = mecab_dict_get_feature(dict, token);
        
        if (!feature) continue;
        
        // Check if feature contains the character type name
        bool matches_type = false;
        if (type_name && strstr(feature, type_name)) {
            matches_type = true;
        } else {
            // Additional matching rules based on character type
            switch (char_type) {
                case 2:  // KANJI
                    if (strstr(feature, "名詞") && strstr(feature, "一般")) {
                        matches_type = true;
                    }
                    break;
                case 3:  // SYMBOL
                    if (strstr(feature, "記号")) {
                        matches_type = true;
                    }
                    break;
                case 4:  // NUMERIC
                    if (strstr(feature, "名詞") && strstr(feature, "数")) {
                        matches_type = true;
                    }
                    break;
                case 5:  // ALPHA
                    if (strstr(feature, "名詞") && strstr(feature, "固有名詞")) {
                        matches_type = true;
                    }
                    break;
                case 6:  // HIRAGANA
                case 7:  // KATAKANA
                    if (strstr(feature, "名詞") || strstr(feature, "動詞") || 
                        strstr(feature, "形容詞")) {
                        matches_type = true;
                    }
                    break;
                default:
                    // For other types, accept general nouns
                    if (strstr(feature, "名詞")) {
                        matches_type = true;
                    }
                    break;
            }
        }
        
        if (matches_type) {
            matches[count].token = token;
            matches[count].length = 0;  // Unknown length
            matches[count].is_unk = true;
            count++;
            
            // Limit results based on character type
            int limit = (char_type == 2 || char_type == 6 || char_type == 7) ? 5 : 3;
            if (count >= limit) break;
        }
    }
    
    return count;
}