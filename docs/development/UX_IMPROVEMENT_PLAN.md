# uPiper UX改善計画

ユーザー視点での包括的な調査に基づく改善計画。全10カテゴリ、優先度順に整理。

## 実装状況サマリ

| フェーズ | 状況 | 備考 |
|---------|------|------|
| Phase 1: クリティカル修正 | ✅ 完了 | 4項目すべて実装済み |
| Phase 2: UX向上 | ✅ 完了 | 6項目すべて実装済み |
| Phase 3: 品質・信頼性 | ✅ 完了 | 5項目実装済み（8-1はAPI削除により対象外） |
| Phase 4: ドキュメント・仕上げ | 未着手 | — |

**レビュー対応（完了）:**
- StreamAudioAsync を IPiperTTS から削除（API汚染防止）
- PlatformInitLoggingTests に値アサーション追加
- Cache hit の `[Obsolete]` API → NativeArray 対応
- BitConverter.GetBytes → union struct（GCフリー化）
- Cache メモリ上限エビクションテスト 3件追加
- ISplitInferenceOrchestrator インターフェース導入
- CJK言語検出をセグメントレベル（±10文字ウィンドウ）に改善

## 目次

- [1. API簡素化](#1-api簡素化)
- [2. エラーハンドリング改善](#2-エラーハンドリング改善)
- [3. 設定システム改善](#3-設定システム改善)
- [4. 初期化・セットアップ改善](#4-初期化セットアップ改善)
- [5. パフォーマンス改善](#5-パフォーマンス改善)
- [6. 多言語体験改善](#6-多言語体験改善)
- [7. カスタム辞書改善](#7-カスタム辞書改善)
- [8. テストカバレッジ拡充](#8-テストカバレッジ拡充)
- [9. プラットフォーム対応改善](#9-プラットフォーム対応改善)
- [10. ドキュメント整備](#10-ドキュメント整備)

---

## 1. API簡素化

### 現状の問題

テキストからAudioClip生成まで5ステップ必要:

```csharp
var config = new PiperConfig();
var tts = new PiperTTS(config);
await tts.InitializeAsync();
await tts.LoadVoiceAsync(voiceConfig);
var clip = await tts.GenerateAudioAsync("こんにちは");
```

### 改善項目

#### 1-1. コンビニエンスファクトリメソッド追加 [優先度: 最高] ✅ 完了

**対象ファイル**: `Assets/uPiper/Runtime/Core/PiperTTS.cs`

```csharp
// 目標: 2ステップで音声生成
var tts = await PiperTTS.CreateAsync();
var clip = await tts.GenerateAudioAsync("こんにちは");
```

- `PiperTTS.CreateAsync()` 静的ファクトリメソッドを追加
- デフォルト設定でインスタンス生成 + 初期化 + デフォルトボイスロードを一括実行
- オーバーロード: `CreateAsync(PiperConfig config)` で設定カスタマイズ可能

#### 1-2. LoadVoice + SetCurrentVoice 統合 [優先度: 高]

**対象ファイル**: `Assets/uPiper/Runtime/Core/PiperTTS.cs`

- `LoadVoiceAsync()` 呼び出し時に自動で `SetCurrentVoice()` も実行するオプション追加
- 現在のボイスが未設定の場合、最初にロードしたボイスを自動選択

#### 1-3. AvailableVoices 型統一 [優先度: 中]

**対象ファイル**: `Assets/uPiper/Runtime/Core/IPiperTTS.cs`, `Assets/uPiper/Runtime/Core/PiperTTS.cs`

- インターフェース: `IReadOnlyList<PiperVoiceConfig>` を返す
- 実装: `IReadOnlyCollection<string>` を返す
- 型を統一して一貫性を確保

#### 1-4. 非推奨API削除 [優先度: 低]

**対象ファイル**: `Assets/uPiper/Runtime/Core/PiperConfig.cs`

- `Validate()` メソッド（line 193、`[Obsolete]`）を削除
- `ToValidated()` パターンに完全移行

---

## 2. エラーハンドリング改善

### 現状の問題

複数箇所でサイレント失敗が発生し、ユーザーが問題の原因を特定できない。

### 改善項目

#### 2-1. サイレント失敗の撲滅 [優先度: 最高] ✅ 完了

| 対象ファイル | 行 | 現状 | 改善 |
|-------------|-----|------|------|
| `PiperTTS.cs` | 569-578 | `_phonemizer == null` で空結果 | `InvalidOperationException` をスロー |
| `PiperTTS.Inference.cs` | 235-251 | 未対応言語がDebugLog + フォールバック | `PiperException` をスロー or 明示的警告 |
| `PhonemeEncoder.cs` | 448-456 | "Unknown phoneme" ログのみ | 有効な音素リストを含むエラーメッセージ |
| `PhonemeEncoder.cs` | 491-500 | PhonemeIdMap=null で3トークン初期化 | 初期化時に例外スロー |

#### 2-2. 初期化時バリデーション一括化 [優先度: 最高] ✅ 完了

**対象ファイル**: `Assets/uPiper/Runtime/Core/PiperTTS.cs` (`InitializeAsync`)

`InitializeAsync()` 内で以下を一括チェック:
- モデルファイル存在確認
- 辞書ファイル整合性チェック
- 音素マップの有効性確認
- プラットフォーム固有の設定（iOS AVAudioSession等）

問題がある場合、具体的なアクション付きの `PiperException` をスロー。

#### 2-3. 辞書ロードエラーの明確化 [優先度: 高]

**対象ファイル**: `Assets/uPiper/Runtime/Core/Phonemizers/CustomDictionary.cs`

- JSONパースエラー時、ファイル名と行番号を含むエラーメッセージ
- 部分的な辞書ロード成功時、ロードされたエントリ数と失敗数を報告
- `Debug.LogWarning` → `Debug.LogError` に昇格（辞書破損は重大な問題）

#### 2-4. モデルロード失敗時の詳細メッセージ [優先度: 中]

**対象ファイル**: `Assets/uPiper/Runtime/Core/PiperTTS.cs`

```
// 現在
"Model asset not found: {modelPath}"

// 改善
"Model asset not found: {modelPath}
  Expected location: Resources/Models/{voiceId}
  Available models: [{availableList}]
  Setup guide: uPiper > Setup > Check Setup Status"
```

---

## 3. 設定システム改善

### 現状の問題

23以上のフィールド、ScriptableObject非対応、専門パラメータの説明不足。

### 改善項目

#### 3-1. PiperConfigScriptableObject 追加 [優先度: 高] ✅ 完了

**新規ファイル**: `Assets/uPiper/Runtime/Core/PiperConfigAsset.cs`

```csharp
[CreateAssetMenu(fileName = "PiperConfig", menuName = "uPiper/Config")]
public class PiperConfigAsset : ScriptableObject
{
    [SerializeField] private PiperConfig _config = PiperConfig.CreateDefault();
    public PiperConfig Config => _config;
}
```

- Inspector上でノーコード設定可能
- プリセットアセット（Fast/Natural/HighQuality）を同梱

#### 3-2. 推論パラメータの説明強化 [優先度: 高] ✅ 完了

**対象ファイル**: `Assets/uPiper/Runtime/Core/PiperConfig.cs`, `Assets/uPiper/Runtime/Core/PiperVoiceConfig.cs`

NoiseScale/NoiseW/LengthScale の Tooltip を実用的な説明に更新:

```csharp
[Tooltip("発話速度の制御。1.0=標準、0.8=やや速い、1.2=やや遅い。範囲: 0.1-2.0")]
public float LengthScale = 1.0f;

[Tooltip("音声のランダム変動。低い値=安定した発声、高い値=自然な揺らぎ。推奨: 0.4-0.8")]
public float NoiseScale = 0.667f;

[Tooltip("音素間タイミングの変動。低い値=均一なリズム、高い値=自然なリズム変化。推奨: 0.5-1.0")]
public float NoiseW = 0.8f;
```

#### 3-3. 設定プリセット [優先度: 中]

**新規ファイル**: `Assets/uPiper/Runtime/Core/PiperConfigPresets.cs`

```csharp
public static class PiperConfigPresets
{
    public static PiperConfig Fast();       // 低品質・高速
    public static PiperConfig Natural();    // バランス型
    public static PiperConfig HighQuality(); // 高品質・低速
}
```

---

## 4. 初期化・セットアップ改善

### 現状の問題

辞書zipの手動展開、モデルパスの完全一致要求、iOS固有の初期化漏れ。

### 改善項目

#### 4-1. iOS AVAudioSession 自動初期化 [優先度: 最高] ✅ 完了

**対象ファイル**: `Assets/uPiper/Runtime/Core/PiperTTS.cs`, `Assets/uPiper/Runtime/Core/Platform/IOSAudioSessionHelper.cs`

- `InitializeAsync()` 内で `#if UNITY_IOS` 時に `IOSAudioSessionHelper.Initialize()` を自動呼び出し
- 初期化失敗時に明確なエラーメッセージ

#### 4-2. 辞書zip自動展開 [優先度: 高]

**対象ファイル**: `Assets/uPiper/Editor/uPiperSetup.cs`

- `UPIPER_DEVELOPMENT` シンボルに依存せず、辞書zipが未展開なら自動展開
- ビルドポストプロセスで展開済みファイルをバンドル

#### 4-3. モデルパス自動解決 [優先度: 中]

**対象ファイル**: `Assets/uPiper/Runtime/Core/PiperTTS.cs`

- `LoadVoiceAsync()` で VoiceId とモデルパスの自動マッチング
- `Resources/Models/` ディレクトリをスキャンして利用可能モデルを検出
- 部分一致・大文字小文字無視によるフォールバック

---

## 5. パフォーマンス改善

### 現状の問題

音声合成結果キャッシュなし、沈黙句分割が逐次処理。

### 改善項目

#### 5-1. 音声合成結果キャッシュ [優先度: 高] ✅ 完了

**対象ファイル**: `Assets/uPiper/Runtime/Core/AudioGeneration/TTSSynthesisOrchestrator.cs`

- テキスト + 合成パラメータのハッシュをキーとしたAudioClipキャッシュ
- LRU方式、設定可能な最大キャッシュサイズ（デフォルト: 50エントリ or 100MB）
- `PiperConfig` にキャッシュ有効/無効オプション追加

#### 5-2. 沈黙句分割の並列推論 [優先度: 中]

**対象ファイル**: `Assets/uPiper/Runtime/Core/AudioGeneration/SplitInferenceOrchestrator.cs`

- 現在: 10文 → 10回逐次推論
- 改善: バッチ推論またはパイプライン化で並列処理
- 制約: ONNX推論がメインスレッド必須のためキューイング方式を検討

#### 5-3. バックエンド選択理由の明示 [優先度: 低]

**対象ファイル**: `Assets/uPiper/Runtime/Core/AudioGeneration/BackendSelector.cs`

- バックエンド選択時に理由を含むInfoログ出力
- `PiperTTS.SelectedBackend` プロパティで現在のバックエンドを公開

---

## 6. 多言語体験改善

### 現状の問題

ラテン文字言語の自動判別がopt-in、未対応言語がサイレントスキップ。

### 改善項目

#### 6-1. Trigram検出のデフォルト有効化 [優先度: 高] ✅ 完了

**対象ファイル**: `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/MultilingualPhonemizerOptions.cs`

- `EnableTrigramDetection` のデフォルト値を `true` に変更
- 2つ以上のラテン言語ハンドラが登録されている場合、自動的に `HybridLanguageDetector` を使用

#### 6-2. 未対応言語の明示的通知 [優先度: 高] ✅ 完了

**対象ファイル**: `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/MultilingualPhonemizer.cs`

- 未対応言語セグメントをスキップする際、`Debug.LogWarning` に加えてイベント発火
- `IPiperTTS.OnUnsupportedLanguageDetected` イベント追加
- オプション: フォールバック言語で合成する設定

#### 6-3. WebGL対話ゲートの多言語化 [優先度: 高] ✅ 完了

**対象ファイル**: `Assets/uPiper/Runtime/Core/Platform/WebGLInteractionGate.cs`

- ブラウザ言語 (`navigator.language`) を取得してUIテキストを切り替え
- 最低限: 日本語 / 英語 の2言語対応

#### 6-4. CJK言語検出の安定化 [優先度: 中] ✅ 完了（レビュー対応）

**対象ファイル**: `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/UnicodeLanguageDetector.cs`

- 日中混合テキストでの仮名検出ロジックをセグメント単位に改善
- テスト追加: 日本語と中国語の混在パターン

---

## 7. カスタム辞書改善

### 現状の問題

Editorツールなし、優先度ガイドなし、バッチ追加API未提供。

### 改善項目

#### 7-1. Editor辞書管理ウィンドウ [優先度: 高] ✅ 完了

**新規ファイル**: `Assets/uPiper/Editor/DictionaryManagerWindow.cs`

- メニュー: `uPiper > Tools > Dictionary Manager`
- 機能:
  - 辞書エントリの一覧・検索・フィルタリング
  - GUI上での追加・編集・削除
  - 優先度の視覚的表示
  - 発音プレビュー（TTS再生）
  - エクスポート / インポート

#### 7-2. バッチ追加API [優先度: 中]

**対象ファイル**: `Assets/uPiper/Runtime/Core/Phonemizers/CustomDictionary.cs`

```csharp
public void AddWords(IEnumerable<(string word, string pronunciation, int priority)> entries)
```

- パターンキャッシュクリアを最後に1回だけ実行
- 大量エントリ追加時のパフォーマンス改善

#### 7-3. 重複上書き警告 [優先度: 中]

**対象ファイル**: `Assets/uPiper/Runtime/Core/Phonemizers/CustomDictionary.cs`

- 高優先度エントリが低優先度エントリを上書きする際に `Debug.LogWarning` 出力
- 上書き前後のエントリ情報を含む

#### 7-4. 優先度定数・ガイドライン [優先度: 低]

**対象ファイル**: `Assets/uPiper/Runtime/Core/Phonemizers/CustomDictionary.cs`

```csharp
public static class DictionaryPriority
{
    public const int Low = 3;
    public const int Default = 5;
    public const int High = 7;
    public const int Override = 9;
    public const int Always = 10;
}
```

---

## 8. テストカバレッジ拡充

### 現状の問題

67テストファイル存在するが、ストリーミングAPI、非同期ライフサイクル、並行合成が未テスト。

### 改善項目

#### 8-1. ~~ストリーミングAPIテスト~~ [優先度: 最高] ✅ 対象外（API削除）

StreamAudioAsync がレビューで IPiperTTS から削除されたため、テスト対象外となった。

~~**新規ファイル**: `Assets/uPiper/Tests/Editor/StreamAudioTests.cs`~~

- ~~`StreamAudioAsync()` の基本動作~~
- ~~キャンセル中のチャンク生成~~
- ~~空テキスト / 超長テキスト~~

#### 8-2. 非同期ライフサイクルテスト [優先度: 高] ✅ 完了（13テスト）

**新規ファイル**: `Assets/uPiper/Tests/Editor/LifecycleTests.cs`

- `InitializeAsync()` → `LoadVoiceAsync()` → `GenerateAudioAsync()` の正常フロー
- 初期化前のメソッド呼び出し
- Dispose後のメソッド呼び出し
- 二重初期化

#### 8-3. CancellationTokenテスト [優先度: 高] ✅ 完了（10テスト）

**新規ファイル**: `Assets/uPiper/Tests/Editor/CancellationTests.cs`

- 合成中キャンセル
- タイムアウト
- キャンセル後のリソースリーク確認

#### 8-4. 並行合成テスト [優先度: 中]

**新規ファイル**: `Assets/uPiper/Tests/Editor/ConcurrencyTests.cs`

- 同時に複数の `GenerateAudioAsync` 呼び出し
- ボイス切り替え中の合成
- スレッドセーフ確認

#### 8-5. 混合言語E2Eテスト [優先度: 中]

**新規ファイル**: `Assets/uPiper/Tests/Editor/MixedLanguageE2ETests.cs`

- 日英混合テキスト
- 3言語以上混合
- 未対応言語を含むテキスト

#### 8-6. エッジケーステスト強化 [優先度: 低]

**対象**: 既存テストファイルへの追加

- 絵文字を含むテキスト
- 50KB超の長文テキスト
- 特殊記号のみのテキスト

---

## 9. プラットフォーム対応改善

### 現状の問題

macOS CPU強制、iOS無音問題、WebGL日本語固定UI、Android初回ロード遅延。

### 改善項目

#### 9-1. プラットフォーム初期化ログ [優先度: 高] ✅ 完了

**対象ファイル**: `Assets/uPiper/Runtime/Core/AudioGeneration/BackendSelector.cs`, `Assets/uPiper/Runtime/Core/PiperTTS.cs`

- 初期化時に以下をInfoログ出力:
  - 検出プラットフォーム
  - 選択されたバックエンド + 選択理由
  - GPU情報（利用可能な場合）
  - フォールバックが発生した場合の理由

#### 9-2. iOS自動設定 [優先度: 最高] ✅ 完了

（4-1 と同一。再掲）

#### 9-3. WebGL改善 [優先度: 高]

**対象ファイル**: 複数

- 対話ゲートの多言語化（6-3 と同一）
- `WebGLStreamingAssetsLoader` にリトライ + タイムアウト追加
- ネットワークエラー時の回復パス

#### 9-4. Android初回ロード最適化 [優先度: 中]

**対象ファイル**: 検討中

- 辞書ファイルのプリキャッシュ
- `Application.persistentDataPath` へのコピー + 2回目以降はローカル読み込み

---

## 10. ドキュメント整備

### 現状の問題

トラブルシューティングガイド、パフォーマンスチューニングガイド、プラットフォーム別セットアップが不足。

### 改善項目

#### 10-1. トラブルシューティングガイド [優先度: 高]

**新規ファイル**: `docs/TROUBLESHOOTING.md`

以下のFAQをカバー:
- 「音が出ない」（iOS AVAudioSession、WebGL対話ゲート、モデル未ロード）
- 「音声品質が悪い」（短テキスト緩和、NoiseScale調整）
- 「特定の単語が正しく読まれない」（カスタム辞書の使い方）
- 「WebGLでタイムアウトする」（モデルサイズ、ネットワーク設定）
- 「GPU推論が使えない」（プラットフォーム制限、バックエンド選択ログ確認）

#### 10-2. パフォーマンスチューニングガイド [優先度: 中]

**新規ファイル**: `docs/PERFORMANCE_TUNING.md`

- キャッシュサイズ推奨値
- バックエンド選択ガイド
- 沈黙句分割の影響
- メモリ使用量の目安

#### 10-3. プラットフォーム別セットアップガイド [優先度: 中]

**新規/更新ファイル**: `docs/platforms/`

- `docs/platforms/ios/SETUP.md` — AVAudioSession、ビルド設定
- `docs/platforms/webgl/SETUP.md` — 対話ゲート、モデル分割、ブラウザ互換性
- `docs/platforms/android/SETUP.md` — 既存ファイル更新、StreamingAssets注意点
- `docs/platforms/macos/SETUP.md` — CPU強制の説明

#### 10-4. 設定パラメータリファレンス [優先度: 中]

**新規ファイル**: `docs/CONFIG_REFERENCE.md`

- 全パラメータの説明、デフォルト値、推奨範囲
- NoiseScale / NoiseW / LengthScale の効果を具体例で説明
- プリセット比較表

---

## 実装順序

フェーズ分けで段階的に実装:

### Phase 1: クリティカル修正（ユーザー離脱防止） ✅ 完了
1. ✅ iOS AVAudioSession 自動初期化 (4-1/9-2)
2. ✅ サイレント失敗の撲滅 (2-1) — InitializationValidator としてバリデーションパイプラインを統合
3. ✅ 初期化時バリデーション一括化 (2-2) — ModelConfigParser として ParseModelConfig を抽出
4. ✅ コンビニエンスファクトリメソッド (1-1) — TTSSynthesisOrchestrator による統一パイプライン + ValidatedPiperConfig / IPiperConfigReadOnly 導入

### Phase 2: UX向上 ✅ 完了
5. ✅ ScriptableObject対応 (3-1)
6. ✅ 推論パラメータ説明強化 (3-2)
7. ✅ Editor辞書管理ウィンドウ (7-1)
8. ✅ WebGL対話ゲート多言語化 (6-3)
9. ✅ Trigram検出デフォルト有効化 (6-1)
10. ✅ 未対応言語の明示的通知 (6-2)

### Phase 3: 品質・信頼性 ✅ 完了
11. ✅ ~~ストリーミングAPIテスト (8-1)~~ — StreamAudioAsync が IPiperTTS から削除されたため対象外
12. ✅ 非同期ライフサイクルテスト (8-2) — 13テスト実装
13. ✅ CancellationTokenテスト (8-3) — 10テスト実装
14. ✅ 音声合成結果キャッシュ (5-1) — AudioSynthesisCache（LRU + メモリ上限エビクション）
15. ✅ プラットフォーム初期化ログ (9-1) — BackendSelector.LogSelectionSummary 実装

### Phase 4: ドキュメント・仕上げ
16. トラブルシューティングガイド (10-1)
17. パフォーマンスチューニングガイド (10-2)
18. プラットフォーム別セットアップガイド (10-3)
19. 設定パラメータリファレンス (10-4)
20. その他の改善項目 (残り全て)
