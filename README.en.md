# uPiper

English | [日本語](README.md)

[![Unity Tests](https://github.com/ayutaz/uPiper/actions/workflows/unity-tests.yml/badge.svg)](https://github.com/ayutaz/uPiper/actions/workflows/unity-tests.yml)
[![Unity Build](https://github.com/ayutaz/uPiper/actions/workflows/unity-build.yml/badge.svg)](https://github.com/ayutaz/uPiper/actions/workflows/unity-build.yml)

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
- 🌍 Multi-language support (Japanese, English)
- 🚀 Fast inference with Unity AI Inference Engine
- 📱 Multi-platform support
- 🔧 High-precision Japanese phonemization with OpenJTalk (Windows/macOS/Linux/Android)
- ⚡ GPU inference support (GPUCompute/GPUPixel)
- 🎭 Advanced samples (streaming, multi-voice, real-time)

## Requirements
* Unity 6000.0.55f1
* Unity AI Interface (Inference Engine) 2.2.x

## Build Requirements

- **Windows**: Visual Studio 2022 or later
- **macOS**: Xcode 14 or later
- **Linux**: GCC 9 or later
- **Android**: NDK r21 or later

## Installation

### Via Unity Package Manager (Recommended)

#### Step 1: Install Package
1. Open `Window > Package Manager` in Unity Editor
2. Click `+` button and select `Add package from git URL...`
3. Enter the following URL:
   ```
   https://github.com/ayutaz/uPiper.git?path=Assets/uPiper
   ```

#### Step 2: Import Required Data

After installing from Package Manager, **you must import the data following these steps**:

1. **Select "In Project" in Package Manager**
2. **Select the "uPiper" package**
3. **Expand the "Samples" section**
4. **Import the following samples**:
   - 📚 **OpenJTalk Dictionary Data** (Required) - Dictionary for Japanese speech synthesis
   - 📚 **CMU Pronouncing Dictionary** (Required) - Dictionary for English speech synthesis
   - 🎤 **Voice Models** (Recommended) - High-quality voice models
   - 🎮 **Basic TTS Demo** (Optional) - Demo scene

#### Step 3: Setup Data

After importing samples:

1. **Run `uPiper > Setup > Install from Samples` from the menu**
2. Click "Install" in the installation dialog
3. Wait for setup to complete

#### Step 4: Verify Installation

1. **Run `uPiper > Setup > Check Setup Status` from the menu**
2. Confirm all items show "✓ Installed"
3. Open the Basic TTS Demo scene to verify functionality

> ⚠️ **Important**: TTS functionality will not work without importing the dictionary data

### From Package Files
Download the latest package from [Releases](https://github.com/ayutaz/uPiper/releases):
- **Unity Package (.unitypackage)**: Legacy format, compatible with all Unity versions
- **UPM Package (.tgz)**: For Unity Package Manager, Unity 2019.3+

### Troubleshooting

#### Only One Sample Appears
- Restart Unity Editor
- Click "Refresh" button in Package Manager

#### Dictionary Files Not Found Error
- Confirm `uPiper > Setup > Install from Samples` was executed
- Check status with `uPiper > Setup > Check Setup Status`

#### Japanese Text Garbled
- Use the NotoSansJP-Regular SDF font included in Basic TTS Demo

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

See the [GPU Inference Guide](docs/features/gpu/gpu-inference.md) for details.

## Documentation

- [Architecture](docs/ARCHITECTURE_en.md) - Design and technical details
- [Development Log](docs/DEVELOPMENT_LOG.md) - Development progress and change history
- [Documentation Index](docs/) - Technical documentation, guides, and specifications

## License

This project is licensed under the Apache License 2.0. See the [LICENSE](LICENSE) file for details.

### Third-party Licenses

#### Fonts
- **Noto Sans Japanese**: SIL Open Font License, Version 1.1
  - Copyright 2014-2021 Adobe (http://www.adobe.com/)
  - Used for Japanese display in TextMeshPro
  - See `Assets/Fonts/LICENSE.txt` for details