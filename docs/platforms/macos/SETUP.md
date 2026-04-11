# macOS セットアップガイド

## 前提条件

- Unity 6000.0 (Unity 6) 以降
- macOS 12 (Monterey) 以降
- Sentis パッケージ（Unity.InferenceEngine）がインストール済み

## 推論バックエンド

macOS では **CPU バックエンドのみ**が使用されます。Metal バックエンドには既知の問題があるため、すべての GPU バックエンドは自動的に CPU に切り替えられます。

| バックエンド | 対応状況 | 備考 |
|------------|---------|------|
| CPU | 対応 | 唯一の安定バックエンド |
| GPUPixel | 非対応 | Metal シェーダーコンパイル問題 |
| GPUCompute | 非対応 | Metal シェーダーコンパイル問題 |

### GPU が使えない理由

macOS の Graphics API は Metal のみです。Unity.InferenceEngine（Sentis）の Metal バックエンドには、VITS モデルで使用されるシェーダーのコンパイルに既知の問題があります。

具体的な問題:
- `'metal_stdlib' file not found` エラーが発生
- GPU 推論で音声データが破損する
- これは Unity / Sentis 側の Metal 対応の制限であり、uPiper 側で回避不可能

`BackendSelector` は Metal デバイスを検出すると、要求されたバックエンドに関わらず CPU に切り替えます:

```
[BackendSelector] Metal detected - using CPU backend due to known shader compilation issues
```

GPU バックエンドを明示的に指定した場合も、警告ログとともに CPU に強制切替されます:

```
[BackendSelector] GPUPixel requested on Metal, but Metal has known issues
with GPU inference. Using CPU backend instead.
```

## セットアップ手順

### 1. ビルドターゲットの切り替え

1. **File > Build Settings** を開く
2. **macOS** を選択
3. **Switch Platform** をクリック

### 2. Player Settings の設定

**Edit > Project Settings > Player > macOS** で以下を設定します。

| 設定項目 | 推奨値 | 備考 |
|---------|--------|------|
| Scripting Backend | IL2CPP または Mono | IL2CPP 推奨（パフォーマンス向上） |
| Api Compatibility Level | .NET Standard 2.1 | |
| Architecture | Intel 64-bit + Apple Silicon | Universal ビルド推奨 |

### 3. 設定コード

```csharp
var config = new PiperConfig
{
    Backend = InferenceBackend.Auto  // Metal が検出され、自動的に CPU が選択される
};

var tts = new PiperTTS(config);
await tts.InitializeAsync();
```

`InferenceBackend.CPU` を明示的に指定しても同じ結果になりますが、`Auto` を使用することで将来の Metal 対応改善時に自動的にGPU バックエンドが有効になる利点があります。

## Apple Silicon vs Intel のパフォーマンス

CPU バックエンドでの推論速度はプロセッサに大きく依存します。

| プロセッサ | 推論速度（相対値） | 備考 |
|-----------|------------------|------|
| Apple M3 | 高速 | 高性能 CPU コアで効率的に処理 |
| Apple M2 | 高速 | |
| Apple M1 | 良好 | |
| Intel i9 (10th gen) | 良好 | クロック周波数依存 |
| Intel i7 (10th gen) | 普通 | |
| Intel i5 (8th gen) | やや遅い | 古い世代の Mac |

Apple Silicon（M1/M2/M3）は高性能 CPU コアと高効率メモリアーキテクチャにより、CPU バックエンドでも十分高速に動作します。multilingual-test-medium モデル（38MB, fp16）であればリアルタイム音声合成が可能です。

Intel Mac では、世代やクロック周波数によってパフォーマンスが変動します。テキストが長い場合、沈黙句分割（`SplitInferenceOrchestrator`）を活用して体感レスポンスを改善できます。

## コード例

```csharp
using uPiper.Core;
using UnityEngine;

public class MacOSTTSExample : MonoBehaviour
{
    private PiperTTS _tts;

    private async void Start()
    {
        var config = new PiperConfig
        {
            Backend = InferenceBackend.Auto
        };

        _tts = new PiperTTS(config);
        await _tts.InitializeAsync();

        // 日本語音声合成
        var result = await _tts.PhonemizeAsync("こんにちは、世界");
        var request = SynthesisRequest.FromPhonemesWithProsody(
            result.Phonemes, result.ProsodyFlat);
        var clip = await _tts.SynthesizeAsync(request);

        var audioSource = GetComponent<AudioSource>();
        audioSource.clip = clip;
        audioSource.Play();
    }

    private void OnDestroy()
    {
        _tts?.Dispose();
    }
}
```

## Editor での開発

macOS 上の Unity Editor でも同様に CPU バックエンドが使用されます。Editor と実機ビルドで動作の差異はありません。

デバッグログで選択されたバックエンドを確認:

```
[BackendSelector] Selection Summary: Requested=Auto, Actual=CPU,
Graphics=Metal, ComputeShaders=True, VRAM=XXXXmb
[BackendSelector] Auto-selection reason: Metal detected - GPU backends
have known shader compilation issues
```

## よくある問題

### Metal シェーダーコンパイルエラー

**症状**: ログに `'metal_stdlib' file not found` が表示される。

**解決策**: `InferenceBackend.Auto` を使用していれば自動的に CPU が選択されるため問題ありません。手動で `GPUPixel` や `GPUCompute` を指定している場合は `Auto` に変更してください。

### 推論が遅い

**原因**: CPU バックエンドは GPU に比べて処理速度が低い。特に Intel Mac で顕著。

**解決策**:
1. IL2CPP ビルドを使用する（Mono より高速）
2. `lengthScale` パラメータで話速を調整（1.0 未満で高速化）
3. 長文は沈黙句で区切って段階的に再生

```csharp
// 高速再生
var request = SynthesisRequest.FromPhonemesWithProsody(
    result.Phonemes, result.ProsodyFlat, lengthScale: 0.8f);
```

### アプリがサンドボックスでブロックされる

**原因**: macOS のゲートキーパーが署名なしアプリをブロック。

**解決策**:
1. 開発中: **System Settings > Privacy & Security** で許可
2. 配布時: Apple Developer ID で署名し、Notarization を実行

### Universal Binary のサイズが大きい

**原因**: Intel + Apple Silicon 両アーキテクチャを含むため。

**解決策**:
- ターゲットユーザーが Apple Silicon のみの場合、Architecture を `Apple Silicon` のみに設定
- ただし、Intel Mac ユーザーもサポートする場合は Universal Binary を推奨