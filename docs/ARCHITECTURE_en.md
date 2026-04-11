# uPiper Architecture Document

[日本語](ARCHITECTURE_ja.md) | [**English**](ARCHITECTURE_en.md)

## Overview

uPiper is a plugin for using Piper TTS in Unity environments. It employs neural network-based voice synthesis (VITS) to achieve high-quality multilingual speech synthesis. Uses C# 10.0 (`csc.rsp -langversion:10.0`).

## Architecture Overview

```
┌─────────────────┐     ┌──────────────────┐     ┌──────────────────────┐
│   Text Input    │ --> │ CustomDictionary │ --> │ MultilingualPhonemizer│
│  (7 Languages)  │     │ (Preprocessing)  │     │ (ILanguageG2PHandler) │
└─────────────────┘     └──────────────────┘     └──────────────────────┘
                                                           │
                                                           ↓
                        ┌──────────────────┐     ┌──────────────────────┐
                        │ PuaTokenMapper   │ --> │ PhonemeEncoder       │
                        │ (pua.json)       │     │ (ProsodyFlat stride=3)│
                        └──────────────────┘     └──────────────────────┘
                                                           │
                                                           ↓
                        ┌──────────────────────────────────────────────┐
                        │ TTSSynthesisOrchestrator                     │
                        │   → AudioSynthesisCache (LRU, FNV-1a)      │
                        │   → IInferenceAudioGenerator (NativeArray)  │
                        │   → AudioNormalizer → AudioClipBuilder      │
                        └──────────────────────────────────────────────┘
                                                           │
                                                           ↓
                                                  ┌─────────────────┐
                                                  │  AudioClip      │
                                                  │  (22050Hz)      │
                                                  └─────────────────┘
```

## Component Details

### 1. Text Input Layer

- **Japanese**: Mixed text with Kanji, Hiragana, and Katakana (DotNetG2P / MeCab dictionary)
- **English**: Alphabetic text (DotNetG2P.English / CMU dictionary + LTS + homograph resolution)
- **Spanish**: DotNetG2P.Spanish (rule-based G2P)
- **French**: DotNetG2P.French (rule-based G2P)
- **Portuguese**: DotNetG2P.Portuguese (rule-based G2P, Brazilian variant)
- **Chinese**: DotNetG2P.Chinese (44K character + 412K phrase dictionary)
- **Korean**: DotNetG2P.Korean (Hangul decomposition + phonological rules)

### 2. Phonemization Layer

#### MultilingualPhonemizer
- **Location**: `Runtime/Core/Phonemizers/Multilingual/MultilingualPhonemizer.cs`
- **Role**: Segment text via language detection, then delegate to per-language ILanguageG2PHandler Strategy implementations
- **Constructor**: Receives `MultilingualPhonemizerOptions` (languages, defaultLatinLanguage, enableTrigramDetection, LanguageDetector, handlers)
- **InitializeAsync**: Creates default handlers for unregistered languages -> initializes all handlers -> upgrades to HybridLanguageDetector when trigram detection is enabled

##### Language Detection (ILanguageDetector)

| Implementation | Location | Role |
|---------------|----------|------|
| `ILanguageDetector` | `Multilingual/` | Language detection interface (public). `SegmentText()` -> `IReadOnlyList<(string language, string text)>` |
| `UnicodeLanguageDetector` | `Multilingual/` | Unicode script range based detection (CJK, Hangul, Latin, etc.). Default |
| `HybridLanguageDetector` | `Multilingual/` | Unicode + Trigram hybrid detection (internal sealed). Resolves Latin language ambiguity via trigram |
| `TrigramLanguageDetector` | `Multilingual/` | Trigram frequency analysis based detection (internal sealed class). en/es/fr/pt disambiguation |
| `LatinSegmentRefiner` | `Multilingual/` | Trigram-based refinement of Latin segments (internal) |

##### ILanguageG2PHandler Strategy Pattern

Per-language G2P processing is unified via the `ILanguageG2PHandler` interface:

```csharp
public interface ILanguageG2PHandler : IDisposable
{
    string LanguageCode { get; }
    bool IsInitialized { get; }
    Task InitializeAsync(CancellationToken cancellationToken = default);
    (string[] Phonemes, int[] ProsodyFlat) Process(string text);
}
```

- **ProsodyFlat**: stride=3 flat array `[a1_0, a2_0, a3_0, a1_1, a2_1, a3_1, ...]`
- **HandlerEntry**: `internal readonly struct` pairing a handler with an `IsOwned` ownership flag

| Handler | Language | Delegates To |
|---------|----------|-------------|
| `JapaneseG2PHandler` | ja | DotNetG2PPhonemizer (MeCab dictionary, prosody-enabled) |
| `EnglishG2PHandler` | en | EnglishG2PEngine (CMU dict + LTS + homograph resolution) |
| `SpanishG2PHandler` | es | SpanishG2PEngine (rule-based G2P) |
| `FrenchG2PHandler` | fr | FrenchG2PEngine (rule-based G2P) |
| `PortugueseG2PHandler` | pt | PortugueseG2PEngine (rule-based G2P, Brazilian variant) |
| `ChineseG2PHandler` | zh | ChineseG2PEngine (44K character + 412K phrase dictionary) |
| `KoreanG2PHandler` | ko | KoreanG2PEngine (Hangul decomposition + phonological rules) |

##### MultilingualPhonemizeResult

```csharp
public class MultilingualPhonemizeResult
{
    public string[] Phonemes { get; }
    public int[] ProsodyFlat { get; }        // stride=3, Length = Phonemes.Length * 3
    public string DetectedPrimaryLanguage { get; }
    public bool HasProsody => ProsodyFlat != null;
}
```

#### dot-net-g2p (Japanese)
- **Role**: Convert Japanese text to phoneme sequences
- **Implementation**: Pure C# implementation (using MeCab dictionary)
- **Dictionary**: Uses mecab-naist-jdic (789,120 entries)
- **Processing Flow**:
  ```
  Text -> MeCab Analysis -> G2P Conversion -> Phoneme Sequence
  ```

#### Important Design Decision: Phoneme Timing
```csharp
// All phonemes are assigned a fixed 50ms duration
// VITS models automatically optimize timing via Duration Predictor
```

**Rationale**:
1. VITS models have built-in Duration Predictors
2. Input durations are used only as references
3. Actual timing is automatically optimized by the model
4. HTS Engine integration is unnecessary (as Piper uses neural synthesis)

### 3. Phoneme Encoding Layer

#### PuaTokenMapper (Instance Class)

Maps multi-character phonemes to single Unicode PUA characters:

```csharp
// Example: Processing "kyou"
"ky" + "o" + "u" -> "\ue006" + "o" + "u"
```

- **Fixed mapping**: `FixedPuaMapping` (`IReadOnlyDictionary<string, int>`, 96 entries, 0xE000-0xE061)
- **pua.json loading**: `InitializeAsync()` / `InitializeFromFile()` loads from `StreamingAssets/uPiper/pua.json`. Copy-on-write for atomic replacement
- **Dynamic allocation**: `Register(token)` auto-assigns new PUA codepoints for unregistered multi-char tokens (0xE062-0xF8FF)
- **Thread-safe**: `ConcurrentDictionary` + lock-based dynamic allocation

#### PhonemeEncoder

- **Location**: `Runtime/Core/AudioGeneration/PhonemeEncoder.cs`
- **Constructor**: Receives `PiperVoiceConfig` + `PuaTokenMapper`
- **ProsodyFlat stride=3 support**: `EncodeWithProsody(phonemes, prosodyFlat)` -> `ProsodyEncodingResult { PhonemeIds, ExpandedProsodyFlat }`
- **BOS/EOS/PAD expansion**: Automatically expands ProsodyFlat to match BOS/EOS/PAD token insertion (inserts zero values at boundaries)
- **`ProsodyStride = 3`**: Public constant used when constructing ProsodyFlat arrays
- **Model type auto-detection**:
  - IPA detection: `_useIpaMapping = !_isMultilingualModel && _phonemeToId.ContainsKey("ɕ")`
  - Multilingual detection: `_isMultilingualModel` flags `phoneme_type: "multilingual"` models
  - Multilingual models use PUA character passthrough (no IPA/PUA conversion)

### 3.5 Initialization Validation

#### InitializationValidator
- **Location**: `Runtime/Core/InitializationValidator.cs` (`internal static class`)
- **Role**: Consolidated up-front validation at the start of `InitializeAsync` / `InitializeWithInferenceAsync`
- **API**:
  - `ValidateForInitialize(config)` -- Validation for lightweight initialization path
  - `ValidateForInference(config, modelAsset, voiceConfig)` -- Validation for full initialization with model
- **Validation categories**: RuntimeEnvironment, Model, VoiceConfig, PhonemeIdMap, StreamingAssets, Dictionary, Platform

### 3.6 Configuration Management Layer

#### IPiperConfigReadOnly

```csharp
public interface IPiperConfigReadOnly
{
    LanguageSettings Language { get; }
    PerformanceSettings Performance { get; }
    InferenceSettings Inference { get; }
    PiperAudioSettings Audio { get; }
    SilenceSettings Silence { get; }
    GeneralSettings General { get; }
}
```

#### PiperConfigPresets
- **Location**: `Runtime/Core/PiperConfigPresets.cs` (`public static class`)
- **Role**: Static factory providing commonly used configuration presets
- **API**:
  - `Fast()` -- Low-latency, high-speed synthesis (enables AudioCache and Warmup)
  - `Natural()` -- Balanced quality and speed (equivalent to default settings)
  - `HighQuality()` -- High-quality narration (enables audio normalization)

#### ValidatedPiperConfig
- **Location**: `Runtime/Core/ValidatedPiperConfig.cs`
- **Role**: An immutable configuration snapshot generated via `PiperConfig.ToValidated()`. Implements `IPiperConfigReadOnly`
- **6 nested readonly record structs**: `LanguageSettings`, `PerformanceSettings`, `InferenceSettings`, `PiperAudioSettings`, `SilenceSettings`, `GeneralSettings`
- **How to obtain**: Call `PiperConfig.ToValidated()` to receive a validated `ValidatedPiperConfig`. Internally `PiperTTS` holds this as `_validatedConfig`
- **Pure function `ToValidated()`**: Does not modify any fields of `PiperConfig`. Clamping, normalization, and auto-detection are performed within the `ValidatedPiperConfig` constructor
- **GPUSettings immutability**: Guaranteed via defensive copy
- **Key properties**:
  - `Silence.ParsedPhonemeSilence: IReadOnlyDictionary<string, float>` -- Pre-parsed map when `EnablePhonemeSilence=true`
  - `Audio.NormalizeAudio`, `Audio.SampleRate` -- AudioNormalizer behavior control
  - `Inference.Backend` -- Input to BackendSelector

### 4. Speech Synthesis Layer (VITS Model)

#### Unity AI Inference Engine Integration
- **Model Format**: ONNX
- **Inference Engine**: Unity AI Inference Engine (formerly Sentis)
- **Input**: Phoneme ID array + optional ProsodyFlat
- **Output**: Audio waveform (`NativeArray<float>`)

#### VITS Architecture
```
Phoneme IDs -> TextEncoder -> Duration Predictor -> Flow Decoder -> Audio Waveform
             ↓
             Latent Representation -> Stochastic Duration Predictor
                                     (Automatic phoneme timing estimation)
```

#### BackendSelector + PlatformInfo
- **Location**: `Runtime/Core/AudioGeneration/BackendSelector.cs`
- **BackendSelector**: Inference backend selection logic (`public static class`). `Determine(requested, platform, gpuMemoryThresholdMB)` -> `BackendType`
- **LogSelectionSummary**: Called after `InitializeAsync` completes, outputs a summary log of the selection rationale. `LogSelectionSummary(requested, actual, platform)` logs the requested value, actual backend, and platform info
- **PlatformInfo**: Platform-dependent info encapsulation (`public readonly struct`). `FromCurrentEnvironment()` factory confines preprocessor directives
- **Preprocessor-free**: `Determine()` branches solely on `PlatformInfo` fields. Easily testable

#### IInferenceAudioGenerator
- **Location**: `Runtime/Core/AudioGeneration/IInferenceAudioGenerator.cs`
- **Unified API**: `GenerateAudioAsync(phonemeIds, prosodyFlat, ...)` -> `NativeArray<float>`. `prosodyFlat=null` for non-prosody path
- **NativeArray output**: Caller is responsible for Dispose

#### InferenceAudioGenerator and InferenceContext
- **Location**: `Runtime/Core/AudioGeneration/InferenceAudioGenerator.cs`
- **Output tensor name caching**: At initialization, `_model.outputs[0].name` is cached in `_cachedOutputName`
- **Prosody tensor construction**: Uses `new int[prosodySize]` for direct allocation (ArrayPool is not used because Tensor constructor requires exact array size)
- **InferenceContext** (`private sealed class`, `IDisposable`):
  - `using var ctx = PrepareInputs(...)` pattern atomically releases all input tensors in `Dispose()`

#### TTSSynthesisOrchestrator
- **Location**: `Runtime/Core/AudioGeneration/TTSSynthesisOrchestrator.cs` (`internal sealed`)
- **Role**: Centrally manages the entire phoneme-to-`AudioClip` conversion pipeline
- **Constructor**: Receives `IInferenceAudioGenerator`, `ISplitInferenceOrchestrator`, `PhonemeEncoder`, `AudioClipBuilder`, `IPiperConfigReadOnly` (nullable), `PiperVoiceConfig`, `AudioSynthesisCache` (nullable)
- **Cache integration**: When `AudioSynthesisCache` is injected, looks up the cache using encoded phoneme IDs + synthesis parameters after encoding; on hit, skips ONNX inference and proceeds directly to AudioClip construction
- **NativeArray Pipeline**:
  1. **PhonemeEncoder** -- Encodes phonemes to model IDs (`EncodeWithProsody` / `Encode`)
  2. **AudioSynthesisCache** -- Skip inference on cache hit (optional)
  3. **IInferenceAudioGenerator** -- ONNX inference -> `NativeArray<float>`
  4. **AudioNormalizer** -- `NativeArray<float>` in-place normalization (configurable via settings)
  5. **AudioClipBuilder** -- `NativeArray<float>` -> `AudioClip` (no managed marshalling)
  6. **NativeArray Dispose** -- `finally` block calls `audioData.Dispose()` (AudioClip.SetData has already copied the data)

#### SynthesisRequest (public readonly struct)

```csharp
public readonly struct SynthesisRequest
{
    public string[] Phonemes { get; }
    public int[] ProsodyFlat { get; }    // stride=3, nullable
    public float LengthScale { get; }
    public float NoiseScale { get; }
    public float NoiseW { get; }
    public int SpeakerId { get; }
    public int LanguageId { get; }
    public bool HasProsody => ProsodyFlat != null;
}
```

- **Factory methods**:
  - `FromPhonemes(phonemes, ...)` -- Without prosody (defensive copy)
  - `FromPhonemesWithProsody(phonemes, prosodyFlat, ...)` -- With prosody (defensive copy)
  - `CreateInternal(...)` -- Internal use (no defensive copy)

#### PhonemizeResult (public sealed class)

```csharp
public sealed class PhonemizeResult
{
    public string[] Phonemes { get; }
    public int[] ProsodyFlat { get; }
    public string DetectedLanguage { get; }
    public int ResolvedLanguageId { get; }
    public bool HasProsody => ProsodyFlat != null;
}
```

#### ISplitInferenceOrchestrator / SplitInferenceOrchestrator
- **Interface location**: `Runtime/Core/AudioGeneration/ISplitInferenceOrchestrator.cs` (`internal interface`)
- **Implementation location**: `Runtime/Core/AudioGeneration/SplitInferenceOrchestrator.cs` (`internal class`)
- **Role**: Orchestrates silence-based phrase splitting. Splits the phoneme sequence at silence token positions, performs independent inference per phrase, and inserts zero-sample silence intervals between phrases before concatenation
- **DI improvement**: The `ISplitInferenceOrchestrator` interface abstracts the dependency from TTSSynthesisOrchestrator, improving testability
- **Parameter type**: `phonemeSilence` is received as `IReadOnlyDictionary<string, float>` via the `GenerateWithSilenceSplitAsync()` method argument (not a constructor parameter)

#### AudioSynthesisCache
- **Location**: `Runtime/Core/AudioGeneration/AudioSynthesisCache.cs` (`internal sealed`)
- **Role**: LRU-based audio synthesis result cache. Caches inference results keyed by phoneme IDs + synthesis parameters, skipping ONNX inference on cache hits
- **Hash**: FNV-1a 64-bit hash. Inputs: phoneme IDs, ProsodyFlat, synthesis parameters (lengthScale, noiseScale, noiseW, speakerId, languageId)
- **Dual eviction**: LRU eviction by both entry count limit (default 50) and memory budget limit (default 100MB)
- **API**:
  - `GenerateKey(phonemeIds, prosodyFlat, ...)` -- FNV-1a cache key generation (`static`)
  - `TryGet(key, out samples, out sampleRate)` -- Cache lookup (updates LRU order)
  - `Set(key, samples, sampleRate)` -- Cache insertion (auto-eviction)
  - `Clear()` -- Clear all cached entries (logs statistics)

#### AudioNormalizer
- **Location**: `Runtime/Core/AudioGeneration/AudioNormalizer.cs`
- **`public static class`**: GC-allocation-free audio normalization
- **API**:
  - `NormalizeInPlace(NativeArray<float>, targetPeak)` -- Zero GC, modifies NativeArray directly
  - `NormalizeInPlace(float[], targetPeak)` -- float[] in-place version
  - `Normalize(float[], targetPeak)` -- Returns new array (non-destructive)

#### AudioClipBuilder
- **Location**: `Runtime/Core/AudioGeneration/AudioClipBuilder.cs`
- **`public class`**: `NativeArray<float>` -> AudioClip (recommended), `float[]` -> AudioClip (`[Obsolete]`)

### 5. Audio Output Layer

- **AudioClipBuilder**: Generates Unity AudioClip from `NativeArray<float>` (`float[]` version is `[Obsolete]`)
- **AudioNormalizer**: `NativeArray<float>` in-place normalization (zero GC allocation)
- **Sample Rate**: 22050Hz (standard)
- **NativeArray Lifecycle**: Disposed in `TTSSynthesisOrchestrator.SynthesizeAsync`'s `finally` block

## Data Flow Example

### Processing Japanese Text "konnichiwa"

1. **Input**: "konnichiwa"

2. **ILanguageG2PHandler.Process()** (JapaneseG2PHandler -> DotNetG2PPhonemizer):
   ```
   Phonemes: [k, o, N_uvular, n, i, ch, i, w, a, $]
   ProsodyFlat: [0,2,1, 1,2,1, 2,2,1, 3,2,1, 4,2,1, 5,2,1, 6,2,1, 7,2,1, 8,2,1, 0,0,0]
   ```

3. **PhonemeEncoder.EncodeWithProsody()**:
   ```
   PhonemeIds: [BOS, PAD, id_k, PAD, id_o, PAD, ..., EOS]
   ExpandedProsodyFlat: [0,0,0, 0,0,0, 0,2,1, 0,0,0, 1,2,1, ...]  (zeros inserted at BOS/PAD)
   ```

4. **IInferenceAudioGenerator.GenerateAudioAsync()**:
   - Input: phoneme_ids + prosodyFlat -> separated into a1, a2, a3 tensors
   - Output: `NativeArray<float>` (audio waveform)

5. **AudioNormalizer -> AudioClipBuilder -> AudioClip**

## Prosody Support

### Overview

Prosody-enabled models (such as multilingual-test-medium) can generate more natural intonation by utilizing ProsodyFlat (stride=3) data obtained from ILanguageG2PHandler implementations.

### Data Flow (Prosody-Enabled)

```
┌─────────────────┐     ┌──────────────────┐     ┌──────────────────────────┐
│   Text Input    │ --> │ CustomDictionary │ --> │ MultilingualPhonemizer   │
│   "Docker..."   │     │ (Preprocessing)  │     │ (ILanguageDetector +     │
└─────────────────┘     └──────────────────┘     │  ILanguageG2PHandler)    │
                                                  └──────────────────────────┘
                                                             │
                                    ┌────────────────────────┼────────────────────────┐
                                    ↓                        ↓                        ↓
                            ┌──────────────┐  ┌────────────────────┐  ┌──────────────────┐
                            │ ja: Japanese │  │ en: English        │  │ es/fr/pt/zh/ko:  │
                            │  G2PHandler  │  │  G2PHandler        │  │ *G2PHandler      │
                            └──────┬───────┘  └────────┬───────────┘  └────────┬─────────┘
                                   │                   │                       │
                                   │   Process() -> (Phonemes[], ProsodyFlat[])│
                                   └───────────────────┼───────────────────────┘
                                                       │
                                                       ↓
                                        ┌──────────────────────────┐
                                        │ MultilingualPhonemize    │
                                        │ Result                   │
                                        │ .Phonemes[]              │
                                        │ .ProsodyFlat[] (stride=3)│
                                        └──────────────────────────┘
                                                       │
                                                       ↓
                                        ┌──────────────────────────┐
                                        │ PhonemeEncoder           │
                                        │ .EncodeWithProsody()     │
                                        │ -> PhonemeIds[]          │
                                        │ -> ExpandedProsodyFlat[] │
                                        └──────────────────────────┘
                                                       │
                                                       ↓
                        ┌─────────────────────────────────────────────────────────┐
                        │              VITS Model (ONNX)                          │
                        │  Input: phoneme_ids, a1, a2, a3 (split from ProsodyFlat)│
                        │  Output: NativeArray<float> audio waveform              │
                        └─────────────────────────────────────────────────────────┘
                                                       │
                                                       ↓
                        ┌─────────────────────────────────────────────────────────┐
                        │  AudioNormalizer.NormalizeInPlace(NativeArray<float>)   │
                        │  -> AudioClipBuilder.BuildAudioClip(NativeArray<float>) │
                        │  -> AudioClip (TTS_{Guid:N})                           │
                        └─────────────────────────────────────────────────────────┘
```

### Prosody Data Format (ProsodyFlat stride=3)

In v2.0, prosody data is managed uniformly as flat arrays:
```
ProsodyFlat = [a1_0, a2_0, a3_0, a1_1, a2_1, a3_1, ...]
Length = Phonemes.Length * PhonemeEncoder.ProsodyStride (=3)
```

### Language-Specific Prosody Mapping

| Language | A1 | A2 | A3 |
|----------|----|----|-----|
| ja | Mora position | Accent nucleus position | Accent phrase position |
| en | 0 | 0 | 0 |
| zh | Tone (1-5) | Syllable position | Word length |
| ko | 0 | 0 | Syllable count |
| es/fr/pt | 0 | Stress (0/2) | Phoneme count in word |

### Usage Example (v2.0 API)

```csharp
// PhonemizeAsync -> SynthesisRequest -> SynthesizeAsync pipeline
var result = await piperTTS.PhonemizeAsync("konnichiwa");
// result.Phonemes: phoneme array
// result.ProsodyFlat: stride=3 flat array
// result.DetectedLanguage: "ja"
// result.ResolvedLanguageId: 0

// Build request with prosody
var request = SynthesisRequest.FromPhonemesWithProsody(
    result.Phonemes, result.ProsodyFlat, lengthScale: 0.8f);
var clip = await piperTTS.SynthesizeAsync(request);

// Direct phoneme input (no prosody)
var request2 = SynthesisRequest.FromPhonemes(
    new[] { "k", "o", "N_uvular", "n", "i", "ch", "w", "a" });
var clip2 = await piperTTS.SynthesizeAsync(request2);
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

### DictionaryPriority

Priority level constants for dictionary entries (`public static class`, nested within `CustomDictionary`):

| Constant | Value | Usage |
|----------|-------|-------|
| `Low` | 3 | Low priority (fallback) |
| `Default` | 5 | Standard priority |
| `High` | 7 | High priority |
| `Override` | 9 | Override (for cross-dictionary overwriting) |
| `Always` | 10 | Highest priority (always applied) |

### Batch Add API

```csharp
// Add a single word
dict.AddWord("MyTerm", "マイターム", priority: DictionaryPriority.High);

// Batch add multiple words
dict.AddWords(new[]
{
    ("Docker", "ドッカー", DictionaryPriority.Override),
    ("GitHub", "ギットハブ", DictionaryPriority.Override),
});
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
- **WebGL**: Supported (dedicated components for file I/O and threading constraint workarounds. Uses GPUCompute on WebGPU, GPUPixel on WebGL2)

### Platform-Specific Implementation

All G2P backends use DotNetG2P packages (pure C# implementations), so no platform-specific native libraries are required.

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
- WebGPU support: `PlatformHelper.IsWebGPU` detects WebGPU environments and automatically switches the Inference Backend
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

### Inference Backend Selection (WebGPU Support)

In WebGL environments, the Inference Backend is automatically selected based on the browser's graphics API:

| Environment | `InferenceBackend.Auto` Selection | Reason |
|-------------|----------------------------------|--------|
| WebGPU | GPUCompute | Higher performance via compute shader support |
| WebGL2 | GPUPixel | Pixel shader inference due to lack of compute shader support |

This detection uses the `PlatformHelper.IsWebGPU` property:

```csharp
// PlatformHelper.cs
public static bool IsWebGPU =>
    IsWebGL && SystemInfo.graphicsDeviceType == GraphicsDeviceType.WebGPU;
```

When `GPUCompute` is explicitly specified, it is allowed on WebGPU environments, but falls back to `GPUPixel` on WebGL2 for compatibility.

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
1. **Audio Synthesis Caching**: LRU cache via `AudioSynthesisCache` (FNV-1a hash, dual eviction)
2. **Model Quantization**: Optional INT8 quantization
3. **GPU Acceleration**: Supported via Unity AI Inference Engine
4. **Silence-based Splitting**: Per-phrase inference via `ISplitInferenceOrchestrator` for long texts

## Extension Points

### Custom Language Handlers (Recommended)
Implement `ILanguageG2PHandler` and register via `MultilingualPhonemizerOptions.Handlers`:
```csharp
public interface ILanguageG2PHandler : IDisposable
{
    string LanguageCode { get; }
    bool IsInitialized { get; }
    Task InitializeAsync(CancellationToken cancellationToken = default);
    (string[] Phonemes, int[] ProsodyFlat) Process(string text);
}
```

### Custom Language Detection
Implement `ILanguageDetector` and inject via `MultilingualPhonemizerOptions.LanguageDetector`:
```csharp
public interface ILanguageDetector
{
    IReadOnlyList<(string language, string text)> SegmentText(string text);
    string DefaultLatinLanguage { get; }
    IReadOnlyList<string> Languages { get; }
}
```

### Legacy Backend (Test Stubs)
`IPhonemizerBackend` interface (used only for test stubs)

### Voice Model Support
- Place ONNX models in `Resources/Models/` (Unity InferenceEngine)
- Configure via `PiperVoiceConfig`

### Language Extensions
1. Implement `ILanguageG2PHandler` (return ProsodyFlat stride=3 from Process())
2. Register via `MultilingualPhonemizerOptions.Handlers`
3. Add phoneme mappings to PuaTokenMapper (via pua.json or Register())
4. Train or obtain compatible VITS model

## Security Considerations

- All processing occurs locally (no cloud dependencies)
- No personal data collection
- Models and dictionaries are read-only
- Sandboxed execution in Unity environment

## Related Documentation

- [Troubleshooting](TROUBLESHOOTING.md)
- [Performance Tuning](PERFORMANCE_TUNING.md)
- [Config Reference](CONFIG_REFERENCE.md)
- Platform Setup Guides:
  - [iOS](platforms/ios/SETUP.md)
  - [Android](platforms/android/SETUP.md)
  - [macOS](platforms/macos/SETUP.md)
  - [WebGL](platforms/webgl/SETUP.md)