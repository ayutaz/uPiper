#!/usr/bin/env python3
"""
Split large files for GitHub Pages deployment (100MB limit)
"""

import os
import sys
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

def split_file(file_path, chunk_size=90*1024*1024):  # 90MB chunks (under 100MB limit)
    """Split a file into chunks"""
    file_path = Path(file_path)
    file_size = file_path.stat().st_size
    
    if file_size <= chunk_size:
        return None  # No need to split
    
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
        'chunks': chunks
    }
    
    manifest_path = file_path.parent / f"{file_path.name}.manifest.json"
    with open(manifest_path, 'w') as f:
        json.dump(manifest, f, indent=2)
    
    print(f"  Created manifest: {manifest_path.name}")
    
    # Delete original file to save space
    file_path.unlink()
    print(f"  Removed original file: {file_path.name}")
    
    return manifest

def reconstruct_file(manifest_path):
    """Reconstruct a file from chunks using manifest"""
    manifest_path = Path(manifest_path)
    
    with open(manifest_path, 'r') as f:
        manifest = json.load(f)
    
    output_path = manifest_path.parent / manifest['original_filename']
    
    print(f"Reconstructing {manifest['original_filename']}...")
    
    with open(output_path, 'wb') as output_file:
        for chunk_info in manifest['chunks']:
            chunk_path = manifest_path.parent / chunk_info['filename']
            
            if not chunk_path.exists():
                raise FileNotFoundError(f"Chunk not found: {chunk_path}")
            
            # Verify chunk hash
            actual_hash = get_file_hash(chunk_path)
            if actual_hash != chunk_info['hash']:
                raise ValueError(f"Hash mismatch for {chunk_path.name}")
            
            with open(chunk_path, 'rb') as chunk_file:
                output_file.write(chunk_file.read())
            
            print(f"  Processed {chunk_info['filename']}")
    
    # Verify reconstructed file
    actual_hash = get_file_hash(output_path)
    if actual_hash != manifest['original_hash']:
        raise ValueError("Reconstructed file hash doesn't match original")
    
    print(f"Successfully reconstructed {output_path.name}")
    return output_path

def process_directory(directory_path, size_limit=100*1024*1024):
    """Process all files in a directory, splitting those over size limit"""
    directory_path = Path(directory_path)
    
    if not directory_path.exists():
        print(f"Directory not found: {directory_path}")
        return
    
    processed_files = []
    
    # Process StreamingAssets directory
    streaming_assets = directory_path / "StreamingAssets"
    if streaming_assets.exists():
        for file_path in streaming_assets.iterdir():
            if file_path.is_file() and not file_path.name.endswith('.manifest.json') and not '.part' in file_path.name:
                file_size = file_path.stat().st_size
                if file_size > size_limit:
                    print(f"\nProcessing large file: {file_path.name}")
                    manifest = split_file(file_path)
                    if manifest:
                        processed_files.append(file_path.name)
    
    # Process Build directory (Unity WebGL data files)
    build_dir = directory_path / "Build"
    if build_dir.exists():
        for file_path in build_dir.iterdir():
            if file_path.is_file() and not file_path.name.endswith('.manifest.json') and not '.part' in file_path.name:
                file_size = file_path.stat().st_size
                if file_size > size_limit:
                    print(f"\nProcessing large file: {file_path.name}")
                    manifest = split_file(file_path)
                    if manifest:
                        processed_files.append(file_path.name)
    
    if processed_files:
        print(f"\n✅ Processed {len(processed_files)} large files for GitHub Pages")
        for filename in processed_files:
            print(f"  - {filename}")
    else:
        print("No files over 100MB found. Ready for GitHub Pages deployment!")
    
    return processed_files

def main():
    """Main entry point"""
    if len(sys.argv) < 2:
        print("Usage:")
        print("  python split-large-files.py process <directory>  # Split large files")
        print("  python split-large-files.py reconstruct <manifest.json>  # Reconstruct file")
        sys.exit(1)
    
    command = sys.argv[1]
    
    if command == "process":
        if len(sys.argv) < 3:
            print("Please specify a directory to process")
            sys.exit(1)
        
        directory = sys.argv[2]
        process_directory(directory)
        
    elif command == "reconstruct":
        if len(sys.argv) < 3:
            print("Please specify a manifest file")
            sys.exit(1)
        
        manifest_file = sys.argv[2]
        reconstruct_file(manifest_file)
        
    else:
        print(f"Unknown command: {command}")
        sys.exit(1)

if __name__ == "__main__":
    main()