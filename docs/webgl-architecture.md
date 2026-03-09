# uPiper WebGL対応 技術選定・アーキテクチャ設計書

> 作成日: 2026-03-09
> 基盤資料: [docs/webgl-research.md](webgl-research.md)
> 対象ブランチ: `feature/webgl-support` (developベース)

---

## 目次

1. [技術選定サマリー](#1-技術選定サマリー)
2. [推論バックエンド](#2-推論バックエンド)
3. [辞書配信・ロード方式](#3-辞書配信ロード方式)
4. [マルチスレッド・非同期処理](#4-マルチスレッド非同期処理)
5. [GitHub Pagesデプロイ構成](#5-github-pagesデプロイ構成)
6. [全体アーキテクチャ](#6-全体アーキテクチャ)
7. [実装フェーズ・ロードマップ](#7-実装フェーズロードマップ)
8. [リスク評価](#8-リスク評価)
9. [テスト戦略](#9-テスト戦略)

---

## 1. 技術選定サマリー

| 項目 | 決定事項 | 理由 |
|------|---------|------|
| **Sentisバージョン** | 2.5.0にアップグレード | WebGPUビルド対応(2.3.0+)、推論精度修正、メモリリーク修正 |
| **WebGL2バックエンド** | GPUPixel | WebGL2はCompute Shader非対応。VITSの全51-52オペレータ対応済み |
| **WebGPUバックエンド** | GPUCompute（自動検出） | `SystemInfo.supportsComputeShaders`でランタイム判定。1.5-4倍高速化 |
| **WebGPU有効化** | 有効化する（WebGL2フォールバック付き） | カバレッジ~70%、フォールバックありでリスク最小 |
| **MeCab辞書配信** | naist_jdic.zip (23MB) + 分割配信 | 100MB制限クリア、ZipArchive WebGL動作確認済み |
| **辞書ロードAPI** | dot-net-g2pにbyte[]ベースAPI追加 | MemoryStream+BinaryReaderで既存ロジック再利用 |
| **StreamingAssetsアクセス** | UnityWebRequest非同期ローダー | WebGLではHTTP URL、条件コンパイルで分岐 |
| **マルチスレッド対応** | `#if UNITY_WEBGL` でTask.Run除去 | async/await + Task.FromResultパターン。追加依存なし |
| **非同期パターン** | async/await + Task.FromResult | 既存API互換維持、UniTask/Awaitable不採用 |
| **圧縮形式** | Disabled（GitHub Pages自動gzipに委任） | Brotli非対応、二重圧縮回避 |
| **分割ローダー** | 過去ブランチから再利用 | WebGLSplitDataProcessor + split-file-loader.js |
| **coi-serviceworker** | 不要（現時点） | マルチスレッド不使用のためSharedArrayBuffer不要 |
| **FP16量子化** | Phase 3で採用（初期は見送り） | 転送サイズ50%削減、音質検証後に適用 |
| **IndexedDBキャッシュ** | Phase 3で実装 | 2回目以降のロード高速化 |

---

## 2. 推論バックエンド

### 2.1 Sentis 2.5.0アップグレード

| 項目 | 内容 |
|------|------|
| 現在 | com.unity.ai.inference@2.2.2 |
| 目標 | com.unity.ai.inference@2.5.0 |
| パッケージID | 変更なし（`com.unity.ai.inference`のまま） |
| 命名 | Sentis（2.4.0でリネーム。コード上の影響なし） |

**アップグレードで得られる改善**:
- WebGPUビルドエラー解消（2.3.0: GroupConvシェーダー修正）
- ConvTranspose group/dilations追加（2.3.0: VITS推論品質向上の可能性）
- 推論精度修正: 最適化パス・CPUコールバック修正（2.4.0）
- メモリリーク修正（2.4.0/2.5.0: 長時間使用の安定性）
- Clipオペレータ CPUフォールバック不要化（2.5.0: GPU上で完結）
- TopK GPUCompute修正（2.5.0）

### 2.2 バックエンド選択フロー

```
WebGLビルド起動時:
  ├─ SystemInfo.supportsComputeShaders == true (WebGPU)
  │   └─ GPUCompute → 高速推論（1.5-4倍）
  │       └─ [フォールバック] クラッシュ検出時 → GPUPixel
  │
  └─ SystemInfo.supportsComputeShaders == false (WebGL2)
      └─ GPUPixel → 通常速度推論
          └─ [フォールバック] 失敗時 → CPU（最終手段）
```

### 2.3 Player Settings構成

```
Graphics APIs (Web):
  1. WebGPU (優先)
  2. WebGL 2 (フォールバック)
Auto Graphics API: OFF
```

### 2.4 コード変更箇所

| ファイル | 変更内容 |
|---------|---------|
| `InferenceAudioGenerator.cs` (DetermineBackendType) | WebGPU判定追加。`supportsComputeShaders`でGPUCompute選択 |
| `InferenceAudioGenerator.cs` (GPUCompute強制変換) | WebGPU環境ではGPUComputeをそのまま許可 |
| `PlatformHelper.cs` | `IsWebGPU`プロパティ追加 |
| `ProjectSettings.asset` | `webGLEnableWebGPU: 1` |

### 2.5 VITSオペレータ互換性

全51-52種類のオペレータがGPUPixel/GPUCompute両方で対応済み。ONNX Opset 15サポート。GroupNormalizationは未対応だがVITSはInstanceNormを使用するため問題なし。

---

## 3. 辞書配信・ロード方式

### 3.1 辞書タイプ別構成

| 辞書タイプ | サイズ | 配信方式 | ロード方式 | ロード順序 |
|-----------|--------|---------|-----------|-----------|
| カスタム辞書JSON | ~48KB | StreamingAssets (.data内) | UnityWebRequest → JSON Parse | 1st (即座) |
| CMU辞書 | 3.5MB | StreamingAssets (.data内) | UnityWebRequest → テキストパース | 2nd (英語有効化) |
| MeCab辞書 | 23MB (ZIP) / 103MB (展開) | StreamingAssets → UnityWebRequestでDL | ZipArchive展開 → byte[] → DictionaryBundle.Load | 3rd (日本語有効化) |
| ONNXモデル | ~60MB | Resources/.data内 | ModelLoader (既存) | 初期化時 |

### 3.2 dot-net-g2p byte[]ロードAPI

**追加API**:
```csharp
// 現在: ファイルパスベース
DictionaryBundle.Load(string dictionaryPath)

// 追加: byte[]ベース（WebGL対応）
DictionaryBundle.Load(byte[] sysDic, byte[] matrix, byte[] charBin, byte[] unkDic)
```

各辞書クラスは内部的に `new MemoryStream(byte[])` + `BinaryReader` を使い、既存パースロジックを最大限再利用。

**変更対象ファイル** (dot-net-g2p):

| ファイル | 追加API |
|---------|---------|
| `DictionaryBundle.cs` | `Load(byte[] sysDic, byte[] matrix, byte[] charBin, byte[] unkDic)` |
| `SystemDictionary.cs` | `Load(byte[] data)` or `Load(Stream stream)` |
| `ConnectionMatrix.cs` | 同上 |
| `CharProperty.cs` | 同上 |
| `UnknownDictionary.cs` | 同上 |
| `MeCabTokenizer.cs` | byte[]ベースコンストラクタ |

### 3.3 StreamingAssets非同期ローダー

新規ファイル: `Assets/uPiper/Runtime/Core/Platform/WebGLStreamingAssetsLoader.cs`

```csharp
public static class WebGLStreamingAssetsLoader
{
    public static async Task<byte[]> LoadBytesAsync(string relativePath)
    public static async Task<string> LoadTextAsync(string relativePath)
}
```

- WebGL: `UnityWebRequest.Get()` → `downloadHandler.data`
- 非WebGL: `File.ReadAllBytes()` / `File.ReadAllText()`
- 条件分岐: `#if UNITY_WEBGL && !UNITY_EDITOR`

### 3.4 起動時ロードシーケンス

```
Phase 0: Unity起動 (.data自動ロード)
  └── 分割ローダーが透過的に結合（100MB超の場合）

Phase 1: カスタム辞書 (~48KB)  → 進捗 0→5%
Phase 2: 英語辞書 (3.5MB)      → 進捗 5→20%    → 英語TTS利用可能
Phase 3: MeCab辞書 (23MB ZIP)   → 進捗 20→100%  → 日本語TTS利用可能
  ├── IndexedDBキャッシュチェック
  ├── UnityWebRequest → naist_jdic.zip (23MB)
  ├── ZipArchive展開 → sys.dic, matrix.bin, char.bin, unk.dic
  └── DictionaryBundle.Load(byte[], byte[], byte[], byte[])

Phase 4: ユーザーインタラクションゲート表示
  └── 「音声合成の準備ができました。クリックして開始」
```

### 3.5 キャッシュ戦略（Phase 3実装）

- 展開済みの各辞書ファイルをIndexedDBにキャッシュ
- 2回目以降はZIP展開もスキップ
- JavaScript interop (`IndexedDB.store`) + バージョンチェック

---

## 4. マルチスレッド・非同期処理

### 4.1 対応パターン

**採用**: `async/await` + `Task.FromResult` (条件コンパイル)

```csharp
#if UNITY_WEBGL && !UNITY_EDITOR
    return PhonemizeInternal(text);
#else
    return await Task.Run(() => PhonemizeInternal(text), cancellationToken);
#endif
```

**理由**: 既存APIシグネチャが全て`Task<T>`を返す設計のため、変更量が最小限。UniTask/Awaitableは追加依存・API全面書き換えが必要で不採用。

### 4.2 Task.Run排除箇所（13箇所）

**uPiperランタイム（8箇所）**:

| ファイル | 行 | 処理時間(推定) | フレーム分割要否 |
|---------|-----|---------------|---------------|
| `DotNetG2PPhonemizer.cs` | 109 | 1-50ms | 長文時要検討 |
| `FlitePhonemizerBackend.cs` | 52 | 1-5ms | 不要 |
| `SimpleLTSPhonemizer.cs` | 57 | 1-5ms | 不要 |
| `CMUDictionary.cs` | 109 | 500ms-2s | **要**（初期化時） |
| `G2PEngine.cs` | 42 | <1ms | 不要 |
| `StatisticalG2PModel.cs` | 39 | 1-5s | **要**（初期化時） |
| `FliteLTSPhonemizer.cs` | 358 | 10-100ms | 不要 |
| `UnityPhonemizerService.cs` | 63 | 1-10ms | 不要 |

**dot-net-g2p（4箇所）**:

| ファイル | 行 | 処理時間(推定) | フレーム分割要否 |
|---------|-----|---------------|---------------|
| `MultilingualPhonemizerService.cs` | 37,55,73 | 50-500ms | 要検討 |
| `PhonemizerService.cs` | 120 | 50-200ms | 要検討 |

### 4.3 SemaphoreSlim（6箇所）

WebGLでコンパイルは通り、`WaitAsync()`も動作する（Task-based実装、ネイティブスレッド非依存）。シングルスレッドでは競合が起きないため基本的に**放置可**。`ThreadSafePhonemizerPool`はクラス全体の存在意義がWebGLでは薄いため `#if !UNITY_WEBGL` で除外を検討。

### 4.4 フレーム分割が必要な処理

| 処理 | 推定時間 | 対策 |
|------|---------|------|
| MeCab辞書パース (103MB) | 3-10秒 | ローディング画面で一括実行 |
| CMU辞書読み込み (3.5MB) | 500ms-2秒 | ローディング画面で一括実行 |
| StatisticalG2Pトレーニング (134K単語) | 1-5秒 | ローディング画面で一括実行 |

→ 全て起動時のローディング画面中に実行するため、フレーム落ちはUXに影響しない。

---

## 5. GitHub Pagesデプロイ構成

### 5.1 デプロイファイル構成

```
gh-pages ブランチ
├── .nojekyll
├── index.html                      # カスタムテンプレート
├── split-file-loader.js            # 分割ファイル透過的結合
├── github-pages-adapter.js         # GitHub Pagesパス解決
├── Build/
│   ├── Web.loader.js               # Unityローダー
│   ├── Web.framework.js            # フレームワーク (~3MB)
│   ├── Web.wasm                    # WASMバイナリ (~30-50MB)
│   ├── Web.data.partaa             # データ分割1 (90MB)
│   └── Web.data.partab             # データ分割2 (残り)
├── StreamingAssets/
│   └── uPiper/
│       ├── Dictionaries/*.json     # カスタム辞書 (~48KB)
│       └── MeCab/naist_jdic.zip    # MeCab辞書 (23MB)
└── TemplateData/                   # テンプレート静的リソース
```

### 5.2 .dataファイルサイズ見積もり

| コンポーネント | サイズ |
|--------------|--------|
| ONNXモデル (Resources/) | ~60MB |
| naist_jdic.zip (StreamingAssets) | 23MB |
| CMU辞書 (StreamingAssets) | 3.5MB |
| カスタム辞書JSON (StreamingAssets) | ~48KB |
| Unityランタイムデータ | ~10-20MB |
| **合計** | **~97-107MB** |

→ 100MB超の可能性が高く、**分割ローダーが必要**

### 5.3 分割ローダー（過去ブランチ再利用）

`feature/webgl-implementation` ブランチから移植・リファクタリング:

| コンポーネント | 役割 | リファクタリング内容 |
|--------------|------|-------------------|
| `WebGLSplitDataProcessor.cs` | PostProcessBuildで90MBチャンクに自動分割 | OpenJTalk固有処理を削除、汎用化 |
| `split-file-loader.js` | Fetch APIインターセプト→分割ファイル並列DL→透過的結合 | そのまま再利用 |
| `github-pages-adapter.js` | リポジトリ名自動検出、パス解決 | ONNX Runtime Web/OpenJTalkパッチ削除 |

### 5.4 CIパイプライン

```
deploy-webgl.yml
│
├── Job 1: build-webgl
│   ├── Checkout (uPiper + dot-net-g2p)
│   ├── Library キャッシュ復元
│   ├── Unity WebGLビルド (Compression: Disabled)
│   └── Upload artifact
│
├── Job 2: deploy (needs: build-webgl)
│   ├── Download artifact
│   ├── .nojekyll 配置
│   ├── 100MB超ファイル分割 (90MBチャンク)
│   ├── index.html修正 (split-loader/adapter挿入)
│   ├── split-file-loader.js / github-pages-adapter.js 配置
│   └── actions/deploy-pages → gh-pagesブランチ
│
└── Job 3: test-deployment (optional)
    └── curl HTTP 200確認
```

### 5.5 圧縮設定

| 項目 | 設定 |
|------|------|
| Unity Compression Format | **Disabled** |
| Decompression Fallback | ON |
| GitHub Pages | 自動gzip（設定不要） |
| Brotli | **非対応**（GitHub Pages制約） |

### 5.6 coi-serviceworker

**現時点では不要**。理由: C#マルチスレッド不使用のためSharedArrayBuffer不要。将来WebGPU+マルチスレッド有効化時に検討。CIにコメントアウトで配置ステップを用意しておく。

---

## 6. 全体アーキテクチャ

### 6.1 WebGLアーキテクチャ図

```
[起動フロー]
  Unity WebGLビルド起動
    ↓
  分割ローダーが.dataファイルを透過的に結合
    ↓
  ┌─────────────────────────────────────────────┐
  │ 非同期初期化（ローディング画面表示中）         │
  │                                              │
  │  1. カスタム辞書ロード (48KB)                 │
  │     UnityWebRequest → JSON Parse             │
  │                                              │
  │  2. CMU辞書ロード (3.5MB)                    │
  │     UnityWebRequest → テキストパース          │
  │                                              │
  │  3. MeCab辞書ロード (23MB ZIP)                │
  │     UnityWebRequest → ZipArchive展開          │
  │     → byte[] → DictionaryBundle.Load          │
  │                                              │
  │  4. ONNXモデル初期化                          │
  │     ModelLoader → InferenceAudioGenerator     │
  └─────────────────────────────────────────────┘
    ↓
  ユーザーインタラクションゲート（クリックで開始）
    ↓
[TTS実行フロー]
  テキスト入力
    ↓
  カスタム辞書による前処理 (CustomDictionary)
    ↓
  DotNetG2PPhonemizer（メインスレッド直接実行）
    → 音素 + Prosody
    ↓
  ┌─ WebGPU → GPUCompute → 高速推論
  │
  └─ WebGL2 → GPUPixel → 通常推論
    ↓
  AudioClip (22050Hz, float32)
    ↓
  AudioSource.Play()
```

### 6.2 プラットフォーム判定マトリクス

| 条件 | Graphics API | バックエンド | 辞書ロード | スレッド |
|------|-------------|------------|-----------|---------|
| デスクトップ（非WebGL） | Vulkan/D3D/Metal | GPUPixel/CPU | FileStream | Task.Run |
| WebGL2 | OpenGLES3 | GPUPixel | UnityWebRequest + byte[] | メインスレッド直接 |
| WebGPU | WebGPU | GPUCompute | UnityWebRequest + byte[] | メインスレッド直接 |

---

## 7. 実装フェーズ・ロードマップ

### 7.1 MVP定義

**MVP = 日本語TTS WebGL動作**（デスクトップブラウザ上で日本語テキスト→音声再生）

**含めるもの**: 日本語G2P、GPUPixel推論、カスタム辞書、基本UI、インタラクションゲート
**含めないもの**: 英語G2P、WebGPU、IndexedDB、FP16量子化、分割配信、モバイル最適化

### 7.2 フェーズ一覧

#### Phase 1: 基盤整備（全タスク並列実行可能）

| # | タスク | 対象 | 工数 | 状態 |
|---|--------|------|------|------|
| 1-1 | dot-net-g2p byte[]ロードAPI追加 | dot-net-g2p | 中 | ✅ 完了 |
| 1-2 | Sentis 2.5.0へアップグレード | uPiper | 中 | ✅ 完了 |
| 1-3 | link.xmlにdot-net-g2pアセンブリ追加 | uPiper | 小 | ✅ 完了 |
| 1-4 | WebGLヒープサイズ拡大 (1024MB) | uPiper | 小 | ✅ 完了 |
| 1-5 | StreamingAssets非同期ローダー作成 | uPiper | 中 | ✅ 完了 |
| 1-6 | Task.Run → WebGL条件分岐 (13箇所) | uPiper + dot-net-g2p | 小 | ✅ 完了 |

#### Phase 2: 日本語TTS WebGL動作（MVP達成）

| # | タスク | 依存 | 工数 | 状態 |
|---|--------|------|------|------|
| 2-1 | DotNetG2PPhonemizerのWebGL初期化パス | 1-1, 1-5, 1-6 | 中 | ✅ 完了 |
| 2-2 | CustomDictionaryのWebGL非同期ロード | 1-5 | 小 | ✅ 完了 |
| 2-3 | PiperTTSのWebGLガード解除・統合 | 2-1, 2-2 | 中 | ✅ 完了 |
| 2-4 | GPUPixel推論動作確認 | 1-2 | 小 | ✅ 完了 |
| 2-5 | ユーザーインタラクションゲートUI | 2-3, 2-4 | 小 | ✅ 完了 |
| 2-6 | WebGLビルド統合テスト | 全Phase 1-2 | 中 | ✅ 完了 |

#### Phase 2b: 英語TTS対応

| # | タスク | 依存 | 工数 | 状態 |
|---|--------|------|------|------|
| 2b-1 | CMUDictionary/FliteLTSのWebGL対応 | 1-5, 1-6 | 中 | ✅ 完了 |
| 2b-2 | UnifiedPhonemizer/MixedLanguagePhonemizerのWebGL対応 | 2b-1 | 中 | ✅ 変更不要 |

#### Phase 3: 最適化・配信

| # | タスク | 依存 | 工数 | 状態 |
|---|--------|------|------|------|
| 3-1 | ZIP辞書圧縮ロード (103MB→23MB) | 2-1 | 中 | ✅ 完了 |
| 3-2 | ONNXモデルFP16量子化 (60MB→30MB) | 2-4 | 中 | ✅ スクリプト作成 |
| 3-3 | IndexedDBキャッシュ | 2-6 | 中 | ✅ 完了 |
| 3-4 | GitHub Pages分割配信（過去実装移植） | 2-6 | 中 | ✅ 完了 |
| 3-5 | 起動時プログレスバーUI | 2-5 | 小 | ✅ 完了 |

#### Phase 4: WebGPU対応

| # | タスク | 依存 | 工数 | 状態 |
|---|--------|------|------|------|
| 4-1 | Player Settings WebGPU有効化 | 1-2 | 小 | 未実装 |
| 4-2 | PlatformHelper.IsWebGPUプロパティ追加 | なし | 小 | 未実装 |
| 4-3 | DetermineBackendType() WebGPU判定追加 | 4-1, 4-2 | 小 | 未実装 |
| 4-4 | GPUComputeバックエンド動作検証 | 4-3, 2-6 | 中 | 未実装 |
| 4-5 | パフォーマンスベンチマーク | 4-4 | 中 | 未実装 |

### 7.3 クリティカルパス（MVP達成まで）

```
1-1 (byte[]API) → 2-1 (WebGL初期化) → 2-3 (ガード解除) → 2-5 (UI) → 2-6 (統合テスト)
```

**タスク1-1（dot-net-g2p byte[]API追加）がクリティカルパスの起点。**

### 7.4 依存関係グラフ

```
Phase 1（全て並列実行可能）:
  1-1 dot-net-g2p byte[]API ─────────────┐
  1-2 Sentis 2.5.0 ──────────────────────┤
  1-3 link.xml ──────────────────────────┤
  1-4 ヒープサイズ ───────────────────────┤
  1-5 StreamingAssetsローダー ────────────┤
  1-6 Task.Run除去 ──────────────────────┤
                                          │
Phase 2:                                  │
  2-1 DotNetG2P WebGL初期化 ◀── 1-1, 1-5, 1-6
  2-2 CustomDict WebGLロード ◀── 1-5
  2-3 PiperTTS ガード解除   ◀── 2-1, 2-2
  2-4 GPUPixel推論確認      ◀── 1-2
  2-5 インタラクションUI    ◀── 2-3, 2-4
  2-6 統合テスト            ◀── 全Phase1-2

Phase 3/4:
  3-1 ZIP辞書ロード         ◀── 2-1
  3-4 分割配信              ◀── 2-6
  4-3 BackendType判定       ◀── 4-1, 4-2
  4-4 GPUCompute検証        ◀── 4-3, 2-6
```

### 7.5 dot-net-g2p側 vs uPiper側

| 対象 | タスク |
|------|--------|
| **dot-net-g2p変更が必要** | 1-1 (byte[]API), 1-6 (Task.Run一部) |
| **uPiper側のみ** | その他全タスク |

### 7.6 スケジュール

```
タスク                          │ Week1  │ Week2  │ Week3  │ Week4  │
────────────────────────────────┼────────┼────────┼────────┼────────┤
Phase 1（並列実行可能）✅ 完了   │████████│██      │        │        │
Phase 2 ✅ 完了                  │        │  ██████│████████│████    │
────────────────────────────────┼────────┼────────┼────────┼────────┤
MVP達成 ─────────────────────── │        │        │        │    ◆   │

タスク                          │ Week5  │ Week6  │ Week7  │ Week8+ │
────────────────────────────────┼────────┼────────┼────────┼────────┤
Phase 2b（英語対応）✅ 完了      │████████│████████│        │        │
Phase 3（最適化）✅ 完了         │████████│████████│████████│        │
Phase 4（WebGPU）               │        │        │████████│████████│
────────────────────────────────┼────────┼────────┼────────┼────────┤
全機能完成 ──────────────────── │        │        │        │    ◆   │
```

| マイルストーン | 目標 |
|-------------|------|
| **MVP（日本語TTS WebGL動作）** | Week 4末 |
| 英語TTS WebGL対応 | Week 6末 |
| 最適化完了 | Week 7末 |
| WebGPU対応 | Week 8末 |

### 7.7 実装コミット履歴

| Phase | コミット | 日付 |
|-------|---------|------|
| Phase 1 | ee774b4 | 2026-03-09 |
| Phase 2 | d42d57c | 2026-03-09 |
| Phase 2b | 3871ebe | 2026-03-09 |
| Phase 3 | 2e844b0 | 2026-03-09 |
| CI/CD | 50918af | 2026-03-09 |

---

## 8. リスク評価

| # | リスク | 影響度 | 確率 | 対策 |
|---|--------|--------|------|------|
| R1 | GPUPixel推論がWebGL2で無音/破損音声 | **高** | **中** | Phase 2早期（2-4）で検証。CPUフォールバック用意 |
| R2 | MeCab辞書byte[]ロードで解析結果不正 | **高** | 低 | 既存テストとの完全一致テスト |
| R3 | メモリフットプリントがブラウザ上限超過 | **高** | 低 | デスクトップ(2-4GB)では余裕。モバイルはPhase 3で最適化 |
| R4 | WebGPU GPUComputeでVITSがクラッシュ | 中 | 中 | Phase 4は実験的。GPUPixelフォールバック常備 |
| R5 | Sentis 2.5.0 API破壊的変更 | 中 | 中 | Phase 1最初に着手（1-2）。問題時は2.4.1検討 |
| R6 | dot-net-g2pにUNITY_WEBGL定義シンボルが渡らない | 中 | 中 | asmdef設定確認 |
| R7 | 103MB辞書ダウンロードがタイムアウト | 中 | 低 | Phase 3のZIP圧縮(23MB)で軽減 |
| R8 | IL2CPPストリッピングでdot-net-g2p型消失 | 中 | 低 | link.xml追加（1-3） |

---

## 9. テスト戦略

### 9.1 テスト種別

| テスト種別 | 内容 | CI自動化 |
|-----------|------|---------|
| EditModeテスト | byte[]初期化、Task.Run分岐、JSON解析 | **可能**（既存CI） |
| WebGLビルド | ビルド成功確認 | **可能**（CI追加） |
| WebGLランタイム | ブラウザでの動作確認 | **困難**（手動テスト） |
| WebGPUテスト | WebGPU対応ブラウザでの動作 | **困難**（手動テスト） |

### 9.2 手動テスト項目（MVP）

| テスト | 期待結果 |
|--------|---------|
| WebGLビルド成功 | エラーなし |
| ブラウザ起動・ロード | Unity起動、ローディング表示 |
| 辞書ダウンロード | プログレス表示、完了後G2P利用可能 |
| 日本語TTS「こんにちは」 | 正常な日本語音声再生 |
| カスタム辞書「Docker」 | 「ドッカー」と発音 |
| メモリ使用量 | 300MB以下（デスクトップ） |

### 9.3 ブラウザ互換性テスト

| ブラウザ | テスト対象 |
|---------|-----------|
| Chrome (最新) | WebGL2 + GPUPixel / WebGPU + GPUCompute |
| Firefox (最新) | WebGL2 + GPUPixel |
| Edge (最新) | WebGL2 + GPUPixel |
| Safari (最新) | WebGL2 + GPUPixel |

---

## 参考ソース

- [docs/webgl-research.md](webgl-research.md) — 包括的調査レポート（16セクション）
- [Sentis 2.5.0 Manual](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.5/manual/index.html)
- [Unity WebGPU (Experimental)](https://docs.unity3d.com/6000.3/Documentation/Manual/WebGPU.html)
- [coi-serviceworker](https://github.com/gzuidhof/coi-serviceworker)
- [WebGPU Inspector](https://github.com/brendan-duncan/webgpu_inspector)