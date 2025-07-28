#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "flite.h"

#ifdef _WIN32
    #ifdef FLITE_UNITY_EXPORTS
        #define EXPORT __declspec(dllexport)
    #else
        #define EXPORT __declspec(dllimport)
    #endif
#else
    #define EXPORT __attribute__((visibility("default")))
#endif

// Forward declarations
cst_voice *register_cmu_us_kal(const char *voxdir);
void unregister_cmu_us_kal(cst_voice *v);

// Flite context for Unity
typedef struct {
    cst_voice* voice;
    int initialized;
} flite_unity_context;

// Initialize Flite
EXPORT flite_unity_context* flite_unity_init() {
    flite_unity_context* ctx = (flite_unity_context*)malloc(sizeof(flite_unity_context));
    if (!ctx) return NULL;
    
    // Initialize Flite
    flite_init();
    
    // Register US English voice (for LTS)
    ctx->voice = register_cmu_us_kal(NULL);
    if (!ctx->voice) {
        free(ctx);
        return NULL;
    }
    
    ctx->initialized = 1;
    return ctx;
}

// Convert text to phonemes using Flite LTS
EXPORT char* flite_unity_text_to_phones(flite_unity_context* ctx, const char* text) {
    if (!ctx || !ctx->initialized || !text) {
        return NULL;
    }
    
    // Create utterance
    cst_utterance *utt = new_utterance();
    if (!utt) return NULL;
    
    // Set text
    utt_set_input_text(utt, text);
    
    // Apply text analysis (tokenization, POS tagging, etc.)
    utt = flite_do_synth(utt, ctx->voice, utt_text);
    
    // Extract phonemes from segment relation
    cst_item *s;
    char *result = (char*)malloc(1024); // Allocate buffer
    if (!result) {
        delete_utterance(utt);
        return NULL;
    }
    result[0] = '\0';
    
    // Get phonemes from segment relation
    for (s = relation_head(utt_relation(utt, "Segment")); s; s = item_next(s)) {
        const char *phone = item_feat_string(s, "name");
        if (phone) {
            if (strlen(result) > 0) {
                strcat(result, " ");
            }
            strcat(result, phone);
        }
    }
    
    delete_utterance(utt);
    
    // Return copy that Unity can free
    char *final_result = (char*)malloc(strlen(result) + 1);
    if (final_result) {
        strcpy(final_result, result);
    }
    free(result);
    
    return final_result;
}

// Free string allocated by Flite
EXPORT void flite_unity_free_string(char* str) {
    if (str) {
        free(str);
    }
}

// Get Flite version
EXPORT const char* flite_unity_get_version() {
    return "2.3-unity";
}

// Cleanup
EXPORT void flite_unity_cleanup(flite_unity_context* ctx) {
    if (ctx) {
        if (ctx->voice) {
            unregister_cmu_us_kal(ctx->voice);
        }
        free(ctx);
    }
}

// Utility: Check if word exists in lexicon
EXPORT int flite_unity_word_in_lexicon(flite_unity_context* ctx, const char* word) {
    if (!ctx || !ctx->initialized || !word) {
        return 0;
    }
    
    cst_lexicon *lex = val_lexicon(feat_val(ctx->voice->features, "lexicon"));
    if (!lex) return 0;
    
    cst_val *phones = lex_lookup(lex, word, NULL);
    int found = (phones != NULL);
    if (phones) delete_val(phones);
    
    return found;
}

// Get letter-to-sound rules for a word
EXPORT char* flite_unity_lts_apply(flite_unity_context* ctx, const char* word) {
    if (!ctx || !ctx->initialized || !word) {
        return NULL;
    }
    
    cst_lexicon *lex = val_lexicon(feat_val(ctx->voice->features, "lexicon"));
    if (!lex || !lex->lts_rule_set) return NULL;
    
    // Apply LTS rules
    cst_val *phones = lts_apply(word, "", lex->lts_rule_set);
    if (!phones) return NULL;
    
    // Convert to string
    char buffer[1024] = "";
    cst_val *p;
    for (p = phones; p; p = val_cdr(p)) {
        if (strlen(buffer) > 0) strcat(buffer, " ");
        strcat(buffer, val_string(val_car(p)));
    }
    
    delete_val(phones);
    
    // Return copy
    char *result = (char*)malloc(strlen(buffer) + 1);
    if (result) {
        strcpy(result, buffer);
    }
    return result;
}