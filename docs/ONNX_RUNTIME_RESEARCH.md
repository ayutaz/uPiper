# Unity ONNX推論ライブラリ調査レポート

**作成日**: 2026-01-06
**更新日**: 2026-01-06
**目的**: Unity Sentis (AI Interface) の精度問題を解決するための代替ONNXライブラリの調査

## 目次

1. [調査背景](#1-調査背景)
2. [Unity Sentis の制約と問題点](#2-unity-sentis-の制約と問題点)
3. [代替ONNXライブラリ比較](#3-代替onnxライブラリ比較)
4. [比較まとめ](#4-比較まとめ)
5. [推奨事項](#5-推奨事項)
6. [ONNXオペレーター（opset）バージョンと精度の関係](#6-onnxオペレーターopsetバージョンと精度の関係)
7. [asus4/onnxruntime-unity 課題調査（詳細）](#7-asus4onnxruntime-unity-課題調査詳細)
8. [ONNX形式 vs ORT形式 比較](#8-onnx形式-vs-ort形式-比較)
9. [参考リンク集](#9-参考リンク集)

---

## 1. 調査背景

### 1.1 現在の問題

uPiperプロジェクトでは Unity Sentis (AI Interface) を使用してONNX推論を行っているが、Python版のONNX Runtime推論と比較して精度の問題が発生している。

| 項目 | 詳細 |
|------|------|
| **症状** | イントネーション/アクセント異常、音素/発音の違い |
| **原因の切り分け** | 同一入力（phoneme_ids, prosody_a1/a2/a3）でも出力が異なる |
| **結論** | 前処理ではなく、**ONNX推論エンジン自体の精度問題** |
| **問題発生モデル** | tsukuyomi-chan (Prosody対応モデル) |
| **対象プラットフォーム** | 全プラットフォーム（Windows/macOS/Linux/iOS/Android） |

### 1.2 現在の実装状況

**ファイル**: `Assets/uPiper/Runtime/Core/AudioGeneration/InferenceAudioGenerator.cs`

| 項目 | 設定 |
|------|------|
| **使用ライブラリ** | Unity.InferenceEngine (Sentis) |
| **テンソル精度** | Float32固定 |
| **バックエンド（Windows/Linux）** | GPUPixel（推奨） |
| **バックエンド（macOS）** | CPU限定（Metal非対応） |
| **既知問題** | GPUComputeはVITSモデルで音声破損 |

---

## 2. Unity Sentis の制約と問題点

### 2.1 技術的制約

| 制約 | 詳細 | 影響 |
|------|------|------|
| **独自実装** | ONNXフォーマットは使用するが、推論エンジンはUnity独自実装 | ONNX Runtimeと異なる推論結果が出る可能性 |
| **演算子制限** | opset 7-15のみ対応 | 新しいONNX演算子が使えない |
| **カスタムレイヤー** | 新シリアライズ方式との非互換で定義不可 | モデル拡張が困難 |
| **フォールバック** | GPU非対応演算でCPUへ自動フォールバック | パフォーマンス低下 |

### 2.2 既知の問題

1. **GPU Compute問題**: VITSモデルで音声が無音または破損（uPiperで確認済み）
2. **Metal（macOS）問題**: ShaderCompilation問題でGPU使用不可
3. **モデル互換性**: ONNX Runtimeで動作するモデルがSentisでエラーになるケースあり

### 2.3 参考リンク

- [Unity Sentis 2.1.3 公式ドキュメント](https://docs.unity3d.com/Packages/com.unity.sentis@2.1/manual/index.html)
- [Unity Forum: Sentis vs ONNX Runtime](https://discussions.unity.com/t/sentis-vs-onnxruntime/299328)
- [Unity Forum: Inference error with Sentis](https://discussions.unity.com/t/inference-error-when-loading-fireredasrs-onnx-model-using-sentis-but-normal-inference-when-using-onnxruntime/1689703)

---

## 3. 代替ONNXライブラリ比較

### 3.1 asus4/onnxruntime-unity（推奨）

**リポジトリ**: https://github.com/asus4/onnxruntime-unity

| 項目 | 詳細 |
|------|------|
| **ベース** | 公式 Microsoft ONNX Runtime |
| **最新バージョン** | ONNX Runtime 1.23.2 |
| **パッケージバージョン** | 0.4.4 |
| **Unity対応** | Unity 6000.0.43f1 (LTS) 以上 |
| **インストール方法** | UPM (npmスコープドレジストリ) |
| **ライセンス** | MIT |
| **追加機能** | ONNX Runtime Extensions 0.14.0, GenAI 0.11.4 |

#### Execution Provider対応

| Platform | CPU | CoreML | NNAPI | CUDA | TensorRT | DirectML | XNNPACK |
|----------|:---:|:------:|:-----:|:----:|:--------:|:--------:|:-------:|
| macOS | O | O | - | - | - | - | - |
| iOS | O | O | - | - | - | - | preview |
| Android | O | - | O | - | - | - | preview |
| Windows | O | - | - | preview | preview | O | - |
| Linux | O | - | - | preview | preview | - | - |

*O = サポート、preview = 試験的プレビュー*

#### パフォーマンス実績

- **MobileOne画像分類**: 100fps以上 (iPhone 13 Pro, M1 Mac)
- **YOLOX物体検出**: 60fps以上 (同上)

#### メリット

1. **精度**: 公式ONNX Runtime基盤でPython版と同等の推論精度が期待できる
2. **macOS GPU対応**: CoreMLでGPU推論可能（Sentisでは不可）
3. **全プラットフォーム対応**: iOS/Android含む主要プラットフォーム対応
4. **統合の容易さ**: UPMパッケージで簡単インストール
5. **実績**: 100fps以上のパフォーマンスが確認されている

#### デメリット

1. **モバイル最適化**: モバイルではORT形式への変換を推奨
2. **iOS問題**: 一部バージョンでDLLロード問題の報告あり（最新版で解決の可能性）
3. **パッケージサイズ**: ネイティブライブラリを含むため比較的大きい

#### インストール方法

```json
// Packages/manifest.json
{
  "scopedRegistries": [
    {
      "name": "NPM",
      "url": "https://registry.npmjs.com",
      "scopes": ["com.github.asus4"]
    }
  ],
  "dependencies": {
    "com.github.asus4.onnxruntime": "0.4.4",
    "com.github.asus4.onnxruntime.unity": "0.4.4"
  }
}
```

#### オプションパッケージ

| パッケージ | 用途 |
|-----------|------|
| `com.github.asus4.onnxruntime.win-x64-gpu` | Windows GPU (CUDA/TensorRT) |
| `com.github.asus4.onnxruntime.linux-x64-gpu` | Linux GPU (CUDA/TensorRT) |
| `com.github.asus4.onnxruntime-extensions` | 前処理/後処理拡張 |

#### piper-plus ONNX との互換性

**結論: 完全に互換性あり。piper-plus出力ONNXをそのまま使用可能。**

| 項目 | 値 | ソース |
|------|-----|--------|
| **piper-plus ONNXエクスポート** | opset 15 | `export_onnx.py` 13行目: `OPSET_VERSION = 15` |
| **asus4/onnxruntime-unity** | ONNX Runtime 1.23.2 | パッケージドキュメント |
| **ONNX Runtime 1.23.2対応opset** | 7〜21+ | 公式互換性ドキュメント |

ONNX Runtimeはスライディングウィンドウ方式で後方互換性を維持しており、opset 7以上の全モデルを実行可能。piper-plus（opset 15）は余裕を持って対応範囲内。

**VITSモデルの演算子**:
- 標準的なPyTorch演算子のみ使用（Conv1d, Linear, LSTM等）
- 特殊なカスタム演算子は不要
- onnxsimによる簡略化後も互換性維持

**参考**:
- [ONNX Runtime Compatibility](https://onnxruntime.ai/docs/reference/compatibility.html)
- [piper-plus export_onnx.py](https://github.com/ayutaz/piper-plus/blob/main/src/python/piper_train/export_onnx.py)

---

### 3.2 Microsoft.ML.OnnxRuntime (NuGet直接)

**NuGet**: https://www.nuget.org/packages/Microsoft.ML.OnnxRuntime

| 項目 | 詳細 |
|------|------|
| **最新バージョン** | 1.23.2 |
| **対応Platform** | Windows, Linux, macOS, Android, iOS |

#### 問題点

| プラットフォーム | 問題 |
|-----------------|------|
| **iOS** | DLLロード問題、コードサイン問題 |
| **Android (IL2CPP)** | P/Invokeマーシャリングエラー |
| **Unity統合** | 手動設定が多く複雑 |

#### 参考

- [GitHub Issue #10427: IL2CPP問題](https://github.com/microsoft/onnxruntime/issues/10427)
- [GitHub Issue #14914: iOS DLLロード問題](https://github.com/microsoft/onnxruntime/issues/14914)

---

### 3.3 TensorFlow Lite

| 項目 | 詳細 |
|------|------|
| **モデル形式** | TFLite (.tflite) |
| **変換** | ONNXからTFLiteへの変換が必要 |

#### 問題点

- ONNXモデルの変換が必要（互換性問題のリスク）
- Piperモデル（VITS）との相性が未検証
- 変換時の精度劣化の可能性

---

### 3.4 PyTorch Mobile

| 項目 | 詳細 |
|------|------|
| **特徴** | 動的グラフ対応 |
| **変換** | ONNXからの変換が必要 |

#### 問題点

- ONNXからの変換が必要
- Unityでの実績が少ない
- 統合の手間が大きい

---

### 3.5 torinos-yt/NNOnnx

**リポジトリ**: https://github.com/torinos-yt/NNOnnx

| 項目 | 詳細 |
|------|------|
| **特徴** | CUDA Graphics Interoperabilityで直接GPU推論 |
| **ベース** | ONNX Runtime + CUDA API |
| **対応Platform** | **Windows x64 (D3D11) のみ** |
| **GPU** | NVIDIA GPU必須 |
| **依存** | CUDA 11.x, cuDNN, TensorRT（オプション） |

#### メリット

- GraphicsBuffer/TextureをCPUコピーなしで直接CUDA利用
- 高解像度画像入力モデルに有効
- Barracuda/Sentisより高速（CUDA環境下）

#### デメリット

- **Windows限定**（クロスプラットフォーム非対応）
- NVIDIA GPU必須
- onnxruntimeの完全ラッパーではない（限定的な機能）

#### インストール方法

```json
// Packages/manifest.json
{
  "scopedRegistries": [{
    "name": "torinos",
    "url": "https://registry.npmjs.com",
    "scopes": ["jp.torinos"]
  }],
  "dependencies": {
    "jp.torinos.nnonnx": "バージョン"
  }
}
```

---

### 3.6 NatML

**リポジトリ**: https://github.com/natmlx/natml-unity
**ドキュメント**: https://docs.natml.ai/unity

| 項目 | 詳細 |
|------|------|
| **特徴** | ハードウェアMLアクセラレータを自動活用 |
| **対応形式** | CoreML (.mlmodel), TFLite (.tflite), ONNX (.onnx) |
| **GPU加速** | CoreML(iOS/macOS), NNAPI(Android), DirectML(Windows) |

#### プラットフォーム制限

| Platform | CoreML | TFLite | ONNX |
|----------|:------:|:------:|:----:|
| iOS/macOS | O | x | x |
| Android | x | O | x |
| Windows | x | x | O |

#### メリット

- ハードウェアアクセラレータ自動活用でBarracudaより高速
- AR最適化（GPU負荷軽減）
- NatML Hubでモデル形式変換可能

#### デメリット

- **ONNX使用はWindows限定**
- プラットフォームごとにモデル形式変換が必要
- クローズドソース要素あり

---

### 3.7 DeNA/onnxruntime-builder

**リポジトリ**: https://github.com/DeNA/onnxruntime-builder

| 項目 | 詳細 |
|------|------|
| **目的** | ミニマルONNX Runtimeバイナリのビルドツール |
| **派生元** | VOICEVOX/onnxruntime-builder |
| **最新リリース** | v1.20.1 (2025年5月) |
| **注意** | ONNX Runtime 1.21以上は未対応（重大バグのため） |

#### 対応プラットフォーム

| Platform | アーキテクチャ | 形式 |
|----------|--------------|------|
| Android | armeabi-v7a, arm64-v8a, x86_64 | .so, .aar |
| iOS | arm64, x86_64 | xcframework |
| Linux | aarch64, x64 | .so |
| macOS | universal2, arm64, x86_64 | .dylib |
| Windows | x86, x64 | .dll |

#### 用途

- カスタムONNX Runtimeバイナリ生成
- ミニマルビルドでアプリサイズ削減
- GitHub Actionsによる自動ビルド
- **Unity直接対応ではない**（バイナリ生成のみ）

---

### 3.8 VOICEVOX/onnxruntime-builder

**リポジトリ**: https://github.com/VOICEVOX/onnxruntime-builder

| 項目 | 詳細 |
|------|------|
| **目的** | VOICEVOX CORE用ONNX Runtimeビルド |
| **特徴** | TTS向け最適化ビルド |

#### 用途

- 音声合成（TTS）向けONNX Runtimeバイナリ
- DeNA版の派生元
- **uPiperと同様のTTSユースケース向け**

---

### 3.9 keijiroのプロジェクト群

keijiro氏（Unityの中の人）による各種MLプロジェクト:

| プロジェクト | 用途 | ベース |
|-------------|------|--------|
| [TFClassify-Unity-Barracuda](https://github.com/keijiro/TFClassify-Unity-Barracuda) | 画像分類/物体検出 | Barracuda |
| [YoloV4TinyBarracuda](https://github.com/keijiro/YoloV4TinyBarracuda) | YOLOv4-tiny物体検出 | Barracuda |
| [MnistBarracuda](https://github.com/keijiro/MnistBarracuda) | MNIST手書き文字認識 | Barracuda |

**注意**: これらはBarracudaベースで古い。Sentis移行推奨。

---

### 3.10 Piper Unity実装

Unity向けPiper TTS実装が複数存在:

| プロジェクト | 推論エンジン | 特徴 |
|-------------|-------------|------|
| [skykim/piper-unity](https://github.com/skykim/piper-unity) | Unity Inference Engine | Windows/macOS/Android対応 |
| [Macoron/piper.unity](https://github.com/Macoron/piper.unity) | Unity Sentis | CPUでも20-30ms |

**uPiperとの関連**: 同じPiper TTSモデルを使用。推論精度問題の参考になる可能性あり。

---

## 4. 比較まとめ

### 4.1 全ライブラリ比較

| ライブラリ | 精度 | Platform対応 | 導入容易さ | モバイルGPU | 推奨度 |
|-----------|:----:|:-----------:|:---------:|:----------:|:------:|
| **asus4/onnxruntime-unity** | A | A | A | A | **★★★** |
| NNOnnx | A | D | B | x | ★★☆ |
| NatML | A | C | B | A | ★★☆ |
| Microsoft.ML.OnnxRuntime | A | C | D | C | ★★☆ |
| DeNA/onnxruntime-builder | A | A | D | - | ★☆☆ |
| Unity Sentis (現状) | C | B | A | C | ★★☆ |
| TensorFlow Lite | C | B | C | B | ★☆☆ |
| PyTorch Mobile | C | B | D | B | ★☆☆ |

評価: A=優秀, B=良好, C=問題あり, D=非推奨/困難, x=非対応

### 4.2 ユースケース別推奨

| ユースケース | 推奨ライブラリ | 理由 |
|-------------|--------------|------|
| **全Platform対応（本命）** | asus4/onnxruntime-unity | 唯一の全Platform GPU対応 |
| **Windows CUDA特化** | NNOnnx | 最高速（GPU直接利用） |
| **カスタムバイナリ** | DeNA/onnxruntime-builder | サイズ最適化可能 |
| **モデル変換許容** | NatML | HWアクセラレータ最適化 |

---

## 5. 推奨事項

### 5.1 推奨ライブラリ

**asus4/onnxruntime-unity** を推奨

### 5.2 理由

1. **精度**: 公式ONNX Runtime基盤でPython版と同等の精度が期待できる
2. **実績**: Unity環境で100fps以上のパフォーマンスが確認されている
3. **macOS対応**: CoreMLでGPU推論可能（Sentisの弱点を補完）
4. **導入容易**: UPMパッケージで簡単にインストール可能
5. **メンテナンス**: 活発に更新されている（2025年時点でONNX Runtime 1.23.2対応）

### 5.3 推奨アプローチ

段階的に検証を行い、問題がなければ移行する：

1. **Phase 1**: パッケージ導入、基本動作確認
2. **Phase 2**: 実験用推論クラス作成（既存インターフェース互換）
3. **Phase 3**: 精度検証（同一入力での出力比較）
4. **Phase 4**: パフォーマンス検証
5. **Phase 5**: 検証結果に基づき移行判断

---

## 6. ONNXオペレーター（opset）バージョンと精度の関係

### 6.1 現状のopsetバージョン

| 項目 | バージョン | ソース |
|------|-----------|--------|
| **piper-plus ONNXエクスポート** | opset 15 | `export_onnx.py` |
| **PyTorch legacy export最大** | opset 18 | PyTorch公式ドキュメント |
| **Unity Sentis対応範囲** | opset 7-15 | Unity公式ドキュメント |
| **ONNX Runtime 1.20対応** | opset 7-21 | ONNX Runtime公式 |
| **ONNX Runtime 1.23対応** | opset 7-21+ (推定) | - |

### 6.2 opsetバージョンを上げても精度は改善しない

**結論: opsetバージョンの引き上げは精度問題の解決策にならない**

opsetバージョンは「どの演算子が使用可能か」を定義するもので、**数値精度とは直接関係がない**。

| opsetバージョン | 意味 |
|----------------|------|
| 高い | 新しい演算子が使える（例: opset 11でSequence型追加） |
| 低い | 古い演算子のみ使用可能 |

同じopsetバージョンでも、推論エンジンの実装が異なれば結果も異なる。

### 6.3 精度に影響する要因（影響度順）

| 要因 | 影響度 | 説明 |
|------|:------:|------|
| **推論エンジン実装** | 高 | Unity Sentis（独自実装） vs ONNX Runtime（Microsoft公式） |
| **Execution Provider** | 中 | CPU vs GPU, CoreML vs DirectML vs CUDA |
| **データ型精度** | 中 | fp32 vs fp16 vs int8 |
| **opsetバージョン** | 低 | ほぼ影響なし |

### 6.4 精度改善のための推奨アクション

**opsetを上げるのではなく、推論エンジンを変更する**

| アプローチ | 効果 | 推奨度 |
|-----------|------|:------:|
| Unity Sentis → ONNX Runtime移行 | 高（根本解決） | ★★★ |
| fp16 → fp32に変更 | 中 | ★★☆ |
| GPU → CPUに変更 | 低（パフォーマンス低下） | ★☆☆ |
| opset 15 → 18に変更 | ほぼなし | ☆☆☆ |

### 6.5 opset 18への引き上げについて

PyTorch legacy exportがサポートする最大はopset 18。試す価値はあるが、精度問題の根本解決にはならない：

```python
# piper-plus/src/python/piper_train/export_onnx.py
OPSET_VERSION = 18  # 15 → 18 に変更可能
```

**注意**: opset 18に上げるとUnity Sentis（opset 7-15のみ対応）では動作しなくなる。ONNX Runtime移行が前提となる。

### 6.6 参考リンク

- [PyTorch ONNX Export Documentation](https://pytorch.org/docs/stable/onnx.html)
- [ONNX Runtime Compatibility](https://onnxruntime.ai/docs/reference/compatibility.html)
- [PyTorch opset support issue #114801](https://github.com/pytorch/pytorch/issues/114801)

---

## 7. asus4/onnxruntime-unity 課題調査（詳細）

**調査日**: 2026-01-06
**リポジトリ**: https://github.com/asus4/onnxruntime-unity

### 7.1 オープンイシュー（3件）

| Issue | タイトル | 重要度 | 状況 |
|-------|---------|:------:|------|
| [#70](https://github.com/asus4/onnxruntime-unity/issues/70) | [Mobile] Support 16 KB page sizes | 高 | Android 15+必須要件。v1.23.2で対応済みだがExtensionsは未対応 |
| [#69](https://github.com/asus4/onnxruntime-unity/issues/69) | win-x64-gpu package not found | 中 | **GPUパッケージはNPMにアップロードされていない**。Releasesから手動DL必要 |
| [#56](https://github.com/asus4/onnxruntime-unity/issues/56) | Migrate to NuGetForUnity | 低 | パッケージ管理改善（enhancement） |

### 7.2 重要なクローズ済みイシュー

#### iOS関連

| Issue | 問題 | 解決状況 |
|-------|------|:--------:|
| #4 | Framework not found when building for iOS | ✅ 解決 |
| #64 | Copying onnxruntime-extensions to Xcode Fails | ✅ 解決 |
| #57 | Remove custom build post process for iOS XCFramework | ✅ 解決 |
| #49 | _RegisterCustomOps not linked on iOS without extensions | ✅ 解決 |
| **#31** | **iOS CoreML メモリスパイク問題** | ❌ **wontfix（未解決）** |

#### Android関連

| Issue | 問題 | 解決状況 |
|-------|------|:--------:|
| #25 | onnxruntime-extensions android aar missing in npmjs.org | ✅ 解決 |

#### その他

| Issue | 問題 | 解決状況 |
|-------|------|:--------:|
| #28 | Exception 0xc0000005 when testing on mini PC | ❌ wontfix |

### 7.3 uPiperへの影響評価

| 課題 | uPiperへの影響 | 対策 |
|------|:-------------:|------|
| **iOS CoreMLメモリスパイク (#31)** | 高 | CPUフォールバックまたは独自修正が必要 |
| Android 16KB page size (#70) | 中 | v1.23.2で解決済み、Extensionsは使用しないので問題なし |
| Windows GPU手動DL (#69) | 低 | Releasesから手動ダウンロードで対応可能 |
| モバイルORT形式推奨 | 中 | ONNX形式でも動作するが、パフォーマンス最適化時に検討 |

### 7.4 フォーク一覧（19件）

| フォーク | 特記事項 |
|---------|---------|
| **ayutaz** | ユーザー自身のフォーク（uPiper用に使用可能） |
| その他18件 | 特筆すべき機能追加は確認できず |

### 7.5 PRステータス

- **オープンPR**: 0件
- **クローズPR**: 48件
- **現状**: メンテナンスフェーズ（活発な機能開発は落ち着いている）

### 7.6 技術的制限まとめ

| 項目 | 制限内容 |
|------|---------|
| **Unity最小バージョン** | 6000.0.43f1 (LTS) |
| **iOS CoreML** | メモリスパイク問題あり（wontfix） |
| **Windows GPU** | NPMからインストール不可、手動DL必要 |
| **Extensions** | 全プラットフォームで実験的（preview） |
| **モバイル推奨形式** | ORT形式（ONNXも動作するが非推奨） |
| **CUDA/TensorRT** | Windows/Linuxでpreview |

### 7.7 Fork検討が必要なケース

以下の場合、独自Forkでの対応を検討：

1. **iOS CoreMLメモリ問題の修正**
   - 本家ではwontfixのため、独自に調査・修正が必要
   - CPUのみ使用で回避も可能

2. **特定プラットフォーム向け最適化**
   - モバイル向けカスタムビルド
   - 特定のExecution Provider優先

3. **パッケージサイズ削減**
   - 不要なプラットフォームのバイナリ除外
   - ミニマルビルド

### 7.8 推奨アクション

| 優先度 | アクション |
|:------:|-----------|
| 1 | まずは本家パッケージで検証 |
| 2 | iOS CoreMLテストで問題発生時、CPU使用で回避 |
| 3 | 問題解決不可の場合、ayutazフォークで独自修正 |
| 4 | 必要に応じてPRを本家に送る |

---

## 8. ONNX形式 vs ORT形式 比較

**調査日**: 2026-01-06

### 8.1 ORT形式とは

ORT (ONNX Runtime) 形式は、ONNX Runtimeの内部モデル形式。**サイズ制約のある環境（モバイル、Web）向けに最適化**されたフォーマット。

### 8.2 比較表

| 項目 | ONNX形式 | ORT形式 |
|------|---------|---------|
| **ファイル拡張子** | `.onnx` | `.ort` |
| **主な用途** | 汎用、相互運用性 | モバイル/Web向け最適化 |
| **バイナリサイズ** | 標準 | 削減可能（ミニマルビルド対応） |
| **変換** | 不要（そのまま使用） | 変換スクリプト必要 |
| **ランタイム最適化** | フル対応 | 制限あり（v1.10以前は非対応） |
| **後方互換性** | 高い | バージョン依存あり |
| **デバッグ** | 容易（可読性高い） | 困難 |
| **逆変換** | - | ORT→ONNX変換は困難 |

### 8.3 ORT形式のメリット

| メリット | 詳細 |
|---------|------|
| **バイナリサイズ削減** | ミニマルビルドと組み合わせでアプリサイズ大幅削減 |
| **事前最適化** | 変換時に最適化が適用される |
| **メモリ効率** | `use_ort_model_bytes_directly`オプションでメモリ使用量削減 |
| **オペレーター絞り込み** | 使用するデータ型のみに限定可能（v1.7+） |

### 8.4 ORT形式のデメリット

| デメリット | 詳細 |
|-----------|------|
| **ランタイム最適化制限** | グラフ最適化コードが除外される場合あり |
| **バージョン互換性** | ORTバージョン間で互換性問題の可能性 |
| **変換エラー** | 複雑なモデルで変換失敗のケースあり |
| **WebGPU問題** | ORT形式でWASM CPUフォールバックが発生する報告あり |
| **逆変換不可** | ORT→ONNXへの変換は困難（相互運用性低下） |
| **追加ワークフロー** | モデル更新のたびに変換が必要 |

### 8.5 uPiperにおける評価

#### uPiperの特性

| 項目 | 値 |
|------|-----|
| モデルサイズ | 20-60MB（VITS/Piper標準） |
| 対象プラットフォーム | Windows, macOS, Linux, iOS, Android |
| 主要課題 | **推論精度**（サイズではない） |
| モデル更新頻度 | 低（一度配布したら変更少ない） |

#### uPiperでのORT形式メリット

| メリット | uPiperへの影響 | 重要度 |
|---------|:-------------:|:------:|
| バイナリサイズ削減 | モバイルアプリサイズ削減に有効 | 中 |
| メモリ効率 | モバイルでの安定性向上の可能性 | 中 |
| 事前最適化 | 初期化時間短縮の可能性 | 低 |

#### uPiperでのORT形式デメリット

| デメリット | uPiperへの影響 | 重要度 |
|-----------|:-------------:|:------:|
| **追加変換ステップ** | piper-plus出力後に変換が必要 | 高 |
| **精度への影響なし** | 推論精度問題の解決にはならない | 高 |
| **デバッグ困難** | 問題発生時の調査が困難 | 中 |
| **バージョン依存** | ONNX Runtime更新時に再変換が必要な可能性 | 中 |
| **逆変換不可** | ONNXに戻せない（フローが一方向） | 低 |

### 8.6 uPiper向け推奨

#### 結論: **最初はONNX形式のまま使用を推奨**

| 理由 | 説明 |
|------|------|
| 1 | 推論精度問題はORT変換では解決しない（エンジン変更で解決） |
| 2 | 追加の変換ステップが不要でシンプル |
| 3 | piper-plus出力をそのまま使用可能 |
| 4 | デバッグ・問題切り分けが容易 |
| 5 | フルONNX Runtimeビルドは両形式をサポート |

#### ORT形式を検討するケース

以下の場合のみORT形式への変換を検討：

1. **モバイルアプリサイズが問題になった場合**
   - iOS/Androidでのアプリサイズ制限に引っかかる場合

2. **モバイルでのメモリ問題が発生した場合**
   - 特にiOS CoreML問題と合わせて検討

3. **パフォーマンステストで明確な改善が見られた場合**
   - NNAPI/CoreML EPでの性能比較後

### 8.7 ORT変換方法（参考）

必要になった場合の変換コマンド：

```bash
# ONNX Runtimeインストール
pip install onnxruntime

# 変換実行
python -m onnxruntime.tools.convert_onnx_models_to_ort <model_path>

# モバイルEP向け推奨オプション
python -m onnxruntime.tools.convert_onnx_models_to_ort \
  --optimization_style Runtime \
  --optimization_level basic \
  <model_path>
```

### 8.8 参考リンク

- [ORT model format | onnxruntime](https://onnxruntime.ai/docs/performance/model-optimizations/ort-format-models.html)
- [ORT format runtime optimization](https://onnxruntime.ai/docs/performance/model-optimizations/ort-format-model-runtime-optimization.html)
- [WebGPU performance issue #24475](https://github.com/microsoft/onnxruntime/issues/24475)
- [sherpa-onnx TTS](https://k2-fsa.github.io/sherpa/onnx/tts/index.html)

---

## 9. 参考リンク集

### 公式ドキュメント

- [Unity Sentis 2.1.3](https://docs.unity3d.com/Packages/com.unity.sentis@2.1/manual/index.html)
- [ONNX Runtime](https://onnxruntime.ai/)
- [ONNX Runtime GitHub](https://github.com/microsoft/onnxruntime)
- [NatML Documentation](https://docs.natml.ai/unity)

### Unity用ライブラリ（推奨順）

1. [asus4/onnxruntime-unity](https://github.com/asus4/onnxruntime-unity) - **推奨**
2. [asus4/onnxruntime-unity-examples](https://github.com/asus4/onnxruntime-unity-examples)
3. [torinos-yt/NNOnnx](https://github.com/torinos-yt/NNOnnx) - CUDA特化
4. [natmlx/natml-unity](https://github.com/natmlx/natml-unity) - HWアクセラレータ活用
5. [cj-mills/onnx-directml-unity-tutorial](https://github.com/cj-mills/onnx-directml-unity-tutorial)

### ビルドツール

- [DeNA/onnxruntime-builder](https://github.com/DeNA/onnxruntime-builder) - ミニマルビルド
- [VOICEVOX/onnxruntime-builder](https://github.com/VOICEVOX/onnxruntime-builder) - TTS向け

### Piper TTS関連

- [skykim/piper-unity](https://github.com/skykim/piper-unity)
- [Macoron/piper.unity](https://github.com/Macoron/piper.unity)

### keijiroプロジェクト（Barracudaベース・参考）

- [keijiro/TFClassify-Unity-Barracuda](https://github.com/keijiro/TFClassify-Unity-Barracuda)
- [keijiro/YoloV4TinyBarracuda](https://github.com/keijiro/YoloV4TinyBarracuda)

### 記事・議論

- [ONNX Runtime on Unity (Medium)](https://medium.com/@asus4/onnx-runtime-on-unity-a40b3416529f)
- [Unity Forum: Sentis vs ONNX Runtime](https://discussions.unity.com/t/sentis-vs-onnxruntime/299328)
- [ONNX Runtime Float16/Mixed Precision](https://onnxruntime.ai/docs/performance/model-optimizations/float16.html)
- [Unity Forum: piper.unity TTS](https://discussions.unity.com/t/piper-unity-open-fast-and-high-quality-tts/337243)

### asus4/onnxruntime-unity 関連

- [GitHub Issues](https://github.com/asus4/onnxruntime-unity/issues)
- [GitHub Releases（GPU版手動DL）](https://github.com/asus4/onnxruntime-unity/releases)
- [iOS CoreML Memory Issue #31](https://github.com/asus4/onnxruntime-unity/issues/31)
- [Discussion #14913: Can't load DLL 'onnxruntime' on iOS](https://github.com/microsoft/onnxruntime/discussions/14913)

### ORT形式関連

- [ORT model format | onnxruntime](https://onnxruntime.ai/docs/performance/model-optimizations/ort-format-models.html)
- [ORT format runtime optimization](https://onnxruntime.ai/docs/performance/model-optimizations/ort-format-model-runtime-optimization.html)
- [WebGPU performance issue #24475](https://github.com/microsoft/onnxruntime/issues/24475)
- [sherpa-onnx TTS](https://k2-fsa.github.io/sherpa/onnx/tts/index.html)
