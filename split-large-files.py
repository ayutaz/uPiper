#!/usr/bin/env python3
"""
GitHub Pages用大容量ファイル分割ツール
100MB制限を回避するためファイルを分割
"""

import os
import json
import sys
from pathlib import Path

# 分割サイズ（90MBに設定、余裕を持たせる）
CHUNK_SIZE = 90 * 1024 * 1024  # 90MB

def split_file(file_path, chunk_size=CHUNK_SIZE):
    """大きなファイルを分割"""
    file_path = Path(file_path)
    
    if not file_path.exists():
        print(f"Error: File not found: {file_path}")
        return False
    
    file_size = file_path.stat().st_size
    
    # 100MB未満なら分割不要
    if file_size < 100 * 1024 * 1024:
        print(f"File {file_path.name} is under 100MB ({file_size / 1024 / 1024:.2f}MB), no split needed")
        return False
    
    print(f"Splitting {file_path.name} ({file_size / 1024 / 1024:.2f}MB)...")
    
    # 分割ファイルの出力ディレクトリ
    output_dir = file_path.parent
    base_name = file_path.name + '.split'
    
    # ファイルを読み込んで分割
    parts = []
    part_num = 0
    
    with open(file_path, 'rb') as f:
        while True:
            chunk = f.read(chunk_size)
            if not chunk:
                break
            
            # 分割ファイル名（例: file.data.split.000）
            part_name = f"{base_name}.{part_num:03d}"
            part_path = output_dir / part_name
            
            with open(part_path, 'wb') as part_file:
                part_file.write(chunk)
            
            parts.append({
                'part': part_num,
                'size': len(chunk),
                'name': part_name
            })
            
            print(f"  Created part {part_num}: {len(chunk) / 1024 / 1024:.2f}MB")
            part_num += 1
    
    # マニフェストファイルを作成
    manifest = {
        'original': file_path.name,
        'totalSize': file_size,
        'chunkSize': chunk_size,
        'parts': part_num,
        'partFiles': parts
    }
    
    manifest_path = output_dir / f"{base_name}.manifest"
    with open(manifest_path, 'w') as f:
        json.dump(manifest, f, indent=2)
    
    print(f"  Created manifest: {manifest_path.name}")
    print(f"  Total parts: {part_num}")
    
    return True

def combine_files(manifest_path):
    """分割ファイルを結合"""
    manifest_path = Path(manifest_path)
    
    if not manifest_path.exists():
        print(f"Error: Manifest not found: {manifest_path}")
        return False
    
    with open(manifest_path, 'r') as f:
        manifest = json.load(f)
    
    output_path = manifest_path.parent / manifest['original']
    base_name = output_path.name + '.split'
    
    print(f"Combining {manifest['parts']} parts into {output_path.name}...")
    
    with open(output_path, 'wb') as output:
        for i in range(manifest['parts']):
            part_path = manifest_path.parent / f"{base_name}.{i:03d}"
            
            if not part_path.exists():
                print(f"Error: Part file not found: {part_path}")
                return False
            
            with open(part_path, 'rb') as part:
                output.write(part.read())
            
            print(f"  Combined part {i}")
    
    print(f"Successfully combined into {output_path.name}")
    return True

def process_unity_webgl_build(build_dir):
    """Unity WebGLビルドの大容量ファイルを処理"""
    build_dir = Path(build_dir)
    
    if not build_dir.exists():
        print(f"Error: Build directory not found: {build_dir}")
        return False
    
    print(f"Processing Unity WebGL build in {build_dir}...")
    
    # 処理対象ファイル
    large_files = [
        build_dir / 'StreamingAssets' / 'openjtalk-unity.data',
        build_dir / 'StreamingAssets' / 'ja_JP-test-medium.onnx',
        # 必要に応じて他のファイルも追加
    ]
    
    split_count = 0
    for file_path in large_files:
        if file_path.exists():
            if split_file(file_path):
                split_count += 1
                
                # オリジナルファイルを削除（オプション）
                # print(f"Removing original file: {file_path.name}")
                # file_path.unlink()
    
    if split_count > 0:
        print(f"\n✅ Split {split_count} large files for GitHub Pages")
        print("\n⚠️  Important: Deploy the .split.* and .manifest files to GitHub Pages")
        print("The adapter will automatically load split files when needed")
    else:
        print("\n✅ No files needed splitting (all under 100MB)")
    
    return True

def main():
    """メイン処理"""
    if len(sys.argv) < 2:
        print("Usage:")
        print("  Split file:    python split-large-files.py split <file>")
        print("  Combine file:  python split-large-files.py combine <manifest>")
        print("  Process build: python split-large-files.py process <build_dir>")
        return 1
    
    command = sys.argv[1]
    
    if command == 'split' and len(sys.argv) >= 3:
        file_path = sys.argv[2]
        if split_file(file_path):
            print("✅ File split successfully")
            return 0
        return 1
    
    elif command == 'combine' and len(sys.argv) >= 3:
        manifest_path = sys.argv[2]
        if combine_files(manifest_path):
            print("✅ Files combined successfully")
            return 0
        return 1
    
    elif command == 'process' and len(sys.argv) >= 3:
        build_dir = sys.argv[2]
        if process_unity_webgl_build(build_dir):
            return 0
        return 1
    
    else:
        print("Invalid command or arguments")
        return 1

if __name__ == '__main__':
    sys.exit(main())