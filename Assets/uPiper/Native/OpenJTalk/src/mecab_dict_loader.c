#include "mecab_dict_loader.h"
#include "mecab_darts.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <fcntl.h>
#include <sys/stat.h>

#ifdef _WIN32
    #include <windows.h>
    #define PATH_SEPARATOR "\\"
#else
    #include <sys/mman.h>
    #include <unistd.h>
    #define PATH_SEPARATOR "/"
#endif

// Magic numbers for dictionary files (from actual dictionary)
#define DIC_MAGIC_ID 0xE954A1B6     // Magic for sys.dic
#define UNK_MAGIC_ID 0xEF71994D     // Magic for unk.dic
#define MATRIX_MAGIC_ID 0xEF718F77

// Load binary file into memory
static void* load_file(const char* filename, size_t* size) {
    FILE* fp = fopen(filename, "rb");
    if (!fp) {
        return NULL;
    }
    
    // Get file size
    fseek(fp, 0, SEEK_END);
    *size = ftell(fp);
    fseek(fp, 0, SEEK_SET);
    
    // Allocate memory
    void* data = malloc(*size);
    if (!data) {
        fclose(fp);
        return NULL;
    }
    
    // Read file
    if (fread(data, 1, *size, fp) != *size) {
        free(data);
        fclose(fp);
        return NULL;
    }
    
    fclose(fp);
    return data;
}

// Memory map file (more efficient for large files)
static void* mmap_file(const char* filename, size_t* size) {
#ifdef _WIN32
    // Windows implementation
    HANDLE hFile = CreateFileA(filename, GENERIC_READ, FILE_SHARE_READ, 
                              NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
    if (hFile == INVALID_HANDLE_VALUE) {
        return NULL;
    }
    
    LARGE_INTEGER file_size;
    if (!GetFileSizeEx(hFile, &file_size)) {
        CloseHandle(hFile);
        return NULL;
    }
    *size = (size_t)file_size.QuadPart;
    
    HANDLE hMapping = CreateFileMappingA(hFile, NULL, PAGE_READONLY, 0, 0, NULL);
    if (!hMapping) {
        CloseHandle(hFile);
        return NULL;
    }
    
    void* addr = MapViewOfFile(hMapping, FILE_MAP_READ, 0, 0, 0);
    CloseHandle(hMapping);
    CloseHandle(hFile);
    
    return addr;
#else
    // Unix implementation
    int fd = open(filename, O_RDONLY);
    if (fd < 0) {
        return NULL;
    }
    
    struct stat st;
    if (fstat(fd, &st) < 0) {
        close(fd);
        return NULL;
    }
    *size = st.st_size;
    
    void* addr = mmap(NULL, *size, PROT_READ, MAP_PRIVATE, fd, 0);
    close(fd);
    
    if (addr == MAP_FAILED) {
        return NULL;
    }
    
    return addr;
#endif
}

// Unmap memory mapped file
static void munmap_file(void* addr, size_t size) {
#ifdef _WIN32
    UnmapViewOfFile(addr);
#else
    munmap(addr, size);
#endif
}

// Load character definition
static CharDef* load_char_def(const char* filename) {
    size_t size;
    void* data = load_file(filename, &size);
    if (!data) {
        return NULL;
    }
    
    CharDef* char_def = (CharDef*)calloc(1, sizeof(CharDef));
    if (!char_def) {
        free(data);
        return NULL;
    }
    
    // Parse binary format
    uint32_t* ptr = (uint32_t*)data;
    char_def->category_num = *ptr++;
    
    // Allocate category names
    char_def->category_names = (char**)calloc(char_def->category_num, sizeof(char*));
    
    // Read category names
    char* str_ptr = (char*)ptr;
    for (uint32_t i = 0; i < char_def->category_num; i++) {
        char_def->category_names[i] = strdup(str_ptr);
        str_ptr += strlen(str_ptr) + 1;
    }
    
    // Align to 4 bytes
    ptr = (uint32_t*)(((uintptr_t)str_ptr + 3) & ~3);
    
    // Read character mappings
    char_def->char_num = *ptr++;
    char_def->char_map = (uint32_t*)malloc(char_def->char_num * sizeof(uint32_t));
    memcpy(char_def->char_map, ptr, char_def->char_num * sizeof(uint32_t));
    
    free(data);
    return char_def;
}

// Load matrix
static Matrix* load_matrix(const char* filename) {
    size_t size;
    void* data = mmap_file(filename, &size);
    if (!data) {
        return NULL;
    }
    
    Matrix* matrix = (Matrix*)calloc(1, sizeof(Matrix));
    if (!matrix) {
        munmap_file(data, size);
        return NULL;
    }
    
    // Parse binary format
    uint16_t* ptr = (uint16_t*)data;
    matrix->lsize = *ptr++;
    matrix->rsize = *ptr++;
    
    // Point directly to mmap'd data
    matrix->matrix = (int16_t*)ptr;
    
    return matrix;
}

// Load POS definitions
static bool load_pos_def(MecabFullDictionary* dict, const char* filename) {
    FILE* fp = fopen(filename, "r");
    if (!fp) {
        return false;
    }
    
    // Count lines
    dict->pos_num = 0;
    char line[1024];
    while (fgets(line, sizeof(line), fp)) {
        dict->pos_num++;
    }
    
    // Allocate POS names
    dict->pos_names = (char**)calloc(dict->pos_num, sizeof(char*));
    
    // Read POS names
    rewind(fp);
    uint32_t i = 0;
    while (fgets(line, sizeof(line), fp) && i < dict->pos_num) {
        // Remove newline
        char* p = strchr(line, '\n');
        if (p) *p = '\0';
        
        // Skip empty lines
        if (strlen(line) == 0) continue;
        
        // Store POS name (format: "index,name")
        char* comma = strchr(line, ',');
        if (comma) {
            dict->pos_names[i] = strdup(comma + 1);
        }
        i++;
    }
    
    fclose(fp);
    return true;
}

// Validate dictionary header
bool mecab_dict_validate_header(const DictionaryHeader* header) {
    return header->magic == DIC_MAGIC_ID;
}

// Main dictionary loading function
MecabFullDictionary* mecab_dict_load(const char* dict_path) {
    MecabFullDictionary* dict = (MecabFullDictionary*)calloc(1, sizeof(MecabFullDictionary));
    if (!dict) {
        fprintf(stderr, "mecab_dict_load: Failed to allocate dictionary\n");
        return NULL;
    }
    
    char filepath[1024];
    
    // Load system dictionary
    snprintf(filepath, sizeof(filepath), "%s%ssys.dic", dict_path, PATH_SEPARATOR);
    dict->sys_data = mmap_file(filepath, &dict->sys_size);
    if (!dict->sys_data) {
        fprintf(stderr, "mecab_dict_load: Failed to mmap %s\n", filepath);
        goto error;
    }
    dict->use_mmap = true;
    dict->mmap_addr[0] = dict->sys_data;
    dict->mmap_size[0] = dict->sys_size;
    
    // Parse system dictionary header
    memcpy(&dict->sys_header, dict->sys_data, sizeof(DictionaryHeader));
    if (!mecab_dict_validate_header(&dict->sys_header)) {
        fprintf(stderr, "mecab_dict_load: Invalid sys.dic header (magic: 0x%08X)\n", dict->sys_header.magic);
        goto error;
    }
    
    // Load unknown word dictionary
    snprintf(filepath, sizeof(filepath), "%s%sunk.dic", dict_path, PATH_SEPARATOR);
    dict->unk_data = mmap_file(filepath, &dict->unk_size);
    if (!dict->unk_data) {
        fprintf(stderr, "mecab_dict_load: Failed to mmap %s\n", filepath);
        goto error;
    }
    dict->mmap_addr[1] = dict->unk_data;
    dict->mmap_size[1] = dict->unk_size;
    
    // Parse unknown word dictionary header
    memcpy(&dict->unk_header, dict->unk_data, sizeof(DictionaryHeader));
    // Unknown word dictionary has different magic number
    if (dict->unk_header.magic != UNK_MAGIC_ID) {
        fprintf(stderr, "mecab_dict_load: Invalid unk.dic header (magic: 0x%08X, expected: 0x%08X)\n", 
                dict->unk_header.magic, UNK_MAGIC_ID);
        goto error;
    }
    
    // Load character definition
    snprintf(filepath, sizeof(filepath), "%s%schar.bin", dict_path, PATH_SEPARATOR);
    dict->char_def = load_char_def(filepath);
    if (!dict->char_def) {
        fprintf(stderr, "mecab_dict_load: Failed to load %s\n", filepath);
        goto error;
    }
    
    // Load matrix
    snprintf(filepath, sizeof(filepath), "%s%smatrix.bin", dict_path, PATH_SEPARATOR);
    dict->matrix = load_matrix(filepath);
    if (!dict->matrix) {
        fprintf(stderr, "mecab_dict_load: Failed to load %s\n", filepath);
        goto error;
    }
    dict->mmap_addr[2] = dict->matrix->matrix;
    dict->mmap_size[2] = dict->matrix->lsize * dict->matrix->rsize * sizeof(int16_t) + 4;
    
    // Load POS definitions
    snprintf(filepath, sizeof(filepath), "%s%spos-id.def", dict_path, PATH_SEPARATOR);
    if (!load_pos_def(dict, filepath)) {
        // POS definitions are optional
    }
    
    // Set feature string pointer
    // Features are located after header + darts + tokens
    size_t feature_offset = sizeof(DictionaryHeader) + dict->sys_header.dsize + dict->sys_header.tsize;
    dict->feature_str = (const char*)dict->sys_data + feature_offset;
    dict->feature_size = dict->sys_header.fsize;
    
    // Load Darts for fast dictionary lookup
    const uint8_t* sys_ptr = (const uint8_t*)dict->sys_data + sizeof(DictionaryHeader);
    dict->sys_darts = darts_load(sys_ptr, dict->sys_header.dsize);
    if (!dict->sys_darts) {
        fprintf(stderr, "mecab_dict_load: Failed to load sys Darts\n");
        goto error;
    }
    
    const uint8_t* unk_ptr = (const uint8_t*)dict->unk_data + sizeof(DictionaryHeader);
    dict->unk_darts = darts_load(unk_ptr, dict->unk_header.dsize);
    if (!dict->unk_darts) {
        fprintf(stderr, "mecab_dict_load: Failed to load unk Darts\n");
        goto error;
    }
    
    return dict;
    
error:
    mecab_dict_free_full(dict);
    return NULL;
}

// Free dictionary
void mecab_dict_free_full(MecabFullDictionary* dict) {
    if (!dict) return;
    
    // Unmap memory mapped files
    if (dict->use_mmap) {
        for (int i = 0; i < 4; i++) {
            if (dict->mmap_addr[i] && dict->mmap_size[i] > 0) {
                munmap_file(dict->mmap_addr[i], dict->mmap_size[i]);
            }
        }
    } else {
        free(dict->sys_data);
        free(dict->unk_data);
    }
    
    // Free character definition
    if (dict->char_def) {
        for (uint32_t i = 0; i < dict->char_def->category_num; i++) {
            free(dict->char_def->category_names[i]);
        }
        free(dict->char_def->category_names);
        free(dict->char_def->char_map);
        free(dict->char_def);
    }
    
    // Free matrix
    if (dict->matrix && !dict->use_mmap) {
        free(dict->matrix);
    }
    
    // Free Darts
    if (dict->sys_darts) {
        darts_free(dict->sys_darts);
    }
    if (dict->unk_darts) {
        darts_free(dict->unk_darts);
    }
    
    // Free POS names
    if (dict->pos_names) {
        for (uint32_t i = 0; i < dict->pos_num; i++) {
            free(dict->pos_names[i]);
        }
        free(dict->pos_names);
    }
    
    free(dict);
}

// Get token from dictionary
const Token* mecab_dict_get_token(const MecabFullDictionary* dict, 
                                  uint32_t index, bool is_unk) {
    if (!dict) return NULL;
    
    const DictionaryHeader* header = is_unk ? &dict->unk_header : &dict->sys_header;
    const void* data = is_unk ? dict->unk_data : dict->sys_data;
    
    if (index >= header->lexsize) {
        return NULL;
    }
    
    // Tokens are located after header + darts
    size_t token_offset = sizeof(DictionaryHeader) + header->dsize;
    const Token* tokens = (const Token*)((uint8_t*)data + token_offset);
    return &tokens[index];
}

// Get feature string
const char* mecab_dict_get_feature(const MecabFullDictionary* dict, 
                                   const Token* token) {
    if (!dict || !token) return NULL;
    
    if (token->feature >= dict->feature_size) {
        return "";
    }
    
    return dict->feature_str + token->feature;
}

// Get connection cost
int16_t mecab_dict_get_connection_cost(const MecabFullDictionary* dict,
                                       uint16_t left_id, uint16_t right_id) {
    if (!dict || !dict->matrix) return 0;
    
    if (left_id >= dict->matrix->lsize || right_id >= dict->matrix->rsize) {
        return 0;
    }
    
    return dict->matrix->matrix[left_id * dict->matrix->rsize + right_id];
}

// Get character category
uint32_t mecab_dict_get_char_category(const MecabFullDictionary* dict, 
                                      uint32_t codepoint) {
    if (!dict || !dict->char_def) return 0;
    
    // Binary search in character map
    // TODO: Implement binary search for efficiency
    for (uint32_t i = 0; i < dict->char_def->char_num; i += 3) {
        uint32_t start = dict->char_def->char_map[i];
        uint32_t end = dict->char_def->char_map[i + 1];
        uint32_t category = dict->char_def->char_map[i + 2];
        
        if (codepoint >= start && codepoint <= end) {
            return category;
        }
    }
    
    return 0; // DEFAULT category
}

// Get POS name
const char* mecab_dict_get_pos_name(const MecabFullDictionary* dict, uint16_t posid) {
    if (!dict || !dict->pos_names || posid >= dict->pos_num) {
        return "不明";
    }
    
    return dict->pos_names[posid];
}

// Common prefix search implementation
int mecab_dict_common_prefix_search(const MecabFullDictionary* dict,
                                    const char* text, size_t len,
                                    DictMatch* results, int max_results) {
    if (!dict || !text || !results || max_results <= 0) {
        return 0;
    }
    
    int total_results = 0;
    
    // Search in system dictionary
    if (dict->sys_darts) {
        DartsResult darts_results[256];
        int count = darts_common_prefix_search(dict->sys_darts, text, len, 
                                               darts_results, 256);
        
        for (int i = 0; i < count && total_results < max_results; i++) {
            results[total_results].token = mecab_dict_get_token(dict, darts_results[i].value, false);
            results[total_results].length = darts_results[i].length;
            results[total_results].is_unk = false;
            total_results++;
        }
    }
    
    // If no matches found, try unknown word dictionary
    if (total_results == 0 && dict->unk_darts) {
        // Get character type for unknown word processing
        uint32_t codepoint = 0;
        // TODO: Proper UTF-8 decoding
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