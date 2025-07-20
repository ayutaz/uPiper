#!/bin/bash

# Build script for OpenJTalk wrapper with actual OpenJTalk integration
# This script builds a complete OpenJTalk implementation for Unity

set -e

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
BUILD_TYPE=${1:-Release}
PLATFORM=$(uname -s)

echo "Building OpenJTalk wrapper with full implementation..."
echo "Platform: $PLATFORM"
echo "Build type: $BUILD_TYPE"

# First, fetch dependencies if not present
if [ ! -d "$SCRIPT_DIR/external/hts_engine_API-1.10" ] || [ ! -d "$SCRIPT_DIR/external/open_jtalk-1.11" ]; then
    echo "Dependencies not found. Fetching..."
    cd "$SCRIPT_DIR"
    ./fetch_dependencies.sh
fi

# Create build directory
BUILD_DIR="$SCRIPT_DIR/build/full/$PLATFORM"
mkdir -p "$BUILD_DIR"
cd "$BUILD_DIR"

# Configure with CMake using the full implementation
cmake -DCMAKE_BUILD_TYPE=$BUILD_TYPE \
      -DCMAKE_INSTALL_PREFIX="$SCRIPT_DIR/output/full/$PLATFORM" \
      -DBUILD_TESTS=ON \
      "$SCRIPT_DIR" \
      -C "$SCRIPT_DIR/CMakeLists_full.txt"

# Build
if [ "$PLATFORM" = "Darwin" ]; then
    make -j$(sysctl -n hw.ncpu)
else
    make -j$(nproc)
fi

# Install
make install

echo "Build complete!"
echo "Libraries installed to: $SCRIPT_DIR/output/full/$PLATFORM"

# Copy to Unity plugin directory if in Unity project
UNITY_PLUGIN_DIR="$SCRIPT_DIR/../../Plugins"
if [ -d "$UNITY_PLUGIN_DIR" ]; then
    echo "Copying to Unity Plugins directory..."
    
    if [ "$PLATFORM" = "Darwin" ]; then
        mkdir -p "$UNITY_PLUGIN_DIR/macOS"
        cp "$SCRIPT_DIR/output/full/$PLATFORM/lib/"*.dylib "$UNITY_PLUGIN_DIR/macOS/" || true
    elif [ "$PLATFORM" = "Linux" ]; then
        ARCH=$(uname -m)
        if [ "$ARCH" = "x86_64" ]; then
            mkdir -p "$UNITY_PLUGIN_DIR/Linux/x86_64"
            cp "$SCRIPT_DIR/output/full/$PLATFORM/lib/"*.so "$UNITY_PLUGIN_DIR/Linux/x86_64/" || true
        fi
    fi
    
    # Copy dictionary files
    mkdir -p "$UNITY_PLUGIN_DIR/../StreamingAssets/OpenJTalk/dic"
    cp -r "$SCRIPT_DIR/output/full/$PLATFORM/share/openjtalk/dic/"* "$UNITY_PLUGIN_DIR/../StreamingAssets/OpenJTalk/dic/" || true
fi

echo "Build and installation complete!"