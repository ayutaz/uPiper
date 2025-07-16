#ifndef MECAB_DICT_MINIMAL_H
#define MECAB_DICT_MINIMAL_H

#include "mecab_light_impl.h"

// Minimal dictionary data for Japanese TTS
// This is a highly compressed subset focused on phonemization

// Common words with readings (most frequent ~1000 words)
typedef struct {
    const char* surface;
    const char* reading;
    PosID pos;
    uint16_t left_id;
    uint16_t right_id;
    uint16_t cost;
} MinimalDictEntry;

// Minimal dictionary entries (expandable)
static const MinimalDictEntry minimal_dict[] = {
    // Particles (助詞)
    {"は", "は", POS_PARTICLE, 100, 100, 100},
    {"が", "が", POS_PARTICLE, 100, 100, 100},
    {"を", "を", POS_PARTICLE, 100, 100, 100},
    {"に", "に", POS_PARTICLE, 100, 100, 100},
    {"で", "で", POS_PARTICLE, 100, 100, 100},
    {"と", "と", POS_PARTICLE, 100, 100, 100},
    {"の", "の", POS_PARTICLE, 100, 100, 100},
    {"から", "から", POS_PARTICLE, 100, 100, 100},
    {"まで", "まで", POS_PARTICLE, 100, 100, 100},
    {"へ", "へ", POS_PARTICLE, 100, 100, 100},
    {"より", "より", POS_PARTICLE, 100, 100, 100},
    
    // Common nouns with readings
    {"今日", "きょう", POS_NOUN, 200, 200, 50},
    {"明日", "あした", POS_NOUN, 200, 200, 50},
    {"昨日", "きのう", POS_NOUN, 200, 200, 50},
    {"今", "いま", POS_NOUN, 200, 200, 100},
    {"人", "ひと", POS_NOUN, 200, 200, 100},
    {"時", "とき", POS_NOUN, 200, 200, 100},
    {"事", "こと", POS_NOUN, 200, 200, 100},
    {"物", "もの", POS_NOUN, 200, 200, 100},
    {"所", "ところ", POS_NOUN, 200, 200, 100},
    {"方", "かた", POS_NOUN, 200, 200, 100},
    {"日本", "にほん", POS_NOUN, 200, 200, 50},
    {"日本語", "にほんご", POS_NOUN, 200, 200, 50},
    {"世界", "せかい", POS_NOUN, 200, 200, 100},
    {"時間", "じかん", POS_NOUN, 200, 200, 100},
    {"場所", "ばしょ", POS_NOUN, 200, 200, 100},
    {"会社", "かいしゃ", POS_NOUN, 200, 200, 100},
    {"学校", "がっこう", POS_NOUN, 200, 200, 100},
    {"先生", "せんせい", POS_NOUN, 200, 200, 100},
    {"学生", "がくせい", POS_NOUN, 200, 200, 100},
    {"友達", "ともだち", POS_NOUN, 200, 200, 100},
    {"家族", "かぞく", POS_NOUN, 200, 200, 100},
    {"仕事", "しごと", POS_NOUN, 200, 200, 100},
    {"問題", "もんだい", POS_NOUN, 200, 200, 100},
    {"質問", "しつもん", POS_NOUN, 200, 200, 100},
    {"答え", "こたえ", POS_NOUN, 200, 200, 100},
    {"電話", "でんわ", POS_NOUN, 200, 200, 100},
    {"電車", "でんしゃ", POS_NOUN, 200, 200, 100},
    {"自動車", "じどうしゃ", POS_NOUN, 200, 200, 100},
    {"飛行機", "ひこうき", POS_NOUN, 200, 200, 100},
    {"新聞", "しんぶん", POS_NOUN, 200, 200, 100},
    {"音楽", "おんがく", POS_NOUN, 200, 200, 100},
    {"映画", "えいが", POS_NOUN, 200, 200, 100},
    {"写真", "しゃしん", POS_NOUN, 200, 200, 100},
    {"料理", "りょうり", POS_NOUN, 200, 200, 100},
    {"天気", "てんき", POS_NOUN, 200, 200, 100},
    {"元気", "げんき", POS_NOUN, 200, 200, 100},
    
    // Common verbs (dictionary form)
    {"する", "する", POS_VERB, 300, 300, 100},
    {"ある", "ある", POS_VERB, 300, 300, 100},
    {"いる", "いる", POS_VERB, 300, 300, 100},
    {"なる", "なる", POS_VERB, 300, 300, 100},
    {"言う", "いう", POS_VERB, 300, 300, 100},
    {"思う", "おもう", POS_VERB, 300, 300, 100},
    {"見る", "みる", POS_VERB, 300, 300, 100},
    {"聞く", "きく", POS_VERB, 300, 300, 100},
    {"行く", "いく", POS_VERB, 300, 300, 100},
    {"来る", "くる", POS_VERB, 300, 300, 100},
    {"帰る", "かえる", POS_VERB, 300, 300, 100},
    {"出る", "でる", POS_VERB, 300, 300, 100},
    {"入る", "はいる", POS_VERB, 300, 300, 100},
    {"作る", "つくる", POS_VERB, 300, 300, 100},
    {"使う", "つかう", POS_VERB, 300, 300, 100},
    {"取る", "とる", POS_VERB, 300, 300, 100},
    {"持つ", "もつ", POS_VERB, 300, 300, 100},
    {"書く", "かく", POS_VERB, 300, 300, 100},
    {"読む", "よむ", POS_VERB, 300, 300, 100},
    {"話す", "はなす", POS_VERB, 300, 300, 100},
    {"教える", "おしえる", POS_VERB, 300, 300, 100},
    {"学ぶ", "まなぶ", POS_VERB, 300, 300, 100},
    {"分かる", "わかる", POS_VERB, 300, 300, 100},
    {"知る", "しる", POS_VERB, 300, 300, 100},
    {"考える", "かんがえる", POS_VERB, 300, 300, 100},
    {"食べる", "たべる", POS_VERB, 300, 300, 100},
    {"飲む", "のむ", POS_VERB, 300, 300, 100},
    {"歩く", "あるく", POS_VERB, 300, 300, 100},
    {"走る", "はしる", POS_VERB, 300, 300, 100},
    {"立つ", "たつ", POS_VERB, 300, 300, 100},
    {"座る", "すわる", POS_VERB, 300, 300, 100},
    {"寝る", "ねる", POS_VERB, 300, 300, 100},
    {"起きる", "おきる", POS_VERB, 300, 300, 100},
    {"始まる", "はじまる", POS_VERB, 300, 300, 100},
    {"終わる", "おわる", POS_VERB, 300, 300, 100},
    {"開ける", "あける", POS_VERB, 300, 300, 100},
    {"閉める", "しめる", POS_VERB, 300, 300, 100},
    {"待つ", "まつ", POS_VERB, 300, 300, 100},
    {"会う", "あう", POS_VERB, 300, 300, 100},
    {"住む", "すむ", POS_VERB, 300, 300, 100},
    {"働く", "はたらく", POS_VERB, 300, 300, 100},
    {"休む", "やすむ", POS_VERB, 300, 300, 100},
    {"遊ぶ", "あそぶ", POS_VERB, 300, 300, 100},
    {"買う", "かう", POS_VERB, 300, 300, 100},
    {"売る", "うる", POS_VERB, 300, 300, 100},
    {"払う", "はらう", POS_VERB, 300, 300, 100},
    {"借りる", "かりる", POS_VERB, 300, 300, 100},
    {"貸す", "かす", POS_VERB, 300, 300, 100},
    
    // Common adjectives (i-adjectives)
    {"大きい", "おおきい", POS_ADJECTIVE, 400, 400, 100},
    {"小さい", "ちいさい", POS_ADJECTIVE, 400, 400, 100},
    {"新しい", "あたらしい", POS_ADJECTIVE, 400, 400, 100},
    {"古い", "ふるい", POS_ADJECTIVE, 400, 400, 100},
    {"良い", "よい", POS_ADJECTIVE, 400, 400, 100},
    {"悪い", "わるい", POS_ADJECTIVE, 400, 400, 100},
    {"高い", "たかい", POS_ADJECTIVE, 400, 400, 100},
    {"安い", "やすい", POS_ADJECTIVE, 400, 400, 100},
    {"長い", "ながい", POS_ADJECTIVE, 400, 400, 100},
    {"短い", "みじかい", POS_ADJECTIVE, 400, 400, 100},
    {"広い", "ひろい", POS_ADJECTIVE, 400, 400, 100},
    {"狭い", "せまい", POS_ADJECTIVE, 400, 400, 100},
    {"早い", "はやい", POS_ADJECTIVE, 400, 400, 100},
    {"遅い", "おそい", POS_ADJECTIVE, 400, 400, 100},
    {"多い", "おおい", POS_ADJECTIVE, 400, 400, 100},
    {"少ない", "すくない", POS_ADJECTIVE, 400, 400, 100},
    {"若い", "わかい", POS_ADJECTIVE, 400, 400, 100},
    {"美しい", "うつくしい", POS_ADJECTIVE, 400, 400, 100},
    {"楽しい", "たのしい", POS_ADJECTIVE, 400, 400, 100},
    {"難しい", "むずかしい", POS_ADJECTIVE, 400, 400, 100},
    {"易しい", "やさしい", POS_ADJECTIVE, 400, 400, 100},
    {"面白い", "おもしろい", POS_ADJECTIVE, 400, 400, 100},
    {"忙しい", "いそがしい", POS_ADJECTIVE, 400, 400, 100},
    {"暑い", "あつい", POS_ADJECTIVE, 400, 400, 100},
    {"寒い", "さむい", POS_ADJECTIVE, 400, 400, 100},
    {"熱い", "あつい", POS_ADJECTIVE, 400, 400, 100},
    {"冷たい", "つめたい", POS_ADJECTIVE, 400, 400, 100},
    {"嬉しい", "うれしい", POS_ADJECTIVE, 400, 400, 100},
    {"悲しい", "かなしい", POS_ADJECTIVE, 400, 400, 100},
    {"怖い", "こわい", POS_ADJECTIVE, 400, 400, 100},
    
    // Numbers
    {"一", "いち", POS_NOUN, 200, 200, 100},
    {"二", "に", POS_NOUN, 200, 200, 100},
    {"三", "さん", POS_NOUN, 200, 200, 100},
    {"四", "よん", POS_NOUN, 200, 200, 100},
    {"五", "ご", POS_NOUN, 200, 200, 100},
    {"六", "ろく", POS_NOUN, 200, 200, 100},
    {"七", "なな", POS_NOUN, 200, 200, 100},
    {"八", "はち", POS_NOUN, 200, 200, 100},
    {"九", "きゅう", POS_NOUN, 200, 200, 100},
    {"十", "じゅう", POS_NOUN, 200, 200, 100},
    {"百", "ひゃく", POS_NOUN, 200, 200, 100},
    {"千", "せん", POS_NOUN, 200, 200, 100},
    {"万", "まん", POS_NOUN, 200, 200, 100},
    
    // Auxiliary verbs
    {"です", "です", POS_AUXILIARY_VERB, 500, 500, 50},
    {"ます", "ます", POS_AUXILIARY_VERB, 500, 500, 50},
    {"ません", "ません", POS_AUXILIARY_VERB, 500, 500, 50},
    {"ました", "ました", POS_AUXILIARY_VERB, 500, 500, 50},
    {"でした", "でした", POS_AUXILIARY_VERB, 500, 500, 50},
    {"だ", "だ", POS_AUXILIARY_VERB, 500, 500, 100},
    {"である", "である", POS_AUXILIARY_VERB, 500, 500, 100},
    {"ない", "ない", POS_AUXILIARY_VERB, 500, 500, 100},
    {"たい", "たい", POS_AUXILIARY_VERB, 500, 500, 100},
    {"れる", "れる", POS_AUXILIARY_VERB, 500, 500, 100},
    {"られる", "られる", POS_AUXILIARY_VERB, 500, 500, 100},
    {"せる", "せる", POS_AUXILIARY_VERB, 500, 500, 100},
    {"させる", "させる", POS_AUXILIARY_VERB, 500, 500, 100},
    
    // Conjunctions
    {"そして", "そして", POS_CONJUNCTION, 600, 600, 100},
    {"しかし", "しかし", POS_CONJUNCTION, 600, 600, 100},
    {"でも", "でも", POS_CONJUNCTION, 600, 600, 100},
    {"それから", "それから", POS_CONJUNCTION, 600, 600, 100},
    {"だから", "だから", POS_CONJUNCTION, 600, 600, 100},
    {"または", "または", POS_CONJUNCTION, 600, 600, 100},
    
    // End marker
    {NULL, NULL, POS_OTHER, 0, 0, 0}
};

// Simple connection cost matrix (simplified)
static const int16_t connection_matrix[10][10] = {
    //   BOS  EOS  NOUN VERB ADJ  PART AUX  CONJ PRE  SUF
    {    0,   0,   100, 100, 100, 100, 100, 100, 100, 100}, // BOS
    { 9999,   0,  9999,9999,9999,9999,9999,9999,9999,9999}, // EOS
    {  100, 100,   200, 300, 200, 100, 100, 200, 500, 100}, // NOUN
    {  100, 100,   200, 300, 200, 100, 100, 200, 500, 200}, // VERB
    {  100, 100,   200, 300, 200, 100, 100, 200, 500, 200}, // ADJ
    {  100, 500,   100, 100, 100, 300, 200, 100, 500, 500}, // PART
    {  100, 100,   300, 500, 300, 100, 200, 200, 500, 500}, // AUX
    {  100, 500,   100, 100, 100, 200, 200, 300, 100, 500}, // CONJ
    { 9999,9999,   100, 100, 100,9999,9999,9999, 300,9999}, // PRE
    {  100, 100,   300, 300, 300, 100, 100, 200,9999, 300}, // SUF
};

#endif // MECAB_DICT_MINIMAL_H