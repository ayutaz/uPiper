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

## License

This wrapper is part of uPiper and follows the same license.
OpenJTalk and its dependencies have their own licenses - see external/licenses/ for details.