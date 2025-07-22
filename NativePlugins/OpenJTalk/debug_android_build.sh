#!/bin/bash

# Debug Android build issues
set -e

echo "=== Debugging Android Build ==="
echo ""

# Check library paths
echo "Checking Android libraries..."
ls -la external/openjtalk_build/android_arm64-v8a/lib/ || echo "Android libs not found"

# Check CMake configuration
echo ""
echo "CMake configuration check..."
cd build_android_arm64-v8a || mkdir -p build_android_arm64-v8a && cd build_android_arm64-v8a

# Clean build
rm -rf *

# Configure with verbose output
echo "Configuring CMake..."
cmake -DCMAKE_TOOLCHAIN_FILE="$ANDROID_NDK_HOME/build/cmake/android.toolchain.cmake" \
      -DANDROID_ABI=arm64-v8a \
      -DANDROID_PLATFORM=android-21 \
      -DANDROID_STL=c++_shared \
      -DCMAKE_BUILD_TYPE=Release \
      -DBUILD_TESTS=OFF \
      -DBUILD_BENCHMARK=OFF \
      -DCMAKE_VERBOSE_MAKEFILE=ON \
      ..

# Check if libraries are found
echo ""
echo "Checking CMakeCache for OPENJTALK_LIBS..."
grep -i "openjtalk" CMakeCache.txt | grep -i "lib" || echo "No OPENJTALK_LIBS found in cache"

# Try to build with debug output
echo ""
echo "Building with verbose output..."
make VERBOSE=1 -j1

cd ..