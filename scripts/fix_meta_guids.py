#!/usr/bin/env python3
"""Fix invalid GUIDs in Unity meta files."""

import os
import re
import hashlib
from pathlib import Path

def generate_valid_guid(filepath):
    """Generate a valid 32-character hex GUID based on file path."""
    # Use file path hash to generate reproducible GUID
    hash_obj = hashlib.md5(filepath.encode())
    return hash_obj.hexdigest()

def fix_meta_file(filepath):
    """Fix GUID in a meta file."""
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            content = f.read()
        
        # Find current GUID
        guid_match = re.search(r'guid:\s*([^\n\r]+)', content)
        if not guid_match:
            print(f"No GUID found in {filepath}")
            return False
            
        old_guid = guid_match.group(1).strip()
        
        # Check if GUID is already valid (32 hex chars)
        if re.match(r'^[0-9a-f]{32}$', old_guid):
            return False
            
        # Generate new valid GUID
        new_guid = generate_valid_guid(str(filepath))
        
        # Replace GUID
        new_content = re.sub(
            r'guid:\s*[^\n\r]+',
            f'guid: {new_guid}',
            content
        )
        
        # Write back
        with open(filepath, 'w', encoding='utf-8') as f:
            f.write(new_content)
            
        print(f"Fixed {filepath}: {old_guid} -> {new_guid}")
        return True
        
    except Exception as e:
        print(f"Error processing {filepath}: {e}")
        return False

def main():
    """Main function."""
    project_root = Path(__file__).parent.parent
    assets_path = project_root / "Assets"
    
    fixed_count = 0
    error_count = 0
    
    # Find all meta files
    for meta_file in assets_path.rglob("*.meta"):
        try:
            if fix_meta_file(meta_file):
                fixed_count += 1
        except Exception as e:
            print(f"Error: {e}")
            error_count += 1
    
    print(f"\nSummary:")
    print(f"  Fixed: {fixed_count} files")
    print(f"  Errors: {error_count} files")

if __name__ == "__main__":
    main()