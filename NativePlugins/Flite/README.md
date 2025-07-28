# Flite Unity Integration

This directory contains the Flite (Festival Lite) native library integration for uPiper.

## About Flite

Flite (Festival Lite) is a small, fast run-time speech synthesis engine developed at CMU. We use it specifically for its Letter-to-Sound (LTS) capabilities to improve English phonemization.

**License**: BSD-style license (see LICENSE file)

## Building

### Prerequisites

- CMake 3.10 or higher
- C compiler (Visual Studio 2019+ on Windows, GCC/Clang on Linux, Xcode on macOS)
- Git (for downloading Flite source)

### Steps

1. **Download Flite source code**:
   ```bash
   ./download_flite.sh  # On macOS/Linux
   ```

2. **Build the library**:
   ```bash
   # Windows
   build.bat
   
   # macOS/Linux
   chmod +x build.sh
   ./build.sh
   ```

3. The built library will be automatically copied to the Unity plugins folder.

## Features

This integration provides:

- Text-to-phoneme conversion using Flite's LTS rules
- CMU lexicon lookups
- Support for out-of-vocabulary words
- Minimal build (LTS only, no audio synthesis)

## API

The C API exposes these functions:

```c
// Initialize Flite
flite_unity_context* flite_unity_init();

// Convert text to phonemes
char* flite_unity_text_to_phones(flite_unity_context* ctx, const char* text);

// Check if word exists in lexicon
int flite_unity_word_in_lexicon(flite_unity_context* ctx, const char* word);

// Apply LTS rules to a word
char* flite_unity_lts_apply(flite_unity_context* ctx, const char* word);

// Free allocated string
void flite_unity_free_string(char* str);

// Cleanup
void flite_unity_cleanup(flite_unity_context* ctx);
```

## Unity Integration

The library is accessed through P/Invoke in `FliteNative.cs` and wrapped by `FlitePhonemizerBackend.cs`.

## Troubleshooting

### Build fails with "Flite source not found"
Run `download_flite.sh` first to download the Flite source code.

### CMake not found
Install CMake from https://cmake.org/

### Missing compiler
- Windows: Install Visual Studio 2019 or later with C++ support
- macOS: Install Xcode Command Line Tools
- Linux: Install build-essential package