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

#ifdef __cplusplus
}
#endif

#endif // UTF8_UTILS_H