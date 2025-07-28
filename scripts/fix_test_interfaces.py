#!/usr/bin/env python3
"""Fix interface implementations in test files."""

import os
import re
from pathlib import Path

def fix_test_backend_implementations(filepath):
    """Fix test backend implementations to implement all required interface members."""
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            content = f.read()
        
        original_content = content
        
        # Find all test backend classes that implement IPhonemizerBackend
        pattern = r'class\s+(\w+)\s*:\s*IPhonemizerBackend\s*\{([^}]+)\}'
        
        def add_missing_members(match):
            class_name = match.group(1)
            class_body = match.group(2)
            
            # Check which members are missing
            missing_members = []
            
            if 'public string Name' not in class_body:
                missing_members.append('        public string Name => "TestBackend";')
            if 'public string Version' not in class_body:
                missing_members.append('        public string Version => "1.0.0";')
            if 'public string License' not in class_body:
                missing_members.append('        public string License => "MIT";')
            if 'public string[] SupportedLanguages' not in class_body:
                missing_members.append('        public string[] SupportedLanguages => new[] { "en" };')
            if 'public int Priority' not in class_body:
                missing_members.append('        public int Priority => 50;')
            if 'public bool IsAvailable' not in class_body:
                missing_members.append('        public bool IsAvailable => true;')
                
            if 'InitializeAsync' not in class_body:
                missing_members.append('''
        public Task<bool> InitializeAsync(PhonemizerBackendOptions options = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }''')
                
            if 'SupportsLanguage' not in class_body:
                missing_members.append('''
        public bool SupportsLanguage(string language)
        {
            return true;
        }''')
                
            if 'GetMemoryUsage' not in class_body:
                missing_members.append('''
        public long GetMemoryUsage()
        {
            return 0;
        }''')
                
            if 'GetCapabilities' not in class_body:
                missing_members.append('''
        public BackendCapabilities GetCapabilities()
        {
            return new BackendCapabilities
            {
                SupportsIPA = false,
                SupportsStress = false,
                SupportsSyllables = false,
                SupportsTones = false,
                SupportsDuration = false,
                SupportsBatchProcessing = true,
                IsThreadSafe = true,
                RequiresNetwork = false
            };
        }''')
                
            if 'Dispose' not in class_body:
                missing_members.append('''
        public void Dispose()
        {
            // Test implementation
        }''')
            
            if missing_members:
                # Insert missing members at the beginning of the class
                new_body = class_body.rstrip() + '\n\n' + '\n\n'.join(missing_members) + '\n    '
                return f'class {class_name} : IPhonemizerBackend\n    {{{new_body}}}'
            
            return match.group(0)
        
        # Apply fixes
        content = re.sub(pattern, add_missing_members, content, flags=re.DOTALL)
        
        if content != original_content:
            with open(filepath, 'w', encoding='utf-8') as f:
                f.write(content)
            print(f"Fixed interface implementations in: {filepath}")
            return True
        
        return False
        
    except Exception as e:
        print(f"Error processing {filepath}: {e}")
        return False

def main():
    """Main function."""
    project_root = Path(__file__).parent.parent
    test_file = project_root / "Assets/uPiper/Tests/Runtime/Phonemizers/PhonemizerErrorHandlingTests.cs"
    
    if test_file.exists():
        fix_test_backend_implementations(test_file)
    else:
        print(f"File not found: {test_file}")

if __name__ == "__main__":
    main()