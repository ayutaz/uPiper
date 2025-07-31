# uPiper

English | [日本語](README.md)

[![Unity Tests](https://github.com/ayutaz/uPiper/actions/workflows/unity-tests.yml/badge.svg)](https://github.com/ayutaz/uPiper/actions/workflows/unity-tests.yml)
[![Unity Build](https://github.com/ayutaz/uPiper/actions/workflows/unity-build.yml/badge.svg)](https://github.com/ayutaz/uPiper/actions/workflows/unity-build.yml)
[![codecov](https://codecov.io/github/ayutaz/uPiper/graph/badge.svg?token=348eb741-4320-4368-89fa-3eee5188bd3f)](https://codecov.io/github/ayutaz/uPiper)

A Unity plugin for [piper-plus](https://github.com/ayutaz/piper-plus) - High-quality neural speech synthesis engine

## Table of Contents

- [Features](#features)
- [Requirements](#requirements)
- [Build Requirements](#build-requirements)
- [Installation](#installation)
  - [Via Unity Package Manager (Recommended)](#via-unity-package-manager-recommended)
  - [From Package Files](#from-package-files)
  - [Importing Samples](#importing-samples)
- [Supported Platforms](#supported-platforms)
- [Build and Package Creation](#build-and-package-creation)
- [GPU Inference](#gpu-inference)
- [Documentation](#documentation)
- [License](#license)

## Features

- 🎤 High-quality speech synthesis (piper-plus based)
- 🌍 Multi-language support (Japanese, English, Chinese, Korean, etc.)
- 🚀 Fast inference with Unity AI Inference Engine
- 📱 Multi-platform support
- 🔧 High-precision Japanese phonemization with OpenJTalk (Windows/macOS/Linux/Android)
- ⚡ GPU inference support (GPUCompute/GPUPixel)
- 🎭 Advanced samples (streaming, multi-voice, real-time)

## Requirements
* Unity 6000.0.35f1
* Unity AI Interface (Inference Engine) 2.2.x

## Build Requirements

- **Windows**: Visual Studio 2019 or later
- **macOS**: Xcode 14 or later
- **Linux**: GCC 9 or later
- **Android**: NDK r21 or later

## Installation

### Via Unity Package Manager (Recommended)
1. Open `Window > Package Manager` in Unity Editor
2. Click `+` button and select `Add package from git URL...`
3. Enter the following URL:
   ```
   https://github.com/ayutaz/uPiper.git?path=Assets/uPiper
   ```

### From Package Files
Download the latest package from [Releases](https://github.com/ayutaz/uPiper/releases):
- **Unity Package (.unitypackage)**: Legacy format, compatible with all Unity versions
- **UPM Package (.tgz)**: For Unity Package Manager, Unity 2019.3+

### Importing Samples
1. Select uPiper in Package Manager
2. Open the `Samples` tab
3. Available samples:
   - `Basic TTS Demo` - Basic speech generation
   - `Streaming TTS Demo` - Real-time streaming speech generation
   - `Multi-Voice Demo` - Simultaneous multi-voice processing
   - `Realtime TTS Demo` - Low-latency speech generation
4. Click `Import` for the sample you want

## Supported Platforms

### Currently Supported
- ✅ Windows (x64)
- ✅ macOS (Apple Silicon/Intel)
- ✅ Linux (x64)
- ✅ Android (ARM64)

### Not Supported
- ❌ WebGL - Under investigation (future support planned via piper-plus integration)
- ❌ iOS - Planned for Phase 5

## Build and Package Creation

### Automated Build (GitHub Actions)
- All platform builds are automatically executed when pushing to the main branch
- Creating a release tag (v*) will automatically create releases and packages

### Package Export (For Developers)
Create packages manually from Unity Editor:
1. `uPiper/Package/Export Unity Package (.unitypackage)` - Legacy format
2. `uPiper/Package/Export UPM Package (.tgz)` - Unity Package Manager format
3. `uPiper/Package/Export Both Formats` - Export both formats simultaneously

## GPU Inference

uPiper supports GPU inference for faster speech generation:

```csharp
var config = new PiperConfig
{
    Backend = InferenceBackend.Auto,  // Auto selection
    AllowFallbackToCPU = true,        // CPU fallback on GPU failure
    GPUSettings = new GPUInferenceSettings
    {
        MaxBatchSize = 4,
        UseFloat16 = true,
        MaxMemoryMB = 512
    }
};
```

See the [GPU Inference Guide](docs/en/guides/technical/gpu-inference.md) for details.

## Documentation

- [Architecture](docs/en/ARCHITECTURE.md) - Design and technical details
- [Development Log](docs/DEVELOPMENT_LOG.md) - Development progress and change history
- [Technical Documentation](docs/en/guides/technical/) - Detailed technical information

## License

This project is licensed under the Apache License 2.0. See the [LICENSE](LICENSE) file for details.

### Third-party Licenses

#### Fonts
- **Noto Sans Japanese**: SIL Open Font License, Version 1.1
  - Copyright 2014-2021 Adobe (http://www.adobe.com/)
  - Used for Japanese display in TextMeshPro
  - See `Assets/Fonts/LICENSE.txt` for details