#!/usr/bin/env python3
"""Fix obsolete test references."""

import os
import re
from pathlib import Path

def fix_proxy_references(filepath):
    """Fix references to non-existent Proxy classes."""
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            content = f.read()
        
        original_content = content
        
        # Replace Proxy classes with actual backend classes
        replacements = {
            'ChinesePhonemizerProxy': 'ChinesePhonemizer',
            'SpanishPhonemizerProxy': 'SpanishPhonemizer', 
            'KoreanPhonemizerProxy': 'KoreanPhonemizer',
        }
        
        for old, new in replacements.items():
            content = content.replace(old, new)
        
        # Add missing using directives if needed
        if 'ChinesePhonemizer' in content and 'using uPiper.Core.Phonemizers.Backend;' not in content:
            # Find where to insert
            match = re.search(r'(using .*;\n)+', content)
            if match:
                insert_pos = match.end()
                content = content[:insert_pos] + 'using uPiper.Core.Phonemizers.Backend;\n' + content[insert_pos:]
        
        if content != original_content:
            with open(filepath, 'w', encoding='utf-8') as f:
                f.write(content)
            print(f"Fixed proxy references in: {filepath}")
            return True
        
        return False
        
    except Exception as e:
        print(f"Error processing {filepath}: {e}")
        return False

def fix_unity_service_references(filepath):
    """Fix UnityPhonemizerService method calls."""
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            content = f.read()
        
        original_content = content
        
        # Fix method calls
        content = re.sub(
            r'service\.PhonemizeAsync\((.*?)\)',
            r'await Task.Run(() => service.Phonemize(\1, result => { }))',
            content
        )
        
        # These methods don't exist - comment them out
        content = re.sub(
            r'(\s*)var languages = service\.GetAvailableLanguages\(\);',
            r'\1// TODO: GetAvailableLanguages not implemented\n\1var languages = new string[] { "ja", "en" };',
            content
        )
        
        content = re.sub(
            r'(\s*)bool available = service\.IsLanguageDataAvailable\(lang\);',
            r'\1// TODO: IsLanguageDataAvailable not implemented\n\1bool available = true;',
            content
        )
        
        content = re.sub(
            r'(\s*)var stats = service\.GetCacheStatistics\(\);',
            r'\1// TODO: GetCacheStatistics not implemented\n\1object stats = null;',
            content
        )
        
        if content != original_content:
            with open(filepath, 'w', encoding='utf-8') as f:
                f.write(content)
            print(f"Fixed Unity service references in: {filepath}")
            return True
        
        return False
        
    except Exception as e:
        print(f"Error processing {filepath}: {e}")
        return False

def main():
    """Main function."""
    project_root = Path(__file__).parent.parent
    
    fixed_count = 0
    
    # Fix test files
    test_files = [
        project_root / "Assets/uPiper/Tests/Runtime/Phonemizers/ChinesePhonemizerTests.cs",
        project_root / "Assets/uPiper/Tests/Runtime/Phonemizers/SpanishPhonemizerTests.cs",
        project_root / "Assets/uPiper/Tests/Runtime/Phonemizers/KoreanPhonemizerTests.cs",
    ]
    
    for test_file in test_files:
        if test_file.exists() and fix_proxy_references(test_file):
            fixed_count += 1
    
    # Fix editor file
    editor_file = project_root / "Assets/uPiper/Editor/Phonemizers/PhonemizerSettingsEditor.cs"
    if editor_file.exists() and fix_unity_service_references(editor_file):
        fixed_count += 1
    
    print(f"\nFixed {fixed_count} files")

if __name__ == "__main__":
    main()