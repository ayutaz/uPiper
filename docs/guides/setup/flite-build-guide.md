# Flite Native Library Build Guide

## Overview

Flite (Festival-Lite) is a small, fast run-time speech synthesis engine developed at CMU. It's licensed under a BSD-style license, making it suitable for commercial use.

## Why Flite?

- **License**: BSD-style (commercial-friendly)
- **Size**: Small footprint (~2-5 MB per platform)
- **Speed**: Fast synthesis
- **Quality**: Good quality for its size
- **Languages**: Primarily English, but extensible

## Build Instructions by Platform

### Prerequisites

```bash
# Common tools needed
git clone https://github.com/festvox/flite.git
cd flite
```

### Windows Build

```batch
# Using Visual Studio 2019 or later

# 1. Install prerequisites
# - Visual Studio with C++ development tools
# - CMake (optional, for easier building)

# 2. Build using provided Windows makefiles
cd flite/windows
nmake /f Makefile.msvc

# 3. Output location
# - flite.dll will be in windows/build/
# - Copy to: Assets/uPiper/Plugins/Windows/x86_64/flite.dll
```

### macOS Build

```bash
# Universal Binary (Intel + Apple Silicon)

# 1. Configure for universal build
./configure --enable-shared \
    CFLAGS="-arch x86_64 -arch arm64" \
    LDFLAGS="-arch x86_64 -arch arm64"

# 2. Build
make

# 3. Create universal library
lipo -create \
    build/x86_64/libflite.dylib \
    build/arm64/libflite.dylib \
    -output libflite_unity.dylib

# 4. Copy to Unity
cp libflite_unity.dylib Assets/uPiper/Plugins/macOS/
```

### Linux Build

```bash
# Standard Linux build

# 1. Configure
./configure --enable-shared --prefix=/usr/local

# 2. Build
make -j$(nproc)

# 3. Output
# libflite.so will be in build/lib/
cp build/lib/libflite.so Assets/uPiper/Plugins/Linux/x86_64/
```

### Android Build

```bash
# Using Android NDK

# 1. Set up environment
export ANDROID_NDK_HOME=/path/to/android-ndk
export PATH=$ANDROID_NDK_HOME/toolchains/llvm/prebuilt/linux-x86_64/bin:$PATH

# 2. Build for each architecture
# ARM64-v8a
./configure --host=aarch64-linux-android \
    --enable-shared \
    CC=aarch64-linux-android21-clang \
    CXX=aarch64-linux-android21-clang++

make clean && make

# Copy output
cp libflite.so Assets/uPiper/Plugins/Android/ARM64/

# 3. Repeat for ARMv7
./configure --host=armv7a-linux-androideabi \
    --enable-shared \
    CC=armv7a-linux-androideabi21-clang

make clean && make
cp libflite.so Assets/uPiper/Plugins/Android/ARMv7/
```

### iOS Build

```bash
# Build for iOS (requires macOS)

# 1. Build for device
./configure --host=arm-apple-darwin \
    --enable-static \
    CC="xcrun -sdk iphoneos clang -arch arm64" \
    CFLAGS="-isysroot $(xcrun -sdk iphoneos --show-sdk-path)"

make clean && make

# 2. Build for simulator
./configure --host=x86_64-apple-darwin \
    --enable-static \
    CC="xcrun -sdk iphonesimulator clang -arch x86_64" \
    CFLAGS="-isysroot $(xcrun -sdk iphonesimulator --show-sdk-path)"

make clean && make

# 3. Create universal library
lipo -create libflite_arm64.a libflite_x86_64.a -output libflite_unity.a
```

## Unity Integration

### Plugin Settings

After copying the libraries, configure in Unity:

1. **Windows (flite.dll)**:
   - Platform: Windows
   - CPU: x86_64
   - OS: Windows

2. **macOS (libflite_unity.dylib)**:
   - Platform: macOS
   - CPU: AnyCPU

3. **Linux (libflite.so)**:
   - Platform: Linux
   - CPU: x86_64

4. **Android (libflite.so)**:
   - Platform: Android
   - CPU: ARM64 or ARMv7

### C# Wrapper Implementation

```csharp
using System;
using System.Runtime.InteropServices;

namespace uPiper.Phonemizers.Backend.Flite
{
    public static class FliteNative
    {
        #if UNITY_IOS
            const string FLITE_LIB = "__Internal";
        #else
            const string FLITE_LIB = "flite";
        #endif

        [DllImport(FLITE_LIB)]
        public static extern IntPtr flite_init();

        [DllImport(FLITE_LIB)]
        public static extern void flite_cleanup(IntPtr flite);

        [DllImport(FLITE_LIB)]
        public static extern IntPtr flite_text_to_phones(
            IntPtr flite, 
            string text, 
            string language);

        [DllImport(FLITE_LIB)]
        public static extern void flite_free_phones(IntPtr phones);
    }
}
```

## Minimal Flite Implementation

If you only need phonemization (not full TTS), you can build a minimal version:

```c
// minimal_flite.c - Just phonemization, no audio synthesis
#include "flite.h"

typedef struct {
    cst_voice *voice;
    cst_lexicon *lex;
    cst_lts_rules *lts;
} flite_phonemizer;

flite_phonemizer* flite_phonemizer_init() {
    flite_init();
    flite_phonemizer *fp = malloc(sizeof(flite_phonemizer));
    fp->lex = cmu_lex_init();
    fp->lts = cmu_lts_rules_init();
    return fp;
}

char* flite_phonemizer_process(flite_phonemizer *fp, const char *text) {
    // Convert text to phonemes using lexicon and LTS rules
    // Return ARPABET string
}
```

## Size Optimization

To reduce library size:

1. **Remove unused voices**: Only include phonemization code
2. **Strip symbols**: `strip -S libflite.so`
3. **Disable features**: Configure with minimal options
   ```bash
   ./configure --disable-audio --disable-lang-usenglish \
               --disable-lang-cmulex --enable-lang-cmu_us_kal
   ```

## Testing the Build

```csharp
[Test]
public void FliteNative_ShouldLoadSuccessfully()
{
    IntPtr flite = IntPtr.Zero;
    
    try
    {
        flite = FliteNative.flite_init();
        Assert.AreNotEqual(IntPtr.Zero, flite, "Flite should initialize");
        
        string testPhones = Marshal.PtrToStringAnsi(
            FliteNative.flite_text_to_phones(flite, "hello world", "en-US")
        );
        
        Assert.IsNotEmpty(testPhones, "Should return phonemes");
    }
    finally
    {
        if (flite != IntPtr.Zero)
            FliteNative.flite_cleanup(flite);
    }
}
```

## Alternative: Flite-Unity Package

Consider creating a Unity Package with pre-built binaries:

```json
{
  "name": "com.upiper.flite",
  "version": "1.0.0",
  "displayName": "Flite for uPiper",
  "description": "Pre-built Flite libraries for all platforms",
  "unity": "2021.3",
  "dependencies": {},
  "author": {
    "name": "uPiper Team"
  }
}
```

This allows users to simply import the package without building.