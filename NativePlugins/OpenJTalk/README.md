# OpenJTalk Native Library for uPiper

This directory contains the native OpenJTalk implementation for the uPiper Unity plugin.

## Overview

OpenJTalk is a Japanese text-to-speech synthesis system that provides:
- High-quality Japanese phonemization using Mecab
- Accurate prosody prediction
- Support for various Japanese text formats

## Dependencies

- OpenJTalk 1.11
- Mecab 0.996
- hts_engine API 1.10
- mecab-naist-jdic dictionary

## Build Instructions

### Prerequisites

#### Windows
- Visual Studio 2019 or later
- CMake 3.10+

#### Linux
- GCC 7+ or Clang 6+
- CMake 3.10+

#### macOS
- Xcode Command Line Tools
- CMake 3.10+

### Building

```bash
# Windows
build.bat Release x64

# Linux/macOS
./build.sh Release
```

## Architecture

The library provides a C API wrapper around OpenJTalk's C++ implementation:

```
Unity (C#)
    ↓ P/Invoke
openjtalk_wrapper (C API)
    ↓
OpenJTalk (C++)
    ├── Mecab (morphological analyzer)
    ├── hts_engine (speech synthesis)
    └── Dictionary files
```

## API

See `include/openjtalk_wrapper.h` for the complete API documentation.

### Core Functions

```c
// Initialize/cleanup
void* openjtalk_initialize(const char* dict_path);
void openjtalk_destroy(void* handle);

// Basic phonemization
char* openjtalk_phonemize(void* handle, const char* text);
void openjtalk_free_string(char* str);
```

### Prosody API

Extract phonemes with A1/A2/A3 prosody values from full-context labels:

```c
// Prosody result structure
typedef struct {
    char* phonemes;      // Space-separated phoneme string
    int* prosody_a1;     // Relative position from accent nucleus
    int* prosody_a2;     // Mora position in accent phrase (1-based)
    int* prosody_a3;     // Total morae in accent phrase
    int phoneme_count;   // Number of phonemes
} ProsodyPhonemeResult;

// Get phonemes with prosody information
ProsodyPhonemeResult* openjtalk_phonemize_with_prosody(void* handle, const char* text);

// Free prosody result
void openjtalk_free_prosody_result(ProsodyPhonemeResult* result);
```

#### Prosody Values

| Value | Description |
|-------|-------------|
| **A1** | Relative position from accent nucleus (can be negative) |
| **A2** | Mora position within accent phrase (1-based) |
| **A3** | Total number of morae in accent phrase |

These values are extracted from OpenJTalk's full-context labels in the format:
```
xx^xx-phoneme+xx=xx/A:a1+a2+a3/B:...
```

## Docker Build

Cross-platform builds are available via Docker:

```bash
# Windows (MinGW)
docker build -f Dockerfile.windows -t openjtalk-windows .
docker run -v $(pwd)/output_windows:/output openjtalk-windows

# Linux
docker build -f Dockerfile.linux -t openjtalk-linux .
docker run -v $(pwd)/output_linux:/output openjtalk-linux

# Android (all ABIs)
docker build -f Dockerfile.android -t openjtalk-android .
docker run -v $(pwd)/output:/NativePlugins/OpenJTalk/output openjtalk-android
```

## License

This wrapper is part of uPiper and follows the same license.
OpenJTalk and its dependencies have their own licenses - see external/licenses/ for details.