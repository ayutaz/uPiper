#!/bin/bash

# Test Android build for arm64-v8a
set -e

echo "=== Testing Android Build (arm64-v8a) ==="

# Clean and create build directory
rm -rf build_android_arm64-v8a
mkdir build_android_arm64-v8a
cd build_android_arm64-v8a

# Configure
cmake -DCMAKE_TOOLCHAIN_FILE="$ANDROID_NDK_HOME/build/cmake/android.toolchain.cmake" \
      -DANDROID_ABI=arm64-v8a \
      -DANDROID_PLATFORM=android-21 \
      -DANDROID_STL=c++_shared \
      -DCMAKE_BUILD_TYPE=Release \
      -DBUILD_TESTS=OFF \
      -DBUILD_BENCHMARK=OFF \
      ..

# Build
make VERBOSE=1

# Check output
if [ -f lib/libopenjtalk_wrapper.so ]; then
    echo "Success! Library built: lib/libopenjtalk_wrapper.so"
    ls -la lib/
else
    echo "Error: Library not found"
    exit 1
fi