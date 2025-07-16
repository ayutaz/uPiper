#include "phoneme_converter.h"
#include <stdlib.h>
#include <string.h>
#include <stdio.h>

// Japanese kana to phoneme mapping table
static const KanaPhonemeMap kana_phoneme_map[] = {
    // Hiragana
    {"あ", "a"}, {"い", "i"}, {"う", "u"}, {"え", "e"}, {"お", "o"},
    {"か", "k a"}, {"き", "k i"}, {"く", "k u"}, {"け", "k e"}, {"こ", "k o"},
    {"が", "g a"}, {"ぎ", "g i"}, {"ぐ", "g u"}, {"げ", "g e"}, {"ご", "g o"},
    {"さ", "s a"}, {"し", "s i"}, {"す", "s u"}, {"せ", "s e"}, {"そ", "s o"},
    {"ざ", "z a"}, {"じ", "z i"}, {"ず", "z u"}, {"ぜ", "z e"}, {"ぞ", "z o"},
    {"た", "t a"}, {"ち", "t i"}, {"つ", "t u"}, {"て", "t e"}, {"と", "t o"},
    {"だ", "d a"}, {"ぢ", "d i"}, {"づ", "d u"}, {"で", "d e"}, {"ど", "d o"},
    {"な", "n a"}, {"に", "n i"}, {"ぬ", "n u"}, {"ね", "n e"}, {"の", "n o"},
    {"は", "h a"}, {"ひ", "h i"}, {"ふ", "h u"}, {"へ", "h e"}, {"ほ", "h o"},
    {"ば", "b a"}, {"び", "b i"}, {"ぶ", "b u"}, {"べ", "b e"}, {"ぼ", "b o"},
    {"ぱ", "p a"}, {"ぴ", "p i"}, {"ぷ", "p u"}, {"ぺ", "p e"}, {"ぽ", "p o"},
    {"ま", "m a"}, {"み", "m i"}, {"む", "m u"}, {"め", "m e"}, {"も", "m o"},
    {"や", "y a"}, {"ゆ", "y u"}, {"よ", "y o"},
    {"ら", "r a"}, {"り", "r i"}, {"る", "r u"}, {"れ", "r e"}, {"ろ", "r o"},
    {"わ", "w a"}, {"ゐ", "w i"}, {"ゑ", "w e"}, {"を", "w o"}, {"ん", "N"},
    
    // Small kana
    {"ゃ", "y a"}, {"ゅ", "y u"}, {"ょ", "y o"},
    {"ぁ", "a"}, {"ぃ", "i"}, {"ぅ", "u"}, {"ぇ", "e"}, {"ぉ", "o"},
    {"っ", "cl"},  // Sokuon (geminate consonant)
    
    // Katakana
    {"ア", "a"}, {"イ", "i"}, {"ウ", "u"}, {"エ", "e"}, {"オ", "o"},
    {"カ", "k a"}, {"キ", "k i"}, {"ク", "k u"}, {"ケ", "k e"}, {"コ", "k o"},
    {"ガ", "g a"}, {"ギ", "g i"}, {"グ", "g u"}, {"ゲ", "g e"}, {"ゴ", "g o"},
    {"サ", "s a"}, {"シ", "s i"}, {"ス", "s u"}, {"セ", "s e"}, {"ソ", "s o"},
    {"ザ", "z a"}, {"ジ", "z i"}, {"ズ", "z u"}, {"ゼ", "z e"}, {"ゾ", "z o"},
    {"タ", "t a"}, {"チ", "t i"}, {"ツ", "t u"}, {"テ", "t e"}, {"ト", "t o"},
    {"ダ", "d a"}, {"ヂ", "d i"}, {"ヅ", "d u"}, {"デ", "d e"}, {"ド", "d o"},
    {"ナ", "n a"}, {"ニ", "n i"}, {"ヌ", "n u"}, {"ネ", "n e"}, {"ノ", "n o"},
    {"ハ", "h a"}, {"ヒ", "h i"}, {"フ", "h u"}, {"ヘ", "h e"}, {"ホ", "h o"},
    {"バ", "b a"}, {"ビ", "b i"}, {"ブ", "b u"}, {"ベ", "b e"}, {"ボ", "b o"},
    {"パ", "p a"}, {"ピ", "p i"}, {"プ", "p u"}, {"ペ", "p e"}, {"ポ", "p o"},
    {"マ", "m a"}, {"ミ", "m i"}, {"ム", "m u"}, {"メ", "m e"}, {"モ", "m o"},
    {"ヤ", "y a"}, {"ユ", "y u"}, {"ヨ", "y o"},
    {"ラ", "r a"}, {"リ", "r i"}, {"ル", "r u"}, {"レ", "r e"}, {"ロ", "r o"},
    {"ワ", "w a"}, {"ヰ", "w i"}, {"ヱ", "w e"}, {"ヲ", "w o"}, {"ン", "N"},
    
    // Small katakana
    {"ャ", "y a"}, {"ュ", "y u"}, {"ョ", "y o"},
    {"ァ", "a"}, {"ィ", "i"}, {"ゥ", "u"}, {"ェ", "e"}, {"ォ", "o"},
    {"ッ", "cl"},  // Sokuon
    
    // Special
    {"ー", "R"},  // Long vowel mark
    
    {NULL, NULL}
};

// Phoneme converter structure
struct PhonemeConverter {
    // Configuration
    uint32_t default_phoneme_duration;  // Default duration in ms
    bool use_accent_info;               // Whether to use accent information
};

// UTF-8 utilities
static int utf8_char_len(unsigned char c) {
    if (c < 0x80) return 1;
    if ((c & 0xE0) == 0xC0) return 2;
    if ((c & 0xF0) == 0xE0) return 3;
    if ((c & 0xF8) == 0xF0) return 4;
    return 1;
}

// Create phoneme sequence
PhonemeSequence* phoneme_sequence_create(uint32_t initial_capacity) {
    PhonemeSequence* seq = (PhonemeSequence*)calloc(1, sizeof(PhonemeSequence));
    if (!seq) return NULL;
    
    seq->capacity = initial_capacity > 0 ? initial_capacity : 64;
    seq->phonemes = (Phoneme*)calloc(seq->capacity, sizeof(Phoneme));
    if (!seq->phonemes) {
        free(seq);
        return NULL;
    }
    
    return seq;
}

// Destroy phoneme sequence
void phoneme_sequence_destroy(PhonemeSequence* seq) {
    if (!seq) return;
    free(seq->phonemes);
    free(seq);
}

// Add phoneme to sequence
bool phoneme_sequence_add(PhonemeSequence* seq, const char* phoneme, 
                          uint32_t duration_ms, uint8_t accent_type, uint8_t stress_level) {
    if (!seq || !phoneme) return false;
    
    // Resize if needed
    if (seq->count >= seq->capacity) {
        uint32_t new_capacity = seq->capacity * 2;
        Phoneme* new_phonemes = (Phoneme*)realloc(seq->phonemes, 
                                                   new_capacity * sizeof(Phoneme));
        if (!new_phonemes) return false;
        
        seq->phonemes = new_phonemes;
        seq->capacity = new_capacity;
    }
    
    // Add phoneme
    Phoneme* p = &seq->phonemes[seq->count];
    strncpy(p->phoneme, phoneme, sizeof(p->phoneme) - 1);
    p->phoneme[sizeof(p->phoneme) - 1] = '\0';
    p->duration_ms = duration_ms;
    p->accent_type = accent_type;
    p->stress_level = stress_level;
    
    seq->count++;
    return true;
}

// Get phoneme for kana
const char* get_phoneme_for_kana(const char* kana) {
    if (!kana) return NULL;
    
    for (int i = 0; kana_phoneme_map[i].kana != NULL; i++) {
        if (strcmp(kana_phoneme_map[i].kana, kana) == 0) {
            return kana_phoneme_map[i].phoneme;
        }
    }
    
    return NULL;
}

// Convert reading to phonemes
bool convert_reading_to_phonemes(const char* reading, PhonemeSequence* seq) {
    if (!reading || !seq) return false;
    
    const char* p = reading;
    while (*p) {
        int char_len = utf8_char_len((unsigned char)*p);
        
        // Extract character
        char kana[5] = {0};
        memcpy(kana, p, char_len);
        
        // Look up phoneme
        const char* phoneme = get_phoneme_for_kana(kana);
        if (phoneme) {
            // Parse phoneme string (may contain multiple phonemes)
            char phoneme_copy[32];
            strncpy(phoneme_copy, phoneme, sizeof(phoneme_copy) - 1);
            
            char* token = strtok(phoneme_copy, " ");
            while (token) {
                phoneme_sequence_add(seq, token, 50, 0, 1);  // Default duration 50ms
                token = strtok(NULL, " ");
            }
        } else {
            // Unknown character - skip
            // Could add handling for numbers, alphabets, etc.
        }
        
        p += char_len;
    }
    
    return true;
}

// Create converter
PhonemeConverter* phoneme_converter_create(void) {
    PhonemeConverter* converter = (PhonemeConverter*)calloc(1, sizeof(PhonemeConverter));
    if (!converter) return NULL;
    
    converter->default_phoneme_duration = 50;  // 50ms default
    converter->use_accent_info = true;
    
    return converter;
}

// Destroy converter
void phoneme_converter_destroy(PhonemeConverter* converter) {
    free(converter);
}

// Convert MecabFullNode to phonemes
PhonemeSequence* phoneme_converter_convert(PhonemeConverter* converter, 
                                           MecabFullNode* nodes) {
    if (!converter || !nodes) return NULL;
    
    PhonemeSequence* seq = phoneme_sequence_create(256);
    if (!seq) return NULL;
    
    // Add silence at beginning
    phoneme_sequence_add(seq, "pau", 100, 0, 0);
    
    MecabFullNode* node = nodes;
    while (node) {
        // Skip BOS/EOS nodes
        if (node->length == 0) {
            node = node->next;
            continue;
        }
        
        // Get reading from feature
        const char* reading = node->feature.pronunciation;
        if (!reading || strlen(reading) == 0) {
            reading = node->feature.reading;
        }
        
        // If still no reading, try to use surface
        if (!reading || strlen(reading) == 0 || strcmp(reading, "*") == 0) {
            // For punctuation and symbols
            if (strstr(node->feature.pos, "記号")) {
                if (strstr(node->feature.pos_detail1, "句点") || 
                    strstr(node->feature.pos_detail1, "読点")) {
                    phoneme_sequence_add(seq, "pau", 200, 0, 0);
                }
                node = node->next;
                continue;
            }
            
            // Try to use surface as reading for simple cases
            char surface[256];
            strncpy(surface, node->surface, node->length);
            surface[node->length] = '\0';
            reading = surface;
        }
        
        // Convert reading to phonemes
        if (reading && strcmp(reading, "*") != 0) {
            convert_reading_to_phonemes(reading, seq);
            
            // Add short pause between words
            if (node->next && node->next->length > 0) {
                phoneme_sequence_add(seq, "pau", 10, 0, 0);
            }
        }
        
        node = node->next;
    }
    
    // Add silence at end
    phoneme_sequence_add(seq, "pau", 100, 0, 0);
    
    return seq;
}

// Convert phoneme sequence to string
const char* phoneme_sequence_to_string(PhonemeSequence* seq, char* buffer, size_t buffer_size) {
    if (!seq || !buffer || buffer_size == 0) return NULL;
    
    buffer[0] = '\0';
    size_t pos = 0;
    
    for (uint32_t i = 0; i < seq->count; i++) {
        Phoneme* p = &seq->phonemes[i];
        int written = snprintf(buffer + pos, buffer_size - pos, "%s ", p->phoneme);
        if (written > 0 && pos + written < buffer_size) {
            pos += written;
        } else {
            break;
        }
    }
    
    // Remove trailing space
    if (pos > 0 && buffer[pos - 1] == ' ') {
        buffer[pos - 1] = '\0';
    }
    
    return buffer;
}