#!/usr/bin/env python3
"""
Split OpenJTalk data file for GitHub Pages deployment
GitHub Pages has a 100MB file size limit, so we split large files
"""
import os
import json
import hashlib

def calculate_hash(file_path):
    """Calculate SHA256 hash of file"""
    sha256_hash = hashlib.sha256()
    with open(file_path, "rb") as f:
        for byte_block in iter(lambda: f.read(4096), b""):
            sha256_hash.update(byte_block)
    return sha256_hash.hexdigest()

def split_file(input_file, chunk_size=94371840):  # 90MB chunks
    """Split file into chunks"""
    file_size = os.path.getsize(input_file)
    base_name = os.path.basename(input_file)
    dir_name = os.path.dirname(input_file)
    
    chunks = []
    chunk_num = 0
    
    with open(input_file, 'rb') as infile:
        while True:
            chunk_data = infile.read(chunk_size)
            if not chunk_data:
                break
            
            chunk_filename = f"{base_name}.part{chunk_num:03d}"
            chunk_path = os.path.join(dir_name, chunk_filename)
            
            with open(chunk_path, 'wb') as chunk_file:
                chunk_file.write(chunk_data)
            
            chunk_info = {
                "filename": chunk_filename,
                "size": len(chunk_data),
                "hash": calculate_hash(chunk_path)
            }
            chunks.append(chunk_info)
            print(f"Created {chunk_filename}: {len(chunk_data) / 1024 / 1024:.1f}MB")
            
            chunk_num += 1
    
    # Create manifest file
    manifest = {
        "original_filename": base_name,
        "original_size": file_size,
        "original_hash": calculate_hash(input_file),
        "chunk_size": chunk_size,
        "chunks": chunks,
        "split_for_github_pages": True
    }
    
    manifest_path = os.path.join(dir_name, f"{base_name}.manifest.json")
    with open(manifest_path, 'w') as f:
        json.dump(manifest, f, indent=2)
    
    print(f"\nCreated manifest: {manifest_path}")
    print(f"Total chunks: {len(chunks)}")
    print(f"Original size: {file_size / 1024 / 1024:.1f}MB")
    
    return manifest_path, chunks

if __name__ == "__main__":
    # Split for Assets/StreamingAssets
    assets_file = "Assets/StreamingAssets/openjtalk-unity.data"
    if os.path.exists(assets_file):
        print(f"Splitting {assets_file}...")
        split_file(assets_file)
    
    # Split for Build/Web/StreamingAssets
    build_file = "Build/Web/StreamingAssets/openjtalk-unity.data"
    if os.path.exists(build_file):
        print(f"\nSplitting {build_file}...")
        split_file(build_file)
    
    print("\nSplit complete!")