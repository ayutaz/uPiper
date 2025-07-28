#!/usr/bin/env python3
"""Fix invalid GUIDs in Unity .meta files"""

import os
import re
import uuid
import yaml

def generate_unity_guid():
    """Generate a Unity-compatible GUID"""
    return uuid.uuid4().hex

def fix_meta_file(filepath):
    """Fix a single .meta file"""
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            content = f.read()
        
        # Try to parse as YAML
        try:
            data = yaml.safe_load(content)
            if data and 'guid' in data:
                # Check if GUID is valid (32 hex chars)
                if not re.match(r'^[a-fA-F0-9]{32}$', data['guid']):
                    data['guid'] = generate_unity_guid()
                    with open(filepath, 'w', encoding='utf-8') as f:
                        yaml.dump(data, f, default_flow_style=False, sort_keys=False)
                    return True, "Fixed invalid GUID"
                return True, "GUID is valid"
            else:
                # No GUID found, add one
                data = data or {}
                data['guid'] = generate_unity_guid()
                data['fileFormatVersion'] = 2
                with open(filepath, 'w', encoding='utf-8') as f:
                    yaml.dump(data, f, default_flow_style=False, sort_keys=False)
                return True, "Added missing GUID"
        except yaml.YAMLError:
            # Fallback: string replacement
            guid_match = re.search(r'guid:\s*([a-fA-F0-9]*)', content)
            if guid_match:
                old_guid = guid_match.group(1)
                if len(old_guid) != 32:
                    new_guid = generate_unity_guid()
                    content = content.replace(f'guid: {old_guid}', f'guid: {new_guid}')
                    with open(filepath, 'w', encoding='utf-8') as f:
                        f.write(content)
                    return True, f"Replaced invalid GUID via string match"
            else:
                # Create minimal meta file
                new_guid = generate_unity_guid()
                content = f"""fileFormatVersion: 2
guid: {new_guid}
MonoImporter:
  externalObjects: {{}}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {{instanceID: 0}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
                with open(filepath, 'w', encoding='utf-8') as f:
                    f.write(content)
                return True, "Created new meta file"
    except Exception as e:
        return False, str(e)

def main():
    """Fix all invalid meta files"""
    project_root = r"C:\Users\yuta\Desktop\Private\uPiper"
    
    # Specific files mentioned in error
    problem_files = [
        r"Assets\uPiper\Runtime\Core\Phonemizers\Backend\Disambiguation\HomographResolver.cs.meta",
        r"Assets\uPiper\Runtime\Core\Phonemizers\Backend\G2P\StatisticalG2PModel.cs.meta"
    ]
    
    fixed_count = 0
    for rel_path in problem_files:
        full_path = os.path.join(project_root, rel_path)
        if os.path.exists(full_path):
            success, message = fix_meta_file(full_path)
            if success:
                print(f"✓ {rel_path}: {message}")
                fixed_count += 1
            else:
                print(f"✗ {rel_path}: Failed - {message}")
        else:
            print(f"✗ {rel_path}: File not found")
    
    # Also check parent directories
    parent_metas = [
        r"Assets\uPiper\Runtime\Core\Phonemizers\Backend\Disambiguation.meta",
        r"Assets\uPiper\Runtime\Core\Phonemizers\Backend\G2P.meta"
    ]
    
    for rel_path in parent_metas:
        full_path = os.path.join(project_root, rel_path)
        if not os.path.exists(full_path):
            # Create directory meta file
            new_guid = generate_unity_guid()
            content = f"""fileFormatVersion: 2
guid: {new_guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
            os.makedirs(os.path.dirname(full_path), exist_ok=True)
            with open(full_path, 'w', encoding='utf-8') as f:
                f.write(content)
            print(f"✓ {rel_path}: Created directory meta file")
            fixed_count += 1
    
    print(f"\nFixed {fixed_count} meta files")

if __name__ == "__main__":
    main()