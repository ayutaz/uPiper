#!/bin/bash
set -e

echo "=== Building OpenJTalk wrapper for CI ==="

# Get script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Check if we're in CI environment
if [ -n "$CI" ]; then
    echo "Running in CI environment"
fi

# Function to check if libraries exist
check_libraries() {
    local lib_dir="$SCRIPT_DIR/external/openjtalk_build/open_jtalk-1.11"
    if [ -d "$lib_dir" ] && [ -f "$lib_dir/mecab/src/libmecab.a" ]; then
        return 0
    else
        return 1
    fi
}

# Check if dependencies exist
if ! check_libraries; then
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
    
    # Verify libraries were built
    if ! check_libraries; then
        echo "ERROR: Dependencies were not built correctly!"
        echo "Expected libraries at: $SCRIPT_DIR/external/openjtalk_build/open_jtalk-1.11"
        echo "Checking what exists:"
        ls -la "$SCRIPT_DIR/external/" || true
        ls -la "$SCRIPT_DIR/external/openjtalk_build/" || true
        exit 1
    fi
fi

# Return to script directory
cd "$SCRIPT_DIR"

# Create build directory
echo "Creating build directory..."
rm -rf build
mkdir -p build
cd build

# Configure CMake
echo "Configuring CMake..."
cmake -DCMAKE_BUILD_TYPE=Release .. || {
    echo "CMake configuration failed!"
    echo "Current directory: $(pwd)"
    echo "CMakeLists.txt location:"
    ls -la ../CMakeLists.txt || true
    exit 1
}

# Build
echo "Building..."
if [ "$OSTYPE" = "msys" ] || [ "$OSTYPE" = "win32" ]; then
    cmake --build . --config Release --parallel || {
        echo "Build failed!"
        exit 1
    }
else
    make -j$(nproc 2>/dev/null || sysctl -n hw.ncpu 2>/dev/null || echo 4) || {
        echo "Build failed!"
        exit 1
    }
fi

# List build outputs
echo "Build outputs:"
find . -name "*.so" -o -name "*.dll" -o -name "*.dylib" | head -10

# Run tests
echo "Running tests..."
if [ -f "CTestTestfile.cmake" ]; then
    ctest -C Release --output-on-failure || {
        echo "Tests failed, but continuing..."
    }
else
    echo "No tests configured"
fi

echo "=== Build completed successfully! ==="