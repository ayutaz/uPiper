#!/usr/bin/env python3
"""除外したテストファイルを復元"""

import os
from pathlib import Path

def main():
    """Main function."""
    project_root = Path(__file__).parent.parent
    test_dir = project_root / "Assets/uPiper/Tests/Runtime/Phonemizers"
    
    restored_count = 0
    
    # .cs.bak ファイルを探して復元
    for bak_file in test_dir.glob("*.cs.bak"):
        original_path = bak_file.with_suffix('')
        try:
            bak_file.rename(original_path)
            print(f"復元: {bak_file.name} -> {original_path.name}")
            restored_count += 1
        except Exception as e:
            print(f"エラー: {bak_file.name} の復元に失敗 - {e}")
    
    # .meta.bak ファイルも復元
    for bak_meta in test_dir.glob("*.meta.bak"):
        original_meta = bak_meta.with_suffix('')
        try:
            bak_meta.rename(original_meta)
            print(f"復元: {bak_meta.name} -> {original_meta.name}")
        except Exception as e:
            print(f"エラー: {bak_meta.name} の復元に失敗 - {e}")
    
    print(f"\n合計 {restored_count} 個のテストファイルを復元しました。")

if __name__ == "__main__":
    main()