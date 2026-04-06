# piper-plus v1.10.0 変更調査 — uPiper対応項目一覧

**調査日**: 2026-04-06
**piper-plus バージョン**: v1.10.0 (最終コミット: 2026-04-06)
**uPiper バージョン**: v1.4.0

---

## 概要

piper-plus v1.10.0 では以下の大きな変更が行われた:

1. ONNX Runtime SessionOptions の全実装統一 (ort-session-contract.toml)
2. Warmup / セッションキャッシュの全実装統一
3. 日本語音素化 LRU キャッシュ
4. WASM G2P 多言語実装 (ES/FR/PT/ZH)
5. スウェーデン語 (SV) G2P 新規追加
6. C API 共有ライブラリ / C# CLI 拡充
7. パフォーマンス最適化 (コールドスタート 2s → 300ms)

本文書では、uPiper 側で対応が必要な項目を優先度別に整理する。

---

## 対応項目一覧

### P0: 互換性に影響 — 早急に対応が必要

#### 1. PUA マッピングの差分 (10エントリ)

**現状**: uPiper の `PuaTokenMapper.cs` は 88 エントリ、piper-plus の `pua.json` は 96 エントリ。

| 差分 | コード | 内容 | 対応 |
|------|--------|------|------|
| uPiper のみ | 0xE053 | `ɔɪ` (英語二重母音) | piper-plus 側では予約(gap)扱い。互換性確認が必要 |
| piper-plus のみ | 0xE059 | `iː` (SV長母音) | スウェーデン語対応時に追加 |
| piper-plus のみ | 0xE05A | `yː` (SV長母音) | 同上 |
| piper-plus のみ | 0xE05B | `eː` (SV長母音) | 同上 |
| piper-plus のみ | 0xE05C | `ɛː` (SV長母音) | 同上 |
| piper-plus のみ | 0xE05D | `øː` (SV長母音) | 同上 |
| piper-plus のみ | 0xE05E | `ɑː` (SV長母音) | 同上 |
| piper-plus のみ | 0xE05F | `oː` (SV長母音) | 同上 |
| piper-plus のみ | 0xE060 | `uː` (SV長母音) | 同上 |
| piper-plus のみ | 0xE061 | `ʉː` (SV長母音) | 同上 |

**影響範囲**:
- `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/PuaTokenMapper.cs`
- `Assets/uPiper/Runtime/Core/AudioGeneration/PhonemeEncoder.cs` (multiCharPhonemeMap)

**対応方針**:
- 0xE053 の扱いを piper-plus と統一（piper-plus 側の pua.json を正とする）
- スウェーデン語 PUA (0xE059-0xE061) を追加
- 動的割り当て開始点を 0xE062 に更新

---

#### 2. ~~Prosody テンソル型の不一致 (int32 vs int64)~~ → 対応不要（調査済み）

**現状**: piper-plus (Python/C#) は `int64` (long) で prosody_features テンソルを構築するが、uPiper は `int` (int32) を使用。

**追加調査結果 (2026-04-06)**:
- Sentis (Unity.InferenceEngine) は ONNX の int64 入力を検出すると、内部で自動的に int32 にキャストして処理する（`ONNXConstantsLoader.IntFromInt64()`）
- uPiper の初期化時に `DataType.Int` チェックを行っており、不一致があれば例外で検出される（行162-169）
- Prosody の実際の値 (A1/A2/A3) は 0-10 程度の小さい整数のため、int32 の値域で十分
- InferenceAudioGenerator.cs 行349 のコメント「`ONNX model expects Int (int64 in Python, mapped to Int in Sentis)`」で設計意図を明示済み

**結論**: **修正不要。現状で正しく動作する。**

---

### P1: 機能追加 — パフォーマンス改善

#### 3. ONNX Warmup 実装

**現状**: piper-plus は全実装（Python/C++/Rust/C#）で統一された Warmup を実装。uPiper は未実装。

**piper-plus の Warmup 仕様** (ort-session-contract.toml):
- 100 音素のダミー入力で 2 回推論実行
- BOS=1, EOS=2, ダミー音素=8
- scales: [0.667, 1.0, 0.8]
- sid/lid/prosody_features はモデルメタデータを検査して動的に構築
- 失敗時は警告ログのみ（非ブロッキング）
- CLI フラグ `--no-warmup` で無効化可能

**期待される効果**: 初回推論レイテンシ 500-800ms 削減

**影響範囲**:
- `Assets/uPiper/Runtime/Core/AudioGeneration/InferenceAudioGenerator.cs` (InitializeAsync)

**対応方針**:
- `InitializeAsync()` 完了後にオプションの Warmup 推論を実行
- `PiperConfig` に `EnableWarmup` フラグを追加
- Unity.InferenceEngine の Worker に対してダミー推論を発行
- エラーハンドリング: try-catch で失敗時は警告のみ

---

#### 4. ~~日本語音素化キャッシュ~~ → 対応不要（実装済み・piper-plusを上回る）

**追加調査結果 (2026-04-06)**:
uPiper の `PhonemeCache.cs` は既に十分な LRU キャッシュを実装済みであり、piper-plus を上回る機能を持つ。

| 項目 | piper-plus | uPiper (実装済み) |
|------|-----------|------------------|
| キャッシュ方式 | `@lru_cache` (functools) | 自実装LRU |
| maxsize | 2,000 | **5,000** |
| キャッシュキー | テキスト全体 | テキスト or FNV-1aハッシュ（500文字超は高速ハッシュ） |
| 有効期限 | なし | 1時間（デフォルト） |
| スレッド安全性 | Python GIL | lock機構で明示的保護 |
| クリアAPI | `clear_phonemize_cache()` | `PhonemeCache.Clear()` |
| 統計情報 | なし | `GetStatistics()` (HitRate, MemoryUsage等) |
| プルーニング | なし | `PruneExpiredEntries()` |

**結論**: **追加対応不要。**

---

#### 5. ArrayPool によるメモリ最適化

**現状**: piper-plus C# は `ArrayPool<long>` でプロソディバッファを再利用。uPiper は毎回新規割り当て。

**piper-plus の実装**:
```csharp
if (prosodySize > 64)
{
    rentedProsody = ArrayPool<long>.Shared.Rent(prosodySize);
    Array.Clear(rentedProsody, 0, prosodySize);
}
```

**影響範囲**:
- `Assets/uPiper/Runtime/Core/AudioGeneration/InferenceAudioGenerator.cs`
- `Assets/uPiper/Runtime/Core/AudioGeneration/PhonemeEncoder.cs`

**対応方針**:
- 推論ループで頻繁に割り当てられる大きなバッファ（音素ID配列、Prosody配列）に ArrayPool を導入
- GC 圧力の低減（特にリアルタイム音声生成時に有効）

---

### P2: 新機能対応

#### 6. スウェーデン語 (SV) G2P 対応

**現状**: piper-plus はスウェーデン語 G2P を完全実装（ルールベース）。uPiper は `LanguageConstants.cs` にコード定義のみ。
**DotNetG2P.Swedish は開発完了済み**（`com.dotnetg2p.swedish` v1.9.0）。uPiper側の統合作業のみが残っている。

**DotNetG2P.Swedish の実装状況** (確認済み):
- `SwedishG2PEngine`: 5フェーズG2Pルール、47音素定義
- 方言対応: Central Swedish (rikssvenska) / Finland Swedish (finlandssvenska)
- ピッチアクセント予測: Accent 1/2 の自動推定
- Prosody API: `ToIpaWithProsody()` → A1=ピッチアクセント(0/1/2), A2=ストレス(0/1), A3=語の音節数
- PUA対応: `SwedishPuaMapper.cs`, `ToPuaPhonemes()`, `ToPuaString()`
- テキスト正規化: 11段階パイプライン（略語・日付・通貨・数字等）
- 例外辞書: 549語の手動キュレーション辞書
- テスト: 399+テストケース（3006行）
- Unity対応: asmdef定義、`[Preserve]` アノテーション、Unity 2021.2+
- **MultilingualG2PEngine に統合済み**（Lazy\<T\>登録）

**piper-plus のスウェーデン語仕様**:
- Prosody: A1=ピッチアクセント(0/1/2), A2=stress(0/1), A3=語の音節数
- 補色的量規則（Complementary Quantity）による長短母音決定
- そり舌化アッセミレーション (r+{t,d,s,n,l} → そり舌音) — Central方言のみ
- PUA マッピング: 0xE059-0xE061（9つの長母音）
- 言語 ID: 7

**uPiper側の残作業**:
- `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/LanguageConstants.cs` に `LanguageIdSwedish = 7` を追加
- `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/PuaTokenMapper.cs` に SV PUA (0xE059-0xE061) を追加
- `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/MultilingualPhonemizer.cs` に SV ルーティング追加（`SwedishG2PEngine` の初期化・`ToPuaPhonemes()`/`ToIpaWithProsody()` 呼び出し）
- `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/UnicodeLanguageDetector.cs` に SV 検出ロジック追加（ラテン文字のため英語との区別が課題。言語指定モードでの対応が現実的）
- `Packages/manifest.json` に `com.dotnetg2p.swedish` パッケージ参照を追加

**対応方針**:
- DotNetG2P.Swedish は開発完了済みのため、uPiper 側の統合作業のみ
- 現行モデル（multilingual-test-medium）がスウェーデン語をサポートしていないため、モデル更新と同時に対応
- Unicode による自動言語検出はラテン文字同士の区別が困難なため、明示的な言語指定モードでの対応を推奨

---

#### 7. 沈黙句分割 (PhonemeSilenceProcessor)

**現状**: piper-plus C# は `PhonemeSilenceProcessor` で音素列を句単位に分割し、句間に沈黙を挿入する機能を持つ。uPiper は未実装。

**piper-plus の仕様**:
- 沈黙トークン（`_`, `#`）を検出して句を分割
- 各句を独立推論（メモリ効率向上）
- 句間に指定秒数のゼロサンプルを挿入
- 設定: `"_ 0.5,# 0.3"` のようなカンマ区切り

**影響範囲**:
- 新規ファイル: `Runtime/Core/AudioGeneration/PhonemeSilenceProcessor.cs`
- `Assets/uPiper/Runtime/Core/AudioGeneration/InferenceAudioGenerator.cs`

**対応方針**:
- 長文の TTS 品質改善に有効
- 句単位推論でメモリピーク削減
- 優先度は低め（現行でも動作する）

---

#### 8. ONNX セッションキャッシュ (.opt.onnx)

**現状**: piper-plus はデバイス別キャッシュ（`.{device}.opt.onnx` + `.ok` センチネル）を実装。uPiper は未実装。

**piper-plus の仕様**:
- 初回: `ORT_ENABLE_ALL` で最適化 → `.opt.onnx` に保存 → `.ok` センチネル作成
- 2回目以降: `.opt.onnx` + `.ok` が存在すれば `ORT_DISABLE_ALL` で直接使用
- 不完全キャッシュは自動削除 → 再最適化
- 環境変数: `PIPER_DISABLE_CACHE=1` で無効化

**影響範囲**:
- Unity.InferenceEngine は ONNX Runtime の SessionOptions を直接公開しないため、直接的な実装は困難

**対応方針**:
- Unity.InferenceEngine の制約上、ORT レベルのキャッシュは実装不可
- ただし、モデルの事前最適化（ビルド時にONNXを最適化済みに変換）は検討可能
- **現時点では対応不要**（Unity.InferenceEngine に委ねる）

---

### P3: 設計変更への追従（低優先度）

#### 9. ort-session-contract.toml への準拠

**現状**: piper-plus は `ort-session-contract.toml` で全実装間の ONNX Runtime 設定を契約化。uPiper は Unity.InferenceEngine のデフォルト設定を使用。

**piper-plus の統一設定**:
| 項目 | 値 |
|------|-----|
| graph_optimization_level | ORT_ENABLE_ALL |
| execution_mode | ORT_SEQUENTIAL |
| max_intra_threads | min(cores/2, 4) |
| inter_op_threads | 1 |
| enable_cpu_mem_arena | true |
| enable_memory_pattern | true |
| dynamic_block_base | 4 |

**対応方針**:
- Unity.InferenceEngine は低レベル設定を公開しないため、直接準拠は困難
- Unity.InferenceEngine の `BackendType` 選択で間接的に最適化
- **現時点では対応不要**

---

#### 10. ピーク正規化 (Peak Normalization)

**現状**: piper-plus C# は出力音声にピーク正規化を実装。uPiper は正規化なし。

**piper-plus の実装**:
```csharp
float maxVal = audio.Max(Math.Abs);
float scale = 32767.0f / Math.Max(0.01f, maxVal);
result[i] = (short)Math.Clamp(audio[i] * scale, -32767f, 32767f);
```

**影響範囲**:
- `Assets/uPiper/Runtime/Core/AudioGeneration/AudioClipBuilder.cs`

**対応方針**:
- uPiper は float32 のまま AudioClip に渡すため、Unity 側で正規化される
- 音量の一貫性が問題になる場合のみ対応
- **オプション機能として検討**

---

#### 11. 環境変数制御

**現状**: piper-plus は環境変数でランタイム動作を制御。uPiper は PiperConfig オブジェクトで制御。

**piper-plus の環境変数**:
| 環境変数 | 機能 |
|---------|------|
| `PIPER_DISABLE_WARMUP` | Warmup 無効化 |
| `PIPER_DISABLE_CACHE` | キャッシュ無効化 |
| `PIPER_INTRA_THREADS` | intra-op スレッド数 |
| `PIPER_GPU_DEVICE_ID` | GPU デバイス ID |

**対応方針**:
- Unity アプリケーションでは環境変数よりも PiperConfig が適切
- Warmup 実装時に `PiperConfig.EnableWarmup` として対応
- **環境変数対応は不要**（Unity の設計パターンに合わない）

---

## 差分なし — 確認済みの一致項目

以下の項目は piper-plus と uPiper で一致していることを確認済み:

| 項目 | 詳細 |
|------|------|
| PUA マッピング (0xE000-0xE058) | 87 エントリが完全一致 |
| N 音素変種ルール | N_m/N_n/N_ng/N_uvular の分類が一致 |
| 拡張質問マーカー | ?!/?./?~ の処理が一致 (piper-plus #210) |
| EOS-like トークン処理 | $, ?, ?!, ?., ?~ のセットが一致 |
| Intersperse PAD ロジック | BOS後PAD + 音素間PAD の挿入が一致 |
| 言語 ID (ja:0 - ko:6) | 7言語の ID マッピングが一致 |
| Prosody 言語別マッピング | A1/A2/A3 の意味が全言語で一致 |
| 中国語トーン PUA | tone1-tone5 (E046-E04A) が一致 |
| Scales 配列 | [noise_scale, length_scale, noise_w] の順序が一致 |

---

## 対応優先度サマリー

| 優先度 | 項目 | 工数目安 | 影響 |
|--------|------|---------|------|
| **P0** | PUA マッピング差分修正 | 小 | モデル互換性 |
| ~~P0~~ | ~~Prosody テンソル型確認~~ | — | ~~対応不要（Sentis自動キャスト確認済み）~~ |
| **P1** | Warmup 実装 | 中 | 初回レイテンシ改善 |
| ~~P1~~ | ~~日本語音素化キャッシュ確認~~ | — | ~~対応不要（LRU 5000エントリ実装済み）~~ |
| **P1** | ArrayPool メモリ最適化 | 小 | GC 圧力低減 |
| **P2** | スウェーデン語対応 | 中（DotNetG2P.Swedish開発完了済み、uPiper統合のみ） | 新言語追加 |
| **P2** | 沈黙句分割 | 中 | 長文品質改善 |
| **P2** | セッションキャッシュ | — | Unity制約で不可 |
| **P3** | ORT設定準拠 | — | Unity制約で不可 |
| **P3** | ピーク正規化 | 小 | 音量一貫性 |
| **P3** | 環境変数制御 | — | Unity設計に不適 |

---

## 参考: piper-plus v1.10.0 主要コミット

| コミット | 日付 | 内容 |
|---------|------|------|
| #318 | 2026-04-06 | CPU推論Tier2: warmup/cache/JA phonemize統一 |
| #317 | 2026-04-04 | dynamic_block_base + メモリアリーナ統一 |
| #316 | 2026-04-03 | WASM G2P: ES/FR/PT/ZH実装 + 841テスト |
| #315 | 2026-04-03 | SessionOptions最適化統一 |
| #313 | — | WebUI自動モデルダウンロード |
| #311 | — | Dependabot: 17件セキュリティ修正 |
| #309 | — | C API共有ライブラリ |
| #302 | — | コールドスタート最適化 (2s→300ms) |
| #300 | — | G2P独立パッケージ化 (Phase 1) |

---

## 参考: piper-plus C# 実装とのファイル対応

| piper-plus (src/csharp/) | uPiper | 備考 |
|-------------------------|--------|------|
| `PiperSession.cs` | `InferenceAudioGenerator.cs` | 推論実行 |
| `SessionFactory.cs` | `InferenceAudioGenerator.InitializeAsync()` | セッション初期化 |
| `PiperModel.cs` | `PiperVoiceConfig` | メタデータ |
| `PhonemeEncoder.cs` | `PhonemeEncoder.cs` | 音素エンコーディング |
| `PiperPhonemeConverter.cs` | `PhonemeEncoder` 内 | N音素・質問マーカー |
| `OpenJTalkToPiperMapping.cs` (96項目) | `PuaTokenMapper.cs` (88項目) | PUAマッピング |
| `PhonemeSilenceProcessor.cs` | N/A | 沈黙句分割（未実装） |
| `SessionFactory.Warmup()` | N/A | Warmup（未実装） |
