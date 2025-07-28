# Flite LTSルール C#移植 技術調査報告書

## 概要

本ドキュメントは、Flite（Festival Lite）のLetter-to-Sound（LTS）ルールをC#に移植するための技術調査結果と実装計画をまとめたものです。

## 1. 背景と目的

### 現状の課題
- 現在のFlite統合はネイティブライブラリ（C/C++）に依存
- プラットフォーム別のビルドが必要（Windows/macOS/Linux）
- IL2CPP環境での動作に制限
- ビルドサイズの増大（各プラットフォーム用ライブラリ）

### 移植の目的
- ネイティブライブラリ依存の排除
- 全プラットフォーム対応の実現
- ビルドプロセスの簡素化
- 保守性とデバッグ性の向上

## 2. Flite LTSシステムの分析

### 2.1 アーキテクチャ
```
[入力テキスト]
    ↓
[トークン化]
    ↓
[CMU辞書検索] ←─── 134,000語の発音辞書
    ↓ (未知語)
[LTSルール適用] ←── WFSTベースのルールエンジン
    ↓
[音素列出力]
```

### 2.2 コンポーネント構成

#### 音素テーブル（76音素）
```c
const char * const cmu_lts_phone_table[76] = {
    "epsilon", "eh1", "aa1", "ey1", "aw1", "ax0",
    "ao1", "ay0", "aa0", "ey0", "ae1", ...
};
```

#### 文字インデックス（25,377エントリー）
```c
const cst_lts_addr cmu_lts_letter_index[27] = {
    0,      // a
    5371,   // b
    6048,   // c
    ...
};
```

#### WFSTルールデータ
- 有限状態トランスデューサーによる文字→音素変換
- コンテキスト依存の発音規則
- 自動生成された最適化済みルール

### 2.3 精度特性
- CMU辞書カバー率：一般的な英単語の90%以上
- LTS精度：未知語に対して85-90%
- 総合精度：95%以上（辞書+LTS）

## 3. C#移植実装設計

### 3.1 全体アーキテクチャ

```csharp
namespace uPiper.Core.Phonemizers.Backend.Flite
{
    // メインクラス
    public class FliteLTSPhonemizer : PhonemizerBackendBase
    {
        private CMUDictionary dictionary;
        private FliteLTSEngine ltsEngine;
        private LRUCache<string, string[]> cache;
    }

    // LTSエンジン
    public class FliteLTSEngine
    {
        private readonly PhoneTable phoneTable;
        private readonly LTSRuleSet ruleSet;
        private readonly WFSTProcessor processor;
    }

    // データ構造
    public class LTSRuleSet
    {
        public byte[] RuleData { get; set; }
        public int[] LetterIndex { get; set; }
        public string[] PhoneTable { get; set; }
    }
}
```

### 3.2 処理フロー

```csharp
public async Task<PhonemeResult> PhonemizeAsync(string text)
{
    var tokens = Tokenize(text);
    var phonemes = new List<string>();
    
    foreach (var token in tokens)
    {
        // 1. キャッシュチェック
        if (cache.TryGet(token, out var cached))
        {
            phonemes.AddRange(cached);
            continue;
        }
        
        // 2. 辞書検索（最高精度）
        if (dictionary.TryGetPronunciation(token, out var dictPhonemes))
        {
            cache.Add(token, dictPhonemes);
            phonemes.AddRange(dictPhonemes);
            continue;
        }
        
        // 3. LTSルール適用（未知語）
        var ltsPhonemes = ltsEngine.ProcessWord(token);
        cache.Add(token, ltsPhonemes);
        phonemes.AddRange(ltsPhonemes);
    }
    
    return new PhonemeResult(phonemes);
}
```

### 3.3 データ移植戦略

#### 自動変換スクリプト
```python
# tools/convert_flite_lts.py
import re
import json

def extract_lts_data(c_source_path):
    """FliteのCソースからLTSデータを抽出"""
    # 音素テーブルの抽出
    phone_table = extract_phone_table(c_source)
    
    # インデックステーブルの抽出
    letter_index = extract_letter_index(c_source)
    
    # ルールデータの抽出
    rule_data = extract_rule_data(c_source)
    
    # C#形式で出力
    generate_csharp_data(phone_table, letter_index, rule_data)
```

#### 生成されるC#データ
```csharp
// 自動生成ファイル: FliteLTSData.Generated.cs
namespace uPiper.Core.Phonemizers.Backend.Flite
{
    public static class FliteLTSData
    {
        public static readonly string[] PhoneTable = new[]
        {
            "epsilon", "eh1", "aa1", "ey1", "aw1", "ax0",
            // ... 76音素
        };
        
        public static readonly int[] LetterIndex = new[]
        {
            0, 5371, 6048, 6256, 10649, 10666,
            // ... 27エントリー
        };
        
        public static readonly byte[] CompressedRules = new byte[]
        {
            // 圧縮されたWFSTルールデータ
        };
    }
}
```

### 3.4 最適化手法

#### メモリ効率
- ルールデータの圧縮（gzip）
- 遅延読み込み（Lazy Loading）
- 共有文字列プール

#### 処理速度
- LRUキャッシュ（最大10,000エントリー）
- 並列処理対応（文単位）
- SIMD最適化（.NET 7+）

## 4. 実装計画

### Phase 1: データ移植準備（2日）

#### Day 1
- [ ] Fliteソースコード解析
- [ ] データ抽出スクリプト作成（Python）
- [ ] C#データ構造設計

#### Day 2
- [ ] 自動変換処理実装
- [ ] データ検証ツール作成
- [ ] 単体テスト準備

### Phase 2: コアエンジン実装（3日）

#### Day 3-4
- [ ] WFSTプロセッサー実装
- [ ] ルール適用エンジン
- [ ] 基本動作テスト

#### Day 5
- [ ] 最適化実装
- [ ] パフォーマンステスト
- [ ] エラーハンドリング

### Phase 3: システム統合（2日）

#### Day 6
- [ ] 既存システムとの統合
- [ ] フォールバック処理
- [ ] 統合テスト

#### Day 7
- [ ] ベンチマーク測定
- [ ] ドキュメント作成
- [ ] コードレビュー対応

## 5. 期待される成果

### 5.1 精度目標
- 辞書カバー単語：100%（CMU辞書使用）
- 未知語精度：90%以上（Flite LTSルール）
- 総合精度：95%以上

### 5.2 パフォーマンス目標
- 処理速度：1,000単語/秒以上
- メモリ使用量：10MB以下
- 起動時間：100ms以下

### 5.3 メリット
1. **開発効率**
   - ネイティブビルド不要
   - 単一コードベース
   - Visual Studioでのデバッグ可能

2. **配布サイズ**
   - ネイティブライブラリ削除で約5MB削減
   - 全プラットフォーム共通

3. **保守性**
   - 純粋なC#実装
   - ユニットテスト容易
   - CI/CD簡素化

## 6. リスクと対策

### 6.1 技術的リスク
| リスク | 影響度 | 対策 |
|--------|--------|------|
| WFSTアルゴリズムの複雑性 | 高 | Fliteコードの詳細分析、段階的実装 |
| パフォーマンス劣化 | 中 | プロファイリング、最適化、キャッシュ活用 |
| 精度低下 | 高 | 徹底的なテスト、Fliteとの比較検証 |

### 6.2 スケジュールリスク
- バッファ期間：各フェーズ+1日
- 並行作業：ドキュメント作成を随時実施
- 早期検証：Day 3時点でのGo/No-Go判断

## 7. 成功基準

### 必須要件
- [ ] Fliteと同等の精度（90%以上）
- [ ] 全プラットフォーム動作
- [ ] 既存APIとの互換性

### 推奨要件
- [ ] 処理速度がネイティブ版の80%以上
- [ ] メモリ使用量が10MB以下
- [ ] 包括的なテストカバレッジ（80%以上）

## 8. まとめ

Flite LTSルールのC#移植は技術的に実現可能であり、以下の理由から推奨されます：

1. **ライセンス**: BSDライセンスで商用利用可能
2. **精度**: 辞書+LTSで95%以上を達成可能
3. **保守性**: 純粋なC#実装による開発効率向上
4. **互換性**: 全Unityプラットフォーム対応

実装には約7日間必要ですが、長期的なメンテナンスコスト削減と開発効率向上を考慮すると、投資価値があると判断します。

## 付録A: 参考資料

- [Flite公式ドキュメント](http://www.festvox.org/flite/)
- [CMU Pronouncing Dictionary](http://www.speech.cs.cmu.edu/cgi-bin/cmudict)
- [Letter-to-Sound Rules in Flite](http://www.festvox.org/flite/doc/flite_9.html)

## 付録B: 用語集

- **LTS (Letter-to-Sound)**: 文字列から音素列への変換規則
- **WFST (Weighted Finite State Transducer)**: 重み付き有限状態トランスデューサー
- **CMU Dictionary**: カーネギーメロン大学の英語発音辞書
- **ARPABET**: 英語音素の表記システム
- **G2P (Grapheme-to-Phoneme)**: 文字から音素への変換