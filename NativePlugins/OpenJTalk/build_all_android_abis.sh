#!/bin/bash

# Build OpenJTalk wrapper for all Android ABIs
set -e

echo "=== Building OpenJTalk Wrapper for All Android ABIs ==="

# Array of ABIs to build
ABIS=("arm64-v8a" "armeabi-v7a" "x86" "x86_64")

# Create output directory
mkdir -p output/android

# Build for each ABI
for ABI in "${ABIS[@]}"; do
    echo ""
    echo "=== Building for $ABI ==="
    
    # Clean previous build
    rm -rf build_android_$ABI
    mkdir build_android_$ABI
    cd build_android_$ABI
    
    # Configure
    cmake -DCMAKE_TOOLCHAIN_FILE="$ANDROID_NDK_HOME/build/cmake/android.toolchain.cmake" \
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
    if [ -f lib/libopenjtalk_wrapper.so ]; then
        echo "Success! Copying to output/android/$ABI/"
        mkdir -p ../output/android/$ABI
        cp lib/libopenjtalk_wrapper.so ../output/android/$ABI/
        echo "Library size: $(ls -lh lib/libopenjtalk_wrapper.so | awk '{print $5}')"
    else
        echo "Error: Library not found for $ABI"
        exit 1
    fi
    
    cd ..
done

echo ""
echo "=== Build Summary ==="
echo "All ABIs built successfully!"
echo ""
echo "Output libraries:"
find output/android -name "*.so" -exec ls -lh {} \;