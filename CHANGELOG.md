# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.3.0] - 2026/03/12

### ✨ Added

- **WebGLプラットフォーム対応**: WebGLビルドをサポート
  - StreamingAssets非同期ローダー（UnityWebRequest経由）
  - IndexedDBキャッシュによるリソース永続化
  - ローディング進捗UI
  - WebGPU検出と適切なInferenceBackend自動選択
  - GitHub Pagesデプロイワークフロー追加
  - 大容量ファイル自動分割（WebGLSplitDataProcessor）

### 🐛 Fixed

- PhonemizerErrorHandlingTestsのコンパイルエラー修正（UnityEngine.TestTools using追加）

## [1.2.0] - 2026/03/07

### ⚠️ Breaking Changes

#### ネイティブOpenJTalk完全削除 → dot-net-g2p（純C#）移行

日本語G2Pバックエンドを**ネイティブOpenJTalk**から**dot-net-g2p**（純粋C#実装、MeCab辞書）に完全移行しました。

- ネイティブプラグイン（`.dll` / `.so` / `.dylib` / `.a`）が不要に
- プラットフォーム固有のビルド手順・P/Invoke定義を完全削除
- IL2CPP互換レイヤー不要

#### sync API削除（デッドロックリスク解消）

以下の同期APIを削除しました。非同期APIを使用してください：

| 削除されたAPI | 代替API |
|--------------|---------|
| `IPiperTTS.GenerateAudio(string)` | `GenerateAudioAsync(string)` |
| `IPiperTTS.GenerateAudio(string, PiperVoiceConfig)` | `GenerateAudioAsync(string, PiperVoiceConfig)` |
| `IPhonemizer.Phonemize(string, string)` | `PhonemizeAsync(string, string)` |

### ✨ Added

- **dot-net-g2p統合**: 純粋C#による日本語G2P（MeCab辞書ベース）でネイティブOpenJTalkを置換
- **英語G2P**: Flite LTS純粋C#実装による英語音素化
- **開発環境向け辞書自動展開機能**

### 🐛 Fixed

- prosody_featuresテンソルの型をFloatからIntに修正

### 🔧 Changed

- ランタイムパフォーマンスの複数最適化
- Unity 6000.0.58f2へのアップグレード

### 🗑️ Removed

- ネイティブOpenJTalkライブラリ（全プラットフォーム）
- IL2CPP互換レイヤー
- 未使用のFliteネイティブバックエンド
- 同期API（デッドロックリスク）

## [1.1.0] - 2026/01/08

### ✨ Added

#### Prosody（韻律）サポート
- **OpenJTalk Prosody API**: ネイティブライブラリにA1/A2/A3パラメータ抽出機能を追加
  - A1: アクセント句内でのモーラ位置
  - A2: アクセント句内のアクセント核位置
  - A3: 呼気段落内でのアクセント句位置
- **C# Prosody API**: `OpenJTalkPhonemizer.PhonemizeWithProsody()` メソッドを追加
- **ONNX Prosody推論**: `InferenceAudioGenerator.GenerateAudioWithProsodyAsync()` を追加
- **Prosody対応モデル判定**: `SupportsProsody` プロパティでモデルのProsody対応を自動判定

#### カスタム辞書機能
- **CustomDictionary クラス**: 技術用語・固有名詞の読み変換機能
- **JSON辞書形式**: piper-plusのPython実装と互換性のある形式をサポート
- **辞書ファイル**: `StreamingAssets/uPiper/Dictionaries/` に配置
  - `default_tech_dict.json`: 技術用語辞書
  - `default_common_dict.json`: IT/ビジネス用語辞書
  - `additional_tech_dict.json`: AI/LLM関連用語辞書

#### 音素エンコーディング改善
- **IPA/PUAデュアルマッピング**: tsukuyomi-chanモデル等のIPAモデルに対応
- **自動モデル判定**: phoneme_id_mapからIPA/PUAモデルを自動判定

### 🐛 Fixed

- **strncmpバグ修正**: 's'音素がスキップされる問題を修正
- **大文字音素保持**: N, U, I, E, O, A の大文字音素が小文字に変換される問題を修正
- **N変異音素マッピング**: N_m, N_n, N_ng, N_uvularを正しくASCII "N" (ID 22)にマッピング
- **sh音素マッピング**: shをɕ (ID 18)ではなくʃ (ID 42)にマッピング（学習データと一致）
- **ch PUAマッピング**: 不正な 't i' → 'ch' 変換を削除

### 🔧 Changed

- **Copilotレビュー対応**:
  - 日本語文字判定のUnicode範囲を適切に定義 (`IsJapaneseChar`)
  - PUA範囲の名前付き定数化 (`BmpPuaStart`, `BmpPuaEnd`)
  - phoneme_len条件を `== 3` から `>= 3` に修正

### 📦 Infrastructure

- **CI互換性**: uLoopMCPをmanifest.jsonから削除（開発ツールのためCI不要）
- **モデル管理**: tsukuyomi-chanモデルをgit追跡から除外

### 📝 Documentation

- **CLAUDE.md**: Prosody機能とカスタム辞書の説明を追加
- **音素エンコーディングアーキテクチャ**: IPA/PUAマッピングの詳細ドキュメント

## [1.0.0] - 2025/10/14

### 🎉 First Stable Release

uPiper reaches version 1.0.0 with production-ready features and full platform support.

### ✨ Key Features

- **Production-Ready TTS Engine**: High-quality neural text-to-speech synthesis
- **Multi-Language Support**: Japanese (OpenJTalk) and English (Flite LTS) phonemization
- **Cross-Platform**: Full support for Windows, macOS, Linux, Android, and iOS
- **Unity Integration**: Seamless integration with Unity 6000.0.55f1+
- **GPU Acceleration**: Unity AI Inference Engine with GPU support
- **Easy Setup**: Streamlined installation via Unity Package Manager

### 📊 Platform Support Matrix

| Platform | Architecture | Status |
|----------|-------------|--------|
| Windows | x64 | ✅ Stable |
| macOS | Intel/Apple Silicon | ✅ Stable |
| Linux | x64 | ✅ Stable |
| Android | ARMv7/ARM64/x86/x86_64 | ✅ Stable |
| iOS | ARM64 (iOS 11.0+) | ✅ Stable |

### 🔧 Recent Improvements (v0.2.0 - v0.2.1)

- **Package Version Management**: Centralized version constant for easier maintenance
- **iOS Platform Support**: Complete implementation with AudioSession integration
- **English Phonemization**: Improved handling of complex suffixes
- **Documentation**: Comprehensive Japanese and English documentation

### 📦 Package Contents

- **Core Runtime**: Production-ready TTS engine
- **Native Libraries**: Platform-specific OpenJTalk wrappers
- **Voice Models**: Pre-trained ONNX models (127MB)
- **Dictionaries**: OpenJTalk (103MB) and CMU (3.5MB)
- **Sample Projects**: Complete demo implementations

### 🚀 What's Next

- Additional language support
- More voice models
- Performance optimizations
- Advanced audio processing features

## [0.2.1] - 2025/10/14

### 🔧 Changed

- **Package Version Management**: Introduced `PACKAGE_VERSION` constant in uPiperSetup.cs (#75)
  - Centralized version string to simplify maintenance
  - Replaced hardcoded version strings with version constant
  - Ensures sample path detection works correctly after version updates
  - Thanks to @dtaddis for the original contribution

## [0.2.0] - 2025/10/11

### ✨ Added

#### iOS Platform Support
- **iOS Native Library**: OpenJTalk static library for iOS (arm64, iOS 11.0+)
  - Built with CMake and iOS toolchain
  - P/Invoke with `__Internal` linking
  - File: `Assets/uPiper/Plugins/iOS/libopenjtalk_wrapper.a` (4.2MB)

- **iOS AudioSession Integration**: Native audio session management for iOS
  - `AudioSessionSetup.mm`: Objective-C plugin for AVAudioSession configuration
  - `IOSAudioSessionHelper.cs`: C# wrapper with P/Invoke
  - AVAudioSessionCategoryPlayback: Override silent switch
  - AVAudioSessionCategoryOptionMixWithOthers: Mix with other apps
  - Hardware volume control support

- **iOS Path Resolver**: iOS-specific StreamingAssets path resolution
  - iOS path: `Application.dataPath + "/Raw"`
  - Dictionary and model file access from iOS bundle

- **iOS Build Processor**: Automated Unity build configuration
  - Automatic Bundle Identifier setup (com.ayutaz.uPiper)
  - iOS minimum version: 11.0
  - Architecture: ARM64
  - API Compatibility: .NET Standard
  - BuildResult.Unknown proper handling for iOS

- **iOS Build Scripts**:
  - `build_ios.sh`: Main iOS build script
  - `build_dependencies_ios.sh`: iOS dependencies builder
  - `combine_ios_libs.sh`: Static library combiner

### 🔧 Changed

- **InferenceEngineDemo**: Integrated iOS AudioSession initialization
  - AudioSession.Initialize() in Start()
  - AudioSession.EnsureActive() before playback
  - Debug logging for AudioSession status

- **PiperBuildProcessor**: Added iOS build configuration
  - ConfigureIOSBuild() method
  - iOS-specific player settings

- **OpenJTalkDebugHelper**: Skip dynamic library check on iOS
  - iOS uses static linking, no dynamic library loading needed

### 📊 Performance

Tested on iPhone 7 (iOS 15.8.4):
- Model Load: 170ms
- OpenJTalk (Japanese): 66ms
- Synthesis (VITS): 195ms
- **Total**: 966ms
- Audio Output: 19,456 samples, 0.88s @ 22,050Hz

### ✅ Platform Support

uPiper now supports **5 platforms**:
- Windows (x64) ✅
- macOS (Intel/Apple Silicon) ✅
- Linux (x64) ✅
- Android (ARMv7/ARM64/x86/x86_64) ✅
- **iOS (ARM64, iOS 11.0+)** ✅ NEW

### 🧪 Testing

- ✅ Real device testing: iPhone 7 (iOS 15.8.4)
- ✅ Japanese TTS: Confirmed working (same quality as Android/Web)
- ✅ English TTS: Confirmed working (same quality as Android/Web)
- ✅ AudioSession: Silent switch override working
- ✅ Performance: Comparable to Android platform
- ✅ Memory: No leaks or crashes detected

## [0.1.0] - 2025-09-02

### 🎉 Initial Release

uPiper is a Unity plugin for high-quality text-to-speech synthesis using the piper-plus TTS engine.

### ✨ Features

- **Multi-language Support**: Japanese and English text-to-speech synthesis
- **High-quality Neural Voice Synthesis**: Based on piper-plus engine
- **Unity AI Inference Engine**: Fast inference with GPU support (GPUCompute/GPUPixel)
- **OpenJTalk Integration**: High-precision Japanese phonemization (Windows/macOS/Linux/Android)
- **CMU Pronouncing Dictionary**: English phonemization support
- **Multi-platform Support**: 
  - Windows (x64)
  - macOS (Intel/Apple Silicon)
  - Linux (x64)
  - Android (ARMv7/ARM64)

### 📦 Package Contents

- **Core Runtime**: TTS engine and phonemization systems
- **Editor Tools**: Package exporter and utilities
- **Samples**:
  - Basic TTS Demo: Simple demonstration of text-to-speech functionality
  - OpenJTalk Dictionary Data: NAIST Japanese Dictionary (103MB)
  - CMU Pronouncing Dictionary: English phonemization data (3.5MB)
  - Voice Models: Pre-trained ONNX models for Japanese and English (127MB)

### 🔧 Recent Improvements

- **#58**: Add missing macOS plugin to Samples directory
- **#57**: Remove EventSystemAutoSetup and clarify Input System dependency
- **#56**: Enhanced Android support with Input System/Manager compatibility
- **#55**: Complete removal of Input System dependency
- **#54**: Fix Japanese font display issue in Basic TTS Demo
- **#53**: Fix compatibility issues between development and Package Manager versions
- **#52**: Fix Package Manager installation issues and implement data distribution via Samples
- **#51**: Menu organization and development environment separation
- **#50**: Update to Unity 6000.0.55f1 and language support improvements

### 📋 Requirements

- Unity 6000.0.55f1 or later
- Unity AI Inference Engine 2.2.x

### 🏗️ Build Requirements

- **Windows**: Visual Studio 2022 or later
- **macOS**: Xcode 14 or later
- **Linux**: GCC 9 or later
- **Android**: NDK r21 or later

### 📝 License

Apache License 2.0 - See [LICENSE](LICENSE) file for details

> **Note**: ライセンスはv0.1.0のMIT LicenseからApache License 2.0に変更されました。
> これにより、特許権の明示的な付与と、より明確な貢献者ライセンス条項が提供されます。

### 🔗 Links

- [GitHub Repository](https://github.com/ayutaz/uPiper)
- [Documentation](https://github.com/ayutaz/uPiper/tree/main/docs)
- [Issues](https://github.com/ayutaz/uPiper/issues)

[1.2.0]: https://github.com/ayutaz/uPiper/releases/tag/v1.2.0
[1.1.0]: https://github.com/ayutaz/uPiper/releases/tag/v1.1.0
[1.0.0]: https://github.com/ayutaz/uPiper/releases/tag/v1.0.0
[0.2.1]: https://github.com/ayutaz/uPiper/releases/tag/v0.2.1
[0.2.0]: https://github.com/ayutaz/uPiper/releases/tag/v0.2.0
[0.1.0]: https://github.com/ayutaz/uPiper/releases/tag/v0.1.0