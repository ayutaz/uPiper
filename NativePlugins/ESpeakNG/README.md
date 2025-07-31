# eSpeak-NG Unity Native Plugin

This directory contains the eSpeak-NG integration for uPiper, providing high-quality English and multilingual phonemization.

## Overview

eSpeak-NG is a compact, open-source text-to-speech synthesizer that supports over 100 languages and accents. For uPiper, we use it specifically for phoneme generation.

## Directory Structure

```
ESpeakNG/
├── src/                    # Native wrapper source code
│   └── espeak_wrapper.c
├── include/               # Header files
│   └── espeak_wrapper.h
├── external/              # eSpeak-NG source (submodule)
│   └── espeak-ng/
├── build/                 # Build output directory
├── scripts/               # Build and utility scripts
├── CMakeLists.txt        # CMake configuration
├── build.bat             # Windows build script
└── build.sh              # Unix build script
```

## Supported Platforms

- Windows (x64)
- macOS (Universal Binary: arm64 + x86_64)
- Linux (x64)
- Android (arm64-v8a, armeabi-v7a, x86, x86_64)

## Building

### Windows
```bash
build.bat
```

### macOS/Linux
```bash
./build.sh
```

### Android
```bash
./scripts/build_android.sh
```

## Features

- Fast and accurate phoneme generation
- Support for 100+ languages
- Lightweight native library
- Thread-safe implementation
- Compatible with Unity's IL2CPP

## API

The wrapper provides a simple C API:

```c
// Initialize eSpeak-NG
int espeak_wrapper_initialize(const char* data_path);

// Convert text to phonemes
ESpeakResult* espeak_wrapper_phonemize(
    const char* text,
    const char* language,
    int voice_variant
);

// Free result memory
void espeak_wrapper_free_result(ESpeakResult* result);

// Terminate eSpeak-NG
void espeak_wrapper_terminate();
```

## License

eSpeak-NG is licensed under GPL v3. The wrapper code is licensed under MIT to maintain compatibility with Unity projects.