# uPiper Architecture Document

[🇯🇵 日本語](../ja/ARCHITECTURE.md) | [🇬🇧 **English**](../en/ARCHITECTURE.md)

## Overview

uPiper is a plugin for using Piper TTS in Unity environments. It employs neural network-based voice synthesis (VITS) to achieve high-quality multilingual speech synthesis.

## Architecture Overview

```
┌─────────────────┐     ┌──────────────────┐     ┌─────────────────┐
│   Text Input    │ --> │   Phonemizer     │ --> │  VITS Model     │
│   (Japanese)    │     │   (OpenJTalk)    │     │  (ONNX/Unity)   │
└─────────────────┘     └──────────────────┘     └─────────────────┘
                                 │                         │
                                 ↓                         ↓
                        ┌──────────────────┐     ┌─────────────────┐
                        │ Phoneme Sequence │     │  Audio Output   │
                        │ "k o n n i ch i" │     │    (Unity)      │
                        └──────────────────┘     └─────────────────┘
```

## Component Details

### 1. Text Input Layer

- **Japanese**: Mixed text with Kanji, Hiragana, and Katakana
- **English**: Alphabetic text (Flite LTS support implemented)
- **Other Languages**: Chinese, Korean support planned for future

### 2. Phonemization Layer

#### OpenJTalk (Japanese)
- **Role**: Convert Japanese text to phoneme sequences
- **Implementation**: C/C++ native library (called via P/Invoke)
- **Dictionary**: Uses mecab-naist-jdic (789,120 entries)
- **Processing Flow**:
  ```
  Text → MeCab Analysis → NJD Processing → JPCommon → Phoneme Sequence
  ```

#### Important Design Decision: Phoneme Timing
```c
// In Phase 1.10, all phonemes are assigned a fixed 50ms duration
result->durations[i] = 0.05f; // Default 50ms duration
```

**Rationale**:
1. VITS models have built-in Duration Predictors
2. Input durations are used only as references
3. Actual timing is automatically optimized by the model
4. HTS Engine integration is unnecessary (as Piper uses neural synthesis)

### 3. Phoneme Encoding Layer

#### PUA (Private Use Area) Mapping
Maps multi-character phonemes to single Unicode characters:

```csharp
// Example: Processing "kyou"
"ky" + "o" + "u" → "\ue006" + "o" + "u"
```

**PUA Mapping Table**:
- `ky` → `\ue006` (kya, kyu, kyo)
- `ch` → `\ue00e` (chi, cha, chu, cho)
- `ts` → `\ue00f` (tsu)
- `sh` → `\ue010` (shi, sha, shu, sho)

### 4. Speech Synthesis Layer (VITS Model)

#### Unity AI Inference Engine Integration
- **Model Format**: ONNX
- **Inference Engine**: Unity AI Inference Engine (formerly Sentis)
- **Input**: Phoneme ID array
- **Output**: Audio waveform (float array)

#### VITS Architecture
```
Phoneme IDs → TextEncoder → Duration Predictor → Flow Decoder → Audio Waveform
            ↓
            Latent Representation → Stochastic Duration Predictor
                                   (Automatic phoneme timing estimation)
```

### 5. Audio Output Layer

- **AudioClipBuilder**: Generates Unity AudioClip from float array
- **Normalization**: Automatic volume level adjustment
- **Sample Rate**: 22050Hz (standard)

## Data Flow Example

### Processing Japanese Text "こんにちは" (konnichiwa)

1. **Input**: "こんにちは"

2. **OpenJTalk Phonemization**:
   ```
   k o N n i ch i w a
   ```

3. **PUA Encoding**:
   ```
   k o N n i \ue00e i w a
   ```

4. **Phoneme ID Conversion**:
   ```
   [23, 30, 4, 28, 21, 10, 21, 36, 7]
   ```

5. **VITS Processing**:
   - Input shape: [1, 9] (batch_size, sequence_length)
   - Output shape: [1, 35840] (batch_size, audio_samples)

6. **AudioClip Generation**:
   - Sample rate: 22050Hz
   - Duration: 1.626 seconds
   - Format: Mono, 32-bit float

## Platform Support

### Supported Platforms
- **Windows**: x64 (Windows 10/11)
- **macOS**: Intel/Apple Silicon (macOS 11+)
- **Linux**: x64 (Ubuntu 20.04+)
- **Android**: arm64-v8a (API 21+)

### In Development
- **iOS**: arm64 (iOS 11+) - Phase 5

### Platform-Specific Implementation

#### Windows
- Native library: `openjtalk_wrapper.dll`
- Compiler: MSVC 2019+
- Unity backend: Mono/IL2CPP

#### macOS
- Native library: `openjtalk_wrapper.bundle`
- Compiler: Clang
- Universal Binary support

#### Android
- Native library: `libopenjtalk_wrapper.so`
- NDK: r21+
- Architectures: arm64-v8a, armeabi-v7a, x86, x86_64

## Performance Characteristics

### Memory Usage
- Model loading: ~100MB (VITS model)
- Dictionary: ~30MB (compressed)
- Runtime: ~50-200MB depending on usage

### Processing Speed
- Phonemization: <10ms for typical sentences
- Inference: ~100-500ms depending on hardware
- Total latency: <1 second for most use cases

### Optimization Strategies
1. **Phoneme Caching**: Frequently used text cached
2. **Model Quantization**: Optional INT8 quantization
3. **GPU Acceleration**: Supported via Unity AI Inference Engine
4. **Streaming**: Chunk-based processing for long texts

## Extension Points

### Custom Phonemizers
Implement `IPhonemizerBackend` interface:
```csharp
public interface IPhonemizerBackend
{
    string Language { get; }
    PhonemeResult Phonemize(string text);
}
```

### Voice Model Support
- Place ONNX models in `StreamingAssets/uPiper/Models/`
- Configure via `PiperVoiceConfig`

### Language Extensions
1. Implement language-specific phonemizer
2. Add phoneme-to-ID mapping
3. Train or obtain compatible VITS model

## Security Considerations

- All processing occurs locally (no cloud dependencies)
- No personal data collection
- Models and dictionaries are read-only
- Sandboxed execution in Unity environment