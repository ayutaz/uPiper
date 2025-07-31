#!/usr/bin/env python3
"""Fix encoding issues in us_text.c"""
import sys
import re

def fix_us_text(input_file, output_file):
    with open(input_file, 'rb') as f:
        content = f.read()
    
    # Decode as UTF-8 with error handling
    text = content.decode('utf-8', errors='replace')
    
    # Fix problematic Unicode characters
    # Replace fancy quotes with regular ones
    text = text.replace(''', "'")  # Right single quotation mark
    text = text.replace(''', "'")  # Left single quotation mark
    text = text.replace('"', '"')  # Left double quotation mark
    text = text.replace('"', '"')  # Right double quotation mark
    text = text.replace('—', '-')  # Em dash
    text = text.replace('–', '-')  # En dash
    
    # Fix the specific lines that cause issues
    text = re.sub(r'static const char \*unicode_single_quote = ".*?";', 
                  'static const char *unicode_single_quote = "\\xe2\\x80\\x99";', text)
    
    # Write as ASCII with escape sequences
    with open(output_file, 'w', encoding='ascii', errors='backslashreplace') as f:
        f.write(text)
    
    print(f"Fixed {input_file} -> {output_file}")

if __name__ == "__main__":
    if len(sys.argv) != 3:
        print("Usage: fix_us_text.py <input_file> <output_file>")
        sys.exit(1)
    
    fix_us_text(sys.argv[1], sys.argv[2])