# OpenJTalk辞書アップグレード計画

## 現状
- 現在: 157語の最小辞書（ハードコード）
- 必要: pyopenjtalk-plus互換の精度（40万語以上）

## 目標
通常のTTSとして使用可能な精度を実現するため、mecab-naist-jdic（IPADICベース）を統合する。

## 実装方針

### 1. 辞書フォーマット
mecab-naist-jdicのバイナリ形式を使用：
- char.bin - 文字定義
- unk.dic - 未知語辞書
- sys.dic - システム辞書（メイン辞書）
- matrix.bin - 連接コスト行列

### 2. ファイルサイズ
- システム辞書: 約40MB
- 連接行列: 約2MB
- 合計: 約45-50MB

### 3. Unity統合方法

#### オプションA: StreamingAssetsフォルダ（推奨）
```
Assets/
  StreamingAssets/
    OpenJTalk/
      Dictionary/
        char.bin
        unk.dic
        sys.dic
        matrix.bin
```

#### オプションB: 圧縮辞書
- LZ4圧縮で約15-20MBに削減
- 起動時に解凍してメモリに展開

### 4. API設計

```c
// 辞書の初期化（パス指定）
MecabDictionary* mecab_dict_load_ipadic(const char* dict_dir);

// 辞書の初期化（メモリから）
MecabDictionary* mecab_dict_load_from_memory(
    const void* sys_dic, size_t sys_size,
    const void* unk_dic, size_t unk_size,
    const void* matrix, size_t matrix_size
);
```

### 5. 実装手順

1. **辞書ローダーの実装**
   - バイナリ辞書形式のパーサー
   - メモリ効率的な辞書構造

2. **Mecabコアの拡張**
   - 大規模辞書対応
   - 未知語処理の実装
   - 連接コストの最適化

3. **Unity統合**
   - StreamingAssetsからの読み込み
   - プラットフォーム別の最適化

4. **テスト**
   - Python側との精度比較
   - パフォーマンステスト

## ライセンス
mecab-naist-jdicはBSDライセンスなので、商用利用可能。

## タイムライン
- Phase 1: 辞書ローダー実装（2-3日）
- Phase 2: Mecabコア拡張（3-4日）
- Phase 3: Unity統合とテスト（2-3日）

合計: 約1-2週間