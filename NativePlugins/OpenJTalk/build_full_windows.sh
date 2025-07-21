#!/bin/bash
# Complete build script for Windows DLL using cross-compilation

set -e

echo "=== Building Full OpenJTalk Windows DLL ==="

# Clean previous builds
rm -rf build_windows output_windows
mkdir -p build_windows output_windows

# Setup toolchain
export CC=x86_64-w64-mingw32-gcc
export CXX=x86_64-w64-mingw32-g++
export AR=x86_64-w64-mingw32-ar
export RANLIB=x86_64-w64-mingw32-ranlib
export CFLAGS="-O3 -static-libgcc -static-libstdc++"
export CXXFLAGS="-O3 -static-libgcc -static-libstdc++"
export LDFLAGS="-static-libgcc -static-libstdc++ -static"

# First, ensure dependencies are fetched
if [ ! -d "external/openjtalk_build" ]; then
    ./fetch_dependencies_ci.sh
fi

# Build dependencies
./build_dependencies_cross.sh

# Build the wrapper DLL
cd build_windows

# Copy the wrapper source if not exists
if [ ! -f "../src/openjtalk_full_wrapper.c" ]; then
    cp ../src/openjtalk_wrapper.c ../src/openjtalk_full_wrapper.c
fi

# Use the cross-compilation specific CMakeLists
cp ../CMakeLists_windows_cross.txt CMakeLists.txt

# Configure with toolchain file
cmake . -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_TOOLCHAIN_FILE=../toolchain-mingw64.cmake

make -j$(nproc)

# Copy output
if [ -f bin/openjtalk_wrapper.dll ]; then
    cp bin/openjtalk_wrapper.dll ../output_windows/
    echo "DLL built successfully: $(ls -la bin/openjtalk_wrapper.dll)"
else
    echo "ERROR: DLL not found at bin/openjtalk_wrapper.dll"
    exit 1
fi
cd ..

echo "=== Build Complete ==="
echo "DLL Location: output_windows/openjtalk_wrapper.dll"
ls -la output_windows/