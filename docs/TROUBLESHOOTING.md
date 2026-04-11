# uPiper トラブルシューティングガイド

uPiper を使用する際に遭遇しやすい問題と解決策をまとめたガイドです。

---

## クイック診断チェックリスト

問題が発生したら、まず以下を確認してください。

- [ ] `InitializeAsync()` を `await` で呼び出し、完了を待っているか
- [ ] `LoadVoiceAsync()` が正常に完了しているか（または自動ロードを使用しているか）
- [ ] モデルファイル（`.onnx`）と設定ファイル（`.onnx.json`）が `Assets/uPiper/Resources/Models/` に配置されているか
- [ ] `PiperConfig.EnableDebugLogging = true` にしてConsoleログを確認したか
- [ ] `AudioSource` コンポーネントがシーンに存在し、有効になっているか
- [ ] 対象プラットフォーム固有の制限事項を確認したか（後述）

---

## 1. 音が出ない

### 1.1 初期化が完了していない

**症状**: `GenerateAudioAsync` を呼んでも `InvalidOperationException` が発生する、または `AudioClip` が `null` になる。

**原因**: `InitializeAsync()` や `LoadVoiceAsync()` の完了を待たずに音声合成を実行している。

**解決策**:

```csharp
// 正しい初期化手順
var piperTTS = new PiperTTS(config);
await piperTTS.InitializeAsync();          // 必ず await する
await piperTTS.LoadVoiceAsync(voiceConfig); // 必ず await する

// 初期化完了後に音声合成
var clip = await piperTTS.GenerateAudioAsync("こんにちは");
audioSource.clip = clip;
audioSource.Play();
```

**エラーメッセージ**: `PiperTTS is not initialized. Call InitializeAsync first.`

### 1.2 モデルファイルが見つからない

**症状**: `PiperException: Model asset not found` が発生する。

**原因**: ONNXモデルファイルが正しい場所に配置されていない。

**解決策**:
1. `.onnx` ファイルを `Assets/uPiper/Resources/Models/` に配置する
2. 対応する `.onnx.json` 設定ファイルも同じディレクトリに配置する
3. ファイル名が `<voiceId>.onnx` / `<voiceId>.onnx.json` の形式になっていることを確認する

**エラーメッセージ**:
- `No model assets found in Resources/Models/. Please import a voice model (.onnx) into Assets/uPiper/Resources/Models/.`
- `Found N model asset(s) in Resources/Models/, but none could be loaded. Ensure a matching .onnx.json config file exists.`

### 1.3 iOS で音が出ない

**症状**: iOSデバイスで音声が再生されない。特にサイレントスイッチ（マナーモード）がONの場合。

**原因**: iOSでは `AVAudioSession` が適切に設定されていないと音声が再生されない。uPiperは初期化時に自動で `AVAudioSession` を `Playback` カテゴリに設定するが、他のオーディオアプリとの競合等で無効化される場合がある。

**解決策**:

```csharp
// 通常は自動処理されるが、手動で確認・再初期化する場合:
#if UNITY_IOS && !UNITY_EDITOR
IOSAudioSessionHelper.Initialize();          // Playbackカテゴリに設定
IOSAudioSessionHelper.LogStatus();           // 現在の状態を確認
IOSAudioSessionHelper.EnsureActive();        // 再生直前に確認（非アクティブなら再初期化）
#endif
```

**確認ポイント**:
- `IOSAudioSessionHelper.GetCategoryName()` が `"AVAudioSessionCategoryPlayback"` を返しているか
- `IOSAudioSessionHelper.GetVolume()` が 0 より大きいか
- 端末のサイレントスイッチの状態（Playbackカテゴリなら無視される）

### 1.4 WebGL で音が出ない

**症状**: WebGLビルドで音声が再生されない。

**原因**: ブラウザのセキュリティポリシーにより、ユーザー操作（クリック/タップ）なしでは `AudioContext` を開始できない。

**解決策**:

uPiperには `WebGLInteractionGate` が組み込まれている。初期化前にユーザー操作を待機する。

```csharp
// WebGLではユーザー操作を待機してからTTSを初期化
await WebGLInteractionGate.WaitForInteractionAsync();  // クリック/タップを待つ
await piperTTS.InitializeAsync();
```

**注意**: `WebGLInteractionGate` は全画面オーバーレイを表示し、ユーザーのクリック/タップ後に `AudioContext` をレジュームする。非WebGLプラットフォームでは即座に完了する。

### 1.5 バックエンドの問題（macOS / Metal）

**症状**: macOSでGPU推論を指定すると、音声が破損する（ノイズ、無音、異常な音声）。

**原因**: Unity.InferenceEngine (Sentis) の Metal バックエンドにはシェーダコンパイルの既知の問題があり、GPU推論が正常に動作しない。

**解決策**:

macOSでは `BackendSelector` が自動的にCPUバックエンドにフォールバックする。明示的にGPUを指定した場合でも強制的にCPUが使用される。

```csharp
// macOSではAutoを指定すれば自動でCPUが選ばれる（推奨）
var config = new PiperConfig
{
    Backend = InferenceBackend.Auto
};
```

ログに以下の警告が出ていれば、フォールバックが正常に動作している:
```
[BackendSelector] Metal detected - using CPU backend due to known shader compilation issues
```

---

## 2. 音声品質が悪い

### 2.1 短いテキストの品質劣化

**症状**: 1〜3文字程度の短いテキスト（例: 「はい」「OK」）で、ノイズが多い音声やゼロ長の音声が生成される。

**原因**: VITSモデルの構造的制限。音素ID列が40トークン未満の場合、Duration Predictorが不安定になる。

**解決策**:

uPiperには `ShortTextMitigatingGenerator` が組み込まれており、自動的に2つの緩和策を適用する:

- **Strategy A**: 音素IDをPADトークンで40要素に拡張し、推論後に無音部分をトリム
- **Strategy B**: `noiseScale` / `noiseW` を動的に低減して Duration Predictor を安定化

これらは `GenerateAudioAsync` 呼び出し時に**自動適用**されるため、追加の設定は不要。

それでも品質が不十分な場合の回避策:
```csharp
// 短いテキストの前後にフィラーを追加し、品質を改善
var text = "はい";
var paddedText = $"、{text}。";  // 句読点を追加して音素列を長くする
var clip = await piperTTS.GenerateAudioAsync(paddedText);
```

### 2.2 NoiseScale / NoiseW の調整

**症状**: 音声が単調すぎる、またはノイズが多い。

**原因**: VITSモデルのノイズパラメータがデフォルト値のままになっている。

**パラメータの役割**:

| パラメータ | デフォルト | 効果 | 推奨範囲 |
|-----------|-----------|------|---------|
| `NoiseScale` | 0.667 | 音素レベルの声質・ピッチ変動を制御 | 0.3 〜 1.0 |
| `NoiseW` | 0.8 | 音素の発話時間（リズム・間）の揺らぎを制御 | 0.3 〜 1.0 |
| `LengthScale` | 1.0 | 話速スケール（小さいほど速い） | 0.5 〜 2.0 |

**調整方法**:

```csharp
// VoiceConfigで設定する場合
voiceConfig.NoiseScale = 0.5f;  // よりクリアな音声
voiceConfig.NoiseW = 0.6f;      // より安定したリズム
voiceConfig.LengthScale = 0.9f; // やや速い話速

// SynthesisRequestで個別に設定する場合
var request = SynthesisRequest.FromPhonemes(
    phonemes,
    noiseScale: 0.5f,
    noiseW: 0.6f,
    lengthScale: 0.9f);
var clip = await piperTTS.SynthesizeAsync(request);
```

**調整のコツ**:
- ノイズが多い場合: `NoiseScale` を 0.3〜0.5 に下げる
- 音声が単調な場合: `NoiseScale` を 0.8〜1.0 に上げる
- リズムが不自然な場合: `NoiseW` を 0.4〜0.6 に下げる
- `LengthScale` を極端に小さくすると（< 0.5）品質が劣化する場合がある

### 2.3 音声の正規化

**症状**: 音量が小さすぎる、または大きすぎる。

**解決策**:

```csharp
var config = new PiperConfig
{
    NormalizeAudio = true,      // 音声正規化を有効化（デフォルト: true）
    TargetRMSLevel = -20f       // 目標RMSレベル（dB）。-40〜0の範囲
};
```

`TargetRMSLevel` を調整して適切な音量を設定する。`-20f` がデフォルトで、`-10f` にすると音量が上がり、`-30f` にすると下がる。

---

## 3. 特定の単語が正しく読まれない

### 3.1 カスタム辞書の使用

**症状**: 技術用語、固有名詞、英略語が正しく読まれない。

**原因**: G2P（Grapheme-to-Phoneme）エンジンが未知の単語をデフォルトルールで変換しているため。

**解決策**: カスタム辞書に単語を登録する。

#### 方法1: JSONファイルで登録

辞書ファイルを `StreamingAssets/uPiper/Dictionaries/` に配置する。

```json
{
  "version": "2.0",
  "entries": {
    "Docker": {"pronunciation": "ドッカー", "priority": 9},
    "GitHub": {"pronunciation": "ギットハブ", "priority": 9},
    "Kubernetes": {"pronunciation": "クバネティス", "priority": 9},
    "Azure": {"pronunciation": "アジュール", "priority": 9}
  }
}
```

**辞書ファイル名**: ファイル名のアルファベット順に読み込まれる。既存の辞書ファイル:

| ファイル | 内容 |
|---------|------|
| `additional_tech_dict.json` | AI/LLM関連用語 |
| `default_common_dict.json` | IT/ビジネス用語 |
| `default_tech_dict.json` | 技術用語 |
| `user_custom_dict.json` | ユーザー定義（テンプレート） |

#### 方法2: コードで動的に登録

```csharp
var dict = new CustomDictionary();
dict.AddWord("MyApp", "マイアップ", priority: 10);
dict.AddWord("uPiper", "ユーパイパー", priority: 10);
```

### 3.2 優先度の仕組み

辞書エントリの `priority` は整数値で、値が大きいほど優先される。同じ単語が複数の辞書にある場合、高い優先度のエントリが使用される。デフォルトの優先度は `5`。

### 3.3 辞書のセキュリティ制限

カスタム辞書には以下のセキュリティ制限がある:

- **ファイルサイズ上限**: 10MB（`MaxDictFileSize`）。超過するとエラー
- **パストラバーサル拒否**: ファイルパスに `..` セグメントが含まれる場合は `ArgumentException`
- **JSONパースエラー**: `JsonUtility` 失敗時は手動パースにフォールバック。手動パースも失敗時は警告ログ

---

## 4. WebGLでタイムアウトする

### 4.1 モデルのダウンロードに時間がかかる

**症状**: WebGLビルドでローディングが極端に長い、またはタイムアウトする。

**原因**: ONNXモデル（multilingual-test-medium: 38MB）のダウンロードに時間がかかっている。

**解決策**:

1. **タイムアウト値を延長する**:
   ```csharp
   var config = new PiperConfig
   {
       TimeoutMs = 60000  // 60秒に延長（デフォルト: 30000ms）
   };
   ```

2. **適切なサーバー設定を行う**:
   - `.onnx` ファイルに対するContent-Typeを設定する
   - gzip/brotli圧縮を有効にする
   - CDNを使用してキャッシュする

3. **ローディングUIを表示する**:
   uPiperには `WebGLLoadingPanel` コンポーネントが含まれており、ダウンロード進捗を表示できる。

### 4.2 大容量ファイルの分割ロード

**症状**: GitHub Pagesなどのホスティングサービスでファイルサイズ制限に引っかかる。

**原因**: WebGLビルドの`.data`ファイルが大きすぎる。

**解決策**:

`WebGLSplitDataProcessor` を使用してファイルを自動分割する:

1. Unity Editor で `uPiper > Development > Prepare WebGL for GitHub Pages` メニューを選択
2. WebGLビルドの出力フォルダを選択
3. 100MBを超えるファイルが自動的に90MBチャンクに分割される
4. `split-file-loader.js` がブラウザ側で自動結合する

### 4.3 IndexedDBキャッシュ

WebGLでは `IndexedDBCache` を使用してモデルデータをブラウザにキャッシュできる。2回目以降のアクセスではネットワークダウンロードが不要になる。

---

## 5. GPU推論が使えない

### 5.1 プラットフォーム別バックエンド対応表

`InferenceBackend.Auto` を指定した場合の自動選択結果:

| プラットフォーム | 選択されるバックエンド | 条件 |
|-----------------|----------------------|------|
| Windows / Linux | GPUPixel | VRAM 512MB以上 + ComputeShader対応時 |
| Windows / Linux | CPU | 上記条件を満たさない場合 |
| macOS (Metal) | CPU | Metal非対応のため強制CPU |
| iOS / Android | GPUPixel | ComputeShader対応時 |
| iOS / Android | CPU | ComputeShader非対応時 |
| WebGL (WebGPU) | GPUCompute | WebGPUネイティブサポート |
| WebGL (WebGL2) | GPUPixel | WebGL2互換 |

### 5.2 macOSでGPUが使えない

**原因**: Unity.InferenceEngine (Sentis) の Metal バックエンドに既知のシェーダコンパイル問題があり、GPU推論が破損した音声を生成する。

**解決策**: macOSでは `InferenceBackend.Auto` を使用する（自動的にCPUが選択される）。CPUバックエンドでも実用的な速度で動作する。

### 5.3 VRAM不足でGPUが選択されない

**症状**: GPU搭載PCなのにCPUバックエンドが選択される。

**原因**: `BackendSelector` は Auto 選択時に VRAM が 512MB 以上であることを条件としている。

**確認方法**: `PiperConfig.EnableDebugLogging = true` にした上で、ログに出力される `BackendSelector` のサマリを確認する:

```
[BackendSelector] Selection Summary: Requested=Auto, Actual=CPU,
    Graphics=Direct3D11, ComputeShaders=True, VRAM=256MB
[BackendSelector] Auto-selection reason: Desktop fallback to CPU
```

**解決策**:
```csharp
// GPUを強制的に使用する場合（VRAMが十分にあることを確認した上で）
var config = new PiperConfig
{
    Backend = InferenceBackend.GPUPixel  // 明示的にGPUPixelを指定
};
```

### 5.4 GPUCompute の制限

**症状**: `GPUCompute` を指定したのに `GPUPixel` にフォールバックされる。

**原因**: VITSオーディオモデルでは `GPUCompute` バックエンドに既知の互換性問題がある。`BackendSelector` は安全のため `GPUPixel` に自動変換する。

**例外**: WebGPU環境では `GPUCompute` がそのまま使用される（WebGPUのCompute Shaderは正常に動作する）。

ログに以下の警告が出る:
```
[BackendSelector] GPU Compute backend has known issues with VITS audio models.
[BackendSelector] Switching to GPU Pixel backend for better compatibility.
```

---

## 6. よくあるエラーメッセージと対処法

### PiperException

| エラーメッセージ | 原因 | 対処法 |
|---------------|------|--------|
| `DefaultLanguage cannot be null or empty` | `PiperConfig.DefaultLanguage` が未設定 | `"ja"` 等の言語コードを設定する |
| `Invalid sample rate: NHz` | サンプルレートが範囲外 | 22050 等の有効な値を設定する |
| `Invalid WorkerThreads: N. Must be >= 0` | ワーカースレッド数が負の値 | 0（自動検出）以上を指定する |
| `Invalid TimeoutMs: N. Must be >= 0` | タイムアウト値が負の値 | 0（無制限）以上を指定する |
| `Invalid PhonemeSilenceSpec: ...` | 沈黙トークンの書式が不正 | `"_ 0.5"` 等の正しい書式を指定する |
| `Model asset not found: Models/<name>` | モデルファイルがResourcesにない | `Assets/uPiper/Resources/Models/` にモデルを配置する |
| `No model assets found in Resources/Models/` | Resourcesフォルダにモデルが1つもない | モデルファイル（`.onnx`）をインポートする |
| `Failed to load voice: <voiceId>` | ボイスのロードに失敗 | モデルとJSONの整合性を確認する |
| `Voice not found: <voiceId>` | 未ロードのボイスIDを参照 | `LoadVoiceAsync` でロードしてから参照する |
| `Failed to convert text to phonemes` | 音素化に失敗 | テキストが空でないか、対応言語か確認する |
| `No phonemes generated` | 音素化は成功したが出力が空 | 入力テキストに発話可能な文字が含まれているか確認する |

### InvalidOperationException

| エラーメッセージ | 原因 | 対処法 |
|---------------|------|--------|
| `PiperTTS is not initialized. Call InitializeAsync first.` | 初期化前にメソッドを呼んだ | `await InitializeAsync()` を先に実行する |
| `No voice selected. Load a voice first.` | ボイス未ロードで `GenerateAudioAsync` を呼んだ | `await LoadVoiceAsync()` を先に実行する |

### PiperPhonemizationException

| エラーメッセージ | 原因 | 対処法 |
|---------------|------|--------|
| `Phonemizer is not initialized.` | 音素化エンジンが未初期化 | `InitializeAsync` の完了を確認する |

---

## 7. その他のよくある問題

### 7.1 Disposeの二重呼び出し

**症状**: `ObjectDisposedException` が発生する。

**解決策**: `PiperTTS` は `IDisposable` を実装している。`using` ステートメントを使用するか、手動で1回だけ `Dispose()` を呼ぶ。

```csharp
// usingを使う（推奨）
using var piperTTS = new PiperTTS(config);
await piperTTS.InitializeAsync();
// ... 使用 ...
// スコープ終了時に自動Dispose

// OnDestroyで明示的にDisposeする場合
private PiperTTS _piperTTS;

private void OnDestroy()
{
    _piperTTS?.Dispose();
    _piperTTS = null;  // 二重Dispose防止
}
```

### 7.2 ウォームアップによる初回レイテンシ削減

**症状**: 初回の `GenerateAudioAsync` が遅い（500〜800ms余計にかかる）。

**解決策**:

```csharp
var config = new PiperConfig
{
    EnableWarmup = true,       // ウォームアップ推論を有効化
    WarmupIterations = 2       // ウォームアップ回数（デフォルト: 2）
};
```

ウォームアップを有効にすると、`InitializeAsync` 完了時にダミー推論を実行し、JITキャッシュを安定化させる。

### 7.3 長文での句切りがない

**症状**: 長い文章を合成すると、息継ぎがなく不自然に聞こえる。

**解決策**:

```csharp
var config = new PiperConfig
{
    EnablePhonemeSilence = true,       // 句分割を有効化
    PhonemeSilenceSpec = "_ 0.5"       // 読点で0.5秒の無音を挿入
    // 複数指定: "_ 0.5,# 0.3"        // 読点0.5秒 + 句点0.3秒
};
```

### 7.4 WebGL での辞書読み込み

**症状**: WebGLビルドでカスタム辞書が読み込まれない。

**原因**: WebGLでは同期ファイルI/Oが使用できないため、`DotNetG2PPhonemizer.InitializeAsync()` で非同期読み込みが必要。

**解決策**: WebGL環境では `InitializeAsync` 内で自動的に非同期辞書読み込みが行われる。`InitializeAsync` の完了を必ず `await` で待機すること。

### 7.5 Android での StreamingAssets 読み込み

**症状**: Androidで辞書やTrigramプロファイルが読み込めない。

**原因**: Android APK 内の `StreamingAssets` は `jar:file://` プロトコルのため、通常のファイルI/Oでは読めない。

**解決策**: uPiperは `WebGLStreamingAssetsLoader` を Android でも使用し、`UnityWebRequest` 経由で読み込む。通常は自動処理される。手動でファイルを読む場合は:

```csharp
// NG: Androidでは動かない
var text = File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "uPiper/data.json"));

// OK: 全プラットフォーム対応
var text = await WebGLStreamingAssetsLoader.LoadTextAsync("uPiper/data.json");
```

---

## 8. デバッグの進め方

### 8.1 デバッグログの有効化

```csharp
var config = new PiperConfig
{
    EnableDebugLogging = true
};
```

これにより、以下の情報がUnityコンソールに出力される:

- `[BackendSelector]` バックエンド選択の詳細と理由
- `[PhonemeEncoder]` 音素エンコーディングの統計（IPA/PUA判定、マッピング数）
- `[CustomDictionary]` 辞書の読み込み・置換ログ
- `[IOSAudioSession]` iOS AudioSession の状態
- `[WebGLInteractionGate]` WebGL AudioContext の状態

### 8.2 バックエンド選択の確認

```csharp
// 初期化後にバックエンド選択ログを確認
// EnableDebugLogging = true の場合、以下のようなログが出力される:
//
// [BackendSelector] Selection Summary: Requested=Auto, Actual=GPUPixel,
//     Graphics=Direct3D11, ComputeShaders=True, VRAM=4096MB
// [BackendSelector] Auto-selection reason: Desktop with compute shaders and 4096MB VRAM
```

### 8.3 音素化結果の確認

```csharp
// PhonemizeAsync で音素化結果を個別に確認できる
var result = await piperTTS.PhonemizeAsync("こんにちは");
Debug.Log($"Phonemes: {string.Join(", ", result.Phonemes)}");
Debug.Log($"Detected language: {result.DetectedLanguage}");
Debug.Log($"Prosody length: {result.ProsodyFlat?.Length ?? 0}");
```

---

## 9. サポート

上記の方法で解決できない場合は、以下の情報を添えて [GitHub Issues](https://github.com/ayutaz/uPiper/issues) に報告してください:

1. uPiper のバージョン
2. Unity のバージョン
3. 対象プラットフォーム（Windows / macOS / iOS / Android / WebGL）
4. `EnableDebugLogging = true` で取得した Console ログ
5. 問題を再現するための最小コード
6. 使用しているモデル名