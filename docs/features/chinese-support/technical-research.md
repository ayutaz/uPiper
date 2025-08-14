# 中国語サポート実装 - 技術調査レポート

## 概要

中国語音素化実装に必要な技術コンポーネントとライセンス互換性のあるリソースについて調査しました。すべてのコンポーネントはMITライセンスまたは同等のオープンライセンスで利用可能です。

## 1. ピンイン辞書データ

### pypinyin（MIT ライセンス）

- **プロジェクト**: https://github.com/mozillazg/python-pinyin
- **ライセンス**: MIT
- **特徴**:
  - 50万エントリの辞書データ
  - 多音字（heteronym）対応
  - 簡体字・繁体字・注音符号対応
  - フレーズベースの読み選択

### pypinyin-dict（データ拡張）

- **プロジェクト**: https://github.com/mozillazg/pypinyin-dict
- **用途**: pypinyinの内蔵データをカスタマイズ
- **データソース**:
  - pinyin-data（kTGHZ2013.txt, kHanyuPinyin.txt等）
  - phrase-pinyin-data（cc_cedict.txt, zdic_cibs.txt等）

### pinyin-json（JSON形式）

- **プロジェクト**: https://github.com/guoyunhe/pinyin-json
- **特徴**: JSON形式の中国語ピンインデータ
- **データソース**: hanzidb（2018年5月10日取得）

## 2. テキスト分割アルゴリズム

### jieba（MIT ライセンス）

- **ライセンス**: MIT
- **アルゴリズム**:
  - プレフィックス辞書（Trie）構造
  - 有向非巡回グラフ（DAG）構築
  - 動的計画法による最適パス探索
  - 未知語に対するHMMモデル + Viterbiアルゴリズム

### 実装の要点

```python
# jieba風アルゴリズムの基本構造
1. プレフィックス辞書で全可能な単語を検索
2. DAGを構築（各文字位置から可能な単語終了位置）
3. 単語頻度に基づく最大確率パスを動的計画法で計算
4. 未知語はHMMで処理
```

### 動作モード

- **精確モード**: デフォルト、精度重視
- **フルモード**: 全可能な単語をスキャン、高速
- **検索エンジンモード**: 精確モード＋長い単語の再分割

## 3. ピンインからIPAへの変換

### MIT ライセンスリソース（Zenodo）

- **リソース**: https://zenodo.org/records/7525638
- **ライセンス**: MIT
- **内容**:
  - pinyin-ipa-map-NORMAL.json（418マッピング）- 声調なし
  - pinyin-ipa-map-TONE.json（1400マッピング）- 声調付き
  - pypinyin + pinyin-to-ipa使用

### 変換例

```json
// 声調なしマッピング
{
  "ma": "ma",
  "zhong": "ʈʂʊŋ",
  "guo": "kwo"
}

// 声調付きマッピング
{
  "mā": "ma˥",      // 第一声
  "má": "ma˧˥",     // 第二声
  "mǎ": "ma˨˩˦",    // 第三声
  "mà": "ma˥˩"      // 第四声
}
```

### IPA声調記号

- 第一声（高平）: ˥ (55)
- 第二声（上昇）: ˧˥ (35)
- 第三声（低降昇）: ˨˩˦ (214)
- 第四声（下降）: ˥˩ (51)
- 軽声: マークなし

## 4. C#実装への移植戦略

### ピンイン辞書

```csharp
// StreamingAssetsからJSONデータを読み込み
public class PinyinDictionary
{
    // 文字 → ピンイン候補のマッピング
    private Dictionary<char, string[]> charToPinyin;
    
    // フレーズ → ピンインのマッピング（多音字解決用）
    private Dictionary<string, string> phraseToPinyin;
}
```

### テキスト分割

```csharp
// jieba風の最大マッチング法実装
public class ChineseTextSegmenter
{
    private TrieNode prefixDict;  // プレフィックス辞書
    private Dictionary<string, float> wordFreq;  // 単語頻度
    
    public string[] Segment(string text)
    {
        var dag = BuildDAG(text);
        var route = CalculateMaxProbPath(dag);
        return ExtractWords(text, route);
    }
}
```

### IPA変換

```csharp
public class PinyinToIPAConverter
{
    private Dictionary<string, string> pinyinToIPA;
    private Dictionary<string, string> toneMarks;
    
    public string[] ConvertToIPA(string pinyin, int tone)
    {
        var baseIPA = pinyinToIPA[pinyin];
        var toneMarked = ApplyToneMarks(baseIPA, tone);
        return SplitToPhonemes(toneMarked);
    }
}
```

## 5. リソースサイズ見積もり

### 必要なデータファイル

1. **ピンイン辞書**（JSON）: 約2-3MB
   - 単一文字マッピング: ~42,000エントリ
   - フレーズマッピング: ~50,000エントリ

2. **分割用辞書**: 約5MB
   - 単語リスト: ~500,000エントリ
   - 単語頻度データ

3. **IPA変換テーブル**: 約200KB
   - ピンイン→IPAマッピング: ~1,400エントリ
   - 声調マーク規則

**合計**: 約7-8MB（圧縮時: 約3-4MB）

## 6. パフォーマンス考慮事項

### 最適化戦略

1. **辞書の遅延ロード**
   - 初回アクセス時のみロード
   - 使用頻度の高いエントリのキャッシュ

2. **分割結果のキャッシュ**
   - LRUキャッシュで頻出フレーズを保存
   - キャッシュサイズ: 10,000エントリ程度

3. **並列処理**
   - 長文の場合、段落単位で並列処理
   - Unity Job Systemの活用検討

## 7. 実装優先順位

1. **Phase 1**: 基本機能（1-2週間）
   - ピンイン辞書の読み込み
   - 基本的なIPA変換
   - 単純な最長一致分割

2. **Phase 2**: 高度な機能（1週間）
   - jieba風の確率的分割
   - 多音字の文脈判定
   - 数字・記号の正規化

3. **Phase 3**: 最適化（3-5日）
   - キャッシュ実装
   - パフォーマンスチューニング
   - メモリ使用量削減

## 8. ライセンスサマリー

すべての主要コンポーネントはMITライセンスまたは互換性のあるオープンライセンスで利用可能：

- **pypinyin**: MIT
- **jieba**: MIT
- **pinyin-ipa-mapping (Zenodo)**: MIT
- **CC-CEDICT**: CC-BY-SA（辞書データのみ）

GPLライセンスのコンポーネントは不要で、完全にMIT互換の実装が可能です。