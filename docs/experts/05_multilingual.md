# uPiper 多言語対応レポート - 2026年3月版

## 最優先: Piperモデル追加（中国語・韓国語）
- Piperは**50以上の言語**に対応、HuggingFaceで配布
- G2Pさえあればモデルは既製品を利用可能
- **難易度**: 低 / **インパクト**: 高

## 追加言語G2P実装

| 言語 | 難易度 | 話者数 | アプローチ |
|------|--------|--------|-----------|
| 中国語 | 中 | 13億人 | pypinyin-g2pのC#移植、声調処理 |
| 韓国語 | 中 | 7700万人 | Hangul分解、音韻変化ルール |
| スペイン語 | 低 | 5.5億人 | ルールベースG2P（正書法≒音素） |
| フランス語 | 中 | 3億人 | サイレント文字・リエゾン処理 |
| ドイツ語 | 中 | 1.3億人 | 複合語分解、ウムラウト処理 |

## テキスト正規化（数字・日付・記号）
- 数字→単語変換（言語別ルール）
- URL除去/メールアドレス読み上げ
- ハッシュタグ処理
- **難易度**: 中 / **インパクト**: 高

## 多言語混在処理（コードスイッチング）
- Unicode範囲ベースの言語検出拡張（CJK、Hangul追加）
- FastTextベース言語識別統合
- 言語境界でのProsody平滑化
- **難易度**: 高 / **インパクト**: 高

## 固有名詞処理
- Universal NER統合で自動検出
- 固有名詞辞書追加（`proper_nouns_dict.json`）
- **難易度**: 高 / **インパクト**: 中

## 音素体系の統一（IPA基盤）
- X-SAMPAベース中間表現で全言語統一
- espeak-ng統合で100+言語対応
- **難易度**: 高 / **インパクト**: 非常に高

## ロードマップ

### Phase 1（3-6ヶ月）: 基礎多言語化
1. 中国語G2P（pypinyin C#移植）
2. 韓国語G2P（Hangul分解）
3. テキスト正規化強化

### Phase 2（3-6ヶ月）: UX向上
1. 言語検出の高度化
2. 絵文字処理
3. カスタム辞書拡張

### Phase 3（6-12ヶ月）: アーキテクチャ刷新
1. espeak-ng統合
2. IPA統一音素体系
3. NER統合

## Sources

- [MeloTTS](https://github.com/myshell-ai/MeloTTS)
- [Piper Voices](https://rhasspy.github.io/piper-samples/)
- [CharsiuG2P](https://github.com/lingjzhu/CharsiuG2P)
- [bootphon/phonemizer](https://github.com/bootphon/phonemizer)
