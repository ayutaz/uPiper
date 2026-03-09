# uPiper Architecture Document

[🇯🇵 日本語](ARCHITECTURE_ja.md) | [🇬🇧 **English**](ARCHITECTURE_en.md)

## Overview

uPiper is a plugin for using Piper TTS in Unity environments. It employs neural network-based voice synthesis (VITS) to achieve high-quality multilingual speech synthesis.

## Architecture Overview

```
┌─────────────────┐     ┌──────────────────┐     ┌─────────────────┐
│   Text Input    │ --> │   Phonemizer     │ --> │  VITS Model     │
│   (Japanese)    │     │   (dot-net-g2p)  │     │  (ONNX/Unity)   │
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

#### dot-net-g2p (Japanese)
- **Role**: Convert Japanese text to phoneme sequences
- **Implementation**: Pure C# implementation (using MeCab dictionary)
- **Dictionary**: Uses mecab-naist-jdic (789,120 entries)
- **Processing Flow**:
  ```
  Text → MeCab Analysis → G2P Conversion → Phoneme Sequence
  ```

#### Important Design Decision: Phoneme Timing
```csharp
// All phonemes are assigned a fixed 50ms duration
duration = 0.05f; // Default 50ms duration
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

2. **dot-net-g2p Phonemization**:
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

## Prosody Support

### Overview

Prosody-enabled models (such as tsukuyomi-chan) can generate more natural intonation by utilizing accent information obtained from dot-net-g2p (MeCab dictionary).

### Data Flow (Prosody-Enabled)

```
┌─────────────────┐     ┌──────────────────┐     ┌─────────────────┐
│   Text Input    │ --> │ CustomDictionary │ --> │   DotNetG2P     │
│   "Dockerを..."  │     │ (Preprocessing)  │     │   Phonemizer    │
└─────────────────┘     └──────────────────┘     └─────────────────┘
                                                         │
                                 ┌───────────────────────┴───────────────────────┐
                                 │                                               │
                                 ↓                                               ↓
                        ┌──────────────────┐                        ┌──────────────────┐
                        │ Phoneme Sequence │                        │ Prosody Data     │
                        │ "d o q k a ..."  │                        │ A1: [0,1,2,...]  │
                        └──────────────────┘                        │ A2: [2,2,2,...]  │
                                 │                                  │ A3: [1,1,1,...]  │
                                 │                                  └──────────────────┘
                                 │                                           │
                                 ↓                                           │
                        ┌──────────────────┐                                 │
                        │ PhonemeEncoder   │                                 │
                        │ IPA/PUA Convert  │                                 │
                        └──────────────────┘                                 │
                                 │                                           │
                                 ↓                                           ↓
                        ┌─────────────────────────────────────────────────────────┐
                        │              VITS Model (ONNX)                          │
                        │  Input: phoneme_ids, a1, a2, a3                        │
                        │  Output: Audio waveform                                │
                        └─────────────────────────────────────────────────────────┘
                                                         │
                                                         ↓
                                                ┌─────────────────┐
                                                │  Audio Output   │
                                                │    (Unity)      │
                                                └─────────────────┘
```

### Prosody Parameters

| Parameter | Description | Value Range |
|-----------|-------------|-------------|
| **A1** | Mora position within the accent phrase (0-based) | 0~ |
| **A2** | Accent nucleus position within the accent phrase (accent type) | 0~ |
| **A3** | Accent phrase position within the breath group (intonation phrase) | 1~ |

### Usage Example

```csharp
// Phonemization with prosody information
var phonemizer = new DotNetG2PPhonemizer();
var result = phonemizer.PhonemizeWithProsody("こんにちは");
// result.Phonemes: phoneme array
// result.ProsodyA1, ProsodyA2, ProsodyA3: prosody values for each phoneme

// Prosody-enabled audio generation
var generator = new InferenceAudioGenerator();
await generator.InitializeAsync(modelAsset, voiceConfig);
if (generator.SupportsProsody)
{
    var audio = await generator.GenerateAudioWithProsodyAsync(
        phonemeIds, prosodyA1, prosodyA2, prosodyA3);
}
```

## Custom Dictionary

### Overview

A preprocessing feature that converts technical terms and proper nouns (English words, alphabetic text) to Japanese readings.

### Processing Flow

```
Input Text
    │
    ↓
┌─────────────────────────────────────┐
│ CustomDictionary.ApplyToText()      │
│                                     │
│ "DockerとGitHubを使った開発"         │
│          ↓                          │
│ "ドッカーとギットハブを使った開発"     │
└─────────────────────────────────────┘
    │
    ↓
dot-net-g2p Phonemization
```

### Dictionary Files

Dictionaries are placed in `StreamingAssets/uPiper/Dictionaries/`:

| File | Contents |
|------|----------|
| `default_tech_dict.json` | Technical terms (programming languages, development tools, etc.) |
| `default_common_dict.json` | IT/Business terms |
| `additional_tech_dict.json` | AI/LLM related terms |
| `user_custom_dict.json` | User-defined dictionary (template) |

### JSON Format

```json
{
  "version": "2.0",
  "entries": {
    "Docker": {"pronunciation": "ドッカー", "priority": 9},
    "GitHub": {"pronunciation": "ギットハブ", "priority": 9}
  }
}
```

## Design Decisions

### 1. Decision Not to Use HTS Engine

**Background**:
- Traditional OpenJTalk used HTS Engine (HMM-based) for speech synthesis
- Piper uses VITS (neural network) for speech synthesis

**Decision**:
- Phonemization is achieved with dot-net-g2p (pure C# implementation)
- HTS Engine is completely excluded
- Result: Lightweight implementation, complete elimination of native dependencies

### 2. Phoneme Timing Simplification

**Investigation Results**:
- Confirmed that Piper's implementation does not use HTS Engine
- VITS model automatically estimates phoneme timing

**Decision**:
- Fixed 50ms is sufficient (the model recalculates)
- Simplification of implementation and improved maintainability

### 3. PUA Character Usage

**Challenge**:
- Japanese has multi-character phonemes ("ky", "ch", etc.)
- Piper models expect 1 phoneme = 1 character

**Solution**:
- Uses Unicode PUA (Private Use Area) region
- Mapping compatible with pyopenjtalk

## Platform Support

### Supported Platforms
- **Windows**: x64 (Windows 10/11)
- **macOS**: Intel/Apple Silicon (macOS 11+)
- **Linux**: x64 (Ubuntu 20.04+)
- **Android**: arm64-v8a, armeabi-v7a, x86, x86_64 (API 21+)
- **iOS**: arm64 (iOS 11.0+)
- **WebGL**: Supported (dedicated components for file I/O and threading constraint workarounds)

### Platform-Specific Implementation

dot-net-g2p is a pure C# implementation, so no platform-specific native libraries are required.

#### Windows
- Unity backend: Mono/IL2CPP

#### macOS
- Universal Binary support (automatic via managed code)

#### Android
- Architectures: arm64-v8a, armeabi-v7a, x86, x86_64

#### iOS
- Xcode: 14+
- Architecture: arm64 (iOS 11.0+)

#### WebGL
- Direct file system access is unavailable; a dedicated async loading mechanism is used
- Multithreading is unavailable; `Task.Run` is replaced with main thread direct execution
- See the "WebGL Support" section below for details

## WebGL Support

### Overview

uPiper supports operation on the WebGL platform.
Since WebGL does not allow direct file system access or multithreading, dedicated alternative implementations are provided.

### WebGL-Specific Components

| Component | Location | Role |
|-----------|----------|------|
| `WebGLStreamingAssetsLoader` | `Runtime/Core/Platform/` | Async file loading via `UnityWebRequest` (with progress reporting) |
| `IndexedDBCache` | `Runtime/Core/Platform/` + `Plugins/WebGL/` | Dictionary data caching to browser IndexedDB (jslib interop) |
| `WebGLLoadingPanel` | `Runtime/Core/Platform/` | Loading progress display UI (progress bar + status text) |
| `WebGLSplitDataProcessor` | `Editor/WebGL/` | Automatic large file splitting (`PostProcessBuild`, for GitHub Pages 100MB limit) |

### Dictionary Load Flow

On non-WebGL platforms, files are read directly via `File.ReadAllBytes`, but on WebGL the following flow is used:

```
┌──────────────────────────┐
│ IndexedDBCache.HasKeyAsync│  Cache check
└───────────┬──────────────┘
            │
     ┌──────┴──────┐
     │Cache exists  │  → IndexedDBCache.LoadAsync → byte[]
     └─────────────┘
     │Cache missing │
     └──────┬──────┘
            ↓
┌──────────────────────────────────────┐
│ WebGLStreamingAssetsLoader           │
│ UnityWebRequest.Get() → byte[]       │
└───────────┬──────────────────────────┘
            ↓
┌──────────────────────────────────────┐
│ IndexedDBCache.StoreAsync            │  Save to cache for next time
└───────────┬──────────────────────────┘
            ↓
        DictionaryBundle.Load(byte[], byte[], byte[], byte[])
```

### Conditional Compilation Pattern

`#if UNITY_WEBGL && !UNITY_EDITOR` is used to branch platform-specific processing:

- **File I/O**: `File.ReadAllBytes` → `WebGLStreamingAssetsLoader.LoadBytesAsync`
- **Threading**: `Task.Run` → Main thread direct execution (`await Task.Yield()`)
- **Cache**: File system → `IndexedDBCache` (uses browser IndexedDB via jslib)

### GitHub Pages Deployment

`WebGLSplitDataProcessor` automatically performs the following in post-build processing (`PostProcessBuild`):

1. Splits files larger than 100MB in the Build/ directory into 90MB chunks
2. Copies `split-file-loader.js` and `github-pages-adapter.js` to the build output
3. Injects loader script tags into `index.html`

For detailed architecture, see [webgl-architecture.md](webgl-architecture.md).

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