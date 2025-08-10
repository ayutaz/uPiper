/**
 * Simplified OpenJTalk WebAssembly Implementation
 * Uses dictionary directly without full MeCab compilation
 */

#include <emscripten.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>

#define TRUE 1
#define FALSE 0

// Dictionary structure
typedef struct {
    char* sys_dic;
    char* unk_dic;
    size_t sys_dic_size;
    size_t unk_dic_size;
    int loaded;
} Dictionary;

static Dictionary g_dict = {NULL, NULL, 0, 0, 0};

// Simple MeCab replacement - dictionary lookup
typedef struct {
    const char* surface;
    const char* phonemes;
} DictEntry;

// Common Japanese words with phonemes
static const DictEntry basic_dict[] = {
    {"こんにちは", "k o N n i ch i w a"},
    {"ありがとう", "a r i g a t o:"},
    {"おはよう", "o h a y o:"},
    {"さようなら", "s a y o: n a r a"},
    {"すみません", "s u m i m a s e N"},
    {"はい", "h a i"},
    {"いいえ", "i: e"},
    {"日本", "n i h o N"},
    {"東京", "t o: ky o:"},
    {"です", "d e s u"},
    {"ます", "m a s u"},
    {"テスト", "t e s u t o"},
    {"音声", "o N s e:"},
    {"合成", "g o: s e:"},
    {"こんばんは", "k o N b a N w a"},
    {"おやすみ", "o y a s u m i"},
    {"わかりました", "w a k a r i m a sh i t a"},
    {"お願いします", "o n e g a i sh i m a s u"},
    {"大丈夫", "d a i j o: b u"},
    {"元気", "g e N k i"},
    {NULL, NULL}
};

// Hiragana to phoneme mapping
static const struct {
    const char* kana;
    const char* phoneme;
} kana_map[] = {
    // Basic hiragana
    {"あ", "a"}, {"い", "i"}, {"う", "u"}, {"え", "e"}, {"お", "o"},
    {"か", "k a"}, {"き", "k i"}, {"く", "k u"}, {"け", "k e"}, {"こ", "k o"},
    {"が", "g a"}, {"ぎ", "g i"}, {"ぐ", "g u"}, {"げ", "g e"}, {"ご", "g o"},
    {"さ", "s a"}, {"し", "sh i"}, {"す", "s u"}, {"せ", "s e"}, {"そ", "s o"},
    {"ざ", "z a"}, {"じ", "z i"}, {"ず", "z u"}, {"ぜ", "z e"}, {"ぞ", "z o"},
    {"た", "t a"}, {"ち", "ch i"}, {"つ", "ts u"}, {"て", "t e"}, {"と", "t o"},
    {"だ", "d a"}, {"ぢ", "d i"}, {"づ", "d u"}, {"で", "d e"}, {"ど", "d o"},
    {"な", "n a"}, {"に", "n i"}, {"ぬ", "n u"}, {"ね", "n e"}, {"の", "n o"},
    {"は", "h a"}, {"ひ", "h i"}, {"ふ", "h u"}, {"へ", "h e"}, {"ほ", "h o"},
    {"ば", "b a"}, {"び", "b i"}, {"ぶ", "b u"}, {"べ", "b e"}, {"ぼ", "b o"},
    {"ぱ", "p a"}, {"ぴ", "p i"}, {"ぷ", "p u"}, {"ぺ", "p e"}, {"ぽ", "p o"},
    {"ま", "m a"}, {"み", "m i"}, {"む", "m u"}, {"め", "m e"}, {"も", "m o"},
    {"や", "y a"}, {"ゆ", "y u"}, {"よ", "y o"},
    {"ら", "r a"}, {"り", "r i"}, {"る", "r u"}, {"れ", "r e"}, {"ろ", "r o"},
    {"わ", "w a"}, {"を", "w o"}, {"ん", "N"},
    // Small kana
    {"ゃ", "y a"}, {"ゅ", "y u"}, {"ょ", "y o"},
    {"っ", "q"},
    // Katakana
    {"ア", "a"}, {"イ", "i"}, {"ウ", "u"}, {"エ", "e"}, {"オ", "o"},
    {"カ", "k a"}, {"キ", "k i"}, {"ク", "k u"}, {"ケ", "k e"}, {"コ", "k o"},
    {"ガ", "g a"}, {"ギ", "g i"}, {"グ", "g u"}, {"ゲ", "g e"}, {"ゴ", "g o"},
    {"サ", "s a"}, {"シ", "sh i"}, {"ス", "s u"}, {"セ", "s e"}, {"ソ", "s o"},
    {"ザ", "z a"}, {"ジ", "z i"}, {"ズ", "z u"}, {"ゼ", "z e"}, {"ゾ", "z o"},
    {"タ", "t a"}, {"チ", "ch i"}, {"ツ", "ts u"}, {"テ", "t e"}, {"ト", "t o"},
    {"ダ", "d a"}, {"ヂ", "d i"}, {"ヅ", "d u"}, {"デ", "d e"}, {"ド", "d o"},
    {"ナ", "n a"}, {"ニ", "n i"}, {"ヌ", "n u"}, {"ネ", "n e"}, {"ノ", "n o"},
    {"ハ", "h a"}, {"ヒ", "h i"}, {"フ", "h u"}, {"ヘ", "h e"}, {"ホ", "h o"},
    {"バ", "b a"}, {"ビ", "b i"}, {"ブ", "b u"}, {"ベ", "b e"}, {"ボ", "b o"},
    {"パ", "p a"}, {"ピ", "p i"}, {"プ", "p u"}, {"ペ", "p e"}, {"ポ", "p o"},
    {"マ", "m a"}, {"ミ", "m i"}, {"ム", "m u"}, {"メ", "m e"}, {"モ", "m o"},
    {"ヤ", "y a"}, {"ユ", "y u"}, {"ヨ", "y o"},
    {"ラ", "r a"}, {"リ", "r i"}, {"ル", "r u"}, {"レ", "r e"}, {"ロ", "r o"},
    {"ワ", "w a"}, {"ヲ", "w o"}, {"ン", "N"},
    {"ー", ":"}, // Long vowel mark
    {NULL, NULL}
};

// Helper function to get UTF-8 character length
static int utf8_char_len(unsigned char c) {
    if (c < 0x80) return 1;
    if ((c & 0xE0) == 0xC0) return 2;
    if ((c & 0xF0) == 0xE0) return 3;
    if ((c & 0xF8) == 0xF0) return 4;
    return 1;
}

// Convert text to phonemes using dictionary and kana mapping
static int text_to_phonemes(const char* text, char* output, int output_size) {
    printf("[OpenJTalk WASM Simple] Converting text: %s\n", text);
    
    // First check if entire text is in dictionary
    for (int i = 0; basic_dict[i].surface != NULL; i++) {
        if (strcmp(text, basic_dict[i].surface) == 0) {
            strncpy(output, basic_dict[i].phonemes, output_size - 1);
            output[output_size - 1] = '\0';
            printf("[OpenJTalk WASM Simple] Found in dictionary: %s\n", output);
            return strlen(output);
        }
    }
    
    // Otherwise, convert character by character
    output[0] = '\0';
    int output_len = 0;
    const char* p = text;
    
    while (*p) {
        int char_len = utf8_char_len((unsigned char)*p);
        char current_char[5] = {0};
        strncpy(current_char, p, char_len);
        
        // Look up in kana map
        const char* phoneme = NULL;
        for (int i = 0; kana_map[i].kana != NULL; i++) {
            if (strncmp(p, kana_map[i].kana, char_len) == 0 &&
                strlen(kana_map[i].kana) == char_len) {
                phoneme = kana_map[i].phoneme;
                break;
            }
        }
        
        if (phoneme) {
            if (output_len > 0) {
                strcat(output, " ");
                output_len++;
            }
            strcat(output, phoneme);
            output_len += strlen(phoneme);
        } else if (char_len == 1 && *p >= 'a' && *p <= 'z') {
            // ASCII letters
            if (output_len > 0) {
                strcat(output, " ");
                output_len++;
            }
            output[strlen(output)] = *p;
            output_len++;
        }
        
        p += char_len;
        
        // Check for buffer overflow
        if (output_len >= output_size - 10) {
            break;
        }
    }
    
    printf("[OpenJTalk WASM Simple] Phonemes: %s\n", output);
    return output_len;
}

/**
 * Initialize OpenJTalk
 */
EMSCRIPTEN_KEEPALIVE
int Open_JTalk_initialize() {
    printf("[OpenJTalk WASM Simple] Initializing...\n");
    return 0;
}

/**
 * Load dictionary (dummy - we use embedded dictionary)
 */
EMSCRIPTEN_KEEPALIVE
int Open_JTalk_load(const char* dict_path) {
    printf("[OpenJTalk WASM Simple] Loading dictionary from: %s\n", 
           dict_path ? dict_path : "embedded");
    g_dict.loaded = 1;
    return 0;
}

/**
 * Convert text to phonemes
 */
EMSCRIPTEN_KEEPALIVE
int Open_JTalk_synthesis(const char* text, char* output, int output_size) {
    if (!text || !output || output_size <= 0) {
        printf("[OpenJTalk WASM Simple] Invalid parameters\n");
        return -1;
    }
    
    // Add silence markers
    strcpy(output, "pau ");
    int len = 4;
    
    // Convert text to phonemes
    char phonemes[4096];
    int phoneme_len = text_to_phonemes(text, phonemes, sizeof(phonemes));
    
    if (phoneme_len > 0) {
        strcat(output, phonemes);
        len += phoneme_len;
        strcat(output, " pau");
        len += 4;
    }
    
    return len;
}

/**
 * Clear resources
 */
EMSCRIPTEN_KEEPALIVE
void Open_JTalk_clear() {
    printf("[OpenJTalk WASM Simple] Clearing...\n");
    g_dict.loaded = 0;
}

/**
 * Helper functions
 */
EMSCRIPTEN_KEEPALIVE
void* allocate_memory(int size) {
    return malloc(size);
}

EMSCRIPTEN_KEEPALIVE
void free_memory(void* ptr) {
    if (ptr) free(ptr);
}

EMSCRIPTEN_KEEPALIVE
int get_string_length(const char* str) {
    return str ? strlen(str) : 0;
}

EMSCRIPTEN_KEEPALIVE
const char* get_version() {
    return "OpenJTalk WASM Simple 1.0";
}

EMSCRIPTEN_KEEPALIVE
int test_function() {
    printf("[OpenJTalk WASM Simple] Test function called\n");
    return 42;
}