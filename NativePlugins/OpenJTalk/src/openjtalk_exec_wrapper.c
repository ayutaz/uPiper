#include "openjtalk_wrapper.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>
#include <sys/wait.h>

#define VERSION "3.0.0-exec"
#define MAX_PHONEME_COUNT 1000

// Debug logging
#ifdef ENABLE_DEBUG_LOG
#define DEBUG_LOG(fmt, ...) fprintf(stderr, "[OpenJTalk] " fmt "\n", ##__VA_ARGS__)
#else
#define DEBUG_LOG(fmt, ...)
#endif

// OpenJTalk context structure
typedef struct {
    char* dict_path;
    char* openjtalk_bin;
    int last_error;
    bool initialized;
} OpenJTalkContext;

// Phoneme mapping from OpenJTalk to Piper format
static int get_phoneme_id(const char* phoneme) {
    // Simplified mapping - should be expanded for full mapping
    if (strcmp(phoneme, "sil") == 0 || strcmp(phoneme, "pau") == 0) return 0;
    if (strcmp(phoneme, "a") == 0) return 2;
    if (strcmp(phoneme, "i") == 0) return 3;
    if (strcmp(phoneme, "u") == 0) return 4;
    if (strcmp(phoneme, "e") == 0) return 5;
    if (strcmp(phoneme, "o") == 0) return 6;
    if (strcmp(phoneme, "k") == 0) return 7;
    if (strcmp(phoneme, "g") == 0) return 8;
    if (strcmp(phoneme, "s") == 0) return 9;
    if (strcmp(phoneme, "sh") == 0) return 10;
    if (strcmp(phoneme, "z") == 0) return 11;
    if (strcmp(phoneme, "t") == 0) return 12;
    if (strcmp(phoneme, "ch") == 0) return 13;
    if (strcmp(phoneme, "ts") == 0) return 14;
    if (strcmp(phoneme, "d") == 0) return 15;
    if (strcmp(phoneme, "n") == 0) return 16;
    if (strcmp(phoneme, "h") == 0) return 17;
    if (strcmp(phoneme, "b") == 0) return 18;
    if (strcmp(phoneme, "p") == 0) return 19;
    if (strcmp(phoneme, "m") == 0) return 20;
    if (strcmp(phoneme, "y") == 0) return 21;
    if (strcmp(phoneme, "r") == 0) return 22;
    if (strcmp(phoneme, "w") == 0) return 24;
    if (strcmp(phoneme, "N") == 0) return 16; // ん
    if (strcmp(phoneme, "cl") == 0) return 0; // Closure
    if (strcmp(phoneme, "q") == 0) return 23; // っ
    
    return 1; // Unknown
}

// Version information
const char* openjtalk_get_version(void) {
    return VERSION;
}

// Create phonemizer instance
void* openjtalk_create(const char* dict_path) {
    if (!dict_path) {
        return NULL;
    }
    
    OpenJTalkContext* ctx = (OpenJTalkContext*)calloc(1, sizeof(OpenJTalkContext));
    if (!ctx) {
        return NULL;
    }
    
    // Store dictionary path
    ctx->dict_path = strdup(dict_path);
    if (!ctx->dict_path) {
        free(ctx);
        return NULL;
    }
    
    // Find OpenJTalk binary
    ctx->openjtalk_bin = strdup("external/openjtalk_build/install/bin/open_jtalk");
    if (access(ctx->openjtalk_bin, X_OK) != 0) {
        // Try system path
        free(ctx->openjtalk_bin);
        ctx->openjtalk_bin = strdup("/usr/local/bin/open_jtalk");
        if (access(ctx->openjtalk_bin, X_OK) != 0) {
            free(ctx->dict_path);
            free(ctx->openjtalk_bin);
            free(ctx);
            return NULL;
        }
    }
    
    ctx->initialized = true;
    ctx->last_error = OPENJTALK_SUCCESS;
    
    DEBUG_LOG("OpenJTalk exec wrapper initialized with dictionary: %s", dict_path);
    DEBUG_LOG("OpenJTalk binary: %s", ctx->openjtalk_bin);
    
    return ctx;
}

// Destroy phonemizer instance
void openjtalk_destroy(void* handle) {
    if (!handle) {
        return;
    }
    
    OpenJTalkContext* ctx = (OpenJTalkContext*)handle;
    
    if (ctx->dict_path) {
        free(ctx->dict_path);
    }
    
    if (ctx->openjtalk_bin) {
        free(ctx->openjtalk_bin);
    }
    
    free(ctx);
}

// Parse label file to extract phonemes
static PhonemeResult* parse_label_file(const char* label_file) {
    FILE* fp = fopen(label_file, "r");
    if (!fp) {
        return NULL;
    }
    
    PhonemeResult* result = (PhonemeResult*)calloc(1, sizeof(PhonemeResult));
    if (!result) {
        fclose(fp);
        return NULL;
    }
    
    // Temporary storage
    char* phonemes[MAX_PHONEME_COUNT];
    int phoneme_ids[MAX_PHONEME_COUNT];
    float durations[MAX_PHONEME_COUNT];
    int phoneme_count = 0;
    
    char line[1024];
    while (fgets(line, sizeof(line), fp) && phoneme_count < MAX_PHONEME_COUNT) {
        // Parse label format: start_time end_time label
        // Label format: xx^xx-phoneme+xx=xx/...
        char* label_start = strrchr(line, ' ');
        if (!label_start) continue;
        label_start++; // Skip space
        
        // Extract phoneme from label
        char* p_start = strchr(label_start, '-');
        char* p_end = strchr(label_start, '+');
        
        if (p_start && p_end && p_start < p_end) {
            p_start++; // Skip '-'
            size_t len = p_end - p_start;
            
            char phoneme[32];
            strncpy(phoneme, p_start, len);
            phoneme[len] = '\0';
            
            // Store phoneme
            phonemes[phoneme_count] = strdup(phoneme);
            phoneme_ids[phoneme_count] = get_phoneme_id(phoneme);
            durations[phoneme_count] = 0.05f; // Default duration
            phoneme_count++;
        }
    }
    
    fclose(fp);
    
    // Allocate result arrays
    result->phoneme_count = phoneme_count;
    result->phonemes = (char*)calloc(phoneme_count * 8 + 1, sizeof(char));
    result->phoneme_ids = (int*)calloc(phoneme_count, sizeof(int));
    result->durations = (float*)calloc(phoneme_count, sizeof(float));
    
    if (!result->phonemes || !result->phoneme_ids || !result->durations) {
        for (int i = 0; i < phoneme_count; i++) {
            free(phonemes[i]);
        }
        openjtalk_free_result(result);
        return NULL;
    }
    
    // Build result
    char* phoneme_ptr = result->phonemes;
    for (int i = 0; i < phoneme_count; i++) {
        // Map special phonemes
        if (strcmp(phonemes[i], "sil") == 0) {
            strcpy(phoneme_ptr, "pau");
            phoneme_ptr += 3;
        } else {
            strcpy(phoneme_ptr, phonemes[i]);
            phoneme_ptr += strlen(phonemes[i]);
        }
        
        if (i < phoneme_count - 1) {
            *phoneme_ptr++ = ' ';
        }
        
        result->phoneme_ids[i] = phoneme_ids[i];
        result->durations[i] = durations[i];
        result->total_duration += durations[i];
        
        free(phonemes[i]);
    }
    
    return result;
}

// Phonemize text
PhonemeResult* openjtalk_phonemize(void* handle, const char* text) {
    if (!handle || !text) {
        return NULL;
    }
    
    OpenJTalkContext* ctx = (OpenJTalkContext*)handle;
    
    if (!ctx->initialized) {
        ctx->last_error = OPENJTALK_ERROR_INITIALIZATION_FAILED;
        return NULL;
    }
    
    DEBUG_LOG("Phonemizing text: %s", text);
    
    // Create temporary files
    char input_file[] = "/tmp/openjtalk_input_XXXXXX";
    char label_file[] = "/tmp/openjtalk_label_XXXXXX";
    
    int input_fd = mkstemp(input_file);
    if (input_fd < 0) {
        ctx->last_error = OPENJTALK_ERROR_UNKNOWN;
        return NULL;
    }
    
    int label_fd = mkstemp(label_file);
    if (label_fd < 0) {
        close(input_fd);
        unlink(input_file);
        ctx->last_error = OPENJTALK_ERROR_UNKNOWN;
        return NULL;
    }
    
    // Write input text
    FILE* fp = fdopen(input_fd, "w");
    if (fp) {
        fprintf(fp, "%s\n", text);
        fclose(fp);
    } else {
        close(input_fd);
    }
    close(label_fd);
    
    // Run OpenJTalk
    pid_t pid = fork();
    if (pid == 0) {
        // Child process
        execl(ctx->openjtalk_bin, "open_jtalk",
              "-x", ctx->dict_path,
              "-ot", label_file,
              "-ow", "/dev/null",
              input_file,
              NULL);
        // If exec fails
        exit(1);
    } else if (pid > 0) {
        // Parent process
        int status;
        waitpid(pid, &status, 0);
        
        if (WIFEXITED(status) && WEXITSTATUS(status) == 0) {
            // Parse label file
            PhonemeResult* result = parse_label_file(label_file);
            
            // Clean up
            unlink(input_file);
            unlink(label_file);
            
            if (result) {
                DEBUG_LOG("Phonemization complete: %d phonemes", result->phoneme_count);
                ctx->last_error = OPENJTALK_SUCCESS;
                return result;
            }
        }
    }
    
    // Clean up on error
    unlink(input_file);
    unlink(label_file);
    
    ctx->last_error = OPENJTALK_ERROR_ANALYSIS_FAILED;
    return NULL;
}

// Free phoneme result
void openjtalk_free_result(PhonemeResult* result) {
    if (!result) return;
    
    if (result->phonemes) free(result->phonemes);
    if (result->phoneme_ids) free(result->phoneme_ids);
    if (result->durations) free(result->durations);
    free(result);
}

// Get last error
int openjtalk_get_last_error(void* handle) {
    if (!handle) {
        return OPENJTALK_ERROR_INVALID_HANDLE;
    }
    
    OpenJTalkContext* ctx = (OpenJTalkContext*)handle;
    return ctx->last_error;
}

// Get error string
const char* openjtalk_get_error_string(int error_code) {
    switch (error_code) {
        case OPENJTALK_SUCCESS:
            return "Success";
        case OPENJTALK_ERROR_INVALID_HANDLE:
            return "Invalid handle";
        case OPENJTALK_ERROR_INVALID_INPUT:
            return "Invalid input";
        case OPENJTALK_ERROR_INITIALIZATION_FAILED:
            return "Initialization failed";
        case OPENJTALK_ERROR_DICTIONARY_NOT_FOUND:
            return "Dictionary not found";
        case OPENJTALK_ERROR_MEMORY_ALLOCATION:
            return "Memory allocation failed";
        case OPENJTALK_ERROR_ANALYSIS_FAILED:
            return "Analysis failed";
        case OPENJTALK_ERROR_INVALID_OPTION:
            return "Invalid option";
        case OPENJTALK_ERROR_UNKNOWN:
        default:
            return "Unknown error";
    }
}

// Set option (stub for now)
int openjtalk_set_option(void* handle, const char* key, const char* value) {
    if (!handle || !key || !value) {
        return OPENJTALK_ERROR_INVALID_INPUT;
    }
    
    // Options can be implemented as needed
    return OPENJTALK_SUCCESS;
}

// Get option (stub for now)
const char* openjtalk_get_option(void* handle, const char* key) {
    if (!handle || !key) {
        return NULL;
    }
    
    return NULL;
}