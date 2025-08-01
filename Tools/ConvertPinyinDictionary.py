#!/usr/bin/env python3
"""
Convert pinyin dictionary from text format to JSON format
"""

import json
import os
import re
from collections import defaultdict

def parse_pinyin_dict_txt(input_file):
    """Parse tab-separated pinyin dictionary"""
    character_dict = {}
    
    with open(input_file, 'r', encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            # Skip comments and empty lines
            if not line or line.startswith('#'):
                continue
                
            parts = line.split('\t')
            if len(parts) != 2:
                continue
                
            char = parts[0]
            pinyin_list = [p.strip() for p in parts[1].split(',')]
            
            if len(char) == 1:  # Single character
                character_dict[char] = pinyin_list
    
    return character_dict

def create_character_pinyin_json(char_dict):
    """Create character to pinyin mapping JSON"""
    entries = []
    for char, pinyin_list in sorted(char_dict.items()):
        entries.append({
            "character": char,
            "pinyin": pinyin_list
        })
    return entries

def create_phrase_pinyin_json(char_dict):
    """Create basic phrase mappings from character data"""
    # Common phrases with their pinyin
    phrases = {
        "你好": "ni3 hao3",
        "中国": "zhong1 guo2",
        "中国人": "zhong1 guo2 ren2",
        "不是": "bu2 shi4",
        "好的": "hao3 de5",
        "一个": "yi1 ge4",
        "这个": "zhe4 ge4",
        "那个": "na4 ge4",
        "什么": "shen2 me5",
        "怎么": "zen3 me5",
        "我们": "wo3 men5",
        "他们": "ta1 men5",
        "大家": "da4 jia1",
        "现在": "xian4 zai4",
        "时间": "shi2 jian1",
        "今天": "jin1 tian1",
        "明天": "ming2 tian1",
        "昨天": "zuo2 tian1",
        "谢谢": "xie4 xie4",
        "对不起": "dui4 bu4 qi3",
        "没关系": "mei2 guan1 xi4",
        "再见": "zai4 jian4",
        "你好吗": "ni3 hao3 ma5",
        "很好": "hen3 hao3",
        "不错": "bu4 cuo4",
        "可以": "ke3 yi3",
        "知道": "zhi1 dao4",
        "明白": "ming2 bai2",
        "喜欢": "xi3 huan1",
        "希望": "xi1 wang4"
    }
    
    entries = []
    for phrase, pinyin in sorted(phrases.items()):
        entries.append({
            "phrase": phrase,
            "pinyin": pinyin
        })
    return entries

def create_pinyin_ipa_json():
    """Create pinyin to IPA mapping"""
    # Basic mappings based on standard Mandarin IPA
    mappings = {
        # Initials (consonants)
        "b": "p", "p": "pʰ", "m": "m", "f": "f",
        "d": "t", "t": "tʰ", "n": "n", "l": "l",
        "g": "k", "k": "kʰ", "h": "x",
        "j": "tɕ", "q": "tɕʰ", "x": "ɕ",
        "zh": "ʈʂ", "ch": "ʈʂʰ", "sh": "ʂ", "r": "ʐ",
        "z": "ts", "c": "tsʰ", "s": "s",
        
        # Finals (vowels and endings)
        "a": "a", "o": "o", "e": "ɤ", "i": "i", "u": "u", "ü": "y",
        "ai": "ai", "ei": "ei", "ui": "uei", "ao": "au", "ou": "ou",
        "iu": "iou", "ie": "iɛ", "üe": "yɛ", "er": "ɚ",
        "an": "an", "en": "ən", "in": "in", "un": "uən", "ün": "yn",
        "ang": "aŋ", "eng": "əŋ", "ing": "iŋ", "ong": "uŋ",
        
        # Complete syllables (common ones)
        "ma": "ma", "mo": "mo", "me": "mɤ", "mi": "mi", "mu": "mu",
        "fa": "fa", "fo": "fo", "fu": "fu", "fei": "fei",
        "da": "ta", "de": "tɤ", "di": "ti", "du": "tu", "dao": "tau",
        "ta": "tʰa", "te": "tʰɤ", "ti": "tʰi", "tu": "tʰu", "tao": "tʰau",
        "na": "na", "ne": "nɤ", "ni": "ni", "nu": "nu", "nao": "nau",
        "la": "la", "le": "lɤ", "li": "li", "lu": "lu", "lai": "lai",
        "ga": "ka", "ge": "kɤ", "gu": "ku", "guo": "kuo", "gai": "kai",
        "ka": "kʰa", "ke": "kʰɤ", "ku": "kʰu", "kai": "kʰai",
        "ha": "xa", "he": "xɤ", "hu": "xu", "hai": "xai", "hao": "xau",
        "ji": "tɕi", "jia": "tɕia", "jie": "tɕiɛ", "jiu": "tɕiou",
        "qi": "tɕʰi", "qia": "tɕʰia", "qie": "tɕʰiɛ", "qiu": "tɕʰiou",
        "xi": "ɕi", "xia": "ɕia", "xie": "ɕiɛ", "xiu": "ɕiou",
        "zhi": "ʈʂʅ", "chi": "ʈʂʰʅ", "shi": "ʂʅ", "ri": "ʐʅ",
        "zi": "tsɿ", "ci": "tsʰɿ", "si": "sɿ",
        "yi": "i", "ya": "ia", "ye": "iɛ", "yao": "iau", "you": "iou",
        "wu": "u", "wa": "ua", "wo": "uo", "wai": "uai", "wei": "uei",
        "yu": "y", "yue": "yɛ", "yuan": "yan",
        
        # More complete syllables
        "ba": "pa", "bei": "pei", "bao": "pau", "ben": "pən", "bang": "paŋ",
        "pa": "pʰa", "pei": "pʰei", "pao": "pʰau", "pen": "pʰən", "pang": "pʰaŋ",
        "mai": "mai", "mei": "mei", "mao": "mau", "men": "mən", "mang": "maŋ",
        "fan": "fan", "fen": "fən", "fang": "faŋ", "feng": "fəŋ",
        "dan": "tan", "deng": "təŋ", "dong": "tuŋ", "dian": "tiɛn",
        "tan": "tʰan", "teng": "tʰəŋ", "tong": "tʰuŋ", "tian": "tʰiɛn",
        "nan": "nan", "neng": "nəŋ", "nong": "nuŋ", "nian": "niɛn",
        "lan": "lan", "leng": "ləŋ", "long": "luŋ", "lian": "liɛn",
        "gan": "kan", "geng": "kəŋ", "gong": "kuŋ", "guang": "kuaŋ",
        "kan": "kʰan", "keng": "kʰəŋ", "kong": "kʰuŋ", "kuang": "kʰuaŋ",
        "han": "xan", "heng": "xəŋ", "hong": "xuŋ", "huang": "xuaŋ",
        "jian": "tɕiɛn", "jiang": "tɕiaŋ", "jing": "tɕiŋ", "jiong": "tɕyŋ",
        "qian": "tɕʰiɛn", "qiang": "tɕʰiaŋ", "qing": "tɕʰiŋ", "qiong": "tɕʰyŋ",
        "xian": "ɕiɛn", "xiang": "ɕiaŋ", "xing": "ɕiŋ", "xiong": "ɕyŋ",
        "zhang": "ʈʂaŋ", "zheng": "ʈʂəŋ", "zhong": "ʈʂuŋ", "zhuang": "ʈʂuaŋ",
        "chang": "ʈʂʰaŋ", "cheng": "ʈʂʰəŋ", "chong": "ʈʂʰuŋ", "chuang": "ʈʂʰuaŋ",
        "shang": "ʂaŋ", "sheng": "ʂəŋ", "shuang": "ʂuaŋ",
        "rang": "ʐaŋ", "reng": "ʐəŋ", "rong": "ʐuŋ",
        "zang": "tsaŋ", "zeng": "tsəŋ", "zong": "tsuŋ",
        "cang": "tsʰaŋ", "ceng": "tsʰəŋ", "cong": "tsʰuŋ",
        "sang": "saŋ", "seng": "səŋ", "song": "suŋ",
        
        # Additional syllables from sample dict
        "ni": "ni", "hao": "xau", "zhong": "ʈʂuŋ", "guo": "kuo",
        "ren": "ʐən", "wo": "uo", "shi": "ʂʅ", "bu": "pu",
        "le": "lɤ", "zai": "tsai", "you": "iou", "zhe": "ʈʂɤ",
        "ge": "kɤ", "shang": "ʂaŋ", "xia": "ɕia", "lai": "lai",
        "qu": "tɕʰy", "shuo": "ʂuo", "hua": "xua", "zuo": "tsuo",
        "tian": "tʰiɛn", "di": "ti", "xin": "ɕin", "nian": "niɛn",
        "yue": "yɛ", "ri": "ʐʅ", "xing": "ɕiŋ", "qi": "tɕʰi",
        "san": "san", "si": "sɿ", "liu": "liou", "ba": "pa",
        "jiu": "tɕiou", "shi": "ʂʅ", "bai": "pai", "qian": "tɕʰiɛn",
        "wan": "uan", "dong": "tuŋ", "xi": "ɕi", "nan": "nan",
        "bei": "pei", "zuo": "tsuo", "you": "iou", "qian": "tɕʰiɛn",
        "hou": "xou", "zuo": "tsuo", "nei": "nei", "wai": "uai"
    }
    
    entries = []
    for pinyin, ipa in sorted(mappings.items()):
        entries.append({
            "pinyin": pinyin,
            "ipa": ipa
        })
    return entries

def main():
    # Input and output paths
    script_dir = os.path.dirname(os.path.abspath(__file__))
    project_root = os.path.dirname(script_dir)
    
    input_file = os.path.join(project_root, 
        "Assets/StreamingAssets/uPiper/Languages/Chinese/pinyin_dict_sample.txt")
    output_dir = os.path.join(project_root, 
        "Assets/StreamingAssets/uPiper/Chinese")
    
    # Create output directory
    os.makedirs(output_dir, exist_ok=True)
    
    # Parse input dictionary
    print(f"Reading {input_file}...")
    char_dict = parse_pinyin_dict_txt(input_file)
    print(f"Loaded {len(char_dict)} characters")
    
    # Create character pinyin JSON
    char_entries = create_character_pinyin_json(char_dict)
    char_output = os.path.join(output_dir, "character_pinyin.json")
    with open(char_output, 'w', encoding='utf-8') as f:
        json.dump(char_entries, f, ensure_ascii=False, indent=2)
    print(f"Wrote {len(char_entries)} character entries to {char_output}")
    
    # Create phrase pinyin JSON
    phrase_entries = create_phrase_pinyin_json(char_dict)
    phrase_output = os.path.join(output_dir, "phrase_pinyin.json")
    with open(phrase_output, 'w', encoding='utf-8') as f:
        json.dump(phrase_entries, f, ensure_ascii=False, indent=2)
    print(f"Wrote {len(phrase_entries)} phrase entries to {phrase_output}")
    
    # Create IPA mapping JSON
    ipa_entries = create_pinyin_ipa_json()
    ipa_output = os.path.join(output_dir, "pinyin_ipa_map.json")
    with open(ipa_output, 'w', encoding='utf-8') as f:
        json.dump(ipa_entries, f, ensure_ascii=False, indent=2)
    print(f"Wrote {len(ipa_entries)} IPA mappings to {ipa_output}")
    
    # Create empty word frequency for now
    freq_output = os.path.join(output_dir, "word_frequency.json")
    with open(freq_output, 'w', encoding='utf-8') as f:
        json.dump([], f, indent=2)
    print(f"Created empty word frequency file at {freq_output}")
    
    print("\nConversion complete!")

if __name__ == "__main__":
    main()