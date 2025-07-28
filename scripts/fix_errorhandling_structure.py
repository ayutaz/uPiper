#!/usr/bin/env python3
"""Fix structure issues in PhonemizerErrorHandlingTests.cs"""

import re
from pathlib import Path

def main():
    file_path = Path("Assets/uPiper/Tests/Runtime/Phonemizers/PhonemizerErrorHandlingTests.cs")
    
    if not file_path.exists():
        print(f"Error: {file_path} not found")
        return
    
    content = file_path.read_text(encoding='utf-8')
    
    # Count braces
    open_braces = content.count('{')
    close_braces = content.count('}')
    
    print(f"Open braces: {open_braces}")
    print(f"Close braces: {close_braces}")
    print(f"Difference: {open_braces - close_braces}")
    
    # Find all public methods that might be misplaced
    pattern = r'^(\s*)public\s+(async\s+)?(?:Task|void|IEnumerator)'
    matches = list(re.finditer(pattern, content, re.MULTILINE))
    
    print(f"\nFound {len(matches)} public methods:")
    for match in matches:
        line_num = content[:match.start()].count('\n') + 1
        indent_level = len(match.group(1))
        print(f"  Line {line_num}: indent={indent_level}, method starts with: {match.group(0).strip()}")
    
    # Check if methods are properly inside the class
    lines = content.split('\n')
    class_indent = None
    in_class = False
    
    for i, line in enumerate(lines):
        if 'class PhonemizerErrorHandlingTests' in line:
            in_class = True
            class_indent = len(line) - len(line.lstrip())
            print(f"\nClass starts at line {i+1} with indent {class_indent}")
        
        if in_class and line.strip().startswith('public') and ('Task' in line or 'void' in line):
            method_indent = len(line) - len(line.lstrip())
            expected_indent = class_indent + 8  # Assuming 8 spaces for method indent
            if method_indent != expected_indent:
                print(f"WARNING: Line {i+1} has indent {method_indent}, expected {expected_indent}")

if __name__ == "__main__":
    main()