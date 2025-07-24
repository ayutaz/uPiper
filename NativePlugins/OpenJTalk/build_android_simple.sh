#!/bin/bash

# Simplified build script for Android platforms
# This version is optimized for CI/CD environments

set -e

# Script directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
cd "$SCRIPT_DIR"

# Check if running in Docker
if [ -f /.dockerenv ]; then
    echo "Running in Docker container"
    export ANDROID_NDK_HOME=/opt/android-ndk
else
    if [ -z "$ANDROID_NDK_HOME" ]; then
        echo "Error: ANDROID_NDK_HOME environment variable is not set"
        exit 1
    fi
fi

echo "Using Android NDK at: $ANDROID_NDK_HOME"

# Android ABIs to build for
ABIS=("armeabi-v7a" "arm64-v8a" "x86" "x86_64")

# Override ABIs if specific one requested
if [ ! -z "$TARGET_ABI" ]; then
    ABIS=("$TARGET_ABI")
fi

# Ensure dependencies are fetched
if [ ! -d "external/open_jtalk-1.11" ]; then
    echo "Dependencies not found. Please run fetch_dependencies.sh first"
    exit 1
fi

# Build for each ABI
for ABI in "${ABIS[@]}"; do
    echo ""
    echo "================================================"
    echo "Building OpenJTalk wrapper for Android $ABI..."
    echo "================================================"
    
    # Check if already built
    if [ -f "output/android/$ABI/libopenjtalk_wrapper.so" ]; then
        echo "Library already exists for $ABI, skipping..."
        continue
    fi
    
    # Create build directory
    mkdir -p build_android_$ABI
    cd build_android_$ABI
    
    # Configure with Android toolchain
    cmake -DCMAKE_TOOLCHAIN_FILE=$ANDROID_NDK_HOME/build/cmake/android.toolchain.cmake \
          -DANDROID_ABI=$ABI \
          -DANDROID_PLATFORM=android-21 \
          -DANDROID_STL=c++_shared \
          -DCMAKE_BUILD_TYPE=Release \
          -DBUILD_TESTS=OFF \
          -DBUILD_BENCHMARK=OFF \
          ..
    
    # Build
    make -j$(nproc)
    
    # Copy output
    mkdir -p ../output/android/$ABI
    cp lib/libopenjtalk_wrapper.so ../output/android/$ABI/
    
    # Verify the library
    echo "Verifying library..."
    file ../output/android/$ABI/libopenjtalk_wrapper.so
    ls -la ../output/android/$ABI/libopenjtalk_wrapper.so
    
    cd ..
done

echo ""
echo "Android build completed successfully!"
echo "Libraries are located in: output/android/"
ls -la output/android/