# piper-plus v1.10.0 対応マイルストーン

**作成日**: 2026-04-06
**ベース文書**: [piper-plus-v1.10.0-migration-analysis.md](piper-plus-v1.10.0-migration-analysis.md)

---

## マイルストーン概要

調査の結果、11項目中 **5項目は対応不要**（既に実装済み or Unity制約で不可 or 設計上不適）と判明。
残る対応項目を3つのマイルストーンに分割する。

**チケット一覧**: [tickets/INDEX.md](tickets/INDEX.md)

| MS | テーマ | 対応項目数 | 依存関係 |
|----|--------|-----------|---------|
| **MS1** | 互換性修正 | 1項目 | なし（即着手可） |
| **MS2** | パフォーマンス改善 | 2項目 | なし（MS1と並行可） |
| **MS3** | 新機能追加 | 2項目 | SV対応はモデル更新待ち / MS3-2はMS2完了推奨 |

### 対応不要と判定された項目

| 項目 | 理由 |
|------|------|
| Prosodyテンソル型 (int32 vs int64) | Sentis が int64→int32 を自動キャスト。設計意図も明示済み |
| 日本語音素化キャッシュ | LRU 5000エントリ実装済み。piper-plus (2000) を上回る |
| ORT SessionOptions / セッションキャッシュ | Unity.InferenceEngine が低レベル設定を公開しないため不可 |
| ORT設定準拠 (ort-session-contract.toml) | Unity.InferenceEngine が低レベル設定を公開しないため不可 |
| 環境変数制御 | PiperConfig による制御が Unity の設計パターンに適合 |

---

## MS1: 互換性修正

**目標**: piper-plus v1.10.0 のモデルとの完全互換性を確保する

### MS1-1: PUA マッピング差分修正

**チケット**: [MS1-1_pua-mapping-fix.md](tickets/MS1-1_pua-mapping-fix.md)
**対応内容**: PuaTokenMapper と PhonemeEncoder の PUA マッピングを piper-plus と統一

**タスク**:

1. **0xE053 の扱い確認と修正**
   - uPiper では `ɔɪ` (英語二重母音) を 0xE053 に割り当てている
   - piper-plus では予約(gap)扱い
   - DotNetG2P.English 側で 0xE053 を使用しているか確認し、不整合があれば修正
   - **ファイル**: `PuaTokenMapper.cs`, `PhonemeEncoder.cs`

2. **スウェーデン語 PUA (0xE059-0xE061) の追加**
   - 9つの長母音 PUA エントリを `PuaTokenMapper.FixedPuaMapping` に追加
   - **ファイル**: `PuaTokenMapper.cs`

3. **動的割り当て開始点の更新**
   - `_nextDynamic` の開始点を 0xE062 に変更（0xE059 から移動）
   - **ファイル**: `PuaTokenMapper.cs`

4. **テスト追加**
   - SV PUA マッピングの正引き・逆引きテスト
   - 0xE053 の扱いの回帰テスト
   - **ファイル**: `PuaTokenMapperTests.cs`

**完了条件**:
- `PuaTokenMapper.FixedPuaMapping` のエントリ数が piper-plus の `pua.json` と一致
- 既存テスト全パス + 新規テスト追加

---

## MS2: パフォーマンス改善

**目標**: 推論パイプラインの初回レイテンシと GC 圧力を改善する

### MS2-1: Warmup 実装

**チケット**: [MS2-1_warmup-implementation.md](tickets/MS2-1_warmup-implementation.md)
**対応内容**: モデル初期化後にダミー推論を実行し、初回レイテンシを削減する

**技術調査結果**:
- `InitializeAsync()` 完了直後が挿入点（メインスレッド上で実行済み）
- `ExecuteInference()` をダミー入力で再利用可能
- モデルの入力検出フラグ（`_supportsProsody`, `_supportsMultiSpeaker`, `_supportsLanguageId`）で動的に入力を構築

**タスク**:

1. **PiperConfig に Warmup 設定を追加**
   - `EnableWarmup: bool = false` (デフォルトOFF)
   - `WarmupIterations: int = 2` (piper-plus準拠)
   - **ファイル**: `PiperConfig.cs`

2. **WarmupAsync メソッドの実装**
   - piper-plus 仕様準拠: 100音素のダミー入力、BOS=1, EOS=2, ダミー音素=8
   - scales: [0.667, 1.0, 0.8]（デフォルト値）
   - `_supportsProsody` / `_supportsMultiSpeaker` / `_supportsLanguageId` に応じてオプション入力を動的構築
   - try-catch で失敗時は警告ログのみ（非ブロッキング）
   - テンソルは finally ブロックで確実に Dispose
   - **ファイル**: `InferenceAudioGenerator.cs`

3. **InitializeAsync に Warmup 呼び出しを追加**
   - モデル情報検出（行134-155）完了後に条件付きで `WarmupAsync()` を実行
   - **ファイル**: `InferenceAudioGenerator.cs`

4. **テスト追加**
   - Warmup 有効時に初期化が成功することを検証
   - Warmup 無効時（デフォルト）に動作が変わらないことを検証
   - **ファイル**: `InferenceAudioGeneratorTests.cs`

**完了条件**:
- `EnableWarmup = true` 時、InitializeAsync 内でダミー推論が実行される
- Warmup 失敗時もモデル初期化は成功する
- 既存テスト全パス

**期待効果**: 初回推論レイテンシ 500-800ms 削減

---

### MS2-2: ArrayPool によるメモリ最適化

**チケット**: [MS2-2_arraypool-optimization.md](tickets/MS2-2_arraypool-optimization.md)
**対応内容**: 推論パイプラインの大きな配列割り当てを ArrayPool で再利用し、GC 圧力を低減する

**技術調査結果**:
- 毎推論で約 530KB の配列が割り当てられている（22050Hz, 3秒テキスト時）
- 最も効果が大きい3箇所を特定済み
- IL2CPP 互換性: `System.Buffers.ArrayPool<T>` は .NET Standard 2.0 で完全サポート
- **注意**: Tensor が配列参照を保持するため、Rent→Tensor→Return の順序に注意が必要

**タスク**:

1. **ExtractAudioData() の ArrayPool 化** (最大効果)
   - 毎推論で 264KB (3秒@22050Hz) の float[] を割り当て
   - `ArrayPool<float>.Shared.Rent/Return` に置き換え
   - **ファイル**: `InferenceAudioGenerator.cs` 行399付近

2. **CreateProsodyTensor() の ArrayPool 化**
   - Prosody 有効時に可変サイズ (90-900要素) の int[] を割り当て
   - Tensor がメモリ参照を保持するため、所有権の管理に注意
   - **ファイル**: `InferenceAudioGenerator.cs` 行350付近

3. **NormalizeAudio() の ArrayPool 化**
   - 正規化時に 264KB の float[] を割り当て
   - **ファイル**: `AudioClipBuilder.cs` 行88付近

4. **IL2CPP 互換性の確保**
   - `IL2CPPCompatibility.cs` に ArrayPool ジェネリック型の `[Preserve]` を追加
   - **ファイル**: `IL2CPPCompatibility.cs`

5. **テスト追加**
   - ArrayPool 導入後の推論結果が変わらないことを検証
   - **ファイル**: `InferenceAudioGeneratorTests.cs`

**完了条件**:
- 3箇所の配列割り当てが ArrayPool に置き換わっている
- 既存テスト全パス
- IL2CPP ビルドで動作確認

**期待効果**: 推論ごとの GC 割り当て 約49% 削減（約530KB → 約272KB）

> ※ ExtractAudioData の public API 戻り値制約により ~100KB 以下には NativeArray 統一等の破壊的変更が必要

---

## MS3: 新機能追加

**目標**: piper-plus v1.10.0 の新機能を uPiper に統合する

### MS3-1: スウェーデン語 (SV) 対応

**チケット**: [MS3-1_swedish-language-support.md](tickets/MS3-1_swedish-language-support.md)
**前提条件**: スウェーデン語対応モデルが利用可能になった時点で着手

**対応内容**: DotNetG2P.Swedish（開発完了済み v1.9.0）を uPiper に統合する

**DotNetG2P.Swedish の準備状況** (確認済み):
- SwedishG2PEngine: 5フェーズG2Pルール、47音素、方言対応 (Central/Finland)
- Prosody API: `ToIpaWithProsody()` 実装済み
- PUA対応: `SwedishPuaMapper`, `ToPuaPhonemes()` 実装済み
- MultilingualG2PEngine に Lazy\<T\> 登録済み
- Unity asmdef 定義済み、`[Preserve]` アノテーション対応
- テスト 399+ケース

**タスク**:

1. **パッケージ参照の追加**
   - `Packages/manifest.json` に `com.dotnetg2p.swedish` を追加
   - **ファイル**: `Packages/manifest.json`

2. **LanguageConstants に SV を追加**
   - `LanguageIdSwedish = 7`
   - `CodeSwedish = "sv"` (既に定義済みなら確認のみ)
   - `AllLanguages` 配列に追加
   - **ファイル**: `LanguageConstants.cs`

3. **PuaTokenMapper に SV PUA を追加** (MS1-1 で対応済みの場合はスキップ)
   - 0xE059-0xE061 の 9 エントリ
   - **ファイル**: `PuaTokenMapper.cs`

4. **MultilingualPhonemizer に SV ルーティングを追加**
   - `SwedishG2PEngine` の Lazy 初期化
   - `ToPuaPhonemes()` / `ToIpaWithProsody()` の呼び出し
   - Prosody マッピング: A1=ピッチアクセント, A2=ストレス, A3=音節数
   - **ファイル**: `MultilingualPhonemizer.cs`

5. **UnicodeLanguageDetector の SV 対応検討**
   - ラテン文字のため自動検出は困難（英語と区別不可）
   - 明示的言語指定モード（`ForceDefault` / `VoiceConfigPrimary`）での対応を推奨
   - スウェーデン語特有の文字（å, ä, ö）による簡易判定を検討
   - **ファイル**: `UnicodeLanguageDetector.cs`

6. **テスト追加**
   - SV G2P ルーティングテスト
   - SV Prosody 値の検証
   - 多言語テキスト (JA+SV混在) のセグメント化テスト
   - **ファイル**: `MultilingualPhonemizerTests.cs`

**完了条件**:
- スウェーデン語テキストが正しく音素化される
- Prosody 情報 (A1/A2/A3) が正しく取得される
- 既存7言語のテストに影響なし

---

### MS3-2: 沈黙句分割 (PhonemeSilenceProcessor)

**チケット**: [MS3-2_phoneme-silence-processor.md](tickets/MS3-2_phoneme-silence-processor.md)
**対応内容**: 長文テキストを句単位に分割し、句間に沈黙を挿入する機能を追加する

**タスク**:

1. **PhonemeSilenceProcessor クラスの実装**
   - 沈黙トークン（`_`, `#`）の検出と句分割
   - 設定文字列のパース（`"_ 0.5,# 0.3"` 形式）
   - 句ごとの音素ID配列とProsodyスライスの分離
   - piper-plus C# 実装 (`PhonemeSilenceProcessor.cs`) を参照
   - **ファイル**: 新規 `Runtime/Core/AudioGeneration/PhonemeSilenceProcessor.cs`

2. **PiperConfig に沈黙句分割設定を追加**
   - `EnableSentenceSilence: bool = false`
   - `SentenceSilenceConfig: string = "_ 0.5"` (デフォルト)
   - **ファイル**: `PiperConfig.cs`

3. **InferenceAudioGenerator に句単位推論を統合**
   - 有効時: 音素列 → 句分割 → 各句を独立推論 → 句間沈黙挿入 → 結合
   - 無効時: 従来通り一括推論
   - **ファイル**: `InferenceAudioGenerator.cs`

4. **テスト追加**
   - 句分割ロジックのユニットテスト
   - 沈黙挿入後の音声長の検証
   - **ファイル**: 新規 `Tests/Editor/PhonemeSilenceProcessorTests.cs`

**完了条件**:
- 長文テキストが句単位で推論される
- 句間に適切な沈黙が挿入される
- 無効時は既存動作と同一

**期待効果**: 長文TTS品質改善 + メモリピーク削減

---

## オプション（優先度 P3 — 必要に応じて対応）

| 項目 | 内容 | トリガー |
|------|------|---------|
| ピーク正規化 | AudioClipBuilder に float→int16 ピーク正規化を追加 | 音量の一貫性が問題になった場合 |

---

## マイルストーンロードマップ

```
MS1: 互換性修正 ─────────────┐
  MS1-1: PUAマッピング修正    │
                              ├─ 並行可 ─┐
MS2: パフォーマンス改善 ──────┘          │
  MS2-1: Warmup実装                     │
  MS2-2: ArrayPool最適化                │
                                        │
MS3: 新機能追加 ────────────────────────┘
  MS3-1: スウェーデン語対応  ← モデル更新待ち
  MS3-2: 沈黙句分割
```

- **MS1 と MS2 は並行して着手可能**（相互依存なし）
- **MS3-1** はスウェーデン語対応モデルが利用可能になってから着手
- **MS3-2** は MS1/MS2 完了後に着手推奨（推論パイプラインの変更が重なるため）
