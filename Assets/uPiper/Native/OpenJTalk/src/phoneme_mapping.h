#ifndef PHONEME_MAPPING_H
#define PHONEME_MAPPING_H

#include "openjtalk_phonemizer.h"
#include <string.h>

// Phoneme mapping table (pyopenjtalk compatible)
typedef struct {
    const char* phoneme_str;
    PhonemeID id;
    const char* ipa;        // IPA representation
    const char* examples;   // Example words
} PhonemeMapping;

// Complete phoneme mapping table
static const PhonemeMapping phoneme_table[] = {
    // Special
    {"pau", PHONEME_PAU, "_", "pause"},
    {"sil", PHONEME_SIL, "_", "silence"},
    
    // Vowels
    {"a", PHONEME_A, "a", "あ、か、さ"},
    {"i", PHONEME_I, "i", "い、き、し"},
    {"u", PHONEME_U, "ɯ", "う、く、す"},
    {"e", PHONEME_E, "e", "え、け、せ"},
    {"o", PHONEME_O, "o", "お、こ、そ"},
    
    // Consonants - K行
    {"k", PHONEME_K, "k", "か、き、く、け、こ"},
    {"g", PHONEME_G, "ɡ", "が、ぎ、ぐ、げ、ご"},
    
    // S行
    {"s", PHONEME_S, "s", "さ、す、せ、そ"},
    {"sh", PHONEME_SH, "ɕ", "し、しゃ、しゅ、しょ"},
    {"z", PHONEME_Z, "z", "ざ、ず、ぜ、ぞ"},
    {"j", PHONEME_J, "ʑ", "じ、じゃ、じゅ、じょ"},
    
    // T行
    {"t", PHONEME_T, "t", "た、て、と"},
    {"ch", PHONEME_CH, "tɕ", "ち、ちゃ、ちゅ、ちょ"},
    {"ts", PHONEME_TS, "ts", "つ、つぁ、つぃ、つぇ、つぉ"},
    {"d", PHONEME_D, "d", "だ、で、ど"},
    
    // N行
    {"n", PHONEME_N, "n", "な、に、ぬ、ね、の"},
    
    // H行
    {"h", PHONEME_H, "h", "は、ひ、へ、ほ"},
    {"f", PHONEME_F, "ɸ", "ふ"},
    {"b", PHONEME_B, "b", "ば、び、ぶ、べ、ぼ"},
    {"p", PHONEME_P, "p", "ぱ、ぴ、ぷ、ぺ、ぽ"},
    
    // M行
    {"m", PHONEME_M, "m", "ま、み、む、め、も"},
    
    // Y行
    {"y", PHONEME_Y, "j", "や、ゆ、よ"},
    
    // R行
    {"r", PHONEME_R, "ɾ", "ら、り、る、れ、ろ"},
    
    // W行
    {"w", PHONEME_W, "w", "わ、を"},
    
    // 拗音
    {"ny", PHONEME_NY, "ɲ", "にゃ、にゅ、にょ"},
    {"hy", PHONEME_HY, "ç", "ひゃ、ひゅ、ひょ"},
    {"my", PHONEME_MY, "mʲ", "みゃ、みゅ、みょ"},
    {"ry", PHONEME_RY, "ɾʲ", "りゃ、りゅ、りょ"},
    {"gy", PHONEME_GY, "ɡʲ", "ぎゃ、ぎゅ、ぎょ"},
    {"by", PHONEME_BY, "bʲ", "びゃ、びゅ、びょ"},
    {"py", PHONEME_PY, "pʲ", "ぴゃ、ぴゅ、ぴょ"},
    
    // Special
    {"cl", PHONEME_CL, "ʔ", "っ（促音）"},
    {"v", PHONEME_V, "v", "ヴァ、ヴィ、ヴ、ヴェ、ヴォ"},
    {"q", PHONEME_Q, "ʔ", "glottal stop"},
    {"dy", PHONEME_DY, "dʲ", "でぃ"},
    {"ty", PHONEME_TY, "tʲ", "てぃ"},
    
    {NULL, PHONEME_COUNT, NULL, NULL}
};

// Mora to phoneme conversion rules
typedef struct {
    const char* mora;           // Input mora (hiragana/katakana)
    const char* phonemes;       // Output phonemes (space separated)
    const char* context_before; // Required context before (optional)
    const char* context_after;  // Required context after (optional)
} MoraRule;

// Basic mora conversion rules
static const MoraRule mora_rules[] = {
    // Basic vowels
    {"あ", "a", NULL, NULL},
    {"い", "i", NULL, NULL},
    {"う", "u", NULL, NULL},
    {"え", "e", NULL, NULL},
    {"お", "o", NULL, NULL},
    
    // K行
    {"か", "k a", NULL, NULL},
    {"き", "k i", NULL, NULL},
    {"く", "k u", NULL, NULL},
    {"け", "k e", NULL, NULL},
    {"こ", "k o", NULL, NULL},
    
    // G行
    {"が", "g a", NULL, NULL},
    {"ぎ", "g i", NULL, NULL},
    {"ぐ", "g u", NULL, NULL},
    {"げ", "g e", NULL, NULL},
    {"ご", "g o", NULL, NULL},
    
    // S行
    {"さ", "s a", NULL, NULL},
    {"し", "sh i", NULL, NULL},
    {"す", "s u", NULL, NULL},
    {"せ", "s e", NULL, NULL},
    {"そ", "s o", NULL, NULL},
    
    // Z行
    {"ざ", "z a", NULL, NULL},
    {"じ", "j i", NULL, NULL},
    {"ず", "z u", NULL, NULL},
    {"ぜ", "z e", NULL, NULL},
    {"ぞ", "z o", NULL, NULL},
    
    // T行
    {"た", "t a", NULL, NULL},
    {"ち", "ch i", NULL, NULL},
    {"つ", "ts u", NULL, NULL},
    {"て", "t e", NULL, NULL},
    {"と", "t o", NULL, NULL},
    
    // D行
    {"だ", "d a", NULL, NULL},
    {"ぢ", "j i", NULL, NULL},  // Same as じ
    {"づ", "z u", NULL, NULL},  // Same as ず
    {"で", "d e", NULL, NULL},
    {"ど", "d o", NULL, NULL},
    
    // N行
    {"な", "n a", NULL, NULL},
    {"に", "n i", NULL, NULL},
    {"ぬ", "n u", NULL, NULL},
    {"ね", "n e", NULL, NULL},
    {"の", "n o", NULL, NULL},
    
    // H行
    {"は", "h a", NULL, NULL},
    {"ひ", "h i", NULL, NULL},
    {"ふ", "f u", NULL, NULL},
    {"へ", "h e", NULL, NULL},
    {"ほ", "h o", NULL, NULL},
    
    // B行
    {"ば", "b a", NULL, NULL},
    {"び", "b i", NULL, NULL},
    {"ぶ", "b u", NULL, NULL},
    {"べ", "b e", NULL, NULL},
    {"ぼ", "b o", NULL, NULL},
    
    // P行
    {"ぱ", "p a", NULL, NULL},
    {"ぴ", "p i", NULL, NULL},
    {"ぷ", "p u", NULL, NULL},
    {"ぺ", "p e", NULL, NULL},
    {"ぽ", "p o", NULL, NULL},
    
    // M行
    {"ま", "m a", NULL, NULL},
    {"み", "m i", NULL, NULL},
    {"む", "m u", NULL, NULL},
    {"め", "m e", NULL, NULL},
    {"も", "m o", NULL, NULL},
    
    // Y行
    {"や", "y a", NULL, NULL},
    {"ゆ", "y u", NULL, NULL},
    {"よ", "y o", NULL, NULL},
    
    // R行
    {"ら", "r a", NULL, NULL},
    {"り", "r i", NULL, NULL},
    {"る", "r u", NULL, NULL},
    {"れ", "r e", NULL, NULL},
    {"ろ", "r o", NULL, NULL},
    
    // W行
    {"わ", "w a", NULL, NULL},
    {"を", "o", NULL, NULL},  // を is pronounced as o
    {"ん", "n", NULL, NULL},  // Special case
    
    // 拗音
    {"きゃ", "ky a", NULL, NULL},
    {"きゅ", "ky u", NULL, NULL},
    {"きょ", "ky o", NULL, NULL},
    {"しゃ", "sh a", NULL, NULL},
    {"しゅ", "sh u", NULL, NULL},
    {"しょ", "sh o", NULL, NULL},
    {"ちゃ", "ch a", NULL, NULL},
    {"ちゅ", "ch u", NULL, NULL},
    {"ちょ", "ch o", NULL, NULL},
    {"にゃ", "ny a", NULL, NULL},
    {"にゅ", "ny u", NULL, NULL},
    {"にょ", "ny o", NULL, NULL},
    {"ひゃ", "hy a", NULL, NULL},
    {"ひゅ", "hy u", NULL, NULL},
    {"ひょ", "hy o", NULL, NULL},
    {"みゃ", "my a", NULL, NULL},
    {"みゅ", "my u", NULL, NULL},
    {"みょ", "my o", NULL, NULL},
    {"りゃ", "ry a", NULL, NULL},
    {"りゅ", "ry u", NULL, NULL},
    {"りょ", "ry o", NULL, NULL},
    {"ぎゃ", "gy a", NULL, NULL},
    {"ぎゅ", "gy u", NULL, NULL},
    {"ぎょ", "gy o", NULL, NULL},
    {"じゃ", "j a", NULL, NULL},
    {"じゅ", "j u", NULL, NULL},
    {"じょ", "j o", NULL, NULL},
    {"びゃ", "by a", NULL, NULL},
    {"びゅ", "by u", NULL, NULL},
    {"びょ", "by o", NULL, NULL},
    {"ぴゃ", "py a", NULL, NULL},
    {"ぴゅ", "py u", NULL, NULL},
    {"ぴょ", "py o", NULL, NULL},
    
    // Special
    {"っ", "cl", NULL, NULL},  // Sokuon
    {"ー", ":", NULL, NULL},   // Long vowel mark
    
    // Katakana - Full set
    {"ア", "a", NULL, NULL},
    {"イ", "i", NULL, NULL},
    {"ウ", "u", NULL, NULL},
    {"エ", "e", NULL, NULL},
    {"オ", "o", NULL, NULL},
    
    {"カ", "k a", NULL, NULL},
    {"キ", "k i", NULL, NULL},
    {"ク", "k u", NULL, NULL},
    {"ケ", "k e", NULL, NULL},
    {"コ", "k o", NULL, NULL},
    
    {"ガ", "g a", NULL, NULL},
    {"ギ", "g i", NULL, NULL},
    {"グ", "g u", NULL, NULL},
    {"ゲ", "g e", NULL, NULL},
    {"ゴ", "g o", NULL, NULL},
    
    {"サ", "s a", NULL, NULL},
    {"シ", "sh i", NULL, NULL},
    {"ス", "s u", NULL, NULL},
    {"セ", "s e", NULL, NULL},
    {"ソ", "s o", NULL, NULL},
    
    {"ザ", "z a", NULL, NULL},
    {"ジ", "j i", NULL, NULL},
    {"ズ", "z u", NULL, NULL},
    {"ゼ", "z e", NULL, NULL},
    {"ゾ", "z o", NULL, NULL},
    
    {"タ", "t a", NULL, NULL},
    {"チ", "ch i", NULL, NULL},
    {"ツ", "ts u", NULL, NULL},
    {"テ", "t e", NULL, NULL},
    {"ト", "t o", NULL, NULL},
    
    {"ダ", "d a", NULL, NULL},
    {"ヂ", "j i", NULL, NULL},
    {"ヅ", "z u", NULL, NULL},
    {"デ", "d e", NULL, NULL},
    {"ド", "d o", NULL, NULL},
    
    {"ナ", "n a", NULL, NULL},
    {"ニ", "n i", NULL, NULL},
    {"ヌ", "n u", NULL, NULL},
    {"ネ", "n e", NULL, NULL},
    {"ノ", "n o", NULL, NULL},
    
    {"ハ", "h a", NULL, NULL},
    {"ヒ", "h i", NULL, NULL},
    {"フ", "f u", NULL, NULL},
    {"ヘ", "h e", NULL, NULL},
    {"ホ", "h o", NULL, NULL},
    
    {"バ", "b a", NULL, NULL},
    {"ビ", "b i", NULL, NULL},
    {"ブ", "b u", NULL, NULL},
    {"ベ", "b e", NULL, NULL},
    {"ボ", "b o", NULL, NULL},
    
    {"パ", "p a", NULL, NULL},
    {"ピ", "p i", NULL, NULL},
    {"プ", "p u", NULL, NULL},
    {"ペ", "p e", NULL, NULL},
    {"ポ", "p o", NULL, NULL},
    
    {"マ", "m a", NULL, NULL},
    {"ミ", "m i", NULL, NULL},
    {"ム", "m u", NULL, NULL},
    {"メ", "m e", NULL, NULL},
    {"モ", "m o", NULL, NULL},
    
    {"ヤ", "y a", NULL, NULL},
    {"ユ", "y u", NULL, NULL},
    {"ヨ", "y o", NULL, NULL},
    
    {"ラ", "r a", NULL, NULL},
    {"リ", "r i", NULL, NULL},
    {"ル", "r u", NULL, NULL},
    {"レ", "r e", NULL, NULL},
    {"ロ", "r o", NULL, NULL},
    
    {"ワ", "w a", NULL, NULL},
    {"ヲ", "o", NULL, NULL},
    {"ン", "n", NULL, NULL},
    {"ッ", "cl", NULL, NULL},
    {"ー", ":", NULL, NULL},
    
    // Foreign sounds
    {"ヴァ", "v a", NULL, NULL},
    {"ヴィ", "v i", NULL, NULL},
    {"ヴ", "v u", NULL, NULL},
    {"ヴェ", "v e", NULL, NULL},
    {"ヴォ", "v o", NULL, NULL},
    {"ティ", "ty i", NULL, NULL},
    {"ディ", "dy i", NULL, NULL},
    
    {NULL, NULL, NULL, NULL}
};

// Utility functions
static inline PhonemeID phoneme_str_to_id(const char* str) {
    for (int i = 0; phoneme_table[i].phoneme_str != NULL; i++) {
        if (strcmp(phoneme_table[i].phoneme_str, str) == 0) {
            return phoneme_table[i].id;
        }
    }
    return PHONEME_PAU;  // Default to pause
}

static inline const char* phoneme_id_to_str(PhonemeID id) {
    if (id >= 0 && id < PHONEME_COUNT) {
        return phoneme_table[id].phoneme_str;
    }
    return "pau";
}

#endif // PHONEME_MAPPING_H