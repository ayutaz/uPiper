#ifndef UTF8_UTILS_H
#define UTF8_UTILS_H

#include <stddef.h>

#ifdef __cplusplus
extern "C" {
#endif

// Get UTF-8 character length in bytes
static inline int utf8_char_len(unsigned char c) {
    if (c < 0x80) return 1;
    if ((c & 0xE0) == 0xC0) return 2;
    if ((c & 0xF0) == 0xE0) return 3;
    if ((c & 0xF8) == 0xF0) return 4;
    return 1;
}

// Count UTF-8 characters in string
static inline size_t utf8_strlen(const char* str) {
    size_t count = 0;
    while (*str) {
        int len = utf8_char_len((unsigned char)*str);
        str += len;
        count++;
    }
    return count;
}

// Count UTF-8 characters in string with byte length
static inline size_t utf8_strlen_n(const char* str, size_t n) {
    size_t count = 0;
    size_t pos = 0;
    while (pos < n && str[pos]) {
        int len = utf8_char_len((unsigned char)str[pos]);
        pos += len;
        count++;
    }
    return count;
}

// Validate UTF-8 string
static inline int utf8_validate(const char* str, size_t length) {
    size_t i = 0;
    
    while (i < length && str[i]) {
        unsigned char c = (unsigned char)str[i];
        int char_len = 0;
        
        // Determine character length and validate first byte
        if (c < 0x80) {
            // ASCII (0xxxxxxx)
            char_len = 1;
        } else if ((c & 0xE0) == 0xC0) {
            // 2-byte sequence (110xxxxx)
            char_len = 2;
            if ((c & 0xFE) == 0xC0) return 0; // Overlong encoding
        } else if ((c & 0xF0) == 0xE0) {
            // 3-byte sequence (1110xxxx)
            char_len = 3;
            if (c == 0xE0 && i + 1 < length && (unsigned char)str[i + 1] < 0xA0) return 0; // Overlong
        } else if ((c & 0xF8) == 0xF0) {
            // 4-byte sequence (11110xxx)
            char_len = 4;
            if (c > 0xF4) return 0; // Out of range
        } else {
            // Invalid first byte
            return 0;
        }
        
        // Check if we have enough bytes
        if (i + char_len > length) return 0;
        
        // Validate continuation bytes
        for (int j = 1; j < char_len; j++) {
            unsigned char cont = (unsigned char)str[i + j];
            if ((cont & 0xC0) != 0x80) {
                // Not a valid continuation byte (10xxxxxx)
                return 0;
            }
        }
        
        // Additional validation for specific cases
        if (char_len == 3) {
            // Check for UTF-16 surrogates (U+D800 to U+DFFF)
            if (c == 0xED && (unsigned char)str[i + 1] >= 0xA0) {
                return 0;
            }
        }
        
        i += char_len;
    }
    
    return 1; // Valid UTF-8
}

#ifdef __cplusplus
}
#endif

#endif // UTF8_UTILS_H