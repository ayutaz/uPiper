# 言語検出 - UnicodeLanguageDetector 拡張設計

## piper-plus UnicodeLanguageDetector の全Unicode範囲マッピング

| 言語/スクリプト | Unicode範囲 | 判定結果 |
|----------------|------------|---------|
| Hiragana | U+3040-309F | `ja` |
| Katakana | U+30A0-30FF | `ja` |
| Katakana Phonetic | U+31F0-31FF | `ja` |
| CJK統合漢字 | U+4E00-9FFF | `ja`/`zh` (コンテキスト依存) |
| CJK拡張A | U+3400-4DBF | `ja`/`zh` (コンテキスト依存) |
| CJK互換漢字 | U+F900-FAFF | `ja`/`zh` (コンテキスト依存) |
| CJK句読点 | U+3000-303F | `ja` |
| 全角形式 | U+FF00-FF20, U+FF3B-FF40, U+FF5B-FFEF | `ja` |
| 全角ラテン | U+FF21-FF3A, U+FF41-FF5A | ラテン言語 |
| Hangul音節 | U+AC00-D7AF | `ko` |
| Hangul Jamo | U+1100-11FF | `ko` |
| Hangul互換Jamo | U+3130-318F | `ko` |
| ラテン基本 | A-Z, a-z | ラテン言語 (デフォルト`en`) |
| ラテン拡張 | U+00C0-00D6, U+00D8-00F6, U+00F8-00FF | ラテン言語 |

**除外文字**: × (U+00D7), ÷ (U+00F7) — 算術記号

## uPiper LanguageDetector の現在の判定ルール

| パターン | 範囲 | 判定 |
|---------|------|------|
| JapaneseRegex | U+3040-309F, U+30A0-30FF, U+4E00-9FAF, U+3400-4DBF | `ja` (注: piper-plusはU+9FFFまで) |
| EnglishRegex | a-zA-Z (アポストロフィ対応) | `en` |
| NumberRegex | 0-9 + 小数点 | コンテキスト依存 |

## 差分分析 - 追加が必要なルール

| 項目 | 優先度 | 説明 |
|------|--------|------|
| Katakana Phonetic (U+31F0-31FF) | P0 | 日本語精度向上 |
| CJK曖昧性解決 (ja vs zh) | P0 | 中国語対応時必須 |
| CJK互換漢字 (U+F900-FAFF) | P1 | 稀文字対応 |
| 日本語句読点 (U+3000-303F) | P1 | セグメント精度向上 |
| 全角形式 (U+FF00-FFEF) | P1 | 全角文字対応 |
| Hangul (U+AC00-D7AF等) | P2 | 韓国語対応時 |
| ラテン拡張 (U+00C0-00FF) | P2 | 多言語ラテン文字対応 |

## CJK曖昧性解決ロジック

piper-plus での判定:

```python
if _RE_CJK.match(ch):
    if has_ja and has_zh:
        return "ja" if context_has_kana else "zh"
    if has_ja: return "ja"
    if has_zh: return "zh"
    return None
```

**判定キー**: テキスト全体に仮名（Kana）が含まれるかを事前スキャン
- 仮名あり → 漢字は日本語と判定
- 仮名なし → 漢字は中国語と判定

## C#実装案

```csharp
public class UnicodeLanguageDetector
{
    private readonly HashSet<string> _languages;
    private readonly string _defaultLatinLanguage;
    private readonly bool _hasJa, _hasZh, _hasKo;

    public UnicodeLanguageDetector(List<string> languages, string defaultLatinLanguage = "en")
    {
        _languages = new HashSet<string>(languages);
        _defaultLatinLanguage = defaultLatinLanguage;
        _hasJa = _languages.Contains("ja");
        _hasZh = _languages.Contains("zh");
        _hasKo = _languages.Contains("ko");
    }

    public string DetectChar(char ch, bool contextHasKana = false)
    {
        // Kana → ja
        if (IsKana(ch)) return _hasJa ? "ja" : null;

        // Hangul → ko
        if (IsHangul(ch)) return _hasKo ? "ko" : null;

        // CJK → ja or zh (コンテキスト依存)
        if (IsCJK(ch))
        {
            if (_hasJa && _hasZh) return contextHasKana ? "ja" : "zh";
            if (_hasJa) return "ja";
            if (_hasZh) return "zh";
            return null;
        }

        // 全角ラテン → ラテン言語
        if (IsFullwidthLatin(ch))
            return _languages.Contains(_defaultLatinLanguage) ? _defaultLatinLanguage : null;

        // 日本語句読点
        if (IsJapanesePunct(ch)) return _hasJa ? "ja" : null;

        // ラテン文字 → ラテン言語
        if (IsLatin(ch))
            return _languages.Contains(_defaultLatinLanguage) ? _defaultLatinLanguage : null;

        return null;  // 中立文字
    }

    public bool HasKana(string text)
    {
        foreach (var ch in text)
            if (IsKana(ch)) return true;
        return false;
    }

    public List<(string language, string text)> SegmentText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new();

        var contextHasKana = HasKana(text);
        var segments = new List<(string, string)>();
        string currentLang = null;
        var currentChars = new List<char>();

        foreach (var ch in text)
        {
            var lang = DetectChar(ch, contextHasKana);
            if (lang != null && lang != currentLang && currentLang != null)
            {
                segments.Add((currentLang, new string(currentChars.ToArray())));
                currentChars.Clear();
            }
            if (lang != null) currentLang = lang;
            currentChars.Add(ch);
        }

        if (currentChars.Count > 0 && currentLang != null)
            segments.Add((currentLang, new string(currentChars.ToArray())));

        return segments;
    }

    // ---- 判定ヘルパー ----

    private static bool IsKana(char ch)
        => (ch >= '\u3040' && ch <= '\u309F')   // Hiragana
        || (ch >= '\u30A0' && ch <= '\u30FF')   // Katakana
        || (ch >= '\u31F0' && ch <= '\u31FF');   // Katakana Phonetic

    private static bool IsCJK(char ch)
        => (ch >= '\u4E00' && ch <= '\u9FFF')   // CJK統合漢字
        || (ch >= '\u3400' && ch <= '\u4DBF')   // CJK拡張A
        || (ch >= '\uF900' && ch <= '\uFAFF');   // CJK互換漢字

    private static bool IsHangul(char ch)
        => (ch >= '\uAC00' && ch <= '\uD7AF')   // Hangul音節
        || (ch >= '\u1100' && ch <= '\u11FF')   // Hangul Jamo
        || (ch >= '\u3130' && ch <= '\u318F');   // Hangul互換Jamo

    private static bool IsLatin(char ch)
        => (ch >= 'A' && ch <= 'Z')
        || (ch >= 'a' && ch <= 'z')
        || (ch >= '\u00C0' && ch <= '\u00D6')
        || (ch >= '\u00D8' && ch <= '\u00F6')
        || (ch >= '\u00F8' && ch <= '\u00FF');

    private static bool IsFullwidthLatin(char ch)
        => (ch >= '\uFF21' && ch <= '\uFF3A')
        || (ch >= '\uFF41' && ch <= '\uFF5A');

    private static bool IsJapanesePunct(char ch)
        => (ch >= '\u3000' && ch <= '\u303F')
        || (ch >= '\uFF00' && ch <= '\uFF20')
        || (ch >= '\uFF3B' && ch <= '\uFF40')
        || (ch >= '\uFF5B' && ch <= '\uFFEF');
}
```

## 処理フローの比較

| 項目 | piper-plus | uPiper現在 | uPiper改修後 |
|------|-----------|-----------|-------------|
| 判定方式 | 文字単位ストリーミング | 複数パス正規表現 | 文字単位ストリーミング |
| 対応言語 | ja,en,zh,ko,es,pt,fr | ja,en | ja,en,zh,ko,es,pt,fr |
| CJK曖昧性 | Kanaコンテキスト | なし（常にja） | Kanaコンテキスト |
| 中立文字 | 前セグメント吸収 | マージで部分対応 | 前セグメント吸収 |
