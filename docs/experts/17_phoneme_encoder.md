# PhonemeEncoder 多言語対応設計

## ステータス: Phase 5 実装済み

多言語PUAマッピングは `PuaTokenMapper.cs`（87固定マッピング、全7言語対応）に一元化済み。
言語IDは `LanguageConstants.cs` で管理。PhonemeEncoder は phoneme_id_map ベースの
直接検索と PuaTokenMapper による逆変換の両方に対応。

## クラス設計

**場所**: `Assets/uPiper/Runtime/Core/AudioGeneration/PhonemeEncoder.cs`

```csharp
public class PhonemeEncoder
{
    private readonly Dictionary<string, int> _phonemeToId;    // 音素→ID
    private readonly Dictionary<int, string> _idToPhoneme;    // ID→音素（逆変換）
    private readonly PiperVoiceConfig _config;
    private readonly bool _useIpaMapping;                     // IPA/PUA判定
}
```

## IPA/PUA自動判定

**判定キー**（行69）:
```csharp
_useIpaMapping = _phonemeToId.ContainsKey("ɕ");
```

| モデル | タイプ | 判定結果 |
|--------|--------|---------|
| ja_JP-test-medium | PUA | `false` |
| tsukuyomi-chan | IPA | `true` |
| multilingual-test-medium | IPA | `true` |

**多言語モデルでの影響**: `ɕ` は ID 102 で存在 → IPA判定される → 問題なし

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

## 複文字→IPAマッピング（行141-163）

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

## 音素エンコーディングフロー

各言語のPhonemizerがIPA音素を出力し、PhonemeEncoder がモデルのIDに変換する:

```
言語別Phonemizer出力（IPA音素）
    ↓
PuaTokenMapper.MapSequence()（マルチ文字→PUA圧縮）
    ↓
PhonemeEncoder.Encode()
    ├─ PUAモデル: PUA文字をそのまま phoneme_id_map で検索
    └─ IPAモデル: PUA→元トークン逆変換 → IPA変換 → phoneme_id_map で検索
    ↓
音素ID配列（ONNX `input` テンソルへ）
```

## EOS-likeトークン処理

```csharp
private static readonly HashSet<string> EosLikeTokens = new()
{
    "$", "?", "?!", "?.", "?~"
};
```

最後の音素がEOS-likeなら別途EOSを追加しない（piper-plus #210互換）

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
