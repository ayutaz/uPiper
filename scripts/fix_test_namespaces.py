#!/usr/bin/env python3
"""Fix namespace references in test files."""

import os
import re
from pathlib import Path

# Namespace mappings
NAMESPACE_FIXES = {
    r'using uPiper\.Phonemizers\.Multilingual;': 'using uPiper.Core.Phonemizers.Multilingual;',
    r'using uPiper\.Phonemizers\.Backend;': 'using uPiper.Core.Phonemizers.Backend;',
    r'using uPiper\.Phonemizers\.Backend\.RuleBased;': 'using uPiper.Core.Phonemizers.Backend.RuleBased;',
    r'using uPiper\.Phonemizers\.Backend\.Flite;': 'using uPiper.Core.Phonemizers.Backend.Flite;',
    r'using uPiper\.Phonemizers\.ErrorHandling;': 'using uPiper.Core.Phonemizers.ErrorHandling;',
    r'using uPiper\.Phonemizers\.Threading;': 'using uPiper.Core.Phonemizers.Threading;',
    r'using uPiper\.Phonemizers\.Caching;': 'using uPiper.Core.Phonemizers.Caching;',
}

def fix_file(filepath):
    """Fix namespace references in a file."""
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            content = f.read()
        
        original_content = content
        
        # Apply all namespace fixes
        for pattern, replacement in NAMESPACE_FIXES.items():
            content = re.sub(pattern, replacement, content)
        
        if content != original_content:
            with open(filepath, 'w', encoding='utf-8') as f:
                f.write(content)
            print(f"Fixed: {filepath}")
            return True
        
        return False
        
    except Exception as e:
        print(f"Error processing {filepath}: {e}")
        return False

def main():
    """Main function."""
    project_root = Path(__file__).parent.parent
    test_path = project_root / "Assets" / "uPiper" / "Tests" / "Runtime" / "Phonemizers"
    
    fixed_count = 0
    
    # Find all test files
    for cs_file in test_path.glob("*.cs"):
        if fix_file(cs_file):
            fixed_count += 1
    
    print(f"\nFixed {fixed_count} files")

if __name__ == "__main__":
    main()