#!/usr/bin/env python3
"""
Split openjtalk-unity.data file for GitHub Pages
"""

import os
import json
import hashlib
from pathlib import Path

def get_file_hash(file_path):
    """Calculate SHA256 hash of a file"""
    sha256_hash = hashlib.sha256()
    with open(file_path, "rb") as f:
        for byte_block in iter(lambda: f.read(4096), b""):
            sha256_hash.update(byte_block)
    return sha256_hash.hexdigest()

def split_openjtalk_data():
    """Split openjtalk-unity.data into 90MB chunks"""
    
    file_path = Path("Assets/StreamingAssets/openjtalk-unity.data")
    
    if not file_path.exists():
        print(f"File not found: {file_path}")
        return False
    
    file_size = file_path.stat().st_size
    chunk_size = 90 * 1024 * 1024  # 90MB chunks
    
    print(f"Splitting {file_path.name} ({file_size / (1024*1024):.2f} MB)...")
    
    chunks = []
    chunk_index = 0
    
    with open(file_path, 'rb') as f:
        while True:
            chunk_data = f.read(chunk_size)
            if not chunk_data:
                break
            
            chunk_filename = f"{file_path.name}.part{chunk_index:03d}"
            chunk_path = file_path.parent / chunk_filename
            
            with open(chunk_path, 'wb') as chunk_file:
                chunk_file.write(chunk_data)
            
            chunks.append({
                'filename': chunk_filename,
                'size': len(chunk_data),
                'hash': get_file_hash(chunk_path)
            })
            
            print(f"  Created {chunk_filename} ({len(chunk_data) / (1024*1024):.2f} MB)")
            chunk_index += 1
    
    # Create manifest file
    manifest = {
        'original_filename': file_path.name,
        'original_size': file_size,
        'original_hash': get_file_hash(file_path),
        'chunk_size': chunk_size,
        'chunks': chunks,
        'split_for_github_pages': True
    }
    
    manifest_path = file_path.parent / f"{file_path.name}.manifest.json"
    with open(manifest_path, 'w') as f:
        json.dump(manifest, f, indent=2)
    
    print(f"\nCreated manifest: {manifest_path.name}")
    print(f"Total chunks: {len(chunks)}")
    
    # Show the files created
    print("\nFiles created:")
    for chunk in chunks:
        print(f"  - {chunk['filename']} ({chunk['size'] / (1024*1024):.2f} MB)")
    print(f"  - {manifest_path.name}")
    
    # Keep original file for local testing
    print(f"\nOriginal file kept: {file_path.name}")
    print("Note: Original file is preserved for local testing.")
    print("For GitHub Pages deployment, use the chunk files with manifest.")
    
    return True

if __name__ == "__main__":
    success = split_openjtalk_data()
    if success:
        print("\n✅ Successfully split openjtalk-unity.data for GitHub Pages!")
        print("\nNext steps:")
        print("1. Commit the .part files and manifest.json")
        print("2. The github-pages-adapter.js will automatically reconstruct the file in browser")
    else:
        print("\n❌ Failed to split file")