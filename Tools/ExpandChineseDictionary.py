#!/usr/bin/env python3
"""
Expand Chinese dictionary using pypinyin and other comprehensive sources
Phase 2 improvement for uPiper Chinese language support
"""

import json
import os
import sys
import gzip
import urllib.request
from collections import defaultdict

def download_pypinyin_data():
    """Download pypinyin dictionary data from GitHub"""
    # pypinyin phrase and character dictionaries
    sources = {
        'phrases_dict': 'https://raw.githubusercontent.com/mozillazg/phrase-pinyin-data/master/pinyin.txt',
        'cc_cedict': 'https://www.mdbg.net/chinese/export/cedict/cedict_1_0_ts_utf-8_mdbg.txt.gz'
    }
    
    data_dir = os.path.join(os.path.dirname(__file__), 'data')
    os.makedirs(data_dir, exist_ok=True)
    
    downloaded_files = {}
    
    for name, url in sources.items():
        print(f"Downloading {name} from {url}...")
        try:
            if url.endswith('.gz'):
                filename = os.path.join(data_dir, f'{name}.txt.gz')
                urllib.request.urlretrieve(url, filename)
                # Extract gz file
                with gzip.open(filename, 'rt', encoding='utf-8') as f:
                    content = f.read()
                txt_filename = filename[:-3]
                with open(txt_filename, 'w', encoding='utf-8') as f:
                    f.write(content)
                downloaded_files[name] = txt_filename
            else:
                filename = os.path.join(data_dir, f'{name}.txt')
                urllib.request.urlretrieve(url, filename)
                downloaded_files[name] = filename
            print(f"[OK] Downloaded {name}")
        except Exception as e:
            print(f"[FAIL] Failed to download {name}: {e}")
    
    return downloaded_files

def parse_phrase_pinyin_data(filename):
    """Parse phrase-pinyin data from mozillazg/phrase-pinyin-data"""
    phrases = {}
    if not os.path.exists(filename):
        return phrases
    
    with open(filename, 'r', encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if not line or line.startswith('#'):
                continue
            
            parts = line.split(':', 1)
            if len(parts) == 2:
                phrase = parts[0].strip()
                pinyin = parts[1].strip()
                phrases[phrase] = pinyin
    
    return phrases

def parse_cc_cedict(filename):
    """Parse CC-CEDICT format dictionary"""
    char_dict = defaultdict(set)
    phrase_dict = {}
    
    if not os.path.exists(filename):
        return char_dict, phrase_dict
    
    with open(filename, 'r', encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if not line or line.startswith('#'):
                continue
            
            # Format: Traditional Simplified [pin1 yin1] /definition/
            parts = line.split(' ', 2)
            if len(parts) < 3:
                continue
            
            traditional = parts[0]
            simplified = parts[1]
            
            # Extract pinyin
            pinyin_start = line.find('[')
            pinyin_end = line.find(']')
            if pinyin_start == -1 or pinyin_end == -1:
                continue
            
            pinyin = line[pinyin_start+1:pinyin_end].lower()
            
            # Process single characters
            if len(simplified) == 1:
                char_dict[simplified].add(pinyin.replace(' ', ''))
            
            # Process phrases (2-4 characters for now)
            elif 2 <= len(simplified) <= 4:
                phrase_dict[simplified] = pinyin
    
    return char_dict, phrase_dict

def merge_dictionaries(existing_chars, new_chars, existing_phrases, new_phrases):
    """Merge existing dictionary with new data"""
    # Merge character dictionary
    merged_chars = defaultdict(list)
    
    # Add existing entries
    for char, pinyin_list in existing_chars.items():
        merged_chars[char].extend(pinyin_list)
    
    # Add new entries
    for char, pinyin_set in new_chars.items():
        for pinyin in pinyin_set:
            if pinyin not in merged_chars[char]:
                merged_chars[char].append(pinyin)
    
    # Sort pinyin lists
    for char in merged_chars:
        merged_chars[char] = sorted(list(set(merged_chars[char])))
    
    # Merge phrase dictionary
    merged_phrases = existing_phrases.copy()
    merged_phrases.update(new_phrases)
    
    return dict(merged_chars), merged_phrases

def expand_ipa_mappings(existing_ipa):
    """Expand IPA mappings with more comprehensive coverage"""
    # Additional IPA mappings for better coverage
    expanded_mappings = {
        # More initials
        "w": "w", "y": "j",
        
        # More finals
        "ia": "ia", "iao": "iau", "ian": "iɛn", "iang": "iaŋ",
        "ua": "ua", "uo": "uo", "uai": "uai", "uan": "uan", "uang": "uaŋ",
        "üan": "yan", "iong": "yŋ",
        
        # Additional complete syllables
        "bian": "piɛn", "pian": "pʰiɛn", "mian": "miɛn", "dian": "tiɛn",
        "tian": "tʰiɛn", "nian": "niɛn", "lian": "liɛn", "jian": "tɕiɛn",
        "qian": "tɕʰiɛn", "xian": "ɕiɛn", "yan": "iɛn", "bian": "piɛn",
        
        # More retroflex syllables
        "zhu": "ʈʂu", "chu": "ʈʂʰu", "shu": "ʂu", "ru": "ʐu",
        "zhuan": "ʈʂuan", "chuan": "ʈʂʰuan", "shuan": "ʂuan", "ruan": "ʐuan",
        "zhuang": "ʈʂuaŋ", "chuang": "ʈʂʰuaŋ", "shuang": "ʂuaŋ",
        
        # Alveolar sibilants
        "zu": "tsu", "cu": "tsʰu", "su": "su",
        "zuan": "tsuan", "cuan": "tsʰuan", "suan": "suan",
        
        # More palatals
        "ju": "tɕy", "qu": "tɕʰy", "xu": "ɕy",
        "juan": "tɕyan", "quan": "tɕʰyan", "xuan": "ɕyan",
        "jue": "tɕyɛ", "que": "tɕʰyɛ", "xue": "ɕyɛ"
    }
    
    # Merge with existing
    merged_ipa = {entry['pinyin']: entry['ipa'] for entry in existing_ipa}
    merged_ipa.update(expanded_mappings)
    
    return merged_ipa

def create_frequency_data():
    """Create basic word frequency data based on common usage"""
    # Top frequency characters and words
    frequency_data = [
        {"word": "的", "frequency": 7922},
        {"word": "一", "frequency": 3728},
        {"word": "是", "frequency": 3651},
        {"word": "了", "frequency": 3347},
        {"word": "我", "frequency": 3186},
        {"word": "不", "frequency": 3161},
        {"word": "人", "frequency": 2830},
        {"word": "在", "frequency": 2796},
        {"word": "他", "frequency": 2718},
        {"word": "有", "frequency": 2615},
        {"word": "这", "frequency": 2483},
        {"word": "个", "frequency": 2420},
        {"word": "上", "frequency": 2322},
        {"word": "们", "frequency": 2175},
        {"word": "来", "frequency": 2162},
        {"word": "到", "frequency": 2136},
        {"word": "时", "frequency": 2098},
        {"word": "大", "frequency": 2034},
        {"word": "地", "frequency": 2009},
        {"word": "为", "frequency": 1953},
        {"word": "子", "frequency": 1896},
        {"word": "中", "frequency": 1867},
        {"word": "你", "frequency": 1835},
        {"word": "说", "frequency": 1813},
        {"word": "生", "frequency": 1768},
        {"word": "国", "frequency": 1731},
        {"word": "年", "frequency": 1664},
        {"word": "着", "frequency": 1650},
        {"word": "就", "frequency": 1638},
        {"word": "那", "frequency": 1611}
    ]
    
    return frequency_data

def main():
    script_dir = os.path.dirname(os.path.abspath(__file__))
    project_root = os.path.dirname(script_dir)
    output_dir = os.path.join(project_root, "Assets/StreamingAssets/uPiper/Chinese")
    
    # Load existing dictionaries
    print("Loading existing dictionaries...")
    existing_chars = {}
    existing_phrases = {}
    existing_ipa = []
    
    char_file = os.path.join(output_dir, "character_pinyin.json")
    if os.path.exists(char_file):
        with open(char_file, 'r', encoding='utf-8') as f:
            char_data = json.load(f)
            existing_chars = {entry['character']: entry['pinyin'] for entry in char_data}
    
    phrase_file = os.path.join(output_dir, "phrase_pinyin.json")
    if os.path.exists(phrase_file):
        with open(phrase_file, 'r', encoding='utf-8') as f:
            phrase_data = json.load(f)
            existing_phrases = {entry['phrase']: entry['pinyin'] for entry in phrase_data}
    
    ipa_file = os.path.join(output_dir, "pinyin_ipa_map.json")
    if os.path.exists(ipa_file):
        with open(ipa_file, 'r', encoding='utf-8') as f:
            existing_ipa = json.load(f)
    
    print(f"Existing: {len(existing_chars)} characters, {len(existing_phrases)} phrases")
    
    # Download and parse new data
    print("\nDownloading comprehensive dictionary data...")
    downloaded = download_pypinyin_data()
    
    # Parse phrase-pinyin data
    new_phrases = {}
    if 'phrases_dict' in downloaded:
        print("\nParsing phrase-pinyin data...")
        phrase_pinyin = parse_phrase_pinyin_data(downloaded['phrases_dict'])
        new_phrases.update(phrase_pinyin)
        print(f"Loaded {len(phrase_pinyin)} phrases from phrase-pinyin data")
    
    # Parse CC-CEDICT
    new_chars = defaultdict(set)
    if 'cc_cedict' in downloaded:
        print("\nParsing CC-CEDICT data...")
        cedict_chars, cedict_phrases = parse_cc_cedict(downloaded['cc_cedict'])
        new_chars.update(cedict_chars)
        new_phrases.update(cedict_phrases)
        print(f"Loaded {len(cedict_chars)} characters and {len(cedict_phrases)} phrases from CC-CEDICT")
    
    # Merge dictionaries
    print("\nMerging dictionaries...")
    merged_chars, merged_phrases = merge_dictionaries(
        existing_chars, new_chars, existing_phrases, new_phrases
    )
    
    print(f"Merged: {len(merged_chars)} characters, {len(merged_phrases)} phrases")
    
    # Create expanded character JSON
    char_entries = []
    for char, pinyin_list in sorted(merged_chars.items()):
        char_entries.append({
            "character": char,
            "pinyin": pinyin_list
        })
    
    expanded_char_file = os.path.join(output_dir, "character_pinyin_expanded.json")
    with open(expanded_char_file, 'w', encoding='utf-8') as f:
        json.dump(char_entries, f, ensure_ascii=False, indent=2)
    print(f"\nWrote {len(char_entries)} character entries to {expanded_char_file}")
    
    # Create expanded phrase JSON
    phrase_entries = []
    for phrase, pinyin in sorted(merged_phrases.items()):
        phrase_entries.append({
            "phrase": phrase,
            "pinyin": pinyin
        })
    
    expanded_phrase_file = os.path.join(output_dir, "phrase_pinyin_expanded.json")
    with open(expanded_phrase_file, 'w', encoding='utf-8') as f:
        json.dump(phrase_entries, f, ensure_ascii=False, indent=2)
    print(f"Wrote {len(phrase_entries)} phrase entries to {expanded_phrase_file}")
    
    # Expand IPA mappings
    expanded_ipa = expand_ipa_mappings(existing_ipa)
    ipa_entries = []
    for pinyin, ipa in sorted(expanded_ipa.items()):
        ipa_entries.append({
            "pinyin": pinyin,
            "ipa": ipa
        })
    
    expanded_ipa_file = os.path.join(output_dir, "pinyin_ipa_map_expanded.json")
    with open(expanded_ipa_file, 'w', encoding='utf-8') as f:
        json.dump(ipa_entries, f, ensure_ascii=False, indent=2)
    print(f"Wrote {len(ipa_entries)} IPA mappings to {expanded_ipa_file}")
    
    # Create word frequency data
    frequency_data = create_frequency_data()
    freq_file = os.path.join(output_dir, "word_frequency_expanded.json")
    with open(freq_file, 'w', encoding='utf-8') as f:
        json.dump(frequency_data, f, ensure_ascii=False, indent=2)
    print(f"Wrote {len(frequency_data)} frequency entries to {freq_file}")
    
    print("\n[SUCCESS] Dictionary expansion complete!")
    print(f"   Characters: {len(existing_chars)} → {len(merged_chars)} ({len(merged_chars) - len(existing_chars):+d})")
    print(f"   Phrases: {len(existing_phrases)} → {len(merged_phrases)} ({len(merged_phrases) - len(existing_phrases):+d})")
    print(f"   IPA mappings: {len(existing_ipa)} → {len(expanded_ipa)} ({len(expanded_ipa) - len(existing_ipa):+d})")

if __name__ == "__main__":
    main()