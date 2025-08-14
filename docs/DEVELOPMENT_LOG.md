# Development Log

This document records the development progress and milestones achieved during the initial development of uPiper.

> **Note**: This is not a formal changelog. The official CHANGELOG.md will be created upon the first public release.

## [Unreleased]

### Chinese Language Support Phase 1 - 2025-08-01

#### Added
- **Chinese Phonemization System** (Phase 1)
  - Dictionary data system with JSON format (364 characters, 30 phrases, 217 IPA mappings)
  - Asynchronous dictionary loader with Android support and fallback mechanism
  - Basic pinyin converter with phrase matching (up to 4 characters)
  - IPA converter with Mandarin tone marks (˥, ˧˥, ˨˩˦, ˥˩)
  - Text normalizer supporting number conversion and mixed Chinese-English text
  - Basic multi-tone character rules (不, 一, 了)
  - Number conversion with proper zero handling (e.g., 10001 → 一万零一)

- **Unity Integration**
  - InferenceEngineDemo updated with Chinese language support
  - Added zh_CN-huayan-medium model configuration
  - Chinese test phrases for easy testing
  - Model switching automatically updates available phrases

- **Testing Infrastructure**
  - Comprehensive test suite for Chinese phonemization
  - Performance tests targeting 50ms for 100 characters
  - Memory usage tests (under 10MB target)

#### Technical Details
- Created `uPiper.Core.Phonemizers.Backend.Chinese` namespace
- Implemented modular architecture with separate components for each stage
- Used Unicode escape sequences for special characters (\u2018, \u2019)
- Python script for dictionary conversion using `uv` runtime
- Proper namespace resolution for PhonemizerBackendOptions

#### Known Limitations
- Dictionary size limited to 364 characters (full implementation needs 20,000+)
- Word segmentation is character-based only (no jieba-style segmentation yet)
- Limited multi-tone character support (only 3 characters)
- Simplified Chinese only (no Traditional Chinese support)

### Phase 3 - Flite LTS Integration - 2025-01-31

#### Added
- **Flite LTS Engine** (Phase 3)
  - Pure C# implementation of Flite Letter-to-Sound rules
  - 25,000+ phoneme transformation rules ported
  - WFST (Weighted Finite State Transducer) based rule engine
  - English phonemization accuracy improved from 60-70% to 90%+
  - No native library dependencies (BSD licensed)

- **CI/CD Improvements**
  - Unity CLI direct execution for Windows/macOS (Docker not available)
  - Automated Unity Hub/Editor installation scripts
  - Platform-specific workarounds for GitHub Actions
  - Windows Unity Test Runner fully functional
  - macOS Unity Test Runner temporarily disabled (license machine binding issue)

#### Changed
- All tests converted from `[UnityTest]` to `[Test]` for EditMode execution
- P/Invoke implementations throw `NotSupportedException` on unsupported platforms
- Test execution speed significantly improved (no PlayMode build required)

#### Fixed
- CMUDictionary loading errors (improved parsing)
- Test timeout issues resolved
- PhonemeResult class implementation completed
- C# code formatting errors
- Unity Hub DMG mount point detection on macOS
- Unity Hub architecture selection prompt on macOS

### Phase 1 Complete - 2025-01-23

#### Added
- **GPU Inference Support** (Phase 1.13)
  - Configurable backend selection (Auto, CPU, GPUCompute, GPUPixel)
  - Automatic platform detection and CPU fallback
  - Metal issue workaround for macOS
  - GPU-specific settings (batch size, FP16, memory limits)
  
- **Advanced Samples**
  - Streaming TTS Demo - Real-time streaming audio generation
  - Multi-Voice Demo - Up to 4 simultaneous voices
  - Realtime TTS Demo - Low-latency (<100ms) audio generation

- **IL2CPP Support** (Phase 1.12)
  - Full Mono and IL2CPP scripting backend support
  - Platform-specific optimizations
  - Comprehensive CI/CD integration
  - Type preservation via link.xml

- **Unity Package Manager Integration** (Phase 1.11)
  - Proper package.json configuration
  - Sample import support
  - Dependency management

- **Japanese Pronunciation** (Phase 1.10)
  - OpenJTalk integration for accurate phonemization
  - PUA character mapping for multi-character phonemes
  - pyopenjtalk-compatible implementation

- **Unity.InferenceEngine Integration** (Phase 1.9)
  - ONNX model inference support
  - Piper VITS model compatibility
  - Audio generation from phonemes

- **Native OpenJTalk Library** (Phase 1.7-1.8)
  - Full mecab-naist-jdic dictionary support (789,120 entries)
  - Cross-platform builds (Windows/Linux/macOS)
  - P/Invoke bindings for Unity

- **Phonemizer System** (Phase 1.4-1.6)
  - Extensible phonemizer interface
  - LRU caching for performance
  - Multi-language text normalization
  - MockPhonemizer for testing

- **Core API** (Phase 1.1-1.3)
  - Complete TTS interface (IPiperTTS)
  - Async/await pattern support
  - Streaming generation (IAsyncEnumerable)
  - Comprehensive configuration system
  - Event system for state changes

#### Changed
- Updated to Unity 6000.0.55f1
- Migrated from deprecated Unity APIs (BuildTargetGroup → NamedBuildTarget)
- Enhanced error handling with custom exception hierarchy

#### Fixed
- Unity API deprecation warnings
- Generic type compilation errors
- Docker IL2CPP build limitations
- Windows Unity cache directory issues
- Metal shader compilation errors (via CPU fallback)

#### Technical Details
- **Test Coverage**: 250+ tests, all passing
- **Platforms**: Windows, Linux, macOS fully supported
- **Performance**: <10ms phonemization, >10x realtime factor
- **CI/CD**: Complete GitHub Actions pipeline
- **Documentation**: Comprehensive guides and API references

### Known Limitations
- WebGL not supported (Unity.InferenceEngine limitation)
- Mobile platforms (iOS/Android) planned for Phase 2
- Metal GPU inference requires CPU fallback

---

## Phase 1 Completion Summary

Phase 1 is now complete with all planned features implemented and additional enhancements:

- **Original scope**: 22 person-days
- **Final scope**: 27 person-days
- **Additional features**: Unity integration, IL2CPP support, GPU inference

The project is now production-ready for desktop platforms with high-quality Japanese TTS support.