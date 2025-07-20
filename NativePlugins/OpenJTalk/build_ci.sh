#!/bin/bash
set -e

echo "Building OpenJTalk wrapper for CI..."

# Get script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Check if dependencies exist
if [ ! -d "$SCRIPT_DIR/external/openjtalk_build/open_jtalk-1.11" ]; then
    echo "Dependencies not found. Fetching and building..."
    
    # Fetch dependencies
    if [ -f "$SCRIPT_DIR/fetch_dependencies_ci.sh" ]; then
        echo "Using CI-specific dependency fetcher..."
        chmod +x "$SCRIPT_DIR/fetch_dependencies_ci.sh"
        "$SCRIPT_DIR/fetch_dependencies_ci.sh"
    else
        echo "Using standard dependency fetcher..."
        chmod +x "$SCRIPT_DIR/fetch_dependencies.sh"
        "$SCRIPT_DIR/fetch_dependencies.sh"
    fi
    
    # Build dependencies
    if [ -f "$SCRIPT_DIR/build_dependencies_ci.sh" ]; then
        echo "Using CI-specific dependency builder..."
        chmod +x "$SCRIPT_DIR/build_dependencies_ci.sh"
        "$SCRIPT_DIR/build_dependencies_ci.sh"
    else
        echo "Using standard dependency builder..."
        chmod +x "$SCRIPT_DIR/build_dependencies.sh"
        "$SCRIPT_DIR/build_dependencies.sh"
    fi
fi

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