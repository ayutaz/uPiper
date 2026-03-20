# dot-net-g2p Unity統合課題と移行計画

## 概要

uPiperの全7言語G2Pバックエンドを `dot-net-g2p` パッケージに統一するための課題と移行計画。

## 現状

全7言語のうち6言語（en/zh/ko/es/fr/pt）が DotNetG2P パッケージに移行完了。全言語で Unknown phoneme 警告ゼロを確認済み。

| 言語 | uPiper現在のバックエンド | dot-net-g2pパッケージ | ステータス |
|------|------------------------|---------------------|-----------|
| ja | DotNetG2P.Core + MeCab | com.dotnetg2p.core + mecab | ✅ 移行済み・稼働中 |
| en | EnglishG2PEngine (ToPuaPhonemes/ToIpaWithProsody) | com.dotnetg2p.english | ✅ 移行済み・稼働中 |
| zh | DotNetG2P.Chinese (ToPuaPhonemes/ToIpaWithProsody) + uPiperトーンPUAワークアラウンド | com.dotnetg2p.chinese | ✅ 移行済み・稼働中（トーンPUAワークアラウンドあり） |
| ko | KoreanG2PEngine (ToPuaPhonemes/ToIpaWithProsody) | com.dotnetg2p.korean | ✅ 移行済み・稼働中 |
| es | SpanishG2PEngine (ToPuaPhonemes/ToIpaWithProsody) | com.dotnetg2p.spanish | ✅ 移行済み・稼働中 |
| fr | FrenchG2PEngine (ToPuaPhonemes/ToIpaWithProsody) | com.dotnetg2p.french | ✅ 移行済み・稼働中 |
| pt | PortugueseG2PEngine (ToPuaPhonemes/ToIpaWithProsody) | com.dotnetg2p.portuguese | ✅ 移行済み・稼働中 |

---

## 解決済みの共通課題

### ✅ Unity embedded resource 問題（全言語で解決済み）

**問題**: Unity は `.asmdef` ベースのコンパイルで `.csproj` の `<EmbeddedResource>` を無視する。`Assembly.GetManifestResourceStream()` が null を返す。

**解決方法**:
- Chinese: `ChineseG2PEngine(charPath, phrasePath)` コンストラクタで StreamingAssets から辞書ロード (PR#47)
- English: `EnglishG2PEngine(cmuDictPath)` コンストラクタで外部CMU辞書ロード対応。embedded resource フォールバックも利用可能
- Korean: グレースフルフォールバック対応。例外辞書 (14エントリ) 読み込み失敗時は空辞書で動作
- Spanish/French/Portuguese: グレースフルフォールバック対応。例外辞書読み込み失敗時も正常動作

### ✅ PhonemeEncoder NFC正規化（Portuguese ũ 修正）

**問題**: PortugueseG2PEngine の `ToPuaPhonemes()` が一部のIPA文字を Unicode分解形式 (NFD) で出力するため、`phoneme_id_map` でマッチしない。例: `u` + `\u0303`（結合チルデ）が `ũ`（NFC形式）にマッチしない。

**解決方法**: PhonemeEncoder に NFC正規化を追加。`phoneme_id_map` 検索前に分解形式のIPA文字を合成形式に正規化する。

```csharp
// NFC normalize decomposed IPA (e.g., u+\u0303 -> ũ) to match phoneme_id_map
var nfc = phoneme.Normalize(System.Text.NormalizationForm.FormC);
```

---

## dot-net-g2p側への残存タスク

### タスク1: [Chinese] `ToPuaPhonemes()` にトーンマーカーPUAを含める

**優先度**: P1（uPiper側ワークアラウンドで稼働中だが、本来はライブラリ側で対応すべき）

**現状**: `ToPuaPhonemes()` は `PinyinToPiperIpa.ConvertToPhonemes()` を経由するが、このメソッドは「声調マーカーは含めない（tone1-tone5 トークンで別管理するため）」という設計。

**問題**: piper-plus の `ChinesePhonemizer.phonemize()` の実際の出力には、各音節末尾にトーンPUA文字 (`\ue046`-`\ue04a`) が含まれる。

```
piper-plus出力: ['ɕ', '\ue032', '\ue049', 'n', 'i', '\ue048']  ← トーンPUA含む
dot-net-g2p:   ['ɕ', '\ue032', 'n', 'i']                       ← トーンPUA欠落
```

**対応案**:
- `ToPuaPhonemes()` の出力に各音節末尾のトーンPUA文字を自動追加する
- または `ToPuaPhonemesWithTones()` メソッドを新設する

**uPiper側ワークアラウンド**: `ChinesePhonemizerBackend.PhonemizeWithDotNetG2P()` が `ToIpaWithProsody()` の A1(声調番号) から TonePuaChars テーブルで手動挿入実装済み。音声品質への影響なし。

---

## 移行完了タスク

### ✅ タスク2: [English] embedded resource → 外部ロード対応（完了）

`EnglishG2PEngine(string cmuDictPath)` コンストラクタが追加され、StreamingAssets からの外部CMU辞書ロードに対応。embedded resource フォールバックも利用可能。

**uPiper統合**: `MultilingualPhonemizer` が `StreamingAssets/uPiper/Phonemizers/cmudict-0.7b.txt` を優先的に読み込み、存在しない場合は `EnglishG2PEngine()` のembedded辞書にフォールバック。

---

### ✅ タスク3: [English] piper-plus互換 IPA 出力 API（完了）

`EnglishG2PEngine` に以下のAPIが実装され、piper-plus互換のIPA出力が利用可能:
- `ToPuaPhonemes(text)` → PUA音素配列
- `ToIpaWithProsody(text)` → Prosody付きIPA（A1=0, A2=stress, A3=語内音素数）

**uPiper統合**: `MultilingualPhonemizer` が `ToPuaPhonemes()` / `ToIpaWithProsody()` を直接呼び出し。Unknown phoneme 警告ゼロを確認。

---

### ✅ タスク4: [Korean] embedded resource → グレースフルフォールバック（完了）

`KoreanExceptionDictionary` の embedded resource 読み込み失敗時にグレースフルに空辞書にフォールバック。例外辞書は 14 エントリのみなので実用上の影響は軽微。

**uPiper統合**: `MultilingualPhonemizer` が `KoreanG2PEngine` を直接インスタンス化して `ToPuaPhonemes()` / `ToIpaWithProsody()` を使用。Unknown phoneme 警告ゼロを確認。

---

### ✅ タスク5: [Spanish/French/Portuguese] PUA + Prosody API 追加（完了）

3言語すべてに `ToPuaPhonemes()` / `ToIpaWithProsody()` が実装済み:
- `SpanishG2PEngine.ToPuaPhonemes(text)` / `ToIpaWithProsody(text)`
- `FrenchG2PEngine.ToPuaPhonemes(text)` / `ToIpaWithProsody(text)`
- `PortugueseG2PEngine.ToPuaPhonemes(text)` / `ToIpaWithProsody(text)`

Prosodyマッピング: A1=0, A2=stress(0/2), A3=語内音素数

**uPiper統合**: `MultilingualPhonemizer` が各エンジンを直接インスタンス化して使用。全3言語で Unknown phoneme 警告ゼロを確認。

---

### ✅ タスク6: [Spanish/French/Portuguese] embedded resource → グレースフルフォールバック（完了）

各言語の例外辞書 TSV が embedded resource 読み込み失敗時にグレースフルにフォールバック。

| パッケージ | リソース | サイズ |
|-----------|---------|-------|
| Spanish | spanish_exceptions.master.tsv | 4KB |
| French | french_exceptions.master.tsv | 36KB |
| Portuguese | portuguese_exceptions.master.tsv | 32KB |

---

### ✅ タスク7: [French] `feature/french-g2p` ブランチを main にマージ（完了）

main にマージ済み。uPiper の manifest.json から参照可能。

---

## 残存タスクまとめ

| 優先度 | タスク | 言語 | 効果 |
|--------|-------|------|------|
| **P1** | タスク1: トーンPUA | zh | uPiper側ワークアラウンド解消（現在も正常動作中） |
