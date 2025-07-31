#!/bin/bash

set -e

echo "==================================="
echo "Building Flite for Unity"
echo "==================================="

# Detect OS
OS=$(uname -s)
ARCH=$(uname -m)

# Create build directory
mkdir -p build
cd build

# Configure based on OS
if [[ "$OS" == "Darwin" ]]; then
    echo "Building for macOS (Universal Binary)..."
    cmake .. -DCMAKE_BUILD_TYPE=MinSizeRel \
             -DCMAKE_OSX_ARCHITECTURES="x86_64;arm64"
    PLUGIN_DIR="../../../../Assets/uPiper/Plugins/macOS"
    PLUGIN_EXT="dylib"
elif [[ "$OS" == "Linux" ]]; then
    echo "Building for Linux..."
    cmake .. -DCMAKE_BUILD_TYPE=MinSizeRel
    PLUGIN_DIR="../../../../Assets/uPiper/Plugins/Linux/x86_64"
    PLUGIN_EXT="so"
else
    echo "Unsupported OS: $OS"
    exit 1
fi

# Build
echo "Building..."
make -j$(nproc 2>/dev/null || echo 4)

# Create plugin directory
mkdir -p "$PLUGIN_DIR"

# Copy library
if [[ "$OS" == "Darwin" ]]; then
    cp libflite_unity.dylib "$PLUGIN_DIR/"
    echo "Installed to: $PLUGIN_DIR/libflite_unity.dylib"
else
    cp libflite_unity.so "$PLUGIN_DIR/"
    echo "Installed to: $PLUGIN_DIR/libflite_unity.so"
fi

echo "==================================="
echo "Build completed successfully!"
echo "==================================="

cd ..