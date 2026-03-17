# 新言語Phonemizer C#実装戦略

## 言語別サマリ

| 言語 | 難易度 | C#行数（実測） | 外部依存 | 実装戦略 | ステータス |
|------|--------|---------------|---------|---------|-----------|
| スペイン語 | **低** | 1,233行 | なし | 直接ポート | **実装済み** |
| フランス語 | 中 | 1,350行 | なし | 直接ポート | **実装済み** |
| ポルトガル語 | 中 | 1,190行 | なし | 直接ポート | **実装済み** |
| 中国語 | **高** | 1,716行（2ファイル） | なし（pypinyin不使用） | ルックアップテーブル+トーン規則 | **実装済み** |
| 韓国語 | **高** | 965行 | なし（g2pk2不使用） | Hangul分解+簡略規則 | **実装済み** |
| 英語 | 高 | 既存 | Flite LTS | 既存実装活用 | **実装済み** |

## 共通コンポーネント

### PuaTokenMapper

- **ファイル**: `Runtime/Core/Phonemizers/Multilingual/PuaTokenMapper.cs`（303行）
- **役割**: 複数文字の音素トークンをUnicode Private Use Area（PUA）の1コードポイントにマッピング
- **主要API**:
  - `Register(string token)` - トークンを登録しPUA文字を返す（既存なら既存マッピングを返す）
  - `MapSequence(IList<string> tokens)` - トークン列を一括変換
  - `MapToken(string token)` / `UnmapChar(char ch)` - 双方向変換
- **PUA割り当て範囲**:
  - `0xE000`-`0xE01C`: 日本語（長母音、口蓋化子音、促音等）
  - `0xE01D`-`0xE01E`: 多言語共有（`rr`=スペイン語トリル、`y_vowel`=中国語/フランス語共有）
  - `0xE020`-`0xE04A`: 中国語（有気音、二重母音、鼻母音韻尾、トーンマーカー）
  - `0xE04B`-`0xE052`: 韓国語（濃音、内破音）
  - `0xE054`-`0xE055`: スペイン語/ポルトガル語共有（破擦音 `tʃ`, `dʒ`）
  - `0xE056`-`0xE058`: フランス語（鼻母音 `ɛ̃`, `ɑ̃`, `ɔ̃`）
  - `0xE059`以降: 動的割り当て
- **重要**: PUA割り当ては学習済みモデルに焼き込まれているため変更不可。Python（token_mapper.py）およびC++実装と完全一致が必須。

### LanguageConstants

- **ファイル**: `Runtime/Core/Phonemizers/Multilingual/LanguageConstants.cs`（199行）
- **役割**: 言語コード（ISO 639-1）と多言語ONNXモデルの言語ID（lid入力テンソル）のマッピング
- **言語ID**: ja=0, en=1, zh=2, es=3, fr=4, pt=5, ko=6
- **主要API**:
  - `GetLanguageId(string languageCode)` / `GetLanguageCode(int languageId)` - 双方向変換
  - `IsLatinLanguage(string code)` - ラテン文字言語判定（en, es, fr, pt）
  - `IsCjkLanguage(string code)` - CJK言語判定（ja, zh, ko）
- **言語グループ**: `LatinLanguages`（en, es, fr, pt）、`CjkLanguages`（ja, zh, ko）

## スペイン語（実装済み: 直接ポート）

### 変換パイプライン
```
テキスト → 正規化 → トークン化 → グラフェム分割
→ 音節化 → ストレス検出 → ルールベースG2P → IPA音素列
```

### 主要な処理
- **グラフェム分割**: ch, ll, rr, qu, gu, gu を単位認識
- **音節化**: 強/弱母音、複合子音クラスタルール
- **ストレス検出**: アクセント記号 or 末尾規則
- **G2P**: 文脈依存ルール（b/d/g のアロフォン等）

### 音素インベントリ
- 母音: a, e, i, o, u
- 子音: p, b, t, d, k, ɡ, f, s, z, m, n, ɲ, l, ɾ, rr, tʃ, x, j, w, v
- アロフォン: β, ɣ, ð, ʝ
- ストレス: ˈ

### C#実装の利点
- 外部ライブラリ不要（純ルールベース）
- テストケースが充実（piper-plus側）
- 明確なルールセット

### 実装状況

- **ファイル**: `Runtime/Core/Phonemizers/Backend/Spanish/SpanishPhonemizerBackend.cs`（1,233行）
- **クラス**: `SpanishPhonemizerBackend : PhonemizerBackendBase`
- **名前空間**: `uPiper.Core.Phonemizers.Backend.Spanish`
- **Name**: `"SpanishG2P"`, **Version**: `"1.0.0"`
- **SupportedLanguages**: `"es"`, `"es-ES"`, `"es-MX"`
- **PUA使用**: `\uE01D`（rr トリル）、`\uE054`（tʃ 破擦音）
- **Pythonからの移植**: piper-plus `spanish.py` からの直接ポート
- **主要処理**: 正規化、トークン化、グラフェム分割（ch/ll/rr/qu/gu/gu 認識）、音節化（強/弱母音ルール、inseparable onset clusters）、ストレス検出（アクセント記号 or 末尾規則）、文脈依存G2P（b/d/g アロフォン変換）
- **ラテンアメリカ発音**: デフォルトで seseo（c/z -> s）を使用

## フランス語（実装済み: 直接ポート）

### 変換パイプライン
```
テキスト → 正規化 → トークン化 → コンテキスト依存G2P
→ ストレスマーカー挿入 → IPA音素列
```

### 主要な処理
- **鼻母音**: an/am→ɑ̃, en/em→ɛ̃, on/om→ɔ̃, in/im→ɛ̃
- **複合音**: eau→o, ouille→uj, tion→sjɔ̃
- **最終音声サイレント**: 末尾 d,g,h,m,n,p,s,t,x,z は消音
- **ストレス**: 最終音節

### 音素インベントリ
- 母音: a, e, i, o, u, y_vowel, ə, ø, œ
- 鼻母音: ɑ̃, ɛ̃, ɔ̃ (PUA: \ue056-\ue058)
- 子音: p, b, t, d, k, ɡ, f, v, s, z, ʃ, ʒ, m, n, ɲ, l, ɥ, j, w, ʁ

### 実装状況

- **ファイル**: `Runtime/Core/Phonemizers/Backend/French/FrenchPhonemizerBackend.cs`（1,350行）
- **クラス**: `FrenchPhonemizerBackend : PhonemizerBackendBase`
- **名前空間**: `uPiper.Core.Phonemizers.Backend.French`
- **Name**: `"FrenchRuleBased"`, **Version**: `"1.0.0"`
- **SupportedLanguages**: `"fr"`, `"fr-FR"`
- **PUA使用**: `\uE056`（ɛ̃ 鼻母音）、`\uE057`（ɑ̃ 鼻母音）、`\uE058`（ɔ̃ 鼻母音）、`\uE01E`（y_vowel 前舌円唇母音）
- **Pythonからの移植**: piper-plus Python実装からの直接ポート。純C#実装、外部G2Pエンジン不要
- **主要処理**: 鼻母音処理、母音/子音ダイグラフ、サイレントレター（e muet）、リエゾン規則、ストレス配置、-er動詞語尾、ille/il例外、文脈依存x処理、半母音

## ポルトガル語（実装済み: 直接ポート）

### 変換パイプライン
```
テキスト → 正規化 → ストレス位置検出
→ G2P → 後処理（鼻母音化、L咽頭化、t/d変更） → IPA音素列
```

### 主要な処理
- **子音ダイグラフ**: nh→ɲ, lh→ʎ, ch→ʃ, rr→ʁ
- **鼻母音化**: 母音+n/m+子音 → 鼻化
- **後処理**: 重複鼻子音削除、L咽頭化(l→w)、t/d→tʃ/dʒ（ブラジルポルトガル語）

### 音素インベントリ
- 母音: a, e, i, o, u, ɛ, ɔ, ə + ã, ẽ, ĩ, õ, ũ
- 子音: p, b, t, d, k, ɡ, f, v, s, z, ʃ, ʒ, tʃ, dʒ, m, n, ɲ, l, ʎ, ɾ, ʁ, w, j

### 実装状況

- **ファイル**: `Runtime/Core/Phonemizers/Backend/Portuguese/PortuguesePhonemizerBackend.cs`（1,190行）
- **クラス**: `PortuguesePhonemizerBackend : PhonemizerBackendBase`
- **名前空間**: `uPiper.Core.Phonemizers.Backend.Portuguese`
- **Name**: `"Portuguese"`, **Version**: `"1.0.0"`
- **SupportedLanguages**: `"pt"`, `"pt-BR"`
- **PUA使用**: `\uE054`（tʃ 無声後部歯茎破擦音）、`\uE055`（dʒ 有声後部歯茎破擦音）。スペイン語と `\uE054` を共有
- **Pythonからの移植**: piper-plus Python実装からの直接ポート。純C#実装、外部G2Pエンジン不要
- **主要処理**: テキスト正規化、アクセント記号によるストレス検出、子音ダイグラフ（nh/lh/ch/rr）、鼻母音化、後処理（L咽頭化、ブラジルポルトガル語のt/d破擦音化）
- **ブラジルポルトガル語**: デフォルトでブラジル発音（t/d→tʃ/dʒ 変換等）を使用

## 中国語（実装済み: ルックアップテーブル + トーン規則）

### 変換パイプライン
```
テキスト → 文字→ピンインルックアップ → 正規化
→ トーンサンディ → ピンイン→IPA変換 → PUAマッピング → トーンマーカー追加
```

### 主要な処理
- **ピンイン取得**: `PinyinData.CharToPinyin` ルックアップテーブル（約700文字、最頻出漢字）
- **ピンイン正規化**: y/w規約、v→u-umlaut変換
- **ピンイン分割**: 声母（initial）と韻母（final）に分割
- **IPA変換**: `InitialToIpa` / `FinalToIpa` 固定マッピングテーブル
- **トーンサンディ**: 4つの連動規則（第3声連続、一/不変調）
- **児化処理**: erhua token挿入

### 音素インベントリ（IPA）
- 声母IPA: p, pʰ, m, f, t, tʰ, n, l, k, kʰ, x, tɕ, tɕʰ, ɕ, tʂ, tʂʰ, ʂ, ɻ, ts, tsʰ, s
- 韻母IPA: a, o, ɤ, i, u, y_vowel + 複合韻母（aɪ, eɪ, aʊ, oʊ等）+ 鼻韻母（an, ən, aŋ, əŋ, uŋ等）
- トーンマーカー: tone1-tone5（PUA: \uE046-\uE04A）

### C#実装戦略（実施済み）
1. **ピンイン取得**: 事前計算データ（`PinyinData.CharToPinyin`）によるルックアップテーブル。**pypinyin不使用** -- 外部依存なしの純C#実装
2. **ピンイン→IPA変換**: 固定マッピングテーブル（`InitialToIpa` / `FinalToIpa`）
3. **トーン規則**: 4つの連動規則（第3声+第3声→第2声、一の変調、不の変調）
4. **phonemize_from_pinyin_syllables API**: ピンイン列入力に対応（外部ピンインソース利用時）

### 実装状況

- **ファイル**:
  - `Runtime/Core/Phonemizers/Backend/Chinese/ChinesePhonemizerBackend.cs`（762行）- メインロジック
  - `Runtime/Core/Phonemizers/Backend/Chinese/PinyinData.cs`（955行）- 静的ルックアップテーブル
- **クラス**: `ChinesePhonemizerBackend : PhonemizerBackendBase`, `PinyinData`（static）
- **名前空間**: `uPiper.Core.Phonemizers.Backend.Chinese`
- **Name**: `"ChinesePhonemizer"`, **Version**: `"1.0.0"`
- **SupportedLanguages**: `"zh"`, `"zh-CN"`
- **PUA使用**: `\uE020`-`\uE04A`（有気音、二重母音、鼻韻母、トーンマーカー等）、`\uE00F`（ts、日本語と共有）、`\uE01E`（y_vowel、フランス語と共有）
- **Pythonとの違い**:
  - **pypinyin不使用**: Python版は `pypinyin` パッケージで文脈依存のピンイン取得を行うが、C#版は `PinyinData.CharToPinyin`（約700文字）のルックアップテーブルで代替。多音字（polyphone）は最頻出読みを使用
  - **Prosody情報**: A1=トーン番号（1-5）、A2=単語内音節位置、A3=単語長。`BuildWordInfo()` で連続漢字群から単語境界を推定
  - **外部ピンインAPI**: `PhonemizeFromPinyinSyllables()` メソッドで外部ピンインソースからの入力にも対応
- **WebGL対応**: `UNITY_WEBGL` 条件分岐で `Task.Run` を除去し `lock` による同期実行

## 韓国語（実装済み: Hangul分解 + 簡略音韻規則）

### 変換パイプライン
```
テキスト → NFC正規化 → Hangul分解（初声/中声/終声）
→ 音韻規則適用 → IPA変換 → PUAマッピング
```

### 主要な処理
- **Hangul分解**: Unicode演算（U+AC00..U+D7A3、19x21x28テーブル）で初声/中声/終声インデックスに分解
- **音韻規則**: 4つの規則を順序通りに適用:
  1. 激音化（gyeogeum-hwa）: h + 平音 or 平音 + h → 激音
  2. 連音化（yeoneum-hwa）: 終声 + 初声ㅇ → 終声が初声に移動
  3. 鼻音化（bieumhwa）: 閉鎖音終声 + 鼻音初声 → 鼻音化
  4. 硬音化（gyeongeumhwa）: 閉鎖音終声 + 平音初声 → 濃音化

### 音素インベントリ
- 初声IPA（19種）: k, kʰ, k̈, n, t, tʰ, ẗ, ɾ, m, p, pʰ, p̈, s, s̈, tɕ, tɕʰ, ẗɕ, h + ㅇ（無音）
- 中声IPA（21種）: a, ɛ, ja, jɛ, ʌ, e, jʌ, je, o, wa, wɛ, we, jo, u, wʌ, we, wi, ju, ɯ, ɰi, i
- 終声IPA（28種、0=なし）: k̚, n, t̚, l, m, p̚, ŋ 等（複合終声は代表音に簡略化）

### C#実装戦略（実施済み）
1. **Hangul分解**: Unicode演算で純C#実装（19x21x28テーブル）-- `DecomposeSyllable()` メソッド
2. **簡略音韻規則**: g2pk2/MeCab不使用で4つの音韻規則を実装
3. **MeCab代替**: 形態素解析なしの簡略版。単語境界は空白ベース

### 実装状況

- **ファイル**: `Runtime/Core/Phonemizers/Backend/Korean/KoreanPhonemizerBackend.cs`（965行）
- **クラス**: `KoreanPhonemizerBackend : PhonemizerBackendBase`
- **名前空間**: `uPiper.Core.Phonemizers.Backend.Korean`
- **Name**: `"Korean"`, **Version**: `"1.0.0"`
- **SupportedLanguages**: `"ko"`, `"ko-KR"`
- **PUA使用**:
  - 中国語と共有: `\uE020`-`\uE024`（pʰ, tʰ, kʰ, tɕ, tɕʰ -- 有気音/破擦音）
  - 韓国語固有: `\uE04B`-`\uE04F`（濃音 p̈, ẗ, k̈, s̈, ẗɕ）、`\uE050`-`\uE052`（内破音 k̚, t̚, p̚）
- **Pythonとの違い**:
  - **g2pk2/MeCab不使用**: Python版は `g2pk2`（MeCab依存）で形態素解析ベースの音韻規則適用を行うが、C#版はHangul Unicode分解と静的ルールテーブルで代替
  - **内部データ構造**: `WordData` / `SyllableUnit` 構造体でテキストを構造化し、音韻規則を初声/中声/終声インデックスの書き換えで実装。IPA変換は最終段階で一括実行
  - **複合終声処理**: `ComplexFinalFirstElement` テーブルで複合終声（ㄳ, ㄵ, ㄶ等）の分割を管理
  - **Prosody情報**: A1=0, A2=0, A3=音節数（単語内のHangul音節数）
- **WebGL対応**: `UNITY_WEBGL` 条件分岐で `Task.Run` を除去し `lock` による同期実行
- **外部依存なし**: 純アルゴリズム実装、メモリフットプリント約64KB（静的テーブルのみ）

## Prosody情報の言語別対応

| 言語 | A1 | A2 | A3 | 備考 |
|------|----|----|----|----|
| ja | モーラ位置 | アクセント核位置 | アクセント句位置 | 完全対応 |
| en | 0 | 0 | 0 | Prosodyなし |
| zh | tone(1-5) | 音節位置 | 単語長 | **非ゼロ値を計算**（ただしモデルのprosody_language_idsに含まれない場合ゼロ化） |
| ko | 0 | 0 | 音節数 | **A3は非ゼロ**（ただしモデル内でゼロ化される可能性） |
| es | 0 | 0 | 0 | Prosodyなし |
| fr | 0 | 0 | 0 | Prosodyなし |
| pt | 0 | 0 | 0 | Prosodyなし |

**注意**: 中国語・韓国語のPhonemizerは `phonemize_with_prosody()` で非ゼロ値を返すが、
現在のモデル（`prosody_language_ids={0}` = 日本語のみ）ではモデル内部でゼロ化される。
将来的に `prosody_language_ids` を拡張すれば、中国語のtone情報等を活用可能。

## 実装優先順位（完了）

全5言語の実装が完了した。実装順序と実績:

1. **スペイン語** - 最も容易、外部依存なし -- **完了**（1,233行、piper-plus `spanish.py` から直接ポート）
2. **フランス語** - ルールベース -- **完了**（1,350行、純C#実装）
3. **ポルトガル語** - ルールベース、後処理多段 -- **完了**（1,190行、ブラジルポルトガル語対応）
4. **中国語** - ルックアップテーブル方式 -- **完了**（1,716行 = 762行+955行、pypinyin不使用）
5. **韓国語** - Hangul分解+簡略規則 -- **完了**（965行、g2pk2/MeCab不使用）

合計: 約6,500行のC#コード（共有コンポーネント除く）。全言語で外部依存なしの純C#実装を実現。