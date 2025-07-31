# Phase 3: MITライセンス専用実装戦略

## 概要

GPLライセンスのコンポーネントを完全に除外し、MITライセンス（または互換性のあるライセンス）のみを使用した実装を行います。

## 実装方針

### 1. 採用するコンポーネント

| コンポーネント | ライセンス | 用途 |
|---------------|-----------|------|
| CMU Pronouncing Dictionary | パブリックドメイン | 英語発音辞書 |
| Flite (Festival Lite) | MIT相当 | 高度な英語音素化（オプション） |
| 独自G2Pルール | MIT（uPiper） | 辞書外単語の処理 |
| OpenJTalk | BSD | 日本語（既存） |

### 2. 除外するコンポーネント

- ❌ eSpeak-NG (GPL v3)
- ❌ Phonemizer (GPL v3)
- ❌ その他GPLライセンスのライブラリ

### 3. アーキテクチャ

```
uPiper Phonemization System (100% MIT/BSD)
├── Core (MIT)
│   ├── RuleBasedPhonemizer
│   ├── DictionaryManager
│   └── G2PEngine
├── Data (Public Domain/MIT)
│   ├── CMU Dictionary
│   └── Custom Rules
└── Optional (MIT)
    └── FlitePhonemizer
```

## 実装計画

### Phase 1: 基本英語サポート
1. CMU辞書ベースの音素化
2. 基本的なG2Pルール
3. テキスト正規化

### Phase 2: 品質向上
1. Flite統合（オプション）
2. カスタム辞書サポート
3. コンテキスト依存処理

### Phase 3: 多言語拡張
1. 言語検出システム
2. 各言語用ルールエンジン
3. 統合インターフェース

## 利点

1. **法的安全性**: GPL汚染のリスクなし
2. **商用利用**: 制限なし
3. **Unity Asset Store**: 配布可能
4. **シンプル**: ライセンス管理が単純

## 品質保証戦略

GPLツールを使わずに品質を確保する方法：

1. **辞書の充実**
   - CMU辞書（13万語）
   - ユーザー辞書機能
   - ドメイン特化辞書

2. **G2Pアルゴリズム**
   - ルールベース（決定木）
   - 統計的手法（n-gram）
   - ハイブリッドアプローチ

3. **コミュニティ貢献**
   - オープンソース化
   - フィードバック収集
   - 継続的改善