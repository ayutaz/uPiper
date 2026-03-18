# dot-net-g2p Unity統合課題と移行計画

## 概要

uPiperの全7言語G2Pバックエンドを `dot-net-g2p` パッケージに統一するための課題と移行計画。

## 現状

| 言語 | uPiper現在のバックエンド | dot-net-g2pパッケージ | 移行可否 |
|------|------------------------|---------------------|---------|
| ja | DotNetG2P.Core + MeCab (**移行済み**) | com.dotnetg2p.core + mecab | ✅ 稼働中 |
| en | FliteLTSPhonemizer (自前) | com.dotnetg2p.english | ⚠️ 要修正 |
| zh | ChinesePhonemizerBackend (自前・950文字) | com.dotnetg2p.chinese | ❌ 要修正 |
| ko | KoreanPhonemizerBackend (自前) | com.dotnetg2p.korean | ⚠️ 要修正 |
| es | SpanishPhonemizerBackend (自前) | com.dotnetg2p.spanish | ⚠️ 要修正 |
| fr | FrenchPhonemizerBackend (自前) | com.dotnetg2p.french | ⚠️ 要修正 |
| pt | PortuguesePhonemizerBackend (自前) | com.dotnetg2p.portuguese | ⚠️ 要修正 |

---

## 共通課題: Unity embedded resource 問題

### 問題

Unity は `.asmdef` ベースのコンパイルパイプラインを使用し、`.csproj` の `<EmbeddedResource>` ディレクティブを無視する。そのため、`Assembly.GetManifestResourceStream()` で埋め込みリソースを読み込むパッケージは Unity で動作しない。

### 影響を受けるパッケージ

| パッケージ | リソースファイル | サイズ | 影響度 |
|-----------|---------------|-------|-------|
| **DotNetG2P.Chinese** | pinyin_char.txt | 556KB (44,435行) | 致命的: デフォルトコンストラクタが失敗 |
| **DotNetG2P.Chinese** | pinyin_phrase.txt | 8.8MB (411,958行) | 致命的: 同上 |
| **DotNetG2P.English** | cmudict.dict | 3.5MB (135K語) | 致命的: 同上 |
| **DotNetG2P.English** | cmu_lts_model.bin | 152KB | 致命的: 同上 |
| **DotNetG2P.Korean** | korean_exceptions.master.tsv | 4KB (14行) | 致命的: static初期化で失敗 |
| **DotNetG2P.Spanish** | spanish_exceptions.master.tsv | 4KB | 致命的: 同上 |
| **DotNetG2P.French** | french_exceptions.master.tsv | 36KB | 致命的: 同上 |
| **DotNetG2P.Portuguese** | portuguese_exceptions.master.tsv | 32KB | 致命的: 同上 |

### エラーメッセージ

```
埋め込みピンイン辞書リソースが見つかりません。
InvalidOperationException: Embedded resource not found
```

Unity コンパイル済み DLL にリソースデータが含まれていないことを確認済み（DotNetG2P.Chinese.dll = 45KB、辞書データ 9.3MB が欠落）。

### 影響を受けないパッケージ

- **DotNetG2P.Core**: 埋め込みリソースなし（外部辞書パスを受け取る設計）
- **DotNetG2P.MeCab**: 埋め込みリソースなし（`DictionaryBundle.Load(string path)` / `Load(byte[])` で外部ロード）

### 推奨修正: StreamingAssets + LoadFromFile/Stream パターン

**DotNetG2P.MeCab の実績あるパターンに準拠:**

#### 1. 各辞書クラスに `LoadFromStream(Stream)` / `LoadFromFile(string)` public APIを追加

```csharp
// PinyinCharDictionary.cs - 現在 internal の ParseFromReader を public API経由で公開
public static PinyinCharDictionary LoadFromStream(Stream stream)
{
    using var reader = new StreamReader(stream);
    return ParseFromReader(reader);
}

public static PinyinCharDictionary LoadFromFile(string path)
{
    using var stream = File.OpenRead(path);
    return LoadFromStream(stream);
}
```

#### 2. G2Pエンジンのコンストラクタを public に変更

```csharp
// ChineseG2PEngine.cs - 現在 internal のコンストラクタを public に
public ChineseG2PEngine(
    PinyinCharDictionary charDictionary,
    PinyinPhraseDictionary? phraseDictionary,
    ChineseG2POptions options)
```

#### 3. 例外辞書のグレースフルフォールバック

```csharp
// KoreanExceptionDictionary.cs 等 - static初期化の失敗をキャッチ
private static readonly Lazy<Dictionary<string, string>> s_entries = new(() =>
{
    try { return LoadEntries(); }
    catch { return new Dictionary<string, string>(); } // グレースフル空辞書
});
```

#### 4. uPiper側の統合パターン（中国語の例）

```csharp
// Non-WebGL
var charDict = PinyinCharDictionary.LoadFromFile(
    Path.Combine(Application.streamingAssetsPath, "uPiper/Chinese/pinyin_char.txt"));
var phraseDict = PinyinPhraseDictionary.LoadFromFile(
    Path.Combine(Application.streamingAssetsPath, "uPiper/Chinese/pinyin_phrase.txt"));
var engine = new ChineseG2PEngine(charDict, phraseDict, ChineseG2POptions.Default);

// WebGL
var charData = await WebGLStreamingAssetsLoader.LoadBytesAsync("uPiper/Chinese/pinyin_char.txt");
var charDict = PinyinCharDictionary.LoadFromStream(new MemoryStream(charData));
```

---

## 言語別課題

### 中国語 (DotNetG2P.Chinese)

**現状**: uPiper は自前の `PinyinData.cs`（約950文字）を使用。dot-net-g2p は 44,435文字 + 411,958フレーズ辞書。

**dot-net-g2p側の修正が必要な項目:**

1. **[必須] embedded resource → StreamingAssets 対応**
   - `PinyinCharDictionary.LoadFromStream(Stream)` public API 追加
   - `PinyinPhraseDictionary.LoadFromStream(Stream)` public API 追加
   - `ChineseG2PEngine(PinyinCharDictionary, PinyinPhraseDictionary?, ChineseG2POptions)` コンストラクタを public に
   - `EmbeddedChineseDictionaryCache` の Unity 非対応を明文化

2. **[既存] PUA + Prosody API**: `ToPuaPhonemes()`, `ToIpaWithProsody()` は実装済み ✅

**uPiper側の移行作業:**
- `StreamingAssets/uPiper/Chinese/` に辞書ファイルを配置
- `ChinesePhonemizerBackend` を `ChineseG2PEngine` ベースに書き換え
- WebGL: 非同期ロードパス追加

**効果**: 文字カバレッジが 950 → 44,435文字（約47倍）、フレーズ多音字解消

---

### 英語 (DotNetG2P.English)

**現状**: uPiper は `FliteLTSPhonemizer`（CMU辞書 + Flite LTS）を使用。

**dot-net-g2p側の修正が必要な項目:**

1. **[必須] embedded resource → StreamingAssets 対応**
   - CMU辞書 (3.5MB) と LTS モデル (152KB) の外部ロード API
   - `EnglishG2PEngine(Stream cmuDictStream, Stream ltsModelStream)` コンストラクタ追加

2. **[要追加] piper-plus互換 IPA 出力 API**
   - `ToPiperIpaPhonemes(text)` → piper-plus `EnglishPhonemizer` と同じ IPA 音素配列
   - コンテキスト依存ルール: AA+R → ɑːɹ マージ
   - 機能語ストレス除去（100語以上のリスト）
   - AH 無強勢 → ə (schwa) 変換

3. **[要追加] PUA + Prosody API**
   - `ToPuaPhonemes(text)` → PUA音素配列
   - `ToIpaWithProsody(text)` → Prosody (A1=0, A2=stress, A3=word phoneme count)

**piper-plus との主な差異（ArpabetToIPAConverter の残課題）:**

| 差異 | 影響 |
|------|------|
| AA+R → ɑːɹ マージ未対応 | "car" で `ː` が欠落 |
| 機能語ストレス除去未対応 | "the", "I", "you" 等に不要なストレス |
| ER2 → ˌɜː の誤マッピング | 二次ストレスの ER が長音に |

---

### 韓国語 (DotNetG2P.Korean)

**現状**: uPiper は自前の `KoreanPhonemizerBackend`（965行）を使用。

**dot-net-g2p側の修正が必要な項目:**

1. **[必須] embedded resource → StreamingAssets 対応**
   - `korean_exceptions.master.tsv` (4KB) の外部ロードまたはグレースフルフォールバック
   - static 初期化の `GetManifestResourceStream` 失敗でクラッシュする問題の修正

2. **[既存] PUA + Prosody API**: `ToPuaPhonemes()`, `ToIpaWithProsody()` は実装済み ✅

**dot-net-g2p で追加される機能:**

| 機能 | uPiper現在 | dot-net-g2p |
|------|-----------|-------------|
| 流音化 (ㄴ/ㄹ) | 未実装 | ✅ |
| ㄴ挿入 (合成語) | 未実装 | ✅ |
| 口蓋音化 | 未実装 | ✅ |
| 例外辞書 | なし | 14語 |
| テキスト正規化 | NFC のみ | NFKC + 句読点 |
| 標準/口語モード | なし | ✅ |

---

### スペイン語 (DotNetG2P.Spanish)

**現状**: uPiper は自前の `SpanishPhonemizerBackend`（1,237行、piper-plus 直接ポート）。

**dot-net-g2p側の修正が必要な項目:**

1. **[必須] embedded resource → StreamingAssets 対応**
   - `spanish_exceptions.master.tsv` の外部ロードまたはグレースフルフォールバック

2. **[要追加] PUA + Prosody API**
   - `ToPuaPhonemes(text)` → PUA音素配列（rr→0xE01D, tʃ→0xE054）
   - `ToIpaWithProsody(text)` → Prosody (A1=0, A2=stress, A3=word phoneme count)
   - 機能語ストレス除去

**dot-net-g2p のメリット**: 方言対応（カスティーリャ/ラテンアメリカ）、例外辞書、テキスト正規化（数字/通貨/日付）、227テスト

---

### フランス語 (DotNetG2P.French)

**現状**: uPiper は自前の `FrenchPhonemizerBackend`（1,351行、piper-plus 直接ポート）。

**dot-net-g2p側の修正が必要な項目:**

1. **[必須] embedded resource → StreamingAssets 対応**
   - `french_exceptions.master.tsv` (36KB) の外部ロードまたはグレースフルフォールバック

2. **[要追加] PUA + Prosody API**（スペイン語と同様）

3. **[要確認] `feature/french-g2p` ブランチを main にマージ**
   - F1-F4 完了済みだが未マージ

**dot-net-g2p のメリット**: 570語例外辞書、方言対応（メトロポリタン/保守的）、トレマ処理、音節分割、テキスト正規化

---

### ポルトガル語 (DotNetG2P.Portuguese)

**現状**: uPiper は自前の `PortuguesePhonemizerBackend`（1,194行、piper-plus 直接ポート）。

**dot-net-g2p側の修正が必要な項目:**

1. **[必須] embedded resource → StreamingAssets 対応**
   - `portuguese_exceptions.master.tsv` (32KB) の外部ロードまたはグレースフルフォールバック

2. **[要追加] PUA + Prosody API**（スペイン語と同様）

**dot-net-g2p のメリット**: 方言対応（ブラジル/ヨーロッパ）、鼻母音ジフトング、母音弱化、r の正確な分類、例外辞書

---

## 移行優先順位

| 優先度 | 言語 | 理由 |
|--------|------|------|
| **P0** | zh (中国語) | 950文字→44,435文字。現在の品質が最も低い |
| **P1** | en (英語) | g2p-en レベルの精度向上。コンテキスト依存ルール |
| **P1** | ko (韓国語) | 流音化/口蓋音化/ㄴ挿入の3ルール追加 |
| **P2** | es/fr/pt | 例外辞書・テキスト正規化・方言対応の追加 |

## dot-net-g2p側の共通修正タスク

1. **全パッケージ: embedded resource の代替ロードAPI追加**
   - `LoadFromStream(Stream)` / `LoadFromFile(string)` public API
   - G2Pエンジンに外部辞書を受け取るコンストラクタ
   - 例外辞書の static 初期化にグレースフルフォールバック

2. **es/fr/pt: PUA + Prosody API 追加**
   - `ToPuaPhonemes()`, `ToIpaWithProsody()` メソッド
   - Chinese/Korean と同じパターン

3. **en: piper-plus互換 IPA 出力API追加**
   - コンテキスト依存ルール（AA+R マージ、機能語ストレス除去）
   - `ToPiperIpaPhonemes()`, Prosody API

4. **fr: `feature/french-g2p` ブランチを main にマージ**
