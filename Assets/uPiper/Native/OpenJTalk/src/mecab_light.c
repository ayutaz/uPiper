#include "mecab_light.h"
#include "mecab_light_impl.h"
#include <stdlib.h>
#include <string.h>
#include <stdio.h>

// MecabLight structure implementation
struct _MecabLight {
    struct MecabLightImpl* impl;
    MecabDictionary* dict;
    char error_message[256];
};

// Create MecabLight instance
MecabLight* mecab_light_create(const char* dic_path) {
    MecabLight* mecab = (MecabLight*)calloc(1, sizeof(MecabLight));
    if (!mecab) {
        return NULL;
    }
    
    // Create minimal dictionary (ignoring dic_path for now)
    mecab->dict = mecab_dict_create_minimal();
    if (!mecab->dict) {
        free(mecab);
        return NULL;
    }
    
    // Create implementation
    mecab->impl = mecab_impl_create(mecab->dict);
    if (!mecab->impl) {
        mecab_dict_free(mecab->dict);
        free(mecab);
        return NULL;
    }
    
    return mecab;
}

// Destroy MecabLight instance
void mecab_light_destroy(MecabLight* mecab) {
    if (!mecab) return;
    
    if (mecab->impl) {
        mecab_impl_destroy(mecab->impl);
    }
    
    if (mecab->dict) {
        mecab_dict_free(mecab->dict);
    }
    
    free(mecab);
}

// Parse text
MecabNode* mecab_light_parse(MecabLight* mecab, const char* text) {
    if (!mecab || !text) {
        return NULL;
    }
    
    // Clear error
    mecab->error_message[0] = '\0';
    
    // Parse with implementation
    LatticeNode* lattice = mecab_impl_parse(mecab->impl, text);
    if (!lattice) {
        snprintf(mecab->error_message, sizeof(mecab->error_message),
                 "Failed to parse text");
        return NULL;
    }
    
    // Convert to MecabNode list
    MecabNode* nodes = mecab_impl_lattice_to_nodes(mecab->impl, lattice);
    
    // Debug output
    if (getenv("DEBUG_MECAB")) {
        printf("Mecab parse result for '%s':\n", text);
        MecabNode* debug_node = nodes;
        while (debug_node) {
            printf("  surface='%s', pos='%s', reading='%s'\n",
                   debug_node->surface,
                   debug_node->feature.pos,
                   debug_node->feature.reading);
            debug_node = debug_node->next;
        }
    }
    
    return nodes;
}

// Free nodes
void mecab_light_free_nodes(MecabLight* mecab, MecabNode* node) {
    while (node) {
        MecabNode* next = node->next;
        free(node);
        node = next;
    }
}

// Get error message
const char* mecab_light_get_error(MecabLight* mecab) {
    if (!mecab) {
        return "Invalid MecabLight instance";
    }
    return mecab->error_message;
}

// Load dictionary (stub for now)
bool mecab_light_load_dictionary(MecabLight* mecab, const char* dic_path) {
    if (!mecab) {
        return false;
    }
    
    // For now, we're using the built-in minimal dictionary
    // In a full implementation, this would load an external dictionary file
    return true;
}

// Parse feature string
bool mecab_light_parse_feature(const char* feature_str, MecabFeature* feature) {
    if (!feature_str || !feature) {
        return false;
    }
    
    // Clear feature
    memset(feature, 0, sizeof(MecabFeature));
    
    // Simple parsing for our minimal format
    // In full Mecab, this would parse CSV format with many fields
    char buffer[1024];
    strncpy(buffer, feature_str, sizeof(buffer) - 1);
    buffer[sizeof(buffer) - 1] = '\0';
    
    // For now, just copy the whole string as POS
    strncpy(feature->pos, buffer, sizeof(feature->pos) - 1);
    
    return true;
}