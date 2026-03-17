# MultilingualPhonemizer C#移植設計

## piper-plus MultilingualPhonemizer 概要

### クラス構成

```
MultilingualPhonemizer
  ├── _languages: list[str]              # サポート言語リスト
  ├── _default_latin_language: str       # ラテン文字のデフォルト言語
  ├── _detector: UnicodeLanguageDetector # Unicode範囲言語検出
  ├── _id_map: dict[str, list[int]]      # 統一音素IDマップ（遅延初期化）
  └── _last_eos: str                     # 最後のEOSマーカー（スレッド非安全）

BilingualPhonemizer(MultilingualPhonemizer)
  └── ["ja", "en"] 固定のサブクラス（後方互換性）
```

## phonemize() 処理フロー

```
入力テキスト
  ↓
1. _segment_text_multilingual(text, detector)
   ├── has_kana(text) で事前スキャン（CJK曖昧性解決用）
   ├── 文字単位で detect_char() → 言語判定
   ├── 言語切り替え時にセグメント作成
   └── 中立文字（スペース、数字等）は前セグメントに吸収
   → List[(lang, text_segment)]
  ↓
2. セグメント別処理
   for lang, segment_text in segments:
     phonemizer = get_phonemizer(lang)  # レジストリから取得
     phonemes = phonemizer.phonemize(segment_text)
     → セグメント間のBOS/EOS除去
     → 音素を連結
  ↓
3. 出力: 統一音素配列
```

## phonemize_with_prosody() 処理フロー

```
入力テキスト
  ↓
1. セグメント分割（同上）
  ↓
2. セグメント別 phonemize_with_prosody()
   for lang, segment_text in segments:
     phonemes, prosody = phonemizer.phonemize_with_prosody(segment_text)
     ├── BOS/EOSトークンを削除（重複防止）
     │   削除対象: ^, $, ?, ?!, ?., ?~
     ├── 最後のEOSを _last_eos に保存
     └── stripped phonemes + prosody を連結
  ↓
3. 出力: (all_phonemes, all_prosody)
```

## post_process_ids() 処理フロー

```
入力: phoneme_ids, prosody_features, phoneme_id_map
  ↓
1. Inter-phoneme パディング挿入
   各非ゼロIDの後に PAD (ID=0) を挿入
   例: [a, b, c] → [a, 0, b, 0, c, 0]
  ↓
2. BOS/EOS ラッピング
   BOS (^) 追加: [^, 0, a, 0, b, 0, c, 0]
   EOS (_last_eos) 追加: [^, 0, a, 0, b, 0, c, 0, $]
  ↓
3. Prosody配列も同サイズに拡張（PAD/BOS/EOS位置はNone）
```

## _last_eos スレッド安全性問題

### 問題

```python
# phonemize_with_prosody() で設定
self._last_eos = last_eos  # インスタンス変数書き込み

# post_process_ids() で読み込み
eos_ids = phoneme_id_map.get(self._last_eos, ...)
```

並行実行時に別スレッドの値を読み込むリスクがある。

### C#での解決策（推奨: 戻り値に含める）

```csharp
// _last_eos をインスタンス変数にせず、戻り値に含める
private (string[] phonemes, ProsodyInfo[] prosody, string lastEos)
    PhonemizeWithProsodyInternal(string text);

// post_process_ids は外部から lastEos を受け取る
public (int[] ids, ProsodyInfo[] prosody) PostProcessIds(
    int[] phonemeIds,
    ProsodyInfo[] prosodyFeatures,
    Dictionary<string, int[]> phonemeIdMap,
    string lastEos = "$");
```

## C# クラス設計案

```csharp
namespace uPiper.Core.Phonemizers.Multilingual
{
    public class MultilingualPhonemizer : IDisposable
    {
        private readonly List<string> _languages;
        private readonly string _defaultLatinLanguage;
        private readonly UnicodeLanguageDetector _detector;
        private Dictionary<string, int[]> _idMap;  // 遅延初期化

        public MultilingualPhonemizer(
            List<string> languages,
            string defaultLatinLanguage = "en");

        public List<string> Languages { get; }

        // 音素化
        public async Task<string[]> PhonemizeAsync(string text);

        // Prosody付き音素化
        public async Task<MultilingualPhonemizeResult> PhonemizeWithProsodyAsync(string text);

        // ID後処理（パディング + BOS/EOS）
        public PostProcessResult PostProcessIds(
            int[] phonemeIds,
            ProsodyInfo[] prosodyFeatures,
            Dictionary<string, int[]> phonemeIdMap,
            string lastEos = "$");

        // 統一音素IDマップ取得
        public Dictionary<string, int[]> GetPhonemeIdMap();
    }

    public struct MultilingualPhonemizeResult
    {
        public string[] Phonemes;
        public ProsodyInfo[] Prosody;
        public string LastEos;
    }

    public struct PostProcessResult
    {
        public int[] PhonemeIds;
        public ProsodyInfo[] ProsodyFeatures;
    }
}
```

## 既存 MixedLanguagePhonemizer との統合方針

### 段階的移行

1. **Phase 1**: MultilingualPhonemizer を独立実装（並行稼働）
2. **Phase 2**: MixedLanguagePhonemizer を MultilingualPhonemizer(["ja","en"]) のラッパーに
3. **Phase 3**: MixedLanguagePhonemizer を `[Obsolete]` 指定

### 互換性マトリックス

| 機能 | MixedLanguagePhonemizer | MultilingualPhonemizer |
|------|------------------------|----------------------|
| JA/EN 二言語 | Yes | Yes |
| N言語対応 | No | Yes |
| Prosody情報 | No | Yes |
| BOS/EOS処理 | No | Yes |
| CJK曖昧性解決 | No | Yes |

## token_mapper の C# 対応

piper-plus の `token_mapper.py` は複数文字音素を1文字のPUA文字に変換する。

```csharp
public static class TokenMapper
{
    private static readonly Dictionary<string, char> Token2Char = new();
    private static readonly Dictionary<char, string> Char2Token = new();

    public static char Register(string token)
    {
        if (token.Length == 1) return token[0];
        if (Token2Char.TryGetValue(token, out var cached)) return cached;

        // 動的割り当て（0xE059以降）
        var ch = (char)(_nextDynamicCodepoint++);
        Token2Char[token] = ch;
        Char2Token[ch] = token;
        return ch;
    }
}
```

固定PUAマッピング範囲:
- 日本語: 0xE000-0xE01C
- 中国語: 0xE020-0xE04A
- 韓国語: 0xE04B-0xE053
- スペイン語/ポルトガル語: 0xE054-0xE055
- フランス語: 0xE056-0xE058
