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
| CJK句読点 | U+3000-303F | `ja`/`zh` (コンテキスト依存) |
| 全角形式 | U+FF00-FF20, U+FF3B-FF40, U+FF5B-FFEF | `ja` |
| 全角ラテン | U+FF21-FF3A, U+FF41-FF5A | ラテン言語 |
| Hangul音節 | U+AC00-D7AF | `ko` |
| Hangul Jamo | U+1100-11FF | `ko` |
| Hangul互換Jamo | U+3130-318F | `ko` |
| ラテン基本 | A-Z, a-z | ラテン言語 (デフォルト`en`) |
| ラテン拡張 (Latin-1 Supplement) | U+00C0-00D6, U+00D8-00F6, U+00F8-00FF | ラテン言語 |
| ラテン拡張A (Latin Extended-A) | U+0100-017F | ラテン言語 |
| ラテン拡張B (Latin Extended-B) | U+0180-024F | ラテン言語 |

**除外文字**: × (U+00D7), ÷ (U+00F7) — 算術記号

**ラテン拡張A/B**: フランス語（OE合字等）、ポルトガル語（鼻母音等）、スペイン語のアクセント付き文字を含む

## uPiper LanguageDetector の現在の判定ルール

| パターン | 範囲 | 判定 |
|---------|------|------|
| JapaneseRegex | U+3040-309F, U+30A0-30FF, U+4E00-9FAF, U+3400-4DBF | `ja` (注: piper-plusはU+9FFFまで) |
| EnglishRegex | a-zA-Z (アポストロフィ対応) | `en` |
| NumberRegex | 0-9 + 小数点 | コンテキスト依存 |

## 差分分析 - 追加が必要なルール

| 項目 | 優先度 | 状態 | 説明 |
|------|--------|------|------|
| Katakana Phonetic (U+31F0-31FF) | P0 | 実装済み | 日本語精度向上 |
| CJK曖昧性解決 (ja vs zh) | P0 | 実装済み | Kanaコンテキストによる曖昧性解決 |
| CJK互換漢字 (U+F900-FAFF) | P1 | 実装済み | 稀文字対応 |
| 日本語/CJK句読点 (U+3000-303F) | P1 | 実装済み | コンテキスト依存のja/zh判定 |
| 全角形式 (U+FF00-FFEF) | P1 | 実装済み | 全角文字対応 |
| Hangul (U+AC00-D7AF等) | P2 | 実装済み | 韓国語対応 |
| ラテン拡張 Latin-1 Supplement (U+00C0-00FF) | P2 | 実装済み | 多言語ラテン文字対応 |
| ラテン拡張A (U+0100-017F) | P2 | 実装済み (Phase 5) | フランス語/ポルトガル語/スペイン語アクセント文字 |
| ラテン拡張B (U+0180-024F) | P2 | 実装済み (Phase 5) | フランス語/ポルトガル語/スペイン語特殊文字 |
| CJK句読点コンテキスト曖昧性解決 | P1 | 実装済み (Phase 5) | ja/zh句読点のKanaコンテキスト判定 |
| es/fr/pt言語フラグ | P2 | 実装済み (Phase 5) | _hasEs, _hasFr, _hasPt フラグ追加 |

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

### CJK句読点の曖昧性解決 (Phase 5追加)

Phase 5でCJK句読点（U+3000-303F, 全角形式）の判定が更新された。従来は常に`"ja"`を返していたが、CJK漢字と同様にKanaコンテキストによるja/zh曖昧性解決を行うようになった。

```csharp
// Priority 5: CJK punctuation → context-aware disambiguation
if (IsCjkPunct(ch))
{
    if (_hasJa && _hasZh)
        return contextHasKana ? "ja" : "zh";
    if (_hasJa) return "ja";
    if (_hasZh) return "zh";
    return null;
}
```

これにより、中国語テキスト中の`。`や`「」`が正しく`"zh"`として判定される。

## C#実装 (Phase 5反映)

```csharp
public class UnicodeLanguageDetector
{
    private readonly IReadOnlyList<string> _languages;
    private readonly string _defaultLatinLanguage;
    private readonly bool _hasJa, _hasZh, _hasKo;
    private readonly bool _hasEs, _hasFr, _hasPt;

    public UnicodeLanguageDetector(IReadOnlyList<string> languages, string defaultLatinLanguage = "en")
    {
        _languages = languages;
        _defaultLatinLanguage = defaultLatinLanguage;
        _hasJa = ContainsLanguage("ja");
        _hasZh = ContainsLanguage("zh");
        _hasKo = ContainsLanguage("ko");
        _hasEs = ContainsLanguage("es");
        _hasFr = ContainsLanguage("fr");
        _hasPt = ContainsLanguage("pt");
    }

    public string DetectChar(char ch, bool contextHasKana = false)
    {
        // Priority 1: Kana → ja
        if (_hasJa && IsKana(ch)) return "ja";

        // Priority 2: Hangul → ko
        if (_hasKo && IsHangul(ch)) return "ko";

        // Priority 3: CJK → ja or zh (コンテキスト依存)
        if (IsCJK(ch))
        {
            if (_hasJa && _hasZh) return contextHasKana ? "ja" : "zh";
            if (_hasJa) return "ja";
            if (_hasZh) return "zh";
            return null;
        }

        // Priority 4: 全角ラテン → ラテン言語
        if (IsFullwidthLatin(ch))
            return ContainsLanguage(_defaultLatinLanguage) ? _defaultLatinLanguage : null;

        // Priority 5: CJK句読点 → コンテキスト依存のja/zh判定
        if (IsCjkPunct(ch))
        {
            if (_hasJa && _hasZh) return contextHasKana ? "ja" : "zh";
            if (_hasJa) return "ja";
            if (_hasZh) return "zh";
            return null;
        }

        // Priority 6: ラテン文字 → ラテン言語
        if (IsLatin(ch))
            return ContainsLanguage(_defaultLatinLanguage) ? _defaultLatinLanguage : null;

        return null;  // 中立文字
    }

    // ---- 静的文字分類子 (AggressiveInlining) ----

    public static bool IsKana(char ch)
        => (ch >= '\u3040' && ch <= '\u309F')   // Hiragana
        || (ch >= '\u30A0' && ch <= '\u30FF')   // Katakana
        || (ch >= '\u31F0' && ch <= '\u31FF');   // Katakana Phonetic

    public static bool IsCJK(char ch)
        => (ch >= '\u4E00' && ch <= '\u9FFF')   // CJK統合漢字
        || (ch >= '\u3400' && ch <= '\u4DBF')   // CJK拡張A
        || (ch >= '\uF900' && ch <= '\uFAFF');   // CJK互換漢字

    public static bool IsHangul(char ch)
        => (ch >= '\uAC00' && ch <= '\uD7AF')   // Hangul音節
        || (ch >= '\u1100' && ch <= '\u11FF')   // Hangul Jamo
        || (ch >= '\u3130' && ch <= '\u318F');   // Hangul互換Jamo

    public static bool IsLatin(char ch)
        => (ch >= 'A' && ch <= 'Z')
        || (ch >= 'a' && ch <= 'z')
        || (ch >= '\u00C0' && ch <= '\u00D6')   // Latin-1 Supplement
        || (ch >= '\u00D8' && ch <= '\u00F6')
        || (ch >= '\u00F8' && ch <= '\u00FF')
        || (ch >= '\u0100' && ch <= '\u017F')   // Latin Extended-A (Phase 5)
        || (ch >= '\u0180' && ch <= '\u024F');   // Latin Extended-B (Phase 5)

    public static bool IsLatinExtended(char ch)             // Phase 5追加
        => (ch >= '\u0100' && ch <= '\u017F')   // Latin Extended-A
        || (ch >= '\u0180' && ch <= '\u024F');   // Latin Extended-B

    public static bool IsFullwidthLatin(char ch)
        => (ch >= '\uFF21' && ch <= '\uFF3A')
        || (ch >= '\uFF41' && ch <= '\uFF5A');

    public static bool IsCjkPunct(char ch)                  // Phase 5: IsJapanesePunctから改名
        => (ch >= '\u3000' && ch <= '\u303F')
        || (ch >= '\uFF00' && ch <= '\uFF20')
        || (ch >= '\uFF3B' && ch <= '\uFF40')
        || (ch >= '\uFF5B' && ch <= '\uFFEF');
}
```

## 処理フローの比較

| 項目 | piper-plus | uPiper (Phase 5実装済み) |
|------|-----------|--------------------------|
| 判定方式 | 文字単位ストリーミング | 文字単位ストリーミング (AggressiveInlining) |
| 対応言語 | ja,en,zh,ko,es,pt,fr | ja,en,zh,ko,es,pt,fr |
| CJK曖昧性 | Kanaコンテキスト | Kanaコンテキスト |
| CJK句読点曖昧性 | Kanaコンテキスト | Kanaコンテキスト (Phase 5で追加) |
| 中立文字 | 前セグメント吸収 | 前セグメント吸収 |
| ラテン拡張 | Latin-1 Supplement のみ | Latin-1 Supplement + Extended-A + Extended-B |
| 言語フラグ | has_ja, has_zh, has_ko | _hasJa, _hasZh, _hasKo, _hasEs, _hasFr, _hasPt |
