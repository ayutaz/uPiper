# 残課題 技術調査結果

## Phase 5 完了により解決済みの課題

### [解決済み] 中国語Phonemizer - uPiper側実装完了

Phase 5 で `ChinesePhonemizerBackend` を uPiper 側に直接実装。dot-net-g2p の `DotNetG2P.Chinese` に依存せず、
piper-plus `chinese.py` から直接ポートした。

**実装内容**:
- `ChinesePhonemizerBackend.cs` - ピンイン→IPA変換（piper-plus互換出力）
- `PinyinData.cs` - ルックアップテーブル（CharToPinyin ~700エントリ、InitialToIpa/FinalToIpa完備）
- 声調変調（三声連読、一/不の声調変調）
- Prosody出力（a1=声調、a2=語内位置、a3=語長）
- PUAマッピング（0xE020-0xE04A、43エントリ）
- 30テスト通過

**旧課題との関係**:
- IPA出力の差異問題 → piper-plus から直接ポートしたため不要（dot-net-g2p 経由しない）
- PUAマッピング → `PuaTokenMapper.cs` で統一管理（全言語共通）
- Prosody情報出力 → `PhonemizeFromPinyinSyllables()` で対応済み

### [解決済み] 韓国語Phonemizer - uPiper側実装完了

Phase 5 で `KoreanPhonemizerBackend` を uPiper 側に直接実装。dot-net-g2p の `DotNetG2P.Korean` に依存せず、
piper-plus `korean.py` から直接ポートした。

**実装内容**:
- `KoreanPhonemizerBackend.cs` - Hangul分解→音韻規則適用→IPA変換
- 初声19 + 中声21 + 終声28 の完全IPAマッピングテーブル
- 4種類の音韻規則（連音化、鼻音化、激音化、硬音化）
- PUAマッピング（0xE04B-0xE052、8エントリ + 中国語と共有5エントリ）
- 純粋アルゴリズム（外部データファイル不要）
- 43テスト通過

**旧課題との関係**:
- Jamo→IPA変換 → `InitialToIpa`/`MedialToIpa`/`FinalToIpa` テーブルで実装済み
- PUAマッピング → backend内蔵 + `PuaTokenMapper.cs` で統一管理
- Prosody情報出力 → a1=0, a2=0, a3=音節数 で piper-plus 互換

### [解決済み] LanguageDetector パフォーマンス最適化

Phase 5 で `UnicodeLanguageDetector` を実装。char範囲チェック方式を採用。

**実装内容**:
- `UnicodeLanguageDetector.cs` - 7言語対応（ja, en, zh, ko, es, fr, pt）
- ゼロアロケーション文字判定（`AggressiveInlining` 付き）
- Hangul範囲（U+AC00-D7A3）対応
- CJK曖昧性解決（日本語/中国語 - Kana有無で判定）
- `defaultLatinLanguage` による Latin 文字言語指定
- `SegmentText()` でテキストを言語セグメントに分割

### [解決済み] 新言語Phonemizerバックエンド

Phase 5 で 5 言語のPhonemizerバックエンドを新規実装:

| バックエンド | 方式 | テスト数 |
|------------|------|---------|
| `SpanishPhonemizerBackend` | ルールベースG2P | 30 |
| `FrenchPhonemizerBackend` | ルールベースG2P | 36 |
| `PortuguesePhonemizerBackend` | ルールベースG2P | 39 |
| `ChinesePhonemizerBackend` | ピンインルックアップ + トーン規則 | 30 |
| `KoreanPhonemizerBackend` | Hangul分解 + 音韻規則 | 43 |

### [解決済み] MultilingualPhonemizer統合・PuaTokenMapper・LanguageConstants

- `MultilingualPhonemizer.cs` - 全7言語のバックエンド管理・セグメント分割・統合
- `PuaTokenMapper.cs` - 全言語のPUA↔IPA双方向マッピング（89固定エントリ、動的拡張対応）
- `LanguageConstants.cs` - 言語ID/コード定数・分類ヘルパー
- 29テスト（Phase 5 統合テスト）

---

## 現在の残課題

### 1. 中国語ピンインルックアップテーブルのカバレッジ制限

**現状**: `PinyinData.CharToPinyin` は約700エントリ（コメントには ~3500 と記載あるが実際は ~700）。
dot-net-g2p の `DotNetG2P.Chinese` は 44,435 エントリの単字辞書 + 411,958 エントリの熟語辞書を持つ。

**影響**:
- 一般的な中国語テキストの大部分はカバーするが、低頻度漢字で未知文字エラーが発生する
- 多音字（同一漢字の複数読み）は最頻出の読みのみ収録
- 熟語単位の読み分けは非対応（文字単位のルックアップのみ）

**対応選択肢**:
- A: 現状維持（頻出文字で十分実用的）
- B: `PinyinData.CharToPinyin` を拡張（3,500-7,000エントリへ）
- C: `DotNetG2P.Chinese` パッケージを将来的に統合（完全カバレッジ）

**推奨**: Phase 6 のテスト結果を踏まえてカバレッジ不足が問題になった場合に B を検討。

### 2. 韓国語 - g2pk2 相当の高度な音韻規則が未実装

**現状**: `KoreanPhonemizerBackend` は主要4規則（連音化、鼻音化、激音化、硬音化）を実装。
g2pk2 が持つ一部の高度な規則は未実装。

| 規則 | KoreanPhonemizerBackend | g2pk2 | 影響 |
|------|:---:|:---:|------|
| 連音化 | 済 | 済 | なし |
| 鼻音化 | 済 | 済 | なし |
| 激音化 | 済 | 済 | なし |
| 硬音化(基本) | 済 | 済 | なし |
| 流音化 | 未 | 済 | 低 |
| 終声中和 | 済（テーブル内蔵） | 済 | なし |
| 助詞「의」読み | 未 | 済 | 低 |
| 用言語尾硬音化 | 未 | 済 | 中 |
| 口蓋音化 | 未 | 済 | 低 |

**結論**: 主要規則のカバレッジはg2pk2と同等。TTS品質は実用レベル。
将来的に品質向上が必要な場合は流音化・口蓋音化の追加を検討。

### 3. dot-net-g2p 既存実装との統合可能性

**背景**: dot-net-g2p に `DotNetG2P.Chinese` と `DotNetG2P.Korean` が既に存在する。
Phase 5 では uPiper 側に独立実装を行ったが、将来的に dot-net-g2p 側の高品質実装と統合する選択肢がある。

**中国語**:
- dot-net-g2p: 44,435 単字 + 411,958 熟語辞書（高カバレッジ）
- uPiper Phase 5: ~700 エントリのルックアップテーブル（軽量）
- 統合する場合: IPA出力の差異（5箇所）を dot-net-g2p 側で修正する必要あり（23_dotnetg2p_tasks.md 参照）

**韓国語**:
- dot-net-g2p: 6種類の音韻規則 + 例外辞書（179テスト）
- uPiper Phase 5: 4種類の音韻規則（43テスト）
- 統合する場合: Jamo→IPA変換レイヤーを dot-net-g2p 側に追加する必要あり

**推奨**: 現時点では uPiper 側の独立実装で運用し、品質要件に応じて将来統合を検討。

---

## 既存課題（変更なし）

### 4. PhonemeEncoder N音素変種の完全なデータフロー

### フロー図

```
[dot-net-g2p] G2PEngine
  出力: 汎用 "N" (変種の区別なし)
    |
    v
[uPiper] DotNetG2PPhonemizer.ApplyNPhonemeRules()
  次の音素を先読みして "N" を変種に置換:
  "N" → "N_m"      (次が m/my/b/by/p/py)
  "N" → "N_n"      (次が n/ny/t/ty/d/dy/ts/ch)
  "N" → "N_ng"     (次が k/ky/kw/g/gy/gw)
  "N" → "N_uvular" (文末、母音前、その他)
    |
    v
[uPiper] OpenJTalkToPiperMapping.PhonemeToPUA
  N_m → U+E019, N_n → U+E01A, N_ng → U+E01B, N_uvular → U+E01C
    |
    v
[uPiper] PhonemeEncoder.MapPhoneme()
  +-- PUAモデル: PUA文字 → phonemeToId でID取得
  |   N_m=ID61, N_n=ID62, N_ng=ID63, N_uvular=ID64
  |
  +-- IPAモデル: puaToPhonemeMap で全変種 → "N" (ID22) に統一
      (後方互換: ASCII "N" を使用、IPA "ɴ" (ID20) ではない)
```

### 処理責務の分離

| コンポーネント | 責務 | N変種の扱い |
|--------------|------|------------|
| dot-net-g2p | MeCab解析→音素生成 | 常に汎用 "N" を出力（変種の区別なし） |
| piper-plus (Python) | 学習データ準備 | `_apply_n_phoneme_rules()` で変種を決定 |
| uPiper (C#) | 推論時の音素化 | `ApplyNPhonemeRules()` で変種を決定（Python側と同一ロジック） |

### 重要ファイル

- `PhonemeEncoder.cs` 行133-137: N変種→PUA定義
- `PhonemeEncoder.cs` 行195-202: PUA→"N"逆変換（IPAモデル用）
- `DotNetG2PPhonemizer.cs` 行472-524: `ApplyNPhonemeRules()`
- `OpenJTalkToPiperMapping.cs` 行218-228: N変種→PUA変換

**注意**: Python/C#のルールは完全一致しており、学習時と推論時で同じN変種が生成される。

---

## Phase 6 残タスク: テスト・品質保証

Phase 1-5 が全て完了し、残るは Phase 6（テスト・品質保証）のみ。

### 6-1. クロスプラットフォーム検証

| プラットフォーム | 検証項目 | ステータス |
|-----------------|---------|-----------|
| Windows/Linux Editor | 全7言語バックエンド初期化・音素化 | 未検証 |
| macOS Editor | 全7言語バックエンド初期化・音素化 | 未検証 |
| WebGL | 非同期初期化・Task.Run除去 | 未検証 |
| iOS/Android | IL2CPP互換性・リフレクション | 未検証 |

### 6-2. CI/CDパイプライン拡張

- 既存 `unity-tests.yml` に Phase 5 テスト（207テスト）の追加
- 新言語バックエンドのEditModeテスト実行確認
- `dotnet-format.yml` で新ファイルのフォーマットチェック

### 6-3. 多言語テストスイート拡充

- MultilingualPhonemizer 混合言語テスト（3言語以上混在）
- エッジケース: 空文字列、句読点のみ、絵文字混在
- パフォーマンステスト: 長文テキストの処理時間計測
- PuaTokenMapper 動的割り当てのスレッドセーフ性テスト