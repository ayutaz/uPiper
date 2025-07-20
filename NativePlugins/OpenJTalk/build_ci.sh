#!/bin/bash
set -e

echo "=== OpenJTalk Native Build for CI ==="

# Detect platform
if [[ "$OSTYPE" == "linux-gnu"* ]]; then
    PLATFORM="linux"
elif [[ "$OSTYPE" == "darwin"* ]]; then
    PLATFORM="macos"
elif [[ "$OSTYPE" == "msys" ]] || [[ "$OSTYPE" == "cygwin" ]]; then
    PLATFORM="windows"
else
    echo "Unsupported platform: $OSTYPE"
    exit 1
fi

echo "Platform detected: $PLATFORM"

# Create build directory
BUILD_DIR="build"
if [ -d "$BUILD_DIR" ]; then
    rm -rf "$BUILD_DIR"
fi
mkdir -p "$BUILD_DIR"
cd "$BUILD_DIR"

echo "=== Using CI Mock Implementation ==="

# Copy CI CMakeLists
cp ../CMakeLists_ci.txt ../CMakeLists.txt

# Configure with CMake
cmake -DCMAKE_BUILD_TYPE=Release ..

# Build
if [[ "$PLATFORM" == "windows" ]]; then
    cmake --build . --config Release --parallel
else
    make -j$(nproc || sysctl -n hw.ncpu || echo 2)
fi

echo "=== Build completed successfully ==="

# List built files
echo "Built files:"
find . -name "*.so" -o -name "*.dylib" -o -name "*.dll" | sort

cd ..