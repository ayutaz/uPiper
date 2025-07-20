#ifndef ERROR_HANDLING_H
#define ERROR_HANDLING_H

// Set last error for a phonemizer instance
void set_last_error(void* phonemizer, int error_code);

// Get error string for an error code
const char* get_error_string(int error_code);

#endif // ERROR_HANDLING_H