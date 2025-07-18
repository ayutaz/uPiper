#include "error_handling.h"
#include "phonemizer_internal.h"
#include "openjtalk_wrapper.h"

// Error messages
static const char* error_strings[] = {
    "Success",
    "Invalid handle",
    "Invalid input",
    "Memory allocation failed",
    "Dictionary not found",
    "Initialization failed",
    "Phonemization failed",
    "Processing error",
    "Invalid option",
    "Invalid dictionary format",
    "Invalid UTF-8 sequence"
};

// Set last error
void set_last_error(void* phonemizer, int error_code) {
    if (phonemizer) {
        OpenJTalkPhonemizer* p = (OpenJTalkPhonemizer*)phonemizer;
        p->last_error = error_code;
    }
}

// Get error string
const char* get_error_string(int error_code) {
    switch (error_code) {
        case OPENJTALK_SUCCESS:
            return error_strings[0];
        case OPENJTALK_ERROR_INVALID_HANDLE:
            return error_strings[1];
        case OPENJTALK_ERROR_INVALID_INPUT:
            return error_strings[2];
        case OPENJTALK_ERROR_MEMORY_ALLOCATION:
            return error_strings[3];
        case OPENJTALK_ERROR_DICTIONARY_NOT_FOUND:
            return error_strings[4];
        case OPENJTALK_ERROR_INITIALIZATION_FAILED:
            return error_strings[5];
        case OPENJTALK_ERROR_PHONEMIZATION_FAILED:
            return error_strings[6];
        case OPENJTALK_ERROR_PROCESSING:
            return error_strings[7];
        case OPENJTALK_ERROR_INVALID_OPTION:
            return error_strings[8];
        case OPENJTALK_ERROR_INVALID_DICTIONARY:
            return error_strings[9];
        case OPENJTALK_ERROR_INVALID_UTF8:
            return error_strings[10];
        default:
            return "Unknown error";
    }
}