#!/usr/bin/env python3
"""Fix Phase 3 warnings"""

import re
from pathlib import Path

def fix_simple_lts_async():
    """Fix async warning in SimpleLTSPhonemizer.cs"""
    file_path = Path("Assets/uPiper/Runtime/Core/Phonemizers/Backend/SimpleLTSPhonemizer.cs")
    content = file_path.read_text(encoding='utf-8')
    
    # Find the PhonemizeAsync method and wrap the synchronous code in Task.Run
    pattern = r'(public override async Task<PhonemeResult> PhonemizeAsync\([\s\S]*?\n\s*\{)'
    replacement = r'\1\n            return await Task.Run(() =>'
    
    # Also need to close the Task.Run at the end of the method
    # Find the return statement and wrap it
    content = re.sub(
        r'(\s+)(return new PhonemeResult\s*\{[\s\S]*?\};)',
        r'\1{\n\1    \2\n\1}, cancellationToken);',
        content
    )
    
    file_path.write_text(content, encoding='utf-8')
    print(f"Fixed async warning in {file_path}")

def fix_data_manager_warnings():
    """Fix warnings in PhonemizerDataManager.cs"""
    file_path = Path("Assets/uPiper/Runtime/Core/Phonemizers/Data/PhonemizerDataManager.cs")
    content = file_path.read_text(encoding='utf-8')
    
    # Remove unused manifestUrl field
    content = re.sub(
        r'\s*private readonly string manifestUrl;\s*\n',
        '',
        content
    )
    
    # Fix async method - add await Task.CompletedTask
    content = re.sub(
        r'(private async Task LoadRemoteManifest\(\)\s*\{)',
        r'\1\n            await Task.CompletedTask; // Placeholder for future remote loading',
        content
    )
    
    file_path.write_text(content, encoding='utf-8')
    print(f"Fixed warnings in {file_path}")

def fix_error_handling_unreachable():
    """Fix unreachable code in PhonemizerErrorHandlingTests.cs"""
    file_path = Path("Assets/uPiper/Tests/Runtime/Phonemizers/PhonemizerErrorHandlingTests.cs")
    content = file_path.read_text(encoding='utf-8')
    
    # The unreachable code is inside if (false) block - comment out the entire block
    lines = content.split('\n')
    new_lines = []
    
    for i, line in enumerate(lines):
        if i == 137 and "// Simulate some failures" in line:  # Line 138 in 1-indexed
            # Comment out the unreachable code block
            new_lines.append(line)
            new_lines.append(lines[i+1].replace('if (i % 2 == 0)', '// if (i % 2 == 0)'))
            new_lines.append(lines[i+2].replace('{', '// {'))
            new_lines.append(lines[i+3].replace('throw new Exception', '// throw new Exception'))
            new_lines.append(lines[i+4].replace('}', '// }'))
            # Skip the original lines
            for j in range(5):
                if i+j+1 < len(lines):
                    lines[i+j+1] = None
        elif line is not None:
            new_lines.append(line)
    
    content = '\n'.join(new_lines)
    file_path.write_text(content, encoding='utf-8')
    print(f"Fixed unreachable code warning in {file_path}")

def fix_integration_test_async():
    """Fix async warnings in PhonemizerIntegrationTests.cs"""
    file_path = Path("Assets/uPiper/Tests/Runtime/Phonemizers/PhonemizerIntegrationTests.cs")
    content = file_path.read_text(encoding='utf-8')
    
    # Add await Task.CompletedTask to async lambdas
    # First occurrence around line 232
    content = re.sub(
        r'(var task = Task\.Run\(async \(\) =>\s*\{)',
        r'\1\n                await Task.CompletedTask;',
        content,
        count=1
    )
    
    # Second occurrence around line 393
    pattern = r'(var task = Task\.Run\(async \(\) =>\s*\{\s*try\s*\{)'
    replacement = r'\1\n                    await Task.CompletedTask;'
    content = re.sub(pattern, replacement, content)
    
    file_path.write_text(content, encoding='utf-8')
    print(f"Fixed async warnings in {file_path}")

def main():
    # Comment out for now since the async fix is complex
    # fix_simple_lts_async()
    fix_data_manager_warnings()
    fix_error_handling_unreachable()
    fix_integration_test_async()

if __name__ == "__main__":
    main()