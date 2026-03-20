# uPiper 多言語対応レポート - 2026年3月版

## 現在の対応状況（Phase 6完了: DotNetG2P移行）

> **Note**: Phase 5で実装した自前バックエンド（SpanishPhonemizerBackend等）はPhase 6でDotNetG2Pパッケージに移行済み。MultilingualPhonemizerはDotNetG2Pエンジンを直接呼び出す。中国語のみレガシーバックエンド（ChinesePhonemizerBackend）を継続使用。

### 対応言語一覧（7言語）

| 言語 | コード | バックエンド | G2P方式 | 状態 |
|------|--------|-------------|---------|------|
| 日本語 | ja | DotNetG2PPhonemizer | MeCab辞書 + Prosody | ✅ 実装済み |
| 英語 | en | EnglishG2PEngine (DotNetG2P) | Flite LTS | ✅ Phase 6移行済み |
| スペイン語 | es | SpanishG2PEngine (DotNetG2P) | ルールベースG2P | ✅ Phase 6移行済み |
| フランス語 | fr | FrenchG2PEngine (DotNetG2P) | ルールベースG2P（鼻母音・黙字処理） | ✅ Phase 6移行済み |
| ポルトガル語 | pt | PortugueseG2PEngine (DotNetG2P) | ルールベースG2P（ブラジルポルトガル語） | ✅ Phase 6移行済み |
| 中国語 | zh | ChinesePhonemizerBackend | ピンイン変換テーブル + IPA変換 + 声調処理 | ✅ Phase 5（レガシー） |
| 韓国語 | ko | KoreanG2PEngine (DotNetG2P) | ハングル分解 + 音韻規則 | ✅ Phase 6移行済み |

### 共有インフラストラクチャ（Phase 5 → Phase 6）

| コンポーネント | 場所 | 役割 |
|--------------|------|------|
| `PuaTokenMapper` | `Multilingual/` | 全言語共通の87固定PUAマッピング |
| `LanguageConstants` | `Multilingual/` | 言語コード（ISO 639-1）・言語IDマッピング |
| `UnicodeLanguageDetector` | `Multilingual/` | Unicode範囲ベースの言語検出（7言語対応） |
| `MultilingualPhonemizer` | `Multilingual/` | テキストを言語別に分割し各バックエンドに委譲 |

### 言語検出の仕組み
- **日本語**: カナ（ひらがな・カタカナ）で一意に検出
- **韓国語**: ハングル文字で一意に検出
- **中国語**: CJK統合漢字（カナ非存在時に日本語と区別）
- **ラテン文字系（en/es/fr/pt）**: Unicode範囲のみでは区別不可。`defaultLatinLanguage`パラメータで指定

### テスト
- Phase 5で194件の新規テストを追加
- 各バックエンドの単体テスト + MultilingualPhonemizer統合テスト

## 今後の改善（未実装）

### 追加言語G2P

| 言語 | 難易度 | 話者数 | アプローチ |
|------|--------|--------|-----------|
| ドイツ語 | 中 | 1.3億人 | 複合語分解、ウムラウト処理 |

### テキスト正規化（数字・日付・記号）
- 数字→単語変換（言語別ルール）
- URL除去/メールアドレス読み上げ
- ハッシュタグ処理
- **難易度**: 中 / **インパクト**: 高

### 多言語混在処理（コードスイッチング）の高度化
- ~~Unicode範囲ベースの言語検出拡張（CJK、Hangul追加）~~ ✅ Phase 5で実装済み
- FastTextベース言語識別統合（ラテン文字系言語の自動判別向上）
- 言語境界でのProsody平滑化
- **難易度**: 高 / **インパクト**: 高

### 固有名詞処理
- Universal NER統合で自動検出
- 固有名詞辞書追加（`proper_nouns_dict.json`）
- **難易度**: 高 / **インパクト**: 中

### 音素体系の統一（IPA基盤）
- X-SAMPAベース中間表現で全言語統一
- espeak-ng統合で100+言語対応
- **難易度**: 高 / **インパクト**: 非常に高

## ロードマップ

### ~~Phase 1~~ ✅ 完了: 基礎多言語化
1. ~~中国語G2P~~ ✅ ChinesePhonemizerBackend
2. ~~韓国語G2P~~ ✅ KoreanPhonemizerBackend
3. ~~スペイン語・フランス語・ポルトガル語G2P~~ ✅ Phase 5
4. ~~PuaTokenMapper・LanguageConstants共有インフラ~~ ✅ Phase 5

### Phase 2（3-6ヶ月）: UX向上
1. ラテン文字系言語の自動検出高度化（FastText等）
2. 絵文字処理
3. カスタム辞書の多言語拡張
4. テキスト正規化強化

### Phase 3（6-12ヶ月）: アーキテクチャ刷新
1. espeak-ng統合
2. IPA統一音素体系
3. NER統合
4. ドイツ語G2P追加

## Sources

- [MeloTTS](https://github.com/myshell-ai/MeloTTS)
- [Piper Voices](https://rhasspy.github.io/piper-samples/)
- [CharsiuG2P](https://github.com/lingjzhu/CharsiuG2P)
- [bootphon/phonemizer](https://github.com/bootphon/phonemizer)
