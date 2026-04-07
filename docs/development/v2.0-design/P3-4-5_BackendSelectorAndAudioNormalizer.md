# P3-4 / P3-5: BackendSelector・AudioNormalizer 切り出し 設計ドキュメント

**作成日**: 2026-04-08
**ステータス**: 設計
**依存**: P3-2 (IPiperConfigReadOnly) — BackendSelector のパラメータ型に影響
**後続**: P2-3 (NativeArray 統一) — AudioNormalizer の NativeArray 版は P2-3 と同時実装が効率的
**breaking change**: なし（internal リファクタリング。public API 変更なし）
**見積もり**: P3-4: 0.5人日 / P3-5: 0.5人日（合計1人日）

---

## 共通パターン: ロジック切り出し

P3-4 と P3-5 は同じリファクタリングパターンに従う:

1. 既存クラスから **単一責務に反する** ロジックを特定する
2. **static クラス** に切り出す（状態を持たない純粋関数群）
3. 元クラスからは切り出し先を **呼び出すだけ** に変更する
4. static メソッドの **単体テスト** を追加する（Unity 依存を最小化）

**共通ファイル配置**: `Assets/uPiper/Runtime/Core/AudioGeneration/` ディレクトリ内

---

# P3-4: BackendSelector 切り出し

## 1. 現状分析

### 1.1 DetermineBackendType メソッドの全容

**ファイル**: `Assets/uPiper/Runtime/Core/AudioGeneration/InferenceAudioGenerator.cs`
**行範囲**: L591–L680（約90行）
**アクセス修飾子**: `private`
**シグネチャ**: `private BackendType DetermineBackendType(PiperConfig config)`

### 1.2 分岐条件の全体構造

```
DetermineBackendType(PiperConfig config)
│
├─ [1] Metal チェック（L594–L602）
│   条件: SystemInfo.graphicsDeviceType == Metal
│   && config.Backend == GPUCompute || GPUPixel
│   → return CPU（Metal は GPU 推論に既知の問題あり）
│
├─ [2] GPUCompute 要求時（L606–L619）
│   ├─ [2a] WebGL && IsWebGPU → return GPUCompute（WebGPU は正常動作）
│   └─ [2b] その他 → return GPUPixel（GPUCompute は VITS で問題あり）
│
├─ [3] CPU 要求時（L621–L623）
│   → return CPU
│
├─ [4] GPUPixel 要求時（L625–L628）
│   → return GPUPixel
│
├─ [5] Auto 選択（L632–L676）
│   ├─ [5a] UNITY_WEBGL
│   │   ├─ IsWebGPU → return GPUCompute
│   │   └─ WebGL2 → return GPUPixel
│   ├─ [5b] UNITY_IOS || UNITY_ANDROID
│   │   ├─ supportsComputeShaders → return GPUCompute
│   │   └─ else → return CPU
│   └─ [5c] Desktop
│       ├─ Metal → return CPU
│       ├─ supportsComputeShaders && 十分な VRAM → return GPUPixel
│       └─ else → return CPU
│
└─ [6] フォールバック（L679）
    → return CPU
```

### 1.3 参照しているプラットフォーム情報

| 情報ソース | 使用箇所 | 用途 |
|-----------|---------|------|
| `SystemInfo.graphicsDeviceType` | [1], [5c] | Metal 検出 |
| `SystemInfo.supportsComputeShaders` | [5b], [5c] | Compute Shader 対応判定 |
| `SystemInfo.graphicsMemorySize` | [5c] | VRAM サイズ確認 |
| `Platform.PlatformHelper.IsWebGPU` | [2a], [5a] | WebGPU 判定（`#if UNITY_WEBGL` 内） |
| `config.Backend` | 全体 | ユーザー指定のバックエンド |
| `config.GPUSettings.MaxMemoryMB` | [5c] | VRAM 閾値 |

### 1.4 呼び出し箇所

| ファイル | 行 | コンテキスト |
|---------|-----|------------|
| `InferenceAudioGenerator.cs` | L132 | `InitializeAsync` 内。`_actualBackendType = DetermineBackendType(_piperConfig);` |

呼び出しは **1箇所のみ**。戻り値は `_actualBackendType` フィールドに格納され、Worker 作成時に使用される。

### 1.5 現状の問題点

1. **テスト不能**: private メソッドかつ `SystemInfo` / `#if` プリプロセッサに依存するため、単体テストできない
2. **責務混在**: プラットフォーム判定ロジックが推論エンジン管理クラスに埋め込まれている
3. **条件分岐の複雑さ**: 6つの分岐層、3つのプラットフォーム区分、プリプロセッサ条件が入り組んでいる

---

## 2. BackendSelector 設計

### 2.1 PlatformInfo record

プラットフォーム依存情報をカプセル化し、テスト時にモック可能にする。

```csharp
// Assets/uPiper/Runtime/Core/AudioGeneration/BackendSelector.cs

namespace uPiper.Core.AudioGeneration
{
    /// <summary>
    /// プラットフォーム情報のスナップショット。テスト時に任意の値を注入可能。
    /// </summary>
    public readonly struct PlatformInfo
    {
        /// <summary>現在のグラフィクスデバイスタイプ</summary>
        public GraphicsDeviceType GraphicsDeviceType { get; init; }

        /// <summary>Compute Shader サポート有無</summary>
        public bool SupportsComputeShaders { get; init; }

        /// <summary>GPU メモリサイズ (MB)</summary>
        public int GraphicsMemorySize { get; init; }

        /// <summary>WebGPU 上で動作しているか（WebGL プラットフォームのみ有効）</summary>
        public bool IsWebGPU { get; init; }

        /// <summary>WebGL プラットフォームか</summary>
        public bool IsWebGL { get; init; }

        /// <summary>モバイルプラットフォームか (iOS/Android)</summary>
        public bool IsMobile { get; init; }

        /// <summary>
        /// 現在のランタイム環境からPlatformInfoを構築する。
        /// </summary>
        public static PlatformInfo FromCurrentEnvironment()
        {
            return new PlatformInfo
            {
                GraphicsDeviceType = SystemInfo.graphicsDeviceType,
                SupportsComputeShaders = SystemInfo.supportsComputeShaders,
                GraphicsMemorySize = SystemInfo.graphicsMemorySize,
#if UNITY_WEBGL
                IsWebGL = true,
                IsWebGPU = Platform.PlatformHelper.IsWebGPU,
#else
                IsWebGL = false,
                IsWebGPU = false,
#endif
#if UNITY_IOS || UNITY_ANDROID
                IsMobile = true,
#else
                IsMobile = false,
#endif
            };
        }
    }
}
```

### 2.2 BackendSelector static クラス

```csharp
// Assets/uPiper/Runtime/Core/AudioGeneration/BackendSelector.cs

using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.Rendering;
using uPiper.Core.Logging;

namespace uPiper.Core.AudioGeneration
{
    /// <summary>
    /// 推論バックエンドの選択ロジック。
    /// InferenceAudioGenerator から切り出した単一責務クラス。
    /// </summary>
    public static class BackendSelector
    {
        /// <summary>
        /// ユーザー設定とプラットフォーム情報からバックエンドを決定する。
        /// </summary>
        /// <param name="requested">ユーザーが指定したバックエンド設定</param>
        /// <param name="platform">プラットフォーム情報</param>
        /// <param name="gpuMemoryThresholdMB">Auto選択時のVRAM閾値 (MB)</param>
        /// <returns>使用すべき BackendType</returns>
        public static BackendType Determine(
            InferenceBackend requested,
            PlatformInfo platform,
            int gpuMemoryThresholdMB = 512)
        {
            // [1] Metal チェック
            if (platform.GraphicsDeviceType == GraphicsDeviceType.Metal)
            {
                if (requested == InferenceBackend.GPUCompute ||
                    requested == InferenceBackend.GPUPixel)
                {
                    PiperLogger.LogWarning(
                        $"[BackendSelector] {requested} requested on Metal, " +
                        "but Metal has known issues with GPU inference. Using CPU.");
                    return BackendType.CPU;
                }
            }

            // [2] GPUCompute 要求時
            if (requested == InferenceBackend.GPUCompute)
            {
                if (platform.IsWebGL && platform.IsWebGPU)
                {
                    PiperLogger.LogInfo(
                        "[BackendSelector] GPUCompute on WebGPU — allowing.");
                    return BackendType.GPUCompute;
                }

                PiperLogger.LogWarning(
                    "[BackendSelector] GPUCompute has known VITS issues. " +
                    "Switching to GPUPixel.");
                return BackendType.GPUPixel;
            }

            // [3] 明示指定
            if (requested == InferenceBackend.CPU)
                return BackendType.CPU;

            if (requested == InferenceBackend.GPUPixel)
                return BackendType.GPUPixel;

            // [5] Auto 選択
            if (requested == InferenceBackend.Auto)
                return DetermineAutoBackend(platform, gpuMemoryThresholdMB);

            // [6] フォールバック
            return BackendType.CPU;
        }

        /// <summary>
        /// Auto モード時のバックエンド自動選択。
        /// </summary>
        private static BackendType DetermineAutoBackend(
            PlatformInfo platform, int gpuMemoryThresholdMB)
        {
            // WebGL
            if (platform.IsWebGL)
            {
                if (platform.IsWebGPU)
                {
                    PiperLogger.LogInfo(
                        "[BackendSelector] Auto: GPUCompute for WebGPU");
                    return BackendType.GPUCompute;
                }
                PiperLogger.LogInfo(
                    "[BackendSelector] Auto: GPUPixel for WebGL2");
                return BackendType.GPUPixel;
            }

            // モバイル
            if (platform.IsMobile)
            {
                if (platform.SupportsComputeShaders)
                {
                    PiperLogger.LogInfo(
                        "[BackendSelector] Auto: GPUCompute for mobile");
                    return BackendType.GPUCompute;
                }
                PiperLogger.LogInfo(
                    "[BackendSelector] Auto: CPU for mobile " +
                    "(no compute shader support)");
                return BackendType.CPU;
            }

            // Desktop
            if (platform.GraphicsDeviceType == GraphicsDeviceType.Metal)
            {
                PiperLogger.LogWarning(
                    "[BackendSelector] Auto: CPU for Metal " +
                    "(known shader compilation issues)");
                return BackendType.CPU;
            }

            if (platform.SupportsComputeShaders &&
                platform.GraphicsMemorySize >= gpuMemoryThresholdMB)
            {
                PiperLogger.LogInfo(
                    "[BackendSelector] Auto: GPUPixel for desktop " +
                    "(better VITS compatibility)");
                return BackendType.GPUPixel;
            }

            PiperLogger.LogInfo(
                "[BackendSelector] Auto: CPU for desktop");
            return BackendType.CPU;
        }
    }
}
```

### 2.3 InferenceAudioGenerator の変更

```csharp
// Before (L132):
_actualBackendType = DetermineBackendType(_piperConfig);

// After:
var platformInfo = PlatformInfo.FromCurrentEnvironment();
_actualBackendType = BackendSelector.Determine(
    _piperConfig.Backend,
    platformInfo,
    _piperConfig.GPUSettings.MaxMemoryMB);
```

変更点:
- `DetermineBackendType` メソッド (L591–L680) を **削除**
- 呼び出し元 (L132) を `BackendSelector.Determine()` に置き換え
- `InferenceAudioGenerator` の行数が約90行減少

### 2.4 プリプロセッサ条件の扱い

現在の `DetermineBackendType` は `#if UNITY_WEBGL` / `#if UNITY_IOS || UNITY_ANDROID` を**メソッド内部**で使用している。切り出し後:

| 項目 | 現状 | 変更後 |
|------|------|--------|
| `#if UNITY_WEBGL` (WebGPU判定) | `DetermineBackendType` 内 | `PlatformInfo.FromCurrentEnvironment()` 内 |
| `#if UNITY_IOS \|\| UNITY_ANDROID` | `DetermineBackendType` 内 | `PlatformInfo.FromCurrentEnvironment()` 内 |
| `BackendSelector.Determine()` | — | プリプロセッサ **不要**（PlatformInfo のフィールドで分岐） |

利点: `BackendSelector.Determine()` 自体はプリプロセッサフリーとなり、全プラットフォームのロジックを単一のテストで検証可能になる。

---

## 3. BackendSelector テスト計画

### 3.1 テストクラス

**ファイル**: `Assets/uPiper/Tests/Runtime/AudioGeneration/BackendSelectorTests.cs`

PlatformInfo を直接構築できるため、Unity の SystemInfo に依存しないテストが可能。

### 3.2 テストケース

| テスト名 | 入力 | 期待結果 |
|---------|------|---------|
| `Determine_MetalWithGPUCompute_ReturnsCPU` | Metal + GPUCompute | CPU |
| `Determine_MetalWithGPUPixel_ReturnsCPU` | Metal + GPUPixel | CPU |
| `Determine_MetalWithCPU_ReturnsCPU` | Metal + CPU | CPU |
| `Determine_MetalWithAuto_ReturnsCPU` | Metal + Auto | CPU |
| `Determine_GPUCompute_WebGPU_ReturnsGPUCompute` | WebGPU + GPUCompute | GPUCompute |
| `Determine_GPUCompute_NonWebGPU_ReturnsGPUPixel` | Desktop + GPUCompute | GPUPixel |
| `Determine_CPU_ReturnsCPU` | any + CPU | CPU |
| `Determine_GPUPixel_ReturnsGPUPixel` | any + GPUPixel | GPUPixel |
| `Determine_Auto_WebGPU_ReturnsGPUCompute` | WebGPU + Auto | GPUCompute |
| `Determine_Auto_WebGL2_ReturnsGPUPixel` | WebGL2 + Auto | GPUPixel |
| `Determine_Auto_Mobile_ComputeShader_ReturnsGPUCompute` | Mobile + CS対応 + Auto | GPUCompute |
| `Determine_Auto_Mobile_NoComputeShader_ReturnsCPU` | Mobile + CS非対応 + Auto | CPU |
| `Determine_Auto_Desktop_SufficientVRAM_ReturnsGPUPixel` | Desktop + VRAM十分 + Auto | GPUPixel |
| `Determine_Auto_Desktop_InsufficientVRAM_ReturnsCPU` | Desktop + VRAM不足 + Auto | CPU |

### 3.3 テストコード例

```csharp
[Test]
public void Determine_MetalWithGPUCompute_ReturnsCPU()
{
    var platform = new PlatformInfo
    {
        GraphicsDeviceType = GraphicsDeviceType.Metal,
        SupportsComputeShaders = true,
        GraphicsMemorySize = 4096,
        IsWebGL = false,
        IsWebGPU = false,
        IsMobile = false,
    };

    var result = BackendSelector.Determine(InferenceBackend.GPUCompute, platform);

    Assert.AreEqual(BackendType.CPU, result);
}

[Test]
public void Determine_Auto_Desktop_SufficientVRAM_ReturnsGPUPixel()
{
    var platform = new PlatformInfo
    {
        GraphicsDeviceType = GraphicsDeviceType.Direct3D11,
        SupportsComputeShaders = true,
        GraphicsMemorySize = 4096,
        IsWebGL = false,
        IsWebGPU = false,
        IsMobile = false,
    };

    var result = BackendSelector.Determine(
        InferenceBackend.Auto, platform, gpuMemoryThresholdMB: 512);

    Assert.AreEqual(BackendType.GPUPixel, result);
}
```

---

# P3-5: AudioNormalizer 切り出し

## 1. 現状分析

### 1.1 AudioClipBuilder の現在の責務

**ファイル**: `Assets/uPiper/Runtime/Core/AudioGeneration/AudioClipBuilder.cs`
**全体行数**: 133行

| メソッド | 行範囲 | 責務 | static化可能 |
|---------|--------|------|-------------|
| `BuildAudioClip(float[], int, string)` | L20–L52 | AudioClip 構築 | No (Unity API) |
| `NormalizeAudio(float[], float)` | L60–L96 | 正規化（新規配列返却） | Yes |
| `NormalizeAudioInPlace(float[], float)` | L103–L130 | 正規化（in-place） | Yes |

`BuildAudioClip` は Unity の `AudioClip.Create` / `SetData` に依存する AudioClip 構築の責務。
`NormalizeAudio` / `NormalizeAudioInPlace` は pure な数値演算で、AudioClip とは無関係。

### 1.2 NormalizeAudio の詳細

```csharp
public float[] NormalizeAudio(float[] audioData, float targetPeak = 0.95f)
```

**ロジック**:
1. null/empty チェック → 早期 return
2. `targetPeak` を `Mathf.Clamp01` でクランプ
3. 最大振幅 `maxAmplitude` を線形スキャンで検出
4. `maxAmplitude <= 0` または `≈ targetPeak` なら早期 return
5. `scale = targetPeak / maxAmplitude` を計算
6. **新規配列** `normalizedData` を作成し、全要素に `scale` を乗算
7. ログ出力後、新規配列を返却

**問題**: P2-3 (NativeArray 統一) の分析で指摘されている通り、非in-place版は `TTSSynthesisOrchestrator` では使用されていない（in-place 版を使用）が、public API として残存している。

### 1.3 NormalizeAudioInPlace の詳細

```csharp
public void NormalizeAudioInPlace(float[] audioData, float targetPeak = 0.95f)
```

**ロジック**: `NormalizeAudio` と同一アルゴリズムだが、新規配列を作成せず元の配列を直接変更する。

### 1.4 呼び出し箇所

| ファイル | 行 | メソッド | 使用バリアント |
|---------|-----|---------|--------------|
| `TTSSynthesisOrchestrator.cs` | L99 | `SynthesizeAsync` | `NormalizeAudioInPlace` |
| `InferenceEngineDemo.cs` | L849 | デモ音声生成 | `NormalizeAudioInPlace` |
| `AudioClipBuilderTests.cs` | L68 | テスト | `NormalizeAudio` |

### 1.5 Mathf 依存

現在のコードは `Mathf.Clamp01` と `Mathf.Abs` と `Mathf.Approximately` を使用している。これらは Unity 依存だが:
- `Mathf.Abs` → `Math.Abs` で置換可能
- `Mathf.Clamp01` → `Math.Clamp(value, 0f, 1f)` で置換可能
- `Mathf.Approximately` → 独自の近似比較で置換可能

ただし、Unity プロジェクト内で動作するため、`Mathf` のままでも問題ない。テスト容易性を優先し、可能な範囲で `System.Math` を使用するが、厳密な要件ではない。

---

## 2. AudioNormalizer 設計

### 2.1 AudioNormalizer static クラス

```csharp
// Assets/uPiper/Runtime/Core/AudioGeneration/AudioNormalizer.cs

using System;
using UnityEngine;
using uPiper.Core.Logging;

namespace uPiper.Core.AudioGeneration
{
    /// <summary>
    /// 音声データの正規化ロジック。AudioClipBuilder から分離した単一責務クラス。
    /// </summary>
    public static class AudioNormalizer
    {
        /// <summary>
        /// 音声データをin-placeで正規化する。
        /// </summary>
        /// <param name="audioData">音声データ（変更される）</param>
        /// <param name="targetPeak">目標ピーク値（0-1）</param>
        public static void NormalizeInPlace(float[] audioData, float targetPeak = 0.95f)
        {
            if (audioData == null || audioData.Length == 0)
                return;

            targetPeak = Mathf.Clamp01(targetPeak);

            // 最大振幅を検出
            var maxAmplitude = 0f;
            for (var i = 0; i < audioData.Length; i++)
            {
                var absValue = Mathf.Abs(audioData[i]);
                if (absValue > maxAmplitude)
                    maxAmplitude = absValue;
            }

            // 既に正規化済み or 無音ならスキップ
            if (maxAmplitude <= 0f || Mathf.Approximately(maxAmplitude, targetPeak))
                return;

            var scale = targetPeak / maxAmplitude;
            for (var i = 0; i < audioData.Length; i++)
            {
                audioData[i] *= scale;
            }

            PiperLogger.LogDebug(
                $"Normalized audio in-place: max amplitude {maxAmplitude:F3} -> {targetPeak:F3}");
        }

        /// <summary>
        /// 音声データを正規化し、新しい配列で返す。元のデータは変更しない。
        /// </summary>
        /// <param name="audioData">音声データ</param>
        /// <param name="targetPeak">目標ピーク値（0-1）</param>
        /// <returns>正規化された音声データ</returns>
        public static float[] Normalize(float[] audioData, float targetPeak = 0.95f)
        {
            if (audioData == null || audioData.Length == 0)
                return audioData;

            targetPeak = Mathf.Clamp01(targetPeak);

            var maxAmplitude = 0f;
            for (var i = 0; i < audioData.Length; i++)
            {
                var absValue = Mathf.Abs(audioData[i]);
                if (absValue > maxAmplitude)
                    maxAmplitude = absValue;
            }

            if (maxAmplitude <= 0f || Mathf.Approximately(maxAmplitude, targetPeak))
                return audioData;

            var scale = targetPeak / maxAmplitude;
            var normalizedData = new float[audioData.Length];
            for (var i = 0; i < audioData.Length; i++)
            {
                normalizedData[i] = audioData[i] * scale;
            }

            PiperLogger.LogDebug(
                $"Normalized audio: max amplitude {maxAmplitude:F3} -> {targetPeak:F3}");
            return normalizedData;
        }
    }
}
```

### 2.2 P2-3 (NativeArray 統一) との関係

P2-3 で `NativeArray<float>` パイプラインが導入される際に、以下のオーバーロードを追加する:

```csharp
/// <summary>
/// NativeArray版 in-place 正規化。P2-3 で追加。
/// </summary>
public static void NormalizeInPlace(NativeArray<float> audioData, float targetPeak = 0.95f)
{
    // NativeArray 上で直接ループ。GCアロケーションなし。
}
```

P3-5 の時点では `float[]` 版のみ実装し、P2-3 実装時に NativeArray 版を追加する設計とする。P3-5 を先に完了しておくことで、P2-3 では NativeArray オーバーロード追加のみで済む。

### 2.3 AudioClipBuilder の変更

```csharp
// Before:
public class AudioClipBuilder
{
    public AudioClip BuildAudioClip(float[] audioData, int sampleRate, string clipName = null) { ... }
    public float[] NormalizeAudio(float[] audioData, float targetPeak = 0.95f) { ... }
    public void NormalizeAudioInPlace(float[] audioData, float targetPeak = 0.95f) { ... }
}

// After:
public class AudioClipBuilder
{
    public AudioClip BuildAudioClip(float[] audioData, int sampleRate, string clipName = null) { ... }

    // 正規化メソッドは削除。AudioNormalizer に移動。
}
```

### 2.4 呼び出し元の変更

**TTSSynthesisOrchestrator.cs (L99)**:
```csharp
// Before:
_audioClipBuilder.NormalizeAudioInPlace(audioData, 0.95f);

// After:
AudioNormalizer.NormalizeInPlace(audioData, 0.95f);
```

**InferenceEngineDemo.cs (L849)**:
```csharp
// Before:
_audioBuilder.NormalizeAudioInPlace(audioData, 0.95f);

// After:
AudioNormalizer.NormalizeInPlace(audioData, 0.95f);
```

---

## 3. AudioNormalizer テスト計画

### 3.1 テストクラス

**ファイル**: `Assets/uPiper/Tests/Runtime/AudioGeneration/AudioNormalizerTests.cs`

### 3.2 テストケース

| テスト名 | 入力 | 期待結果 |
|---------|------|---------|
| `NormalizeInPlace_ValidData_NormalizesToTarget` | `{0.5f, -0.5f, 0.25f}`, target=0.95 | maxAbs == 0.95 |
| `NormalizeInPlace_SilentAudio_NoChange` | `{0f, 0f, 0f}` | 変更なし |
| `NormalizeInPlace_AlreadyNormalized_NoChange` | `{0.95f, -0.95f}`, target=0.95 | 変更なし |
| `NormalizeInPlace_NullArray_NoException` | `null` | 例外なし |
| `NormalizeInPlace_EmptyArray_NoException` | `{}` | 例外なし |
| `NormalizeInPlace_TargetPeakClamped_ClampedTo01` | target=1.5 | Clamp01 適用 |
| `Normalize_ValidData_ReturnsNewArray` | `{0.5f, -0.5f}`, target=0.95 | 新規配列、元は変更なし |
| `Normalize_ValidData_NormalizesToTarget` | `{0.5f, -0.5f}`, target=0.95 | maxAbs == 0.95 |
| `Normalize_SilentAudio_ReturnsSameArray` | `{0f, 0f}` | 同一参照を返す |

### 3.3 既存テストの移行

`AudioClipBuilderTests.NormalizeAudio_ValidData_NormalizesToTarget` (L61–L81) を:
1. `AudioNormalizerTests.Normalize_ValidData_NormalizesToTarget` に移動
2. `_builder.NormalizeAudio(...)` → `AudioNormalizer.Normalize(...)` に変更
3. `AudioClipBuilderTests` からは正規化テストを削除（BuildAudioClip テストのみ残す）

---

# 共通: 影響範囲まとめ

## ファイル変更一覧

| ファイル | 変更種別 | P3-4 | P3-5 |
|---------|---------|------|------|
| `AudioGeneration/BackendSelector.cs` | **新規作成** | Yes | — |
| `AudioGeneration/AudioNormalizer.cs` | **新規作成** | — | Yes |
| `AudioGeneration/InferenceAudioGenerator.cs` | 修正（DetermineBackendType 削除、呼び出し変更） | Yes | — |
| `AudioGeneration/AudioClipBuilder.cs` | 修正（正規化メソッド削除） | — | Yes |
| `AudioGeneration/TTSSynthesisOrchestrator.cs` | 修正（呼び出し変更） | — | Yes |
| `Demo/InferenceEngineDemo.cs` | 修正（呼び出し変更） | — | Yes |
| `Tests/AudioGeneration/BackendSelectorTests.cs` | **新規作成** | Yes | — |
| `Tests/AudioGeneration/AudioNormalizerTests.cs` | **新規作成** | — | Yes |
| `Tests/AudioGeneration/AudioClipBuilderTests.cs` | 修正（正規化テスト削除） | — | Yes |

## リスク・注意事項

| リスク | 対策 |
|-------|------|
| P3-2 (IPiperConfigReadOnly) との順序 | BackendSelector は `InferenceBackend` enum と `int` (gpuMemoryThresholdMB) を受け取るため、P3-2 とは独立して実装可能。P3-2 適用時に `InferenceAudioGenerator` 側の config アクセスが変わるが、BackendSelector 自体は影響なし |
| PiperLogger への依存 | 切り出し先でも PiperLogger を使用。テスト時はログ出力を無視する設計（ログ内容のアサートは行わない） |
| `Mathf.Approximately` の精度 | AudioNormalizer は `Mathf.Approximately` をそのまま使用。精度要件は元のコードと同一 |
| InferenceEngineDemo の変更 | Demo コードのため破壊リスクは低い。手動動作確認で十分 |

## 実装順序

P3-4 と P3-5 は **互いに独立** しているため、並行実装可能。推奨順序:

1. **P3-5 (AudioNormalizer)** — 変更がシンプルで、テストも容易。先行実装でパターンを確立。
2. **P3-4 (BackendSelector)** — PlatformInfo 設計を含むため若干複雑。P3-5 の成功を確認後に実施。