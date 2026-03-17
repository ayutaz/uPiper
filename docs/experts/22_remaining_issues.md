# 残課題 技術調査結果

## 1. 中国語ピンイン変換 - dot-net-g2p に既存実装あり

### 重要な発見

**`DotNetG2P.Chinese` (v1.5.0) が既に存在する。**

場所: `C:\Users\yuta\Desktop\Private\dot-net-g2p\src\DotNetG2P.Chinese\`

実装済み機能:
- `ChineseG2PEngine` - メインエンジン（スレッドセーフ）
- `PinyinCharDictionary` - 単字辞書（44,435エントリ, 568KB, EmbeddedResource）
- `PinyinPhraseDictionary` - 熟語辞書（411,958エントリ, 9.14MB, EmbeddedResource）、最長一致検索
- `ToneSandhiProcessor` - 三声連読、一/不の声調変調
- `PinyinToIpa` - IPA変換
- `PinyinParser` - 声母/韻母分離
- バッチ変換API
- UPM package.json 付き（`com.dotnetg2p.chinese`）、.NET Standard 2.1対応

### IPA出力の差異（要対応）

piper-plus と dot-net-g2p で一部のIPA表記が異なる:

| 音素 | piper-plus | dot-net-g2p | 要対応 |
|------|-----------|-------------|--------|
| zh声母 | tʂ | ʈʂ | Yes |
| ch声母 | tʂʰ | ʈʂʰ | Yes |
| ong韻母 | uŋ | ʊŋ | Yes |
| iu韻母 | iou | ioʊ | Yes |
| er韻母 | ɚ | əɻ | Yes |

→ dot-net-g2p 側に **piper-plus互換モード** を追加する必要がある（詳細は 23_dotnetg2p_tasks.md）

### 辞書サイズとプラットフォーム影響

| データ | 生サイズ | gzip圧縮 | メモリ推定 |
|--------|---------|---------|-----------|
| pinyin_char.txt | 568 KB | 223 KB | ~2.5 MB |
| pinyin_phrase.txt | 9.14 MB | 3.19 MB | ~3.9 MB |
| 合計 | 9.71 MB | 3.41 MB | ~6.4 MB |

WebGLではEmbeddedResourceでDLLに含まれるため、初回ダウンロードサイズに影響する。

---

## 2. 韓国語G2P - dot-net-g2p に既存実装あり

### 重要な発見

**`DotNetG2P.Korean` (v1.5.0) が既に存在する。**

場所: `C:\Users\yuta\Desktop\Private\dot-net-g2p\src\DotNetG2P.Korean\`

実装済み機能:
- Hangul音節分解（`KoreanSyllable.TryDecompose` - Unicode演算）
- **6種類の音韻規則**:
  1. `ApplyHTransformations` - ㅎ変換/激音化
  2. `ApplyResyllabification` - 連音化 + 口蓋音化
  3. `ApplyLiquidization` - 流音化
  4. `ApplyNasalization` - 鼻音化
  5. `ApplyTensification` - 硬音化
  6. `ApplyFinalNeutralization` - 終声中和
- 例外辞書（`korean_exceptions.master.tsv`）
- テキスト正規化（`KoreanNormalizer`）
- 179テスト通過
- 純粋C#（MeCab/ネイティブ依存なし）、WebGL/IL2CPP互換

### 未実装部分（唯一の欠損ピース）

**Jamo→IPA変換レイヤーがない。**

- DotNetG2P.Korean は **Jamo** (ㄱ, ㅏ, ㄴ) を出力
- piper-plus korean.py は **IPA** (k, a, n) を出力
- 初声19 + 中声21 + 終声28 の固定マッピングテーブル追加が必要

→ dot-net-g2p 側に **Jamo→IPA変換API** を追加する必要がある（詳細は 23_dotnetg2p_tasks.md）

### g2pk2なしの品質影響

| 規則 | DotNetG2P.Korean | g2pk2 | 影響 |
|------|:---:|:---:|------|
| 連音化 | 済 | 済 | なし |
| 鼻音化 | 済 | 済 | なし |
| 激音化 | 済 | 済 | なし |
| 硬音化(基本) | 済 | 済 | なし |
| 流音化 | 済 | 済 | なし |
| 終声中和 | 済 | 済 | なし |
| 助詞「의」読み | 未 | 済 | 低 |
| 用言語尾硬音化 | 未 | 済 | 中 |

**結論**: 主要規則のカバレッジはg2pk2と同等。TTS品質は実用レベル。

---

## 3. PhonemeEncoder N音素変種の完全なデータフロー

### フロー図

```
[dot-net-g2p] G2PEngine
  出力: 汎用 "N" (変種の区別なし)
    |
    v
[uPiper] DotNetG2PPhonemizer.ApplyNPhonemeRules()
  次の音素を先読みして "N" を変種に置換:
  "N" → "N_m"      (次が m/my/b/by/p/py)
  "N" → "N_n"      (次が n/ny/t/ty/d/dy/ts/ch)
  "N" → "N_ng"     (次が k/ky/kw/g/gy/gw)
  "N" → "N_uvular" (文末、母音前、その他)
    |
    v
[uPiper] OpenJTalkToPiperMapping.PhonemeToPUA
  N_m → U+E019, N_n → U+E01A, N_ng → U+E01B, N_uvular → U+E01C
    |
    v
[uPiper] PhonemeEncoder.MapPhoneme()
  +-- PUAモデル: PUA文字 → phonemeToId でID取得
  |   N_m=ID61, N_n=ID62, N_ng=ID63, N_uvular=ID64
  |
  +-- IPAモデル: puaToPhonemeMap で全変種 → "N" (ID22) に統一
      (後方互換: ASCII "N" を使用、IPA "ɴ" (ID20) ではない)
```

### 処理責務の分離

| コンポーネント | 責務 | N変種の扱い |
|--------------|------|------------|
| dot-net-g2p | MeCab解析→音素生成 | 常に汎用 "N" を出力（変種の区別なし） |
| piper-plus (Python) | 学習データ準備 | `_apply_n_phoneme_rules()` で変種を決定 |
| uPiper (C#) | 推論時の音素化 | `ApplyNPhonemeRules()` で変種を決定（Python側と同一ロジック） |

### 重要ファイル

- `PhonemeEncoder.cs` 行133-137: N変種→PUA定義
- `PhonemeEncoder.cs` 行195-202: PUA→"N"逆変換（IPAモデル用）
- `DotNetG2PPhonemizer.cs` 行472-524: `ApplyNPhonemeRules()`
- `OpenJTalkToPiperMapping.cs` 行218-228: N変種→PUA変換

**注意**: Python/C#のルールは完全一致しており、学習時と推論時で同じN変種が生成される。

---

## 4. LanguageDetector パフォーマンス最適化

### 問題の特定

3つの LanguageDetector 実装が存在:

| 実装 | 方式 | 問題 |
|------|------|------|
| `LanguageDetector.cs` | 4パス正規表現 | 中程度のGCアロケーション |
| `Multilingual\LanguageDetector.cs` | **`regex.IsMatch(c.ToString())`** | **最悪: 文字単位で8回正規表現×文字列生成** |
| docs提案 `UnicodeLanguageDetector` | char範囲チェック | 最適（軽微な改善余地あり） |

### パフォーマンス比較

| 方式 | 1文字あたり | 100文字でのGC | 推奨 |
|------|-----------|-------------|------|
| **char範囲チェック** | **~2-5 ns** | **0 bytes** | **採用** |
| `regex.IsMatch(c.ToString())` | ~100-400 ns | 4-32 KB | 排除 |
| `Regex.Matches()` 全文 | ~50-200 ns | 2-8 KB | 置換 |
| StringBuilder (セグメント構築) | ~5 ns | 1 string/セグメント | 採用 |

### 推奨実装

char範囲チェック + StringBuilder の組み合わせ:

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
private static bool IsKana(char ch)
    => (ch >= '\u3040' && ch <= '\u309F')
    || (ch >= '\u30A0' && ch <= '\u30FF')
    || (ch >= '\u31F0' && ch <= '\u31FF');
```

ポイント:
- `AggressiveInlining` でJIT最適化を促進
- `for` ループ（`foreach` はenumeratorアロケーションのリスク）
- `StringBuilder` でセグメント構築（`List<char>.ToArray()` より効率的）
- 頻出順序で判定（ja/en混合テキストではKana→CJK→Latinの順）
- `const string` で言語コードを定義（文字列インターニング活用）

Python (piper-plus) の正規表現パターンは全て単純な文字クラスのため、
char範囲チェックで100%置換可能。動作の差異なし。
