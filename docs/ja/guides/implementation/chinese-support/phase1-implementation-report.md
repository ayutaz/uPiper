# 中国語サポート Phase 1 実装報告書

## 概要

2025年1月、uPiperの中国語音素化機能のPhase 1実装が完了しました。基本的な中国語音素化パイプラインが動作可能になり、限定的ながら実用的な機能を提供します。

## Phase 1 実装内容

### 1.1 辞書データの準備とローダー実装 ✅

#### 実装内容
- **辞書データのJSON形式変換**
  - `ConvertPinyinDictionary.py`スクリプトの作成
  - タブ区切りテキストからJSON形式への変換
  - 4つのJSONファイル生成:
    - `character_pinyin.json`: 364文字の基本辞書
    - `phrase_pinyin.json`: 30フレーズの辞書
    - `pinyin_ipa_map.json`: 217のピンイン→IPAマッピング
    - `word_frequency.json`: 空（将来の拡張用）

- **非同期辞書ローダー**
  - `ChineseDictionaryLoader.cs`: StreamingAssetsからの読み込み
  - Android対応（UnityWebRequest使用）
  - デスクトップ対応（File.ReadAllText）
  - フォールバック機能（辞書が見つからない場合の最小限データ）

- **辞書データ構造**
  - `ChineseDictionaryData.cs`: シリアライズ可能なデータ構造
  - `ChinesePinyinDictionary.cs`: ランタイム用の高速検索辞書

### 1.2 基本的なピンイン変換 ✅

#### 実装内容
- **PinyinConverter.cs**
  - フレーズマッチング（最長4文字）
  - 文字単位の変換（フォールバック）
  - 基本的な多音字ルール:
    - 不: bu4/bu2（次の文字が4声の場合）
    - 一: yi1/yi2/yi4（文脈による）
    - 了: le5/liao3（文末/動詞）
  - 未知文字のUnicodeエスケープ（u+XXXX形式）

### 1.3 IPA変換実装 ✅

#### 実装内容
- **PinyinToIPAConverter.cs**
  - 声調マーク付きIPA変換
  - Mandarin声調システム:
    - 第1声: ˥ (55)
    - 第2声: ˧˥ (35)
    - 第3声: ˨˩˦ (214)
    - 第4声: ˥˩ (51)
    - 軽声: マークなし
  - IPA音素分割アルゴリズム
  - 217の基本ピンイン→IPAマッピング

### 1.4 テキスト正規化 ✅

#### 実装内容
- **ChineseTextNormalizer.cs**
  - 数字変換（2モード）:
    - Individual: 123 → 一二三
    - Formal: 123 → 一百二十三
  - 句読点正規化:
    - 中国語句読点を標準記号に変換
    - Unicodeエスケープ使用（\u2018, \u2019）
  - 中英混在テキスト分離
  - 特殊文字処理（Mr. → 先生等）

### 1.5 統合とテスト ✅

#### 実装内容
- **ChinesePhonemizer.cs更新**
  - 新しいコンポーネントの統合
  - 完全な音素化パイプライン:
    1. テキスト正規化
    2. 中英分離
    3. ピンイン変換
    4. IPA変換
  - メモリ使用量レポート

- **包括的テストスイート**
  - `ChinesePhonemizationTests.cs`:
    - 基本文字テスト
    - フレーズテスト
    - 数字正規化テスト
    - 混在テキストテスト
    - パフォーマンステスト（50ms目標）
    - メモリ使用量テスト（10MB以内）
  - `ChineseTextNormalizerTests.cs`:
    - 数字変換テスト
    - 句読点正規化テスト
    - 混在テキスト分離テスト

## 実装結果

### パフォーマンス指標
- **処理速度**: 100文字を平均XX ms以内で処理（目標: 50ms）
- **メモリ使用量**: 約XXX KB（目標: 10MB以内）
- **辞書カバレッジ**: 364文字（基本的な日常会話をカバー）

### 制限事項
1. **辞書サイズ**: 364文字のみ（完全な実装には20,000+文字必要）
2. **単語分割**: 文字単位のみ（jieba風の単語分割は未実装）
3. **多音字対応**: 基本的な3文字のみ（不、一、了）
4. **繁体字**: 未対応（簡体字のみ）

## 技術的詳細

### ファイル構成
```
Assets/
├── StreamingAssets/uPiper/Chinese/
│   ├── character_pinyin.json
│   ├── phrase_pinyin.json
│   ├── pinyin_ipa_map.json
│   └── word_frequency.json
├── uPiper/Runtime/Core/Phonemizers/
│   ├── Backend/
│   │   ├── ChinesePhonemizer.cs (更新)
│   │   └── Chinese/
│   │       ├── ChineseDictionaryData.cs
│   │       ├── ChineseDictionaryLoader.cs
│   │       ├── ChineseTextNormalizer.cs
│   │       ├── PinyinConverter.cs
│   │       └── PinyinToIPAConverter.cs
│   └── Tests/Runtime/ChinesePhonemizer/
│       ├── ChinesePhonemizationTests.cs
│       └── ChineseTextNormalizerTests.cs
Tools/
└── ConvertPinyinDictionary.py
```

### 修正済みの問題
1. **コンパイルエラー**:
   - 中国語句読点のエスケープエラー（\u2018, \u2019使用で解決）
   - 名前空間の競合（完全修飾名使用で解決）

2. **設計上の課題**:
   - 辞書データの非同期読み込み
   - Android/デスクトップの差異吸収

## 次のステップ（Phase 2）

1. **辞書の拡張**
   - pypinyinの完全辞書（42,000+文字）
   - CC-CEDICTデータの統合
   - 単語頻度データの追加

2. **高度な機能**
   - jieba風の単語分割アルゴリズム
   - 文脈を考慮した多音字選択
   - 繁体字サポート

3. **最適化**
   - 辞書データの圧縮
   - キャッシュ機能の強化
   - メモリ使用量の削減

## まとめ

Phase 1実装により、uPiperは基本的な中国語音素化機能を獲得しました。限定的な辞書サイズながら、完全な音素化パイプラインが動作し、今後の拡張の基盤が整いました。