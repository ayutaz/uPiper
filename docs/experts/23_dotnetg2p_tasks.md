# dot-net-g2p 側タスク一覧

本ドキュメントは、uPiper 多言語対応のために dot-net-g2p リポジトリ (`ayutaz/dot-net-g2p`) 側で
対応が必要なタスクをまとめたものです。

## タスク概要

| # | パッケージ | タスク | 優先度 | 難易度 | 工数見積 |
|---|-----------|--------|--------|--------|---------|
| 1 | DotNetG2P.Chinese | piper-plus互換IPA出力追加 | **高** | 中 | 2-3日 |
| 2 | DotNetG2P.Chinese | PUAマッピング対応 | 高 | 低 | 1日 |
| 3 | DotNetG2P.Chinese | Prosody情報出力API | 中 | 低 | 1日 |
| 4 | DotNetG2P.Korean | Jamo→IPA変換API追加 | **高** | 低 | 1-2日 |
| 5 | DotNetG2P.Korean | PUAマッピング対応 | 高 | 低 | 1日 |
| 6 | DotNetG2P.Korean | Prosody情報出力API | 中 | 低 | 1日 |
| 7 | DotNetG2P.Chinese | WebGL辞書ロード最適化 | 低 | 中 | 2-3日 |

---

## タスク詳細

### タスク1: DotNetG2P.Chinese - piper-plus互換IPA出力追加

**背景**: 既存の `PinyinToIpa.cs` の IPA 出力が piper-plus のモデル学習時の IPA と一部異なる。

**修正が必要な IPA マッピング**:

| ピンイン | 現在の出力 (dot-net-g2p) | 期待値 (piper-plus) | 備考 |
|---------|------------------------|-------------------|------|
| zh | ʈʂ | tʂ | retroflex表記の違い |
| ch | ʈʂʰ | tʂʰ | 同上 |
| -ong | ʊŋ | uŋ | 母音の違い |
| -iu | ioʊ | iou | 二重母音の違い |
| er | əɻ | ɚ | r-colored schwa表記 |
| -iong | iʊŋ | iuŋ | ong同様 |

**実装方針**:
- `PinyinToIpa.cs` に `PiperCompatible` モード追加
- または新クラス `PinyinToPiperIpa.cs` を作成
- piper-plus の `chinese.py` の `_INITIAL_TO_IPA`, `_FINAL_TO_IPA` テーブルと完全一致させる

**参照ファイル**:
- piper-plus: `src/python/piper_train/phonemize/chinese.py` (行45-126)
- dot-net-g2p: `src/DotNetG2P.Chinese/Conversion/PinyinToIpa.cs`

---

### タスク2: DotNetG2P.Chinese - PUAマッピング対応

**背景**: piper-plus の多言語モデルは中国語音素にPUA文字を使用する（0xE020-0xE04A）。

**実装内容**:
- piper-plus の `token_mapper.py` FIXED_PUA_MAPPING (中国語部分) に対応する変換メソッド追加
- `ChineseG2PEngine.ToPiperPhonemes(string text) -> string[]` のようなAPI

**PUAマッピング** (0xE020-0xE04A, 43エントリ):
```
0xE020: pʰ, 0xE021: tʰ, 0xE022: kʰ, 0xE023: tɕ, 0xE024: tɕʰ,
0xE025: tʂ, 0xE026: tʂʰ, 0xE027: ɕ, 0xE028: ʂ, 0xE029: ɻ,
0xE02A: tsʰ, ...（以下省略、token_mapper.py参照）
```

---

### タスク3: DotNetG2P.Chinese - Prosody情報出力API

**背景**: piper-plus の中国語Phonemizerは `phonemize_with_prosody()` で声調・位置情報を返す。

**実装内容**:
```csharp
// 新規API
public (string[] phonemes, int[] a1, int[] a2, int[] a3)
    PhonemizeWithProsody(string text);
```

- a1: 声調番号 (1-5)
- a2: 語内のモーラ位置
- a3: 語の長さ

**参照**: piper-plus `chinese.py` 行426付近の `ProsodyInfo(a1=tone, a2=syl_pos, a3=word_len)`

---

### タスク4: DotNetG2P.Korean - Jamo→IPA変換API追加

**背景**: 既存の `KoreanG2PEngine` は Jamo (ㄱ, ㅏ, ㄴ) を出力するが、piper-plus モデルは IPA (k, a, n) を入力とする。

**実装内容**:
```csharp
// 新規API
public string[] ToIpaPhonemes(string text);
```

**マッピングテーブル** (piper-plus `korean.py` から移植):

初声 (19エントリ):
```
ㄱ→k, ㄲ→k͈, ㄴ→n, ㄷ→t, ㄸ→t͈, ㄹ→ɾ, ㅁ→m, ㅂ→p, ㅃ→p͈,
ㅅ→s, ㅆ→s͈, ㅇ→(empty), ㅈ→tɕ, ㅉ→t͈ɕ, ㅊ→tɕʰ, ㅋ→kʰ, ㅌ→tʰ, ㅍ→pʰ, ㅎ→h
```

中声 (21エントリ):
```
ㅏ→a, ㅐ→ɛ, ㅑ→ja, ㅒ→jɛ, ㅓ→ʌ, ㅔ→e, ㅕ→jʌ, ㅖ→je,
ㅗ→o, ㅘ→wa, ㅙ→wɛ, ㅚ→we, ㅛ→jo, ㅜ→u, ㅝ→wʌ, ㅞ→we,
ㅟ→wi, ㅠ→ju, ㅡ→ɯ, ㅢ→ɰi, ㅣ→i
```

終声 (28エントリ):
```
(none)→(none), ㄱ→k̚, ㄲ→k̚, ㄳ→k̚, ㄴ→n, ㄵ→n, ㄶ→n,
ㄷ→t̚, ㄹ→l, ㄺ→l, ㄻ→m, ㄼ→l, ㄽ→l, ㄾ→l, ㄿ→l, ㅀ→l,
ㅁ→m, ㅂ→p̚, ㅄ→p̚, ㅅ→t̚, ㅆ→t̚, ㅇ→ŋ, ㅈ→t̚, ㅊ→t̚,
ㅋ→k̚, ㅌ→t̚, ㅍ→p̚, ㅎ→(none)
```

**参照**: piper-plus `korean.py` 行38-123

---

### タスク5: DotNetG2P.Korean - PUAマッピング対応

**背景**: piper-plus の多言語モデルは韓国語音素にPUA文字を使用する（0xE04B-0xE052）。

**PUAマッピング** (8エントリ):
```
0xE04B: p͈, 0xE04C: t͈, 0xE04D: k͈, 0xE04E: s͈,
0xE04F: t͈ɕ, 0xE050: k̚, 0xE051: t̚, 0xE052: p̚
```

中国語と共有するPUA:
```
0xE020: pʰ, 0xE021: tʰ, 0xE022: kʰ, 0xE023: tɕ, 0xE024: tɕʰ
```

---

### タスク6: DotNetG2P.Korean - Prosody情報出力API

**実装内容**:
```csharp
public (string[] phonemes, int[] a1, int[] a2, int[] a3)
    ToIpaPhonemesWithProsody(string text);
```

- a1: 0（固定）
- a2: 0（固定）
- a3: 音節数（`max(syllable_count, 1)`）

**参照**: piper-plus `korean.py` 行253

---

### タスク7: DotNetG2P.Chinese - WebGL辞書ロード最適化（低優先度）

**背景**: 中国語辞書（特に熟語辞書 9.14MB）が EmbeddedResource として DLL に含まれるため、WebGL の初回ダウンロードサイズに影響する。

**選択肢**:
- A: EmbeddedResource のまま（シンプル、DLLサイズ +9MB）
- B: StreamingAssets + 非同期読み込み（日本語MeCab辞書と同パターン）
- C: gzip圧縮した EmbeddedResource（3.4MBに縮小、起動時展開コスト）

**推奨**: まずは A（EmbeddedResource）で進め、WebGL パフォーマンスが問題になった場合に B に移行。

---

## uPiper 側で対応するタスク（参考）

dot-net-g2p 側のタスク完了後、uPiper 側で以下の対応が必要:

| タスク | 内容 | 依存 |
|--------|------|------|
| ChinesePhonemizerBackend 実装 | DotNetG2PPhonemizer と同パターン | タスク1,2,3 |
| KoreanPhonemizerBackend 実装 | DotNetG2PPhonemizer と同パターン | タスク4,5,6 |
| PhonemeEncoder 中国語PUA対応 | multiCharPhonemeMap 拡張 | タスク2 |
| PhonemeEncoder 韓国語PUA対応 | multiCharPhonemeMap 拡張 | タスク5 |
| LanguageDetector Hangul追加 | U+AC00-D7AF等の範囲追加 | なし |

---

## 実装順序の推奨

```
Phase 1 (dot-net-g2p):
  タスク4 (Korean Jamo→IPA) → タスク5 (Korean PUA) → タスク6 (Korean Prosody)
  ※ 韓国語は変更量が少なく、テストも既存179件ベースで拡張しやすい

Phase 2 (dot-net-g2p):
  タスク1 (Chinese IPA互換) → タスク2 (Chinese PUA) → タスク3 (Chinese Prosody)
  ※ 中国語はIPA差異の調整が必要で、慎重なテストが必要

Phase 3 (uPiper):
  ChinesePhonemizerBackend + KoreanPhonemizerBackend 実装
  PhonemeEncoder 拡張
  LanguageDetector Hangul 対応
```
