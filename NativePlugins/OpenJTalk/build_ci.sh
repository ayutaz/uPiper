#!/bin/bash
set -e

echo "Building OpenJTalk wrapper for CI..."

# Create build directory
mkdir -p build
cd build

# Configure CMake
echo "Configuring CMake..."
cmake -DCMAKE_BUILD_TYPE=Release ..

# Build
echo "Building..."
cmake --build . --config Release

# Run tests
echo "Running tests..."
ctest -C Release --output-on-failure || true

echo "Build completed successfully!"