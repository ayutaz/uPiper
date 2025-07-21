#!/bin/bash
# Complete build script for Windows DLL using cross-compilation

set -ex  # Enable verbose output and exit on error

echo "=== Building Full OpenJTalk Windows DLL ==="

# Clean previous builds
echo "=== Cleaning previous builds ==="
echo "Current directory at start: $(pwd)"
rm -rf build_windows output_windows

echo "=== Creating directories ==="
mkdir -p build_windows output_windows
echo "Created directories:"
ls -la | grep -E "(build_windows|output_windows)"

# Setup toolchain
export CC=x86_64-w64-mingw32-gcc
export CXX=x86_64-w64-mingw32-g++
export AR=x86_64-w64-mingw32-ar
export RANLIB=x86_64-w64-mingw32-ranlib
export CFLAGS="-O3 -static-libgcc -static-libstdc++"
export CXXFLAGS="-O3 -static-libgcc -static-libstdc++"
export LDFLAGS="-static-libgcc -static-libstdc++ -static"

# First, ensure dependencies are fetched
echo "=== Checking dependencies ==="
echo "Current directory: $(pwd)"
echo "Contents of current directory:"
ls -la
if [ ! -d "external/openjtalk_build" ]; then
    echo "Dependencies not found, fetching..."
    ./fetch_dependencies_ci.sh
else
    echo "Dependencies already exist"
fi

# Build dependencies
./build_dependencies_cross.sh

# Build the wrapper DLL
echo "=== Before cd to build_windows ==="
echo "Current directory: $(pwd)"
echo "Directory contents:"
ls -la
echo "Checking if build_windows exists:"
ls -la | grep build_windows || echo "build_windows not found"

cd build_windows || {
    echo "ERROR: Failed to cd to build_windows"
    echo "Current directory: $(pwd)"
    exit 2
}
echo "Current directory after cd: $(pwd)"

# Copy the wrapper source if not exists
if [ ! -f "../src/openjtalk_full_wrapper.c" ]; then
    if [ -f "../src/openjtalk_wrapper.c" ]; then
        cp ../src/openjtalk_wrapper.c ../src/openjtalk_full_wrapper.c
        echo "Created openjtalk_full_wrapper.c from openjtalk_wrapper.c"
    else
        echo "ERROR: openjtalk_wrapper.c not found"
        echo "Current directory: $(pwd)"
        ls -la ../src/ || true
        exit 1
    fi
fi

# Use the cross-compilation specific CMakeLists
if [ -f "../CMakeLists_windows_cross.txt" ]; then
    cp ../CMakeLists_windows_cross.txt CMakeLists.txt
else
    echo "ERROR: CMakeLists_windows_cross.txt not found"
    echo "Current directory: $(pwd)"
    echo "Looking for: ../CMakeLists_windows_cross.txt"
    ls -la ../CMakeLists* || true
    exit 1
fi

# Check toolchain file exists
if [ ! -f "../toolchain-mingw64.cmake" ]; then
    echo "ERROR: toolchain-mingw64.cmake not found"
    echo "Current directory: $(pwd)"
    echo "Looking for: ../toolchain-mingw64.cmake"
    ls -la ../toolchain* || true
    exit 1
fi

# Configure with toolchain file
echo "=== Running CMake ==="
echo "Current directory: $(pwd)"
echo "CMakeLists.txt content (first 10 lines):"
head -10 CMakeLists.txt || echo "CMakeLists.txt not found"

cmake . -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_TOOLCHAIN_FILE=../toolchain-mingw64.cmake || {
    echo "ERROR: CMake failed with exit code $?"
    exit 2
}

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