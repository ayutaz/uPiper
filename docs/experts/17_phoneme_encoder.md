# PhonemeEncoder 多言語対応設計

## 現在のクラス設計

**場所**: `Assets/uPiper/Runtime/Core/AudioGeneration/PhonemeEncoder.cs` (603行)

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

## PUA→元音素マッピング（行167-203）

| PUA | 元音素 | 用途 |
|-----|--------|------|
| \ue000-\ue004 | a:, i:, u:, e:, o: | 長母音 |
| \ue005 | cl | 促音 |
| \ue006-\ue015 | ky, kw, gy, gw, ty, dy, py, by, ch, ts, sh, zy, hy, ny, my, ry | 複合子音 |
| \ue016-\ue018 | ?!, ?., ?~ | 疑問マーカー |
| \ue019-\ue01c | N (N_m, N_n, N_ng, N_uvular) | 撥音変種 |

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

## 多言語対応で必要な変更

### 1. 言語別マッピングテーブル（行102-163）

```csharp
private static class LanguagePhonemeMapping
{
    public static class Japanese
    {
        public static readonly Dictionary<string, string> MultiCharToPua = new()
        {
            ["ch"] = "\ue00e", ["ts"] = "\ue00f", // ... 既存
        };
        public static readonly Dictionary<string, string> MultiCharToIpa = new()
        {
            ["ch"] = "tɕ", ["sh"] = "ʃ", // ... 既存
        };
    }

    public static class Chinese
    {
        public static readonly Dictionary<string, string> PuaMapping = new()
        {
            ["pʰ"] = "\ue020", ["tʂ"] = "\ue025", ["tɕ"] = "\ue023", // ...
        };
    }

    public static class Korean
    {
        public static readonly Dictionary<string, string> PuaMapping = new()
        {
            ["p͈"] = "\ue04b", ["t͈"] = "\ue04c", ["k͈"] = "\ue04d", // ...
        };
    }
}
```

### 2. IsESpeakModel() の拡張（行346-352）

**現在**: 二者択一（eSpeak or OpenJTalk）

```csharp
// 改善案
private PhonemizationMode DetectPhonemizationMode()
{
    if (!string.IsNullOrEmpty(_config.PhonemeType))
    {
        return _config.PhonemeType.ToLower() switch
        {
            "espeak" => PhonemizationMode.ESpeakWithPad,
            "openjtalk" => PhonemizationMode.OpenJTalkNoPad,
            "multilingual" => PhonemizationMode.MultilingualWithPad,
            _ => PhonemizationMode.Generic
        };
    }
    return _config.Language switch
    {
        "ja" => PhonemizationMode.OpenJTalkNoPad,
        "en" => PhonemizationMode.ESpeakWithPad,
        _ => PhonemizationMode.MultilingualWithPad
    };
}
```

### 3. MapPhoneme の言語対応（行357-388）

```csharp
private string MapPhoneme(string phoneme, string language = null)
{
    // 多言語モデルの場合: phoneme_id_map から直接検索
    if (_phonemeToId.ContainsKey(phoneme))
        return phoneme;

    // 言語別フォールバック
    if (_useIpaMapping)
    {
        // PUA→元音素→IPA変換（既存ロジック）
        if (puaToPhonemeMap.TryGetValue(phoneme, out var orig))
            if (multiCharToIpaMap.TryGetValue(orig, out var ipa))
                return ipa;
    }
    return phoneme;
}
```

### 4. EOS-likeトークン処理

```csharp
private static readonly HashSet<string> EosLikeTokens = new()
{
    "$", "?", "?!", "?.", "?~"
};
```

最後の音素がEOS-likeなら別途EOSを追加しない（piper-plus #210互換）

## Prosodyエンコーディングの言語対応

**現在**: 日本語アクセント型のみ（A1/A2/A3）

多言語モデルでは:
- 日本語: A1/A2/A3 を正常に渡す
- 他言語: A1=A2=A3=0 を渡す（モデル内部でゼロ化される）

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
