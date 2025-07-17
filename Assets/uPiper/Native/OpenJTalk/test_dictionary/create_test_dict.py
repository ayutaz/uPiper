#!/usr/bin/env python3
"""
Create minimal test dictionary files for OpenJTalk tests.
"""

import struct
import os

def create_test_dictionary():
    """Create minimal dictionary files for testing."""
    
    # Create directory if it doesn't exist
    os.makedirs("test_dictionary", exist_ok=True)
    
    # Minimal dictionary entries for testing
    test_words = [
        # surface, left_id, right_id, cost, pos, pos_detail1, pos_detail2, pos_detail3, 
        # conj_form, conj_type, base_form, reading, pronunciation
        ("今日", 1, 1, -1000, "名詞", "副詞可能", "*", "*", "*", "*", "今日", "キョウ", "キョー"),
        ("は", 2, 2, -500, "助詞", "係助詞", "*", "*", "*", "*", "は", "ハ", "ワ"),
        ("良い", 3, 3, -800, "形容詞", "自立", "*", "*", "形容詞・イ段", "基本形", "良い", "ヨイ", "ヨイ"),
        ("天気", 1, 1, -900, "名詞", "一般", "*", "*", "*", "*", "天気", "テンキ", "テンキ"),
        ("です", 4, 4, -700, "助動詞", "*", "*", "*", "特殊・デス", "基本形", "です", "デス", "デス"),
        ("日本", 1, 1, -1200, "名詞", "固有名詞", "地域", "国", "*", "*", "日本", "ニホン", "ニホン"),
        ("語", 1, 1, -800, "名詞", "一般", "*", "*", "*", "*", "語", "ゴ", "ゴ"),
        ("日本語", 1, 1, -1500, "名詞", "一般", "*", "*", "*", "*", "日本語", "ニホンゴ", "ニホンゴ"),
        ("。", 5, 5, -100, "記号", "句点", "*", "*", "*", "*", "。", "。", "。"),
        ("、", 5, 5, -100, "記号", "読点", "*", "*", "*", "*", "、", "、", "、"),
    ]
    
    # Create sys.dic (system dictionary)
    with open("sys.dic", "wb") as f:
        # Header
        header = struct.pack("<IIIIIIIIII32s",
            0xE954A1B6,  # magic
            1,           # version
            0,           # type (system)
            len(test_words),  # lexsize
            6,           # lsize (left context size)
            6,           # rsize (right context size)
            1024,        # dsize (Darts size - dummy)
            len(test_words) * 16,  # tsize (token size)
            1024,        # fsize (feature size)
            0,           # reserved
            b"UTF-8" + b"\0" * 26  # charset
        )
        f.write(header)
        
        # Darts data (dummy - just zeros)
        f.write(b"\0" * 1024)
        
        # Tokens
        for i, word in enumerate(test_words):
            token = struct.pack("<HHHhII",
                word[1],     # lcAttr
                word[2],     # rcAttr
                i,           # posid
                word[3],     # wcost
                i * 100,     # feature offset
                0            # compound
            )
            f.write(token)
        
        # Features
        for word in test_words:
            feature = ",".join([word[4], word[5], word[6], word[7], word[8], 
                               word[9], word[10], word[11], word[12]])
            f.write(feature.encode("utf-8") + b"\0")
            # Pad to align
            padding = 100 - len(feature) - 1
            if padding > 0:
                f.write(b"\0" * padding)
    
    # Create unk.dic (unknown word dictionary)
    with open("unk.dic", "wb") as f:
        # Header
        header = struct.pack("<IIIIIIIIII32s",
            0xEF71994D,  # magic (unknown dict)
            1,           # version
            2,           # type (unknown)
            3,           # lexsize (3 templates)
            6,           # lsize
            6,           # rsize
            512,         # dsize (Darts size - dummy)
            3 * 16,      # tsize
            512,         # fsize
            0,           # reserved
            b"UTF-8" + b"\0" * 26  # charset
        )
        f.write(header)
        
        # Darts data (dummy)
        f.write(b"\0" * 512)
        
        # Unknown word templates
        unk_templates = [
            (1, 1, -2000, "名詞", "一般", "*", "*", "*", "*"),
            (3, 3, -3000, "動詞", "自立", "*", "*", "五段・ラ行", "基本形"),
            (5, 5, -1000, "記号", "一般", "*", "*", "*", "*"),
        ]
        
        # Tokens
        for i, template in enumerate(unk_templates):
            token = struct.pack("<HHHhII",
                template[0],  # lcAttr
                template[1],  # rcAttr
                100 + i,      # posid
                template[2],  # wcost
                i * 64,       # feature offset
                0             # compound
            )
            f.write(token)
        
        # Features
        for template in unk_templates:
            feature = ",".join(template[3:])
            f.write(feature.encode("utf-8") + b"\0")
            # Pad to align
            padding = 64 - len(feature) - 1
            if padding > 0:
                f.write(b"\0" * padding)
    
    # Create matrix.bin (connection cost matrix)
    with open("matrix.bin", "wb") as f:
        lsize = 6
        rsize = 6
        f.write(struct.pack("<HH", lsize, rsize))
        
        # Simple connection costs
        for l in range(lsize):
            for r in range(rsize):
                if l == r:
                    cost = -100  # Same POS connects well
                else:
                    cost = 100   # Different POS has penalty
                f.write(struct.pack("<h", cost))
    
    # Create char.bin (character type definitions) 
    with open("char.bin", "wb") as f:
        # Number of categories
        f.write(struct.pack("<I", 11))
        
        # Category names (32 bytes each)
        categories = [
            "DEFAULT", "SPACE", "KANJI", "SYMBOL", "NUMERIC",
            "ALPHA", "HIRAGANA", "KATAKANA", "KANJINUMERIC",
            "GREEK", "CYRILLIC"
        ]
        
        for cat in categories:
            f.write(cat.encode("ascii").ljust(32, b"\0"))
        
        # Character mappings (simplified - only BMP)
        # For each Unicode codepoint 0-65534, write category info
        for i in range(65535):
            if 0x3040 <= i <= 0x309F:
                category = 1 << 6  # HIRAGANA
            elif 0x30A0 <= i <= 0x30FF:
                category = 1 << 7  # KATAKANA
            elif 0x4E00 <= i <= 0x9FFF:
                category = 1 << 2  # KANJI
            elif 0x30 <= i <= 0x39:
                category = 1 << 4  # NUMERIC
            elif (0x41 <= i <= 0x5A) or (0x61 <= i <= 0x7A):
                category = 1 << 5  # ALPHA
            elif i == 0x20:
                category = 1 << 1  # SPACE
            elif i in [0x3001, 0x3002]:  # 、。
                category = 1 << 3  # SYMBOL
            else:
                category = 1 << 0  # DEFAULT
            
            # Pack as CharInfo (simplified)
            char_info = category | (0 << 14)  # type bits in lower 8, other fields 0
            f.write(struct.pack("<I", char_info))
    
    # Create definition files
    with open("left-id.def", "w", encoding="utf-8") as f:
        f.write("0 BOS/EOS,*,*,*,*,*,*,*,*\n")
        f.write("1 名詞,*,*,*,*,*,*,*,*\n")
        f.write("2 助詞,*,*,*,*,*,*,*,*\n")
        f.write("3 形容詞,*,*,*,*,*,*,*,*\n")
        f.write("4 助動詞,*,*,*,*,*,*,*,*\n")
        f.write("5 記号,*,*,*,*,*,*,*,*\n")
    
    with open("right-id.def", "w", encoding="utf-8") as f:
        f.write("0 BOS/EOS,*,*,*,*,*,*,*,*\n")
        f.write("1 名詞,*,*,*,*,*,*,*,*\n")
        f.write("2 助詞,*,*,*,*,*,*,*,*\n")
        f.write("3 形容詞,*,*,*,*,*,*,*,*\n")
        f.write("4 助動詞,*,*,*,*,*,*,*,*\n")
        f.write("5 記号,*,*,*,*,*,*,*,*\n")
    
    with open("pos-id.def", "w", encoding="utf-8") as f:
        for i, word in enumerate(test_words):
            f.write(f"{i} {word[4]},{word[5]},{word[6]},{word[7]},{word[8]},{word[9]},*,*,*\n")
        # Unknown word POS
        f.write("100 名詞,一般,*,*,*,*,*,*,*\n")
        f.write("101 動詞,自立,*,*,五段・ラ行,基本形,*,*,*\n")
        f.write("102 記号,一般,*,*,*,*,*,*,*\n")
    
    with open("rewrite.def", "w", encoding="utf-8") as f:
        f.write("# No rewrite rules for test dictionary\n")
    
    print("Test dictionary created successfully!")
    print("Files created:")
    for fname in ["sys.dic", "unk.dic", "matrix.bin", "char.bin", 
                  "left-id.def", "right-id.def", "pos-id.def", "rewrite.def"]:
        if os.path.exists(fname):
            print(f"  - {fname} ({os.path.getsize(fname)} bytes)")

if __name__ == "__main__":
    create_test_dictionary()