#!/usr/bin/env python3
"""Complete fix for PhonemizerErrorHandlingTests.cs structure"""

import re
from pathlib import Path

def main():
    file_path = Path("Assets/uPiper/Tests/Runtime/Phonemizers/PhonemizerErrorHandlingTests.cs")
    
    if not file_path.exists():
        print(f"Error: {file_path} not found")
        return
    
    lines = file_path.read_text(encoding='utf-8').split('\n')
    
    # Fix line 153 - the closing brace should not be commented
    for i, line in enumerate(lines):
        if i == 152 and line.strip() == "// }":  # Line 153 in 1-indexed
            lines[i] = "            }"
            print(f"Fixed line {i+1}: uncommented closing brace")
    
    # Remove duplicate closing braces at the end
    # Find the last non-empty line
    last_content_idx = len(lines) - 1
    while last_content_idx >= 0 and not lines[last_content_idx].strip():
        last_content_idx -= 1
    
    # Check if we have duplicate closing braces
    if last_content_idx >= 1:
        if lines[last_content_idx].strip() == "}" and lines[last_content_idx-1].strip() == "}":
            # Remove one of the duplicate braces
            lines[last_content_idx] = ""
            print("Removed duplicate closing brace at end of file")
    
    # Rejoin and save
    content = '\n'.join(lines)
    
    # Count braces for verification
    open_braces = content.count('{')
    close_braces = content.count('}')
    print(f"Final brace count - Open: {open_braces}, Close: {close_braces}")
    
    if open_braces != close_braces:
        print(f"WARNING: Brace mismatch! Open: {open_braces}, Close: {close_braces}")
    
    file_path.write_text(content, encoding='utf-8')
    print(f"Fixed {file_path}")

if __name__ == "__main__":
    main()