# dot-net-g2p 側タスク一覧

本ドキュメントは、uPiper 多言語対応のために dot-net-g2p リポジトリ (`ayutaz/dot-net-g2p`) 側で
対応が必要なタスクをまとめたものです。

## ステータスサマリ（Phase 5 完了時点）

**Phase 5 で uPiper 側に独立実装を完了したため、dot-net-g2p 側タスクの優先度が大幅に低下。**

- 中国語・韓国語の Phonemizer は piper-plus Python 実装から直接 C# にポートした
- dot-net-g2p の `DotNetG2P.Chinese` / `DotNetG2P.Korean` は使用していない
- 日本語 G2P（`DotNetG2PPhonemizer`）は Phase 5 で変更なし（安定稼働中）
- 以下のタスクは将来的に dot-net-g2p 統合が必要になった場合のみ対応

## タスク概要

| # | パッケージ | タスク | 優先度 | ステータス | 備考 |
|---|-----------|--------|--------|-----------|------|
| 1 | DotNetG2P.Chinese | piper-plus互換IPA出力追加 | ~~高~~ → **低** | 未着手 | uPiper側で直接実装により不要化 |
| 2 | DotNetG2P.Chinese | PUAマッピング対応 | ~~高~~ → **低** | 未着手 | PuaTokenMapper で uPiper 側対応済み |
| 3 | DotNetG2P.Chinese | Prosody情報出力API | ~~中~~ → **低** | 未着手 | ChinesePhonemizerBackend で対応済み |
| 4 | DotNetG2P.Korean | Jamo→IPA変換API追加 | ~~高~~ → **低** | 未着手 | KoreanPhonemizerBackend で対応済み |
| 5 | DotNetG2P.Korean | PUAマッピング対応 | ~~高~~ → **低** | 未着手 | PuaTokenMapper で uPiper 側対応済み |
| 6 | DotNetG2P.Korean | Prosody情報出力API | ~~中~~ → **低** | 未着手 | KoreanPhonemizerBackend で対応済み |
| 7 | DotNetG2P.Chinese | WebGL辞書ロード最適化 | 低 | 未着手 | uPiper側がルックアップテーブルのため不要 |

---

## Phase 5 の影響

### DotNetG2PPhonemizer（日本語）は変更なし

Phase 5 で追加された多言語バックエンドは `DotNetG2PPhonemizer` に一切影響しない。

- `DotNetG2PPhonemizer` は引き続き日本語 G2P 専用
- `MultilingualPhonemizer` は日本語セグメントを `DotNetG2PPhonemizer` に委譲
- dot-net-g2p パッケージ（`com.dotnetg2p`）のバージョン・API に変更なし
- MeCab辞書・カスタム辞書の読み込みパスも変更なし

### uPiper 側独立実装の採用理由

1. **依存関係の最小化**: dot-net-g2p パッケージの更新を待たずに実装可能
2. **piper-plus 完全互換**: Python 実装から直接ポートすることでIPA差異問題を回避
3. **軽量性**: 中国語は ~700 エントリのルックアップテーブルで頻出文字をカバー（dot-net-g2p の 44,435 + 411,958 エントリと比較して大幅に軽量）
4. **外部データ不要**: 韓国語は純粋アルゴリズム、中国語はコード内埋め込みテーブル

---

## タスク詳細（将来統合時の参考情報）

以下は dot-net-g2p 側の実装が将来的に必要になった場合の参考情報として残す。

### タスク1: DotNetG2P.Chinese - piper-plus互換IPA出力追加

**ステータス**: 未着手（uPiper側で直接実装により優先度低下）

**背景**: 既存の `PinyinToIpa.cs` の IPA 出力が piper-plus のモデル学習時の IPA と一部異なる。

**修正が必要な IPA マッピング**:

| ピンイン | 現在の出力 (dot-net-g2p) | 期待値 (piper-plus) | 備考 |
|---------|------------------------|-------------------|------|
| zh | ʈʂ | tʂ | retroflex表記の違い |
| ch | ʈʂʰ | tʂʰ | 同上 |
| -ong | ʊŋ | uŋ | 母音の違い |
| -iu | ioʊ | iou | 二重母音の違い |
| er | əɻ | ɚ | r-colored schwa表記 |
| -iong | iʊŋ | iuŋ | ong同様 |

**統合が必要になるケース**:
- 中国語テキストのカバレッジが不足した場合（現在 ~700 エントリ → 44,435 単字辞書が必要）
- 多音字（ポリフォン）の文脈依存読み分けが必要になった場合
- 熟語単位のピンイン解決が必要になった場合

**参照ファイル**:
- piper-plus: `src/python/piper_train/phonemize/chinese.py` (行45-126)
- dot-net-g2p: `src/DotNetG2P.Chinese/Conversion/PinyinToIpa.cs`

---

### タスク2: DotNetG2P.Chinese - PUAマッピング対応

**ステータス**: 未着手（uPiper側 `PuaTokenMapper.cs` で対応済み）

**PUAマッピング** (0xE020-0xE04A, 43エントリ): `PuaTokenMapper.FixedPuaMapping` で管理。

---

### タスク3: DotNetG2P.Chinese - Prosody情報出力API

**ステータス**: 未着手（uPiper側 `ChinesePhonemizerBackend` で対応済み）

---

### タスク4: DotNetG2P.Korean - Jamo→IPA変換API追加

**ステータス**: 未着手（uPiper側 `KoreanPhonemizerBackend` で対応済み）

**背景**: 既存の `KoreanG2PEngine` は Jamo (ㄱ, ㅏ, ㄴ) を出力するが、piper-plus モデルは IPA (k, a, n) を入力とする。

**uPiper Phase 5 での対応**: `KoreanPhonemizerBackend` 内に完全な Jamo→IPA マッピングテーブルを実装。
初声19 + 中声21 + 終声28 の全エントリを `InitialToIpa`/`MedialToIpa`/`FinalToIpa` 配列で管理。

---

### タスク5: DotNetG2P.Korean - PUAマッピング対応

**ステータス**: 未着手（uPiper側 `KoreanPhonemizerBackend` + `PuaTokenMapper.cs` で対応済み）

**PUAマッピング** (8エントリ + 中国語共有5エントリ):
```
Korean固有: 0xE04B-0xE052 (tense consonants + unreleased finals)
中国語共有: 0xE020-0xE024 (aspirated/affricate consonants)
```

---

### タスク6: DotNetG2P.Korean - Prosody情報出力API

**ステータス**: 未着手（uPiper側 `KoreanPhonemizerBackend` で対応済み）

uPiper Phase 5 での実装: a1=0, a2=0, a3=音節数 (piper-plus `korean.py` 互換)

---

### タスク7: DotNetG2P.Chinese - WebGL辞書ロード最適化（低優先度）

**ステータス**: 不要化

uPiper Phase 5 では `PinyinData.cs` にルックアップテーブルをコード内埋め込みしたため、
WebGL での辞書ロード問題は発生しない。dot-net-g2p の `DotNetG2P.Chinese` を統合する場合にのみ関連。

---

## uPiper 側で完了したタスク（参考）

Phase 5 で以下のタスクが全て完了:

| タスク | 内容 | ステータス |
|--------|------|-----------|
| ChinesePhonemizerBackend 実装 | piper-plus chinese.py から直接ポート | **完了** |
| KoreanPhonemizerBackend 実装 | piper-plus korean.py から直接ポート | **完了** |
| SpanishPhonemizerBackend 実装 | ルールベースG2P | **完了** |
| FrenchPhonemizerBackend 実装 | ルールベースG2P | **完了** |
| PortuguesePhonemizerBackend 実装 | ルールベースG2P | **完了** |
| PuaTokenMapper 実装 | 全言語PUA↔IPA双方向マッピング | **完了** |
| LanguageConstants 実装 | 言語ID/コード定数 | **完了** |
| UnicodeLanguageDetector 拡張 | 7言語対応（Hangul, CJK曖昧性解決） | **完了** |
| MultilingualPhonemizer 統合 | 全バックエンドの初期化・委譲・結合 | **完了** |
| Phase 5 テスト | 207テスト（6ファイル） | **完了** |

---

## クロス言語依存関係

| 依存元 | 依存先 | 内容 | ステータス |
|--------|--------|------|-----------|
| MultilingualPhonemizer | DotNetG2PPhonemizer | 日本語セグメント委譲 | 安定 |
| MultilingualPhonemizer | FliteLTSPhonemizer | 英語セグメント委譲 | 安定 |
| KoreanPhonemizerBackend | PuaTokenMapper | PUAコードポイント共有（zh共通5エントリ） | 安定 |
| ChinesePhonemizerBackend | PuaTokenMapper | PUAコードポイント（43エントリ） | 安定 |
| PhonemeEncoder | PuaTokenMapper | IPAモデルでのPUA→IPA逆変換 | Phase 6 で検証予定 |

**重要**: dot-net-g2p は別リポジトリ（`ayutaz/dot-net-g2p`）であり、uPiper 側の責務でコードを変更しないこと。