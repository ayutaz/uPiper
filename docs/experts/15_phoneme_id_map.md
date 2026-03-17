# 統一音素IDマップと PhonemeEncoder

## ステータス: Phase 5 実装済み

PUA（Private Use Area）マッピングは `PuaTokenMapper.cs` で87個の固定マッピングとして一元管理される。
言語IDマッピングは `LanguageConstants.cs` で管理される。

## 多言語統一音素IDマップの構造

### IDレイアウト

```
0-9:       共有特殊トークン (_, ^, $, ?, \ue016, \ue017, \ue018, #, [, ])
10+:       言語別音素（重複排除で順次追加）
合計:       173音素（6言語: ja,en,zh,es,fr,pt）/ 183音素（ko含む7言語）

※ ID番号は get_multilingual_id_map() で動的に割当。
  phoneme_id_map として model.onnx.json に含まれる。
```

### 統合ロジック（`get_multilingual_id_map()`）

```python
def get_multilingual_id_map(languages: list[str]) -> dict[str, list[int]]:
    # Phase 1: 共有特殊トークン
    for s in SPECIAL_TOKENS:
        all_symbols.append(normalize(s))  # 重複排除

    # Phase 2: 言語別音素（順序保持）
    for lang in languages:
        for s in LANGUAGE_PHONEMES[lang]:
            if normalize(s) not in seen:  # 共有音素は最初の言語IDを使用
                all_symbols.append(normalize(s))

    # Phase 3: IDマップ構築
    return {symbol: [idx] for idx, symbol in enumerate(all_symbols)}
```

**重要**: Unicode NFC正規化で合成形に統一

## PuaTokenMapper（実装済み）

**実装**: `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/PuaTokenMapper.cs`

piper-plus Python実装（token_mapper.py）およびC++実装との完全互換を保証する固定PUAマッピングテーブル。

### 固定PUAマッピング一覧（87エントリ）

| 範囲 | 言語 | 内容 | エントリ数 |
|------|------|------|-----------|
| `\uE000`-`\uE01C` | 日本語 (JA) | 長母音, 促音, 拗音, 疑問マーカー, 撥音変種 | 29 |
| `\uE01D`-`\uE01E` | 共有 | rr (スペイン語trill), y_vowel (ZH/FR) | 2 |
| `\uE020`-`\uE04A` | 中国語 (ZH) | 有気音, 二重母音, 鼻韻母, 声調マーカー | 43 |
| `\uE04B`-`\uE052` | 韓国語 (KO) | 濃音, 未破裂終声 | 8 |
| `\uE054`-`\uE055` | ES/PT共有 | 破擦音 (tʃ, dʒ) | 2 |
| `\uE056`-`\uE058` | フランス語 (FR) | 鼻母音 | 3 |

### 主要API

```csharp
// トークン→PUA文字変換（未登録なら動的割当）
char ch = PuaTokenMapper.Register("ch");  // → '\uE00E'

// トークンリスト一括変換
List<char> chars = PuaTokenMapper.MapSequence(new[] { "k", "o", "N", "n", "i", "ch", "i", "w", "a" });

// PUA文字→元トークンへの逆変換
string token = PuaTokenMapper.UnmapChar('\uE00E');  // → "ch"

// 固定PUA範囲の判定
bool isFixed = PuaTokenMapper.IsFixedPua('\uE00E');  // → true
```

### 動的割当

固定テーブルに含まれないマルチ文字トークンは `0xE059` 以降に動的割当される。
割当はスレッドセーフ（`lock` で保護）。

## 各言語の音素一覧

### 日本語（55音素）
- 母音: a, i, u, e, o, A, I, U, E, O
- 長母音: a:, i:, u:, e:, o: (PUA: \ue000-\ue004)
- 撥音: N, N_m, N_n, N_ng, N_uvular
- 促音: cl/q (\ue005)
- 子音基本: k, g, t, d, p, b, s, z, h, f, m, n, r, w, y, j, v
- 子音複合: ch, ts, sh, ky, kw, gy, gw, ty, dy, py, by, ny, my, ry, zy, hy

### 英語（46音素、日本語と一部共有）
- 母音: ɑ, æ, ʌ, ə, ɔ, ɛ, ɚ, ɜ, ɪ, ʊ, ɔɪ
- 長さ: ː
- 子音: l, ɡ, ŋ, ɹ, ʃ, ʒ, θ, ð, tʃ, dʒ
- ストレス: ˈ, ˌ
- 句読点・スペース等
- 日本語と共有: b, d, f, h, k, m, n, p, s, t, w, j, v, z

### 中国語（51音素, PUA: \ue020-\ue04a）
- 有気音: pʰ, tʰ, kʰ, tɕ, tɕʰ, ɕ, tʂ, tʂʰ, ʂ, ɻ, tsʰ, x
- 母音: ɤ, y_vowel
- 複合: aɪ, eɪ, aʊ, oʊ, an, ən, aŋ, əŋ, uŋ, ia, iɛ, iou...
- トーン: tone1-tone5

### 韓国語（16音素, PUA: \ue04b-\ue052）
- テンス子音: p͈, t͈, k͈, s͈, t͈ɕ
- 無声終止音: k̚, t̚, p̚
- 母音: ɯ, ɰ
- 子音: ɾ
- 共有（中国語と）: tɕ, tɕʰ, pʰ, tʰ, kʰ

### スペイン語（9独自音素）
- ɲ, ɾ, rr(\ue01d), β, ɣ, x, ʝ, ¡, ¿

### ポルトガル語（12音素、一部英語と共有）
- 鼻母音: ã, ẽ, ĩ, õ, ũ
- ʎ, ʁ, tʃ(英語共有), dʒ(英語共有)
- 句読点: —, –, …

### フランス語（16音素、一部他言語と共有）
- 鼻母音: ɛ̃(\ue056), ɑ̃(\ue057), ɔ̃(\ue058)
- 丸唇: ø, œ, y_vowel
- 開母音: ɔ, ə
- 半子音: ɥ
- ʁ(ポルトガル語共有), ɲ(スペイン語共有)
- 句読点: —, –, …, «, »

## PhonemeEncoder の動作

### IPA/PUA判定（行69）

```csharp
_useIpaMapping = _phonemeToId.ContainsKey("ɕ");
```

- `ɕ` あり → IPAモデル（tsukuyomi-chan, 多言語モデル）
- `ɕ` なし → PUAモデル（ja_JP-test-medium）

### 多言語モデルでの動作

多言語モデルは `phoneme_id_map` に `ɕ` を含む → **IPA判定される**

phoneme_id_map はモデルの `.onnx.json` に含まれるため、PhonemeEncoder は
そのまま多言語モデルの音素マップを処理可能。

PuaTokenMapper が全言語のPUA逆変換（PUA文字 → 元のマルチ文字トークン）を提供するため、
PhonemeEncoder は PuaTokenMapper.UnmapChar() を使用して任意の言語のPUA音素を
元トークンに復元し、phoneme_id_map でIDを取得できる。

## モデルタイプ別の音素数比較

| モデル | 言語数 | 音素数 | 特殊 | 共有 | 固有 |
|--------|--------|--------|------|------|------|
| ja_JP-test-medium | 1 | 65 (特殊10+音素55) | 10 | 55 | 0 |
| tsukuyomi-chan | 1 | 65 (特殊10+音素55) | 10 | 55 | 0 |
| multilingual-6lang | 6 | 173 | 10 | - | - |
| multilingual-7lang (ko含む) | 7 | 183 | 10 | - | - |
