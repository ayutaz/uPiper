/**
 * OpenJTalk Progressive WebAssembly Implementation
 * 段階的に完全版へ移行するための実装
 * Phase 1: 拡張辞書による実装
 * Phase 2: MeCab統合（次のステップ）
 */

#include <emscripten.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>

#define TRUE 1
#define FALSE 0
#define MAX_OUTPUT_SIZE 8192

// Phase 1: 拡張辞書実装
typedef struct {
    const char* surface;
    const char* phonemes;
} DictEntry;

// 拡張された辞書（製品レベルの基本語彙）
static const DictEntry extended_dict[] = {
    // 基本的な挨拶
    {"こんにちは", "k o N n i ch i w a"},
    {"こんばんは", "k o N b a N w a"},
    {"おはよう", "o h a y o:"},
    {"おはようございます", "o h a y o: g o z a i m a s u"},
    {"ありがとう", "a r i g a t o:"},
    {"ありがとうございます", "a r i g a t o: g o z a i m a s u"},
    {"さようなら", "s a y o: n a r a"},
    {"すみません", "s u m i m a s e N"},
    {"ごめんなさい", "g o m e N n a s a i"},
    
    // 基本動詞
    {"です", "d e s u"},
    {"ます", "m a s u"},
    {"ある", "a r u"},
    {"いる", "i r u"},
    {"する", "s u r u"},
    {"なる", "n a r u"},
    {"できる", "d e k i r u"},
    {"わかる", "w a k a r u"},
    {"わかりました", "w a k a r i m a sh i t a"},
    
    // 数字
    {"一", "i ch i"},
    {"二", "n i"},
    {"三", "s a N"},
    {"四", "y o N"},
    {"五", "g o"},
    {"六", "r o k u"},
    {"七", "n a n a"},
    {"八", "h a ch i"},
    {"九", "ky u:"},
    {"十", "j u:"},
    {"百", "hy a k u"},
    {"千", "s e N"},
    {"万", "m a N"},
    
    // 日常語彙
    {"今日", "ky o:"},
    {"明日", "a sh i t a"},
    {"昨日", "k i n o:"},
    {"時間", "j i k a N"},
    {"分", "f u N"},
    {"秒", "by o:"},
    {"日本", "n i h o N"},
    {"東京", "t o: ky o:"},
    {"音声", "o N s e:"},
    {"合成", "g o: s e:"},
    {"テスト", "t e s u t o"},
    {"システム", "sh i s u t e m u"},
    {"コンピューター", "k o N py u: t a:"},
    {"プログラム", "p u r o g u r a m u"},
    
    // よく使う単語
    {"私", "w a t a sh i"},
    {"あなた", "a n a t a"},
    {"彼", "k a r e"},
    {"彼女", "k a n o j o"},
    {"人", "h i t o"},
    {"もの", "m o n o"},
    {"こと", "k o t o"},
    {"時", "t o k i"},
    {"場所", "b a sh o"},
    {"名前", "n a m a e"},
    
    {NULL, NULL}
};

// ひらがな・カタカナ→音素マッピング（完全版）
static const struct {
    const char* kana;
    const char* phoneme;
} kana_map[] = {
    // ひらがな - 基本
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
    {"わ", "w a"}, {"ゐ", "w i"}, {"ゑ", "w e"}, {"を", "w o"}, {"ん", "N"},
    
    // ひらがな - 拗音
    {"きゃ", "ky a"}, {"きゅ", "ky u"}, {"きょ", "ky o"},
    {"ぎゃ", "gy a"}, {"ぎゅ", "gy u"}, {"ぎょ", "gy o"},
    {"しゃ", "sh a"}, {"しゅ", "sh u"}, {"しょ", "sh o"},
    {"じゃ", "j a"}, {"じゅ", "j u"}, {"じょ", "j o"},
    {"ちゃ", "ch a"}, {"ちゅ", "ch u"}, {"ちょ", "ch o"},
    {"にゃ", "ny a"}, {"にゅ", "ny u"}, {"にょ", "ny o"},
    {"ひゃ", "hy a"}, {"ひゅ", "hy u"}, {"ひょ", "hy o"},
    {"びゃ", "by a"}, {"びゅ", "by u"}, {"びょ", "by o"},
    {"ぴゃ", "py a"}, {"ぴゅ", "py u"}, {"ぴょ", "py o"},
    {"みゃ", "my a"}, {"みゅ", "my u"}, {"みょ", "my o"},
    {"りゃ", "ry a"}, {"りゅ", "ry u"}, {"りょ", "ry o"},
    
    // 特殊
    {"っ", "q"}, {"ー", ":"}, {"。", "."}, {"、", ","},
    
    // カタカナ - 基本
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
    {"ワ", "w a"}, {"ヰ", "w i"}, {"ヱ", "w e"}, {"ヲ", "w o"}, {"ン", "N"},
    
    {NULL, NULL}
};

// UTF-8文字の長さを取得
static int utf8_char_len(unsigned char c) {
    if (c < 0x80) return 1;
    if ((c & 0xE0) == 0xC0) return 2;
    if ((c & 0xF0) == 0xE0) return 3;
    if ((c & 0xF8) == 0xF0) return 4;
    return 1;
}

// テキストを音素に変換（Phase 1実装）
static int text_to_phonemes_phase1(const char* text, char* output, int output_size) {
    printf("[OpenJTalk WASM Progressive] Phase 1 - Converting: %s\n", text);
    
    // まず辞書で完全一致を検索
    for (int i = 0; extended_dict[i].surface != NULL; i++) {
        if (strcmp(text, extended_dict[i].surface) == 0) {
            strncpy(output, extended_dict[i].phonemes, output_size - 1);
            output[output_size - 1] = '\0';
            printf("[OpenJTalk WASM Progressive] Dictionary match: %s\n", output);
            return strlen(output);
        }
    }
    
    // 辞書にない場合は文字単位で変換
    output[0] = '\0';
    int output_len = 0;
    const char* p = text;
    
    while (*p) {
        int char_len = utf8_char_len((unsigned char)*p);
        char current_char[5] = {0};
        strncpy(current_char, p, char_len);
        
        // 拗音の処理（2文字の組み合わせ）
        if (char_len == 3 && *(p + 3) && utf8_char_len((unsigned char)*(p + 3)) == 3) {
            char two_chars[7] = {0};
            strncpy(two_chars, p, 6);
            
            const char* phoneme = NULL;
            for (int i = 0; kana_map[i].kana != NULL; i++) {
                if (strcmp(two_chars, kana_map[i].kana) == 0) {
                    phoneme = kana_map[i].phoneme;
                    p += 6; // 2文字分進める
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
                continue;
            }
        }
        
        // 1文字の処理
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
        }
        
        p += char_len;
        
        if (output_len >= output_size - 10) {
            break;
        }
    }
    
    printf("[OpenJTalk WASM Progressive] Phonemes: %s\n", output);
    return output_len;
}

// グローバル状態
static int g_initialized = 0;
static int g_phase = 1; // 現在のフェーズ（1: 拡張辞書, 2: MeCab統合予定）

/**
 * Initialize OpenJTalk
 */
EMSCRIPTEN_KEEPALIVE
int Open_JTalk_initialize() {
    printf("[OpenJTalk WASM Progressive] Initializing Phase %d...\n", g_phase);
    g_initialized = 1;
    return 0;
}

/**
 * Load dictionary (Phase 2で実装)
 */
EMSCRIPTEN_KEEPALIVE
int Open_JTalk_load(const char* dict_path) {
    printf("[OpenJTalk WASM Progressive] Load dictionary - Phase %d\n", g_phase);
    if (g_phase == 1) {
        printf("[OpenJTalk WASM Progressive] Using embedded dictionary (Phase 1)\n");
        return 0;
    }
    // Phase 2でMeCab辞書のロードを実装
    return 0;
}

/**
 * Convert text to phonemes
 */
EMSCRIPTEN_KEEPALIVE
int Open_JTalk_synthesis(const char* text, char* output, int output_size) {
    if (!g_initialized || !text || !output || output_size <= 0) {
        printf("[OpenJTalk WASM Progressive] Invalid parameters\n");
        return -1;
    }
    
    // 前後に無音を追加
    strcpy(output, "pau ");
    int len = 4;
    
    // Phase 1の実装を使用
    char phonemes[MAX_OUTPUT_SIZE];
    int phoneme_len = text_to_phonemes_phase1(text, phonemes, sizeof(phonemes));
    
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
    printf("[OpenJTalk WASM Progressive] Clearing...\n");
    g_initialized = 0;
}

/**
 * Get current phase
 */
EMSCRIPTEN_KEEPALIVE
int get_implementation_phase() {
    return g_phase;
}

/**
 * Set implementation phase (for testing)
 */
EMSCRIPTEN_KEEPALIVE
void set_implementation_phase(int phase) {
    if (phase >= 1 && phase <= 2) {
        g_phase = phase;
        printf("[OpenJTalk WASM Progressive] Switched to Phase %d\n", g_phase);
    }
}

// Helper functions
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
    return "OpenJTalk WASM Progressive Phase 1";
}