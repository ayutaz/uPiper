#ifndef OPENJTALK_WRAPPER_H
#define OPENJTALK_WRAPPER_H

#ifdef _WIN32
    #ifdef OPENJTALK_EXPORTS
        #define OPENJTALK_API __declspec(dllexport)
    #else
        #define OPENJTALK_API __declspec(dllimport)
    #endif
#else
    #define OPENJTALK_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

// OpenJTalk wrapper handle
typedef void* OpenJTalkHandle;

// Initialize OpenJTalk instance
// Returns: OpenJTalk handle on success, NULL on failure
OPENJTALK_API OpenJTalkHandle openjtalk_create();

// Destroy OpenJTalk instance
// Parameters:
//   handle: OpenJTalk handle to destroy
OPENJTALK_API void openjtalk_destroy(OpenJTalkHandle handle);

// Check if OpenJTalk is available
// Returns: 1 if available, 0 otherwise
OPENJTALK_API int openjtalk_is_available();

// Ensure dictionary is available (download if needed)
// Returns: 1 on success, 0 on failure
OPENJTALK_API int openjtalk_ensure_dictionary();

// Convert Japanese text to phonemes
// Parameters:
//   handle: OpenJTalk handle
//   text: UTF-8 encoded Japanese text
//   phonemes_out: Output buffer for phonemes (must be freed by caller)
//   phonemes_len: Length of phonemes output
// Returns: 1 on success, 0 on failure
OPENJTALK_API int openjtalk_text_to_phonemes(
    OpenJTalkHandle handle,
    const char* text,
    char** phonemes_out,
    int* phonemes_len
);

// Free phonemes string allocated by openjtalk_text_to_phonemes
// Parameters:
//   phonemes: Phonemes string to free
OPENJTALK_API void openjtalk_free_phonemes(char* phonemes);

// Get last error message
// Returns: Error message string (do not free)
OPENJTALK_API const char* openjtalk_get_last_error();

// Version information
OPENJTALK_API const char* openjtalk_get_version();

#ifdef __cplusplus
}
#endif

#endif // OPENJTALK_WRAPPER_H