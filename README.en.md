# uPiper

English | [日本語](README.md)

[![openupm](https://img.shields.io/npm/v/com.ayutaz.upiper?label=openupm&registry_uri=https://package.openupm.com)](https://openupm.com/packages/com.ayutaz.upiper/)
[![Unity Tests](https://github.com/ayutaz/uPiper/actions/workflows/unity-tests.yml/badge.svg)](https://github.com/ayutaz/uPiper/actions/workflows/unity-tests.yml)
[![Unity Build](https://github.com/ayutaz/uPiper/actions/workflows/unity-build.yml/badge.svg)](https://github.com/ayutaz/uPiper/actions/workflows/unity-build.yml)

A Unity plugin for [piper-plus](https://github.com/ayutaz/piper-plus) - High-quality neural speech synthesis engine

## Table of Contents

- [Features](#features)
- [Requirements](#requirements)
- [Installation](#installation)
  - [Via OpenUPM (Recommended)](#via-openupm-recommended)
  - [Via Git URL](#via-git-url)
  - [Install from Package File](#install-from-package-file)
  - [Troubleshooting](#troubleshooting)
- [Supported Platforms](#supported-platforms)
- [GPU Inference](#gpu-inference)
- [Documentation](#documentation)
- [License](#license)

## Features

- High-quality speech synthesis (piper-plus based)
- Multi-language support (Japanese, English, Chinese, Spanish, French, Portuguese, Korean)

| Language | G2P Backend |
|----------|-------------|
| Japanese | DotNetG2P.Japanese (MeCab dictionary) |
| English | DotNetG2P.English (Flite LTS) |
| Chinese | DotNetG2P.Chinese (44K character dictionary) |
| Spanish | DotNetG2P.Spanish |
| French | DotNetG2P.French |
| Portuguese | DotNetG2P.Portuguese |
| Korean | DotNetG2P.Korean |

- Fast inference with Unity AI Inference Engine
- High-precision multilingual phonemization with DotNetG2P packages (all platforms)
- GPU inference support (GPUCompute/GPUPixel)
- **Prosody Support**: More natural intonation in speech synthesis
- **Custom Dictionary**: Reading conversion for technical terms and proper nouns

### Supported Models

| Model Name | Language | Prosody | Description |
|-----------|----------|---------|-------------|
| multilingual-test-medium | Multilingual (ja/en/zh/es/fr/pt) | Yes | 6-language multilingual model (Prosody-enabled) |

## Requirements
* Unity 6000.0.58f2
* Unity AI Inference Engine (com.unity.ai.inference) 2.2.2

## Installation

### Via OpenUPM (Recommended)

#### Using openupm-cli

```bash
openupm add com.ayutaz.upiper
```

#### Manually editing manifest.json

Add the following scoped registry to `Packages/manifest.json`:

```json
{
  "scopedRegistries": [
    {
      "name": "package.openupm.com",
      "url": "https://package.openupm.com",
      "scopes": [
        "com.ayutaz.upiper"
      ]
    }
  ],
  "dependencies": {
    "com.ayutaz.upiper": "1.4.0"
  }
}
```

### Via Git URL

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
   - **MeCab Dictionary Data** (Required) - MeCab dictionary for Japanese speech synthesis
   - **CMU Pronouncing Dictionary** (Required) - Dictionary for English speech synthesis
   - **Voice Models** (Recommended) - High-quality voice models
   - **Basic TTS Demo** (Optional) - Demo scene

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

### Install from Package File

Download the latest version from the [Releases](https://github.com/ayutaz/uPiper/releases) page.

#### Unity Package (.unitypackage)

1. Download `uPiper-vX.X.X.unitypackage` from [Releases](https://github.com/ayutaz/uPiper/releases)
2. In Unity Editor, go to `Assets > Import Package > Custom Package`
3. Select the downloaded `.unitypackage` file and click "Import"

> **Note**: The `.unitypackage` does not include DotNetG2P packages. Add the following packages to your `Packages/manifest.json` `dependencies`:

```json
{
  "dependencies": {
    "com.dotnetg2p.core": "https://github.com/ayutaz/dot-net-g2p.git?path=src/DotNetG2P.Core#v1.8.2",
    "com.dotnetg2p.mecab": "https://github.com/ayutaz/dot-net-g2p.git?path=src/DotNetG2P.MeCab#v1.8.2",
    "com.dotnetg2p.english": "https://github.com/ayutaz/dot-net-g2p.git?path=src/DotNetG2P.English#v1.8.2",
    "com.dotnetg2p.chinese": "https://github.com/ayutaz/dot-net-g2p.git?path=src/DotNetG2P.Chinese#v1.8.2",
    "com.dotnetg2p.korean": "https://github.com/ayutaz/dot-net-g2p.git?path=src/DotNetG2P.Korean#v1.8.2",
    "com.dotnetg2p.spanish": "https://github.com/ayutaz/dot-net-g2p.git?path=src/DotNetG2P.Spanish#v1.8.2",
    "com.dotnetg2p.french": "https://github.com/ayutaz/dot-net-g2p.git?path=src/DotNetG2P.French#v1.8.2",
    "com.dotnetg2p.portuguese": "https://github.com/ayutaz/dot-net-g2p.git?path=src/DotNetG2P.Portuguese#v1.8.2"
  }
}
```

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

- ✅ Windows (x64)
- ✅ macOS (Apple Silicon/Intel)
- ✅ Linux (x64)
- ✅ Android (ARM64/ARMv7/x86/x86_64)
- ✅ iOS (ARM64, iOS 11.0+)
- ✅ WebGL (WebGPU / WebGL2)

> **WebGL**: Browsers with WebGPU support use GPUCompute for fast inference. In WebGL2 environments, it automatically falls back to GPUPixel.
> [Demo Page](https://ayutaz.github.io/uPiper/)

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

### InferenceBackend.Auto Selection Logic

When `InferenceBackend.Auto` is specified, the optimal backend is automatically selected based on the platform:

| Platform | Auto-selected Backend | Reason |
|----------|----------------------|--------|
| Windows/Linux | GPUPixel | Best compatibility with VITS models |
| macOS | CPU | Metal has issues with Unity.InferenceEngine |
| iOS/Android | GPUPixel | Optimized for mobile GPU |
| WebGL (WebGPU) | GPUCompute | Fast inference via WebGPU Compute Shaders |
| WebGL (WebGL2) | GPUPixel | WebGL2 fallback |

> **Note**: On desktop, GPUCompute may not correctly generate audio with VITS models. GPUPixel or CPU is recommended. GPUCompute works correctly in WebGPU environments.

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