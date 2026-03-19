# dot-net-g2p Unity統合課題と移行計画

## 概要

uPiperの全7言語G2Pバックエンドを `dot-net-g2p` パッケージに統一するための課題と移行計画。

## 現状

| 言語 | uPiper現在のバックエンド | dot-net-g2pパッケージ | ステータス |
|------|------------------------|---------------------|-----------|
| ja | DotNetG2P.Core + MeCab | com.dotnetg2p.core + mecab | ✅ 移行済み・稼働中 |
| zh | DotNetG2P.Chinese + uPiperワークアラウンド | com.dotnetg2p.chinese | ⚠️ 部分移行（トーンPUA問題あり） |
| en | FliteLTSPhonemizer + ArpabetToIPAConverter (自前) | com.dotnetg2p.english | ❌ 未移行（embedded resource問題） |
| ko | KoreanPhonemizerBackend (自前) | com.dotnetg2p.korean | ❌ 未移行（embedded resource問題） |
| es | SpanishPhonemizerBackend (自前) | com.dotnetg2p.spanish | ❌ 未移行（PUA/Prosody API未実装） |
| fr | FrenchPhonemizerBackend (自前) | com.dotnetg2p.french | ❌ 未移行（PUA/Prosody API未実装） |
| pt | PortuguesePhonemizerBackend (自前) | com.dotnetg2p.portuguese | ❌ 未移行（PUA/Prosody API未実装） |

---

## 解決済みの共通課題

### ✅ Unity embedded resource 問題（中国語で解決済み）

**問題**: Unity は `.asmdef` ベースのコンパイルで `.csproj` の `<EmbeddedResource>` を無視する。`Assembly.GetManifestResourceStream()` が null を返す。

**解決方法** (PR#47): `LoadFromFile(string path)` コンストラクタを public 化し、StreamingAssets からの外部ロードに対応。

**中国語で実証済み**: `ChineseG2PEngine(charPath, phrasePath)` で StreamingAssets から辞書ロード成功。

---

## dot-net-g2p側への依頼タスク

### タスク1: [Chinese] `ToPuaPhonemes()` にトーンマーカーPUAを含める

**優先度**: P0（uPiper側ワークアラウンドあるが、本来はライブラリ側で対応すべき）

**現状**: `ToPuaPhonemes()` は `PinyinToPiperIpa.ConvertToPhonemes()` を経由するが、このメソッドは「声調マーカーは含めない（tone1-tone5 トークンで別管理するため）」という設計。

**問題**: piper-plus の `ChinesePhonemizer.phonemize()` の実際の出力には、各音節末尾にトーンPUA文字 (`\ue046`-`\ue04a`) が含まれる。

```
piper-plus出力: ['ɕ', '\ue032', '\ue049', 'n', 'i', '\ue048']  ← トーンPUA含む
dot-net-g2p:   ['ɕ', '\ue032', 'n', 'i']                       ← トーンPUA欠落
```

**対応案**:
- `ToPuaPhonemes()` の出力に各音節末尾のトーンPUA文字を自動追加する
- または `ToPuaPhonemesWithTones()` メソッドを新設する

**uPiper側ワークアラウンド**: `ToIpaWithProsody()` の A1(声調番号) から TonePuaChars テーブルで手動挿入実装済み。

---

### タスク2: [English] embedded resource → 外部ロード対応

**優先度**: P1

**現状**: `EnglishG2PEngine` のデフォルトコンストラクタが CMU辞書 (3.5MB) と LTSモデル (152KB) を embedded resource から読み込むため Unity で動作しない。

**必要な対応**:
- `EnglishG2PEngine(string cmuDictPath)` / `EnglishG2PEngine(string cmuDictPath, string ltsModelPath)` コンストラクタ追加
- または `LoadFromFile` / `LoadFromStream` パターンで辞書・モデルの外部ロード API 公開

**参考**: Chinese で PR#47 の `ChineseG2PEngine(string charDictPath, string phraseDictPath)` パターンが実績あり。

---

### タスク3: [English] piper-plus互換 IPA 出力 API

**優先度**: P1

**現状**: `EnglishG2PEngine.ToIPA()` は IPA 文字列を返すが、piper-plus `EnglishPhonemizer` の出力形式と差異がある。

**必要な対応**:
- `ToPiperIpaPhonemes(text)` → piper-plus 互換の個別 IPA 音素配列を返すメソッド
- コンテキスト依存ルール:
  - AA+R → `ɑ` + `ː` + `ɹ` マージ（"car" → `k ˈ ɑ ː ɹ`）
  - 機能語ストレス除去（"the", "a", "I", "you" 等100語以上）
  - AH 無強勢 → `ə` (schwa) 変換
  - ER1 → `ɜ` + `ː`、ER0 → `ɚ` のストレス依存マッピング
- `ToPuaPhonemes(text)` → PUA音素配列
- `ToIpaWithProsody(text)` → Prosody (A1=0, A2=stress(0/1/2), A3=word phoneme count)

---

### タスク4: [Korean] embedded resource → グレースフルフォールバック

**優先度**: P1

**現状**: `KoreanExceptionDictionary` の static 初期化が `GetManifestResourceStream()` を呼び、Unity で `TypeInitializationException` が発生して `KoreanG2PEngine` が使用不能になる。

**必要な対応**:
- `KoreanExceptionDictionary` の embedded resource 読み込み失敗時にグレースフルに空辞書にフォールバック
- または Chinese と同様に `LoadFromFile(string)` パターンで外部ロード対応

**備考**: 例外辞書は 14 エントリのみ（4KB）なので、グレースフルフォールバック（空辞書）でも実用上の影響は軽微。

---

### タスク5: [Spanish/French/Portuguese] PUA + Prosody API 追加

**優先度**: P2

**現状**: Chinese/Korean には `ToPuaPhonemes()` / `ToIpaWithProsody()` が実装済みだが、Spanish/French/Portuguese には未実装。

**必要な対応** (3言語共通):
- `ToPuaPhonemes(text)` → PUA音素配列（multi-char IPA → PUA 変換）
- `ToIpaWithProsody(text)` → Prosody (A1=0, A2=stress(0/2), A3=word phoneme count)
- 機能語ストレス除去（piper-plus 準拠）

**言語別PUAマッピング**:

| 言語 | multi-char IPA | PUA |
|------|---------------|-----|
| es | `rr` (トリル) | 0xE01D |
| es/pt | `tʃ` (破擦音) | 0xE054 |
| pt | `dʒ` (破擦音) | 0xE055 |
| fr | `ɛ̃` (鼻母音) | 0xE056 |
| fr | `ɑ̃` (鼻母音) | 0xE057 |
| fr | `ɔ̃` (鼻母音) | 0xE058 |
| fr | `y_vowel` | 0xE01E |

---

### タスク6: [Spanish/French/Portuguese] embedded resource → グレースフルフォールバック

**優先度**: P2

**現状**: 各言語の例外辞書 TSV が embedded resource 経由のため Unity で読み込めない。

| パッケージ | リソース | サイズ |
|-----------|---------|-------|
| Spanish | spanish_exceptions.master.tsv | 4KB |
| French | french_exceptions.master.tsv | 36KB |
| Portuguese | portuguese_exceptions.master.tsv | 32KB |

**必要な対応**: Korean と同様に、embedded resource 失敗時のグレースフルフォールバック、または `LoadFromFile` パターン追加。

---

### タスク7: [French] `feature/french-g2p` ブランチを main にマージ

**優先度**: P2

**現状**: F1-F4 完了済みだが `main` 未マージ。uPiper の `manifest.json` で参照するには `main` ブランチまたはタグが必要。

---

## 移行優先順位まとめ

| 優先度 | タスク | 言語 | 効果 |
|--------|-------|------|------|
| **P0** | タスク1: トーンPUA | zh | ワークアラウンド解消 |
| **P1** | タスク2: EN外部ロード | en | DotNetG2P.English 統合可能に |
| **P1** | タスク3: EN piper-plus互換 | en | AA+R/機能語/schwa 精度向上 |
| **P1** | タスク4: KO グレースフル | ko | DotNetG2P.Korean 統合可能に |
| **P2** | タスク5: PUA/Prosody API | es/fr/pt | 3言語移行可能に |
| **P2** | タスク6: 例外辞書 | es/fr/pt | 例外辞書利用可能に |
| **P2** | タスク7: FR マージ | fr | パッケージ参照可能に |
