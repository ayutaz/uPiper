# uPiper Native Libraries Build Guide

This directory contains the native C++ libraries for uPiper's phonemization support.

## Prerequisites

### All Platforms
- CMake 3.10 or higher
- C++11 compatible compiler

### Platform-specific Requirements

#### Windows
- Visual Studio 2019/2022 with C++ support OR
- MinGW-w64 OR
- MSYS2 with MinGW toolchain

#### Linux
- GCC or Clang
- Build essentials: `sudo apt-get install build-essential cmake`
- For cross-compilation to Windows: `sudo apt-get install mingw-w64`

#### macOS
- Xcode Command Line Tools: `xcode-select --install`
- CMake: `brew install cmake`
- For cross-compilation to Windows: `brew install mingw-w64`

## Building

### Quick Build (Current Platform Only)

#### Linux/macOS
```bash
cd Assets/uPiper/Native
./build.sh
```

#### Windows
```cmd
cd Assets\uPiper\Native
build.bat
```

### Build for All Platforms

#### Linux/macOS
```bash
cd Assets/uPiper/Native
./build.sh --all
```

This will:
1. Build for the current platform
2. Cross-compile for Windows (if MinGW is available)

### Manual Build

If you prefer to build manually:

```bash
cd Assets/uPiper/Native
mkdir Build
cd Build

# Configure
cmake .. -DCMAKE_BUILD_TYPE=Release

# Build
cmake --build . --config Release

# Libraries will be in ../Plugins/{Platform}/
```

## Output Locations

Built libraries are automatically copied to:
- Windows: `Assets/uPiper/Plugins/Windows/x86_64/openjtalk_wrapper.dll`
- Linux: `Assets/uPiper/Plugins/Linux/x86_64/libopenjtalk_wrapper.so`
- macOS: `Assets/uPiper/Plugins/macOS/libopenjtalk_wrapper.dylib`

## Unity Integration

The libraries are automatically detected by Unity when placed in the Plugins folder.
Platform settings are configured via .meta files.

## Troubleshooting

### Windows Build Issues
- If Visual Studio is not detected, install Visual Studio Build Tools
- For MinGW issues, ensure it's in your PATH
- Try using the "x64 Native Tools Command Prompt" for Visual Studio

### Linux Build Issues
- Install missing dependencies: `sudo apt-get install build-essential cmake`
- For permission issues: `chmod +x build.sh`

### macOS Build Issues
- Install Xcode Command Line Tools if missing
- For universal binary support, CMake automatically builds for both x86_64 and arm64

### Cross-compilation Issues
- Ensure MinGW-w64 is properly installed
- Check the toolchain file in `cmake/mingw-w64-x86_64.cmake`

## Adding New Native Libraries

To add a new native library:

1. Create a new directory under `Native/`
2. Add source files (.cpp, .h)
3. Update `CMakeLists.txt` to include the new library
4. Follow the same pattern as `openjtalk_wrapper`

## Dependencies

### OpenJTalk
The OpenJTalk wrapper expects the OpenJTalk binary to be installed on the system:
- Linux: `sudo apt-get install open-jtalk`
- macOS: `brew install open-jtalk`
- Windows: Download from official site or use pre-built binaries

### Environment Variables
- `OPENJTALK_PATH`: Path to OpenJTalk binary
- `OPENJTALK_DICTIONARY_DIR`: Path to OpenJTalk dictionary
- `PIPER_OFFLINE_MODE`: Set to "1" to disable auto-download features