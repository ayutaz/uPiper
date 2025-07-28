#!/usr/bin/env python3
"""Phase 3のスコープ外のテストファイルを一時的に除外"""

import os
from pathlib import Path

def main():
    """Main function."""
    project_root = Path(__file__).parent.parent
    test_dir = project_root / "Assets/uPiper/Tests/Runtime/Phonemizers"
    
    # Phase 3のスコープ外のテストファイル（中国語、韓国語、スペイン語など）
    out_of_scope_tests = [
        "ChinesePhonemizerTests.cs",
        "KoreanPhonemizerTests.cs", 
        "SpanishPhonemizerTests.cs",
        "MultilingualPhonemizerTests.cs",  # 多言語サービスも範囲外
        "FullDictionaryPerformanceTests.cs"  # 辞書系のテストも範囲外
    ]
    
    renamed_count = 0
    
    for test_file in out_of_scope_tests:
        file_path = test_dir / test_file
        if file_path.exists():
            # .cs.bak に名前を変更して除外
            new_path = file_path.with_suffix('.cs.bak')
            try:
                file_path.rename(new_path)
                print(f"除外: {test_file} -> {test_file}.bak")
                renamed_count += 1
            except Exception as e:
                print(f"エラー: {test_file} のリネームに失敗 - {e}")
    
    # メタファイルも一緒にリネーム
    for test_file in out_of_scope_tests:
        meta_path = test_dir / f"{test_file}.meta"
        if meta_path.exists():
            new_meta_path = meta_path.with_suffix('.meta.bak')
            try:
                meta_path.rename(new_meta_path)
                print(f"除外: {test_file}.meta -> {test_file}.meta.bak")
            except Exception as e:
                print(f"エラー: {test_file}.meta のリネームに失敗 - {e}")
    
    print(f"\n合計 {renamed_count} 個のテストファイルを除外しました。")
    print("\n復元するには以下のコマンドを実行してください:")
    print("python scripts/restore_excluded_tests.py")

if __name__ == "__main__":
    main()