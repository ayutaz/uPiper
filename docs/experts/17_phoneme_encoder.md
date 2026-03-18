# PhonemeEncoder 多言語対応設計

## ステータス: Phase 6 実装済み（多言語モデル互換性修正含む）

多言語PUAマッピングは `PuaTokenMapper.cs`（87固定マッピング、全7言語対応）に一元化済み。
言語IDは `LanguageConstants.cs` で管理。PhonemeEncoder は phoneme_id_map ベースの
直接検索と PuaTokenMapper による逆変換の両方に対応。

Phase 6 で追加された多言語モデル互換性修正:
- `NeedsInterspersePadding()` メソッド追加（`IsESpeakModel()` を置き換え）
- `MapPhoneme()` の多言語パススルー対応
- intersperse PAD の多言語モデルサポート
- skip-after-pad ロジック（PAD ID の二重挿入防止）
- N variant 保持用の `puaToPhonemeMapMultilingual` 追加

## クラス設計

**場所**: `Assets/uPiper/Runtime/Core/AudioGeneration/PhonemeEncoder.cs`

```csharp
public class PhonemeEncoder
{
    private readonly Dictionary<string, int> _phonemeToId;    // 音素→ID
    private readonly Dictionary<int, string> _idToPhoneme;    // ID→音素（逆変換）
    private readonly PiperVoiceConfig _config;
    private readonly bool _useIpaMapping;                     // IPA/PUA判定
    private readonly bool _isMultilingualModel;               // 多言語モデル判定
    private readonly int _padId;                              // キャッシュ済みPAD ID
}
```

## モデルタイプ判定

### 多言語モデル判定（`_isMultilingualModel`）

コンストラクタで `PhonemeType` から判定（IPA判定より先に実行）:
```csharp
_isMultilingualModel = !string.IsNullOrEmpty(_config.PhonemeType)
    && _config.PhonemeType.Equals("multilingual", StringComparison.OrdinalIgnoreCase);
```

### IPA/PUA自動判定（`_useIpaMapping`）

多言語モデルの場合はIPA判定をスキップ。音素は既にモデルのネイティブフォーマットであるため:
```csharp
_useIpaMapping = !_isMultilingualModel && _phonemeToId.ContainsKey("ɕ");
```

| モデル | PhonemeType | `_isMultilingualModel` | `_useIpaMapping` |
|--------|-------------|----------------------|-----------------|
| ja_JP-test-medium | openjtalk | `false` | `false` (PUA) |
| tsukuyomi-chan | espeak | `false` | `true` (IPA) |
| multilingual-test-medium | multilingual | `true` | `false` (パススルー) |

**多言語モデルでの動作**: `ɕ` は phoneme_id_map に存在するが、`_isMultilingualModel` が `true` のため
IPA判定は `false` になる。音素変換は行わず、phoneme_id_map への直接検索のみ。

### `NeedsInterspersePadding()` メソッド

eSpeak方式および multilingual方式の場合に intersperse PAD 挿入が必要かを判定する。
従来の `IsESpeakModel()` を置き換え、multilingual モデルもカバーする。

```csharp
private bool NeedsInterspersePadding()
{
    if (!string.IsNullOrEmpty(_config.PhonemeType))
        return _config.PhonemeType.Equals("espeak", StringComparison.OrdinalIgnoreCase)
            || _config.PhonemeType.Equals("multilingual", StringComparison.OrdinalIgnoreCase);
    return !(_config.VoiceId != null && _config.VoiceId.Contains("ja_JP"));
}
```

| PhonemeType | NeedsInterspersePadding | 備考 |
|-------------|------------------------|------|
| openjtalk | `false` | ja_JP-test-medium等 |
| espeak | `true` | tsukuyomi-chan等 |
| multilingual | `true` | 多言語モデル |
| (未指定) | VoiceIdにja_JPが含まれなければ`true` | フォールバック |

## PUA→元音素マッピング（PuaTokenMapper.cs で一元管理）

**実装**: `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/PuaTokenMapper.cs`

従来 PhonemeEncoder 内にハードコードされていた日本語PUAマッピングに加え、
PuaTokenMapper が全7言語（JA, EN, ZH, KO, ES, FR, PT）の87固定マッピングを提供する。

### 日本語PUAマッピング（既存、PuaTokenMapper にも含まれる）

| PUA | 元音素 | 用途 |
|-----|--------|------|
| \ue000-\ue004 | a:, i:, u:, e:, o: | 長母音 |
| \ue005 | cl | 促音 |
| \ue006-\ue015 | ky, kw, gy, gw, ty, dy, py, by, ch, ts, sh, zy, hy, ny, my, ry | 複合子音 |
| \ue016-\ue018 | ?!, ?., ?~ | 疑問マーカー |
| \ue019-\ue01c | N_m, N_n, N_ng, N_uvular | 撥音変種 |

### 多言語PUAマッピング（Phase 5 追加）

| PUA範囲 | 言語 | 内容 | エントリ数 |
|---------|------|------|-----------|
| \ue01d-\ue01e | 共有 | rr, y_vowel | 2 |
| \ue020-\ue04a | 中国語 | 有気音, 二重母音, 鼻韻母, 声調 | 43 |
| \ue04b-\ue052 | 韓国語 | 濃音, 未破裂終声 | 8 |
| \ue054-\ue055 | ES/PT共有 | 破擦音 | 2 |
| \ue056-\ue058 | フランス語 | 鼻母音 | 3 |

PhonemeEncoder は `PuaTokenMapper.UnmapChar()` を使用して任意のPUA文字を元トークンに
逆変換し、その後 phoneme_id_map でIDを取得できる。

## MapPhoneme() — 音素マッピング戦略

`MapPhoneme()` はモデルタイプに応じて3つの分岐で音素を変換する。

### 分岐ロジック

```
MapPhoneme(phoneme)
  ├── _useIpaMapping == true  → IPA変換（PUA→元音素→IPA）
  ├── !_useIpaMapping && multiCharPhonemeMap にヒット → PUA変換
  └── どちらでもない（多言語モデル含む） → パススルー（phoneme をそのまま返す）
```

**多言語モデルでのパススルー**: `_useIpaMapping` は `false`、かつ多言語モデルの音素は
`multiCharPhonemeMap` に含まれない形式で渡されるため、`return phoneme` で素通りする。
MultilingualPhonemizer が既にモデルのネイティブフォーマット（phoneme_id_map のキーと一致する形式）で
音素を出力しているため、追加の変換は不要。

### 複文字→IPAマッピング（multiCharToIpaMap）

| 複文字 | IPA | 備考 |
|--------|-----|------|
| ch | tɕ | ty と同じIPA |
| ts | ts | IPAモデルに"ts"がない場合はt+sに分割 |
| sh | ʃ | ɕ(ID 18)ではなくʃ(ID 42)を使用 |
| cl | q | glottal stop |
| ky | kʲ | |
| gy | ɡʲ | |
| ny | ɲ | スペイン語と共有 |
| ry | ɽ | |

**重要**: `sh` → `ʃ`（ID 42）であり `ɕ`（ID 18）ではない（学習データとの整合性）

## 多言語対応の共有コンポーネント

### PuaTokenMapper（実装済み）

**場所**: `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/PuaTokenMapper.cs`

全言語のPUAマッピングを統一管理。piper-plus Python/C++実装と完全互換。

```csharp
// PUA文字→元トークン逆変換
string token = PuaTokenMapper.UnmapChar('\uE023');  // → "tɕ" (中国語)

// トークン→PUA文字変換
char ch = PuaTokenMapper.Register("tone1");  // → '\uE046'

// 固定PUA範囲判定（\uE000..\uE058）
bool isFixed = PuaTokenMapper.IsFixedPua(ch);
```

### LanguageConstants（実装済み）

**場所**: `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/LanguageConstants.cs`

言語ID（ONNX `lid` テンソル用）と言語コードの双方向変換を提供。

```csharp
// 言語コード→言語ID
int id = LanguageConstants.GetLanguageId("zh");  // → 2

// 言語ID→言語コード
string code = LanguageConstants.GetLanguageCode(2);  // → "zh"

// 言語グループ判定
bool isLatin = LanguageConstants.IsLatinLanguage("fr");  // → true
bool isCjk = LanguageConstants.IsCjkLanguage("ko");      // → true
```

### 言語IDマッピング

| 言語ID | コード | 定数 |
|--------|-------|------|
| 0 | ja | `LanguageConstants.LanguageIdJapanese` |
| 1 | en | `LanguageConstants.LanguageIdEnglish` |
| 2 | zh | `LanguageConstants.LanguageIdChinese` |
| 3 | es | `LanguageConstants.LanguageIdSpanish` |
| 4 | fr | `LanguageConstants.LanguageIdFrench` |
| 5 | pt | `LanguageConstants.LanguageIdPortuguese` |
| 6 | ko | `LanguageConstants.LanguageIdKorean` |

## Intersperse PAD 動作

### 概要

`NeedsInterspersePadding()` が `true` のモデル（espeak/multilingual）では、piper-plus の
`post_process_ids` と同じ形式で音素間に PAD トークンを挿入する。

### エンコード結果のID配列構造

**PADなし（openjtalk モデル）**:
```
[BOS, ph1, ph2, ph3, ..., EOS]
```

**PADあり（espeak/multilingual モデル）**:
```
[BOS, PAD, ph1, PAD, ph2, PAD, ph3, PAD, ..., EOS]
```

### skip-after-pad ロジック

PAD自体がエンコードされた直後に再度PADを追加すると `[..., PAD, PAD, ...]` のような
連続PADが発生する。これを防ぐため、エンコード済みの音素IDが PAD ID と一致する場合は
追加のPAD挿入をスキップする:

```csharp
// eSpeak/multilingual方式では各音素の後にPADを追加
// Skip if the phoneme itself is already PAD (ID 0) to prevent triple-zero sequences
if (needsInterspersePad && phonemeId != _padId)
{
    AddPadToken(ids, expandedA1, expandedA2, expandedA3);
}
```

`_padId` はコンストラクタでキャッシュされ、ループ内の毎回の辞書検索を回避する。

## N variant 保持（多言語モデル）

### 単一言語モデル vs 多言語モデル

| PUA | 単一言語モデル (`puaToPhonemeMap`) | 多言語モデル (`puaToPhonemeMapMultilingual`) |
|-----|----------------------------------|-------------------------------------------|
| \ue019 | → `N`（全て ASCII "N" に統合） | → `N_m`（bilabial 保持） |
| \ue01a | → `N` | → `N_n`（alveolar 保持） |
| \ue01b | → `N` | → `N_ng`（velar 保持） |
| \ue01c | → `N` | → `N_uvular`（uvular 保持） |

**理由**: 単一言語モデルは撥音バリアントを区別しない（すべて ID 22 = "N"）。
多言語モデルは各バリアントに個別IDを持つため、`puaToPhonemeMapMultilingual` で
元の形式を保持して phoneme_id_map で正しいIDを取得する。

## 音素エンコーディングフロー

各言語のPhonemizerがIPA音素を出力し、PhonemeEncoder がモデルのIDに変換する:

```
言語別Phonemizer出力（IPA音素）
    ↓
PuaTokenMapper.MapSequence()（マルチ文字→PUA圧縮）
    ↓
PhonemeEncoder.Encode()
    ├─ PUAモデル: PUA文字をそのまま phoneme_id_map で検索
    ├─ IPAモデル: PUA→元トークン逆変換 → IPA変換 → phoneme_id_map で検索
    └─ 多言語モデル: パススルー → phoneme_id_map で直接検索
    ↓
音素ID配列（ONNX `input` テンソルへ）
```

## EOS-likeトークン処理

PhonemeEncoder の EosLikeTokens（ASCII形式のみ）:
```csharp
private static readonly HashSet<string> EosLikeTokens = new()
{
    "$", "?", "?!", "?.", "?~"
};
```

最後の音素がEOS-likeなら別途EOSを追加しない（piper-plus #210互換）。

**注意**: MultilingualPhonemizer 側の EosLikeTokens には PUA 形式も含まれる
（`\ue016`, `\ue017`, `\ue018`）。詳細は [13_multilingual_phonemizer.md](13_multilingual_phonemizer.md) 参照。

## Prosodyエンコーディングの言語対応

多言語モデルでは:
- 日本語: A1/A2/A3 を正常に渡す
- 他言語: A1=A2=A3=0 を渡す（モデル内部の `prosody_language_ids` でゼロ化される）

```csharp
public ProsodyEncodingResult EncodeWithProsody(
    string[] phonemes,
    int[] prosodyA1, int[] prosodyA2, int[] prosodyA3,
    string language = "ja")
{
    // 日本語以外はProsodyをゼロ化
    if (language != "ja")
    {
        prosodyA1 = new int[phonemes.Length];
        prosodyA2 = new int[phonemes.Length];
        prosodyA3 = new int[phonemes.Length];
    }
    // ... 既存ロジック
}
```
