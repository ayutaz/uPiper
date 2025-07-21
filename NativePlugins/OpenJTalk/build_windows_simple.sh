#!/bin/bash
# Simplified Windows build script for debugging

set -ex

echo "=== Simple Windows Build Test ==="
echo "Starting directory: $(pwd)"

# Check if we're in the right place
if [ ! -f "CMakeLists.txt" ]; then
    echo "ERROR: CMakeLists.txt not found in current directory"
    echo "Contents of current directory:"
    ls -la
    exit 1
fi

# Create build directory
mkdir -p build_test
cd build_test

# Try simple cmake
echo "=== Running CMake ==="
cmake .. || {
    echo "CMake failed"
    exit 2
}

echo "=== Build test successful ==="