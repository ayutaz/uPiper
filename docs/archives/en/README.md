# uPiper Documentation

[🇯🇵 日本語](../ja/README.md) | [🇬🇧 **English**](../en/README.md)

## 📋 Overview

uPiper is a high-quality text-to-speech plugin for Unity using the Piper TTS engine. It leverages neural network-based (VITS) voice synthesis technology to generate natural and expressive speech.

## 🚀 Key Features

- **High-Quality Voice Synthesis**: Natural voice generation based on Piper TTS
- **Multi-Language Support**: Japanese, English, Chinese, Korean, and more
- **Fast Inference**: Efficient processing with Unity AI Inference Engine (formerly Sentis)
- **Cross-Platform**: Windows, macOS, Linux, Android (iOS support in development)
- **OpenJTalk Integration**: High-accuracy Japanese phonemization

## 📚 Documentation Structure

### Architecture
- [ARCHITECTURE.md](./ARCHITECTURE.md) - System design and overall structure

### Guides

#### 📦 Setup
- [Additional Language Support](./guides/setup/additional-language-support.md)
- [CMU Dictionary Setup](./guides/setup/cmu-dictionary-setup.md)
- [Flite Build Guide](./guides/setup/flite-build-guide.md)

#### 🎮 Usage
- [Phonemization Guide](./guides/usage/phonemization-guide.md)
- [Test Execution Guide](./guides/usage/test-execution-guide.md)

#### 🔧 Implementation
- [Android Implementation](./guides/implementation/android/)
  - [Implementation Guide](./guides/implementation/android/implementation-guide.md)
  - [Build Settings](./guides/implementation/android/build-settings.md)
  - [Performance Optimization](./guides/implementation/android/performance-optimization.md)
  - [Technical Report](./guides/implementation/android/technical-report.md)
- [Phonemization System](./guides/implementation/phonemization-system/)
  - [Implementation Summary](./guides/implementation/phonemization-system/implementation-summary.md)
  - [Technical Specification](./guides/implementation/phonemization-system/technical-specification.md)
  - [License Evaluation Report](./guides/implementation/phonemization-system/license-evaluation-report.md)
- [Asian Language Support](./guides/implementation/asian-language-support.md)

#### ⚙️ Technical Details
- [Technical Documentation Overview](./guides/technical/README.md)
- [IL2CPP Compatibility](./guides/technical/il2cpp-compatibility.md)
- [IL2CPP Guide](./guides/technical/il2cpp.md)
- [GPU Inference](./guides/technical/gpu-inference.md)

#### 🔄 CI/CD
- [CI/CD Overview](./guides/ci-cd/README.md)
- [IL2CPP Solutions](./guides/ci-cd/il2cpp-solutions.md)

## 🎯 Quick Start

### 1. Installation
1. Open Unity Package Manager
2. Select "Add package from git URL"
3. Enter the following URL:
   ```
   https://github.com/ayutaz/uPiper.git
   ```

### 2. Basic Usage

```csharp
using uPiper;

// Initialize
var config = PiperConfig.LoadDefault();
var tts = new PiperTTS(config);

// Generate speech
var audioClip = await tts.GenerateAudioAsync("Hello, World!");

// Play
audioSource.clip = audioClip;
audioSource.Play();
```

### 3. Setting Up Voice Models
1. Download voice models from [Piper official site](https://github.com/rhasspy/piper)
2. Place them in `Assets/StreamingAssets/uPiper/Models/`
3. Specify the model path in `PiperConfig`

## 🛠️ Developer Information

### Development Environment
1. Unity 6000.0.35f1 or later
2. Unity AI Inference Engine 2.2.x
3. Visual Studio 2022 / Rider

### Build Requirements
- **Windows**: Visual Studio 2019 or later
- **macOS**: Xcode 14 or later
- **Linux**: GCC 9 or later
- **Android**: NDK r21 or later

### Running Tests
```bash
# Run with Unity Test Runner
Window > General > Test Runner

# From command line
Unity.exe -runTests -projectPath . -testResults results.xml
```

## 📝 License

This project is released under the MIT License. See the [LICENSE](../../../LICENSE) file for details.

## 🤝 Contributing

Contributions are welcome! You can participate in the following ways:

1. Report bugs or suggest features via [Issues](https://github.com/ayutaz/uPiper/issues)
2. Create pull requests
3. Improve documentation
4. Add sample code

## 📞 Support

- **Issues**: [GitHub Issues](https://github.com/ayutaz/uPiper/issues)
- **Discussions**: [GitHub Discussions](https://github.com/ayutaz/uPiper/discussions)
- **Wiki**: [Project Wiki](https://github.com/ayutaz/uPiper/wiki)

## 🔗 Related Links

- [Piper TTS](https://github.com/rhasspy/piper) - Original Piper TTS project
- [Unity AI Inference Engine](https://docs.unity3d.com/Packages/com.unity.sentis@latest) - Unity official documentation
- [OpenJTalk](http://open-jtalk.sourceforge.net/) - Japanese phonemization engine