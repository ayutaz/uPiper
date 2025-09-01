# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

[0.1.0]: https://github.com/ayutaz/uPiper/releases/tag/v0.1.0