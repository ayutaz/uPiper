# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

MIT License - See [LICENSE](LICENSE) file for details

### 🔗 Links

- [GitHub Repository](https://github.com/ayutaz/uPiper)
- [Documentation](https://github.com/ayutaz/uPiper/tree/main/docs)
- [Issues](https://github.com/ayutaz/uPiper/issues)

[1.0.0]: https://github.com/ayutaz/uPiper/releases/tag/v1.0.0
[0.2.1]: https://github.com/ayutaz/uPiper/releases/tag/v0.2.1
[0.2.0]: https://github.com/ayutaz/uPiper/releases/tag/v0.2.0
[0.1.0]: https://github.com/ayutaz/uPiper/releases/tag/v0.1.0