#!/bin/bash
# Build script for CI environment

set -e  # Exit on error

echo "=== OpenJTalk Native Build for CI ==="

# Detect platform
if [[ "$OSTYPE" == "linux-gnu"* ]]; then
    PLATFORM="linux"
elif [[ "$OSTYPE" == "darwin"* ]]; then
    PLATFORM="macos"
elif [[ "$OSTYPE" == "msys" ]] || [[ "$OSTYPE" == "win32" ]]; then
    PLATFORM="windows"
else
    echo "Unknown platform: $OSTYPE"
    exit 1
fi

echo "Platform detected: $PLATFORM"

# Create build directory
BUILD_DIR="build"
if [ -d "$BUILD_DIR" ]; then
    echo "Removing existing build directory..."
    rm -rf "$BUILD_DIR"
fi

mkdir -p "$BUILD_DIR"
cd "$BUILD_DIR"

# Configure with CMake
echo "=== Configuring with CMake ==="
# Always use full CMakeLists - build dependencies if needed
if [ ! -f "../external/openjtalk_build/install/lib/libHTSEngine.a" ]; then
    echo "OpenJTalk dependencies not found, building them first..."
    cd ..
    # Use CI-specific scripts for faster builds
    if [ -f "./fetch_dependencies_ci.sh" ]; then
        ./fetch_dependencies_ci.sh
        ./build_dependencies_ci.sh
    else
        ./fetch_dependencies.sh
        ./build_dependencies.sh
    fi
    cd build
fi
cmake .. \
    -DCMAKE_BUILD_TYPE=Release \
    -DBUILD_TESTS=OFF \
    -DBUILD_FULL_VERSION=ON

# Build
echo "=== Building ==="
cmake --build . --config Release

# Create test dictionary if it doesn't exist
if [ ! -f "../test_dictionary/sys.dic" ]; then
    echo "=== Creating test dictionary ==="
    cd ..
    if [ ! -d "test_dictionary" ]; then
        mkdir -p test_dictionary
    fi
    cd test_dictionary
    python3 create_test_dict.py || {
        echo "Failed to create test dictionary"
        exit 1
    }
    cd ../build
fi

# Run tests
echo "=== Running tests ==="
if [ "$PLATFORM" == "windows" ]; then
    ctest -C Release --output-on-failure
else
    ctest --output-on-failure
fi

# Run benchmark
echo "=== Running performance benchmark ==="
BENCHMARK_PATH="./bin/benchmark_openjtalk"
if [ "$PLATFORM" == "windows" ]; then
    BENCHMARK_PATH="./bin/Release/benchmark_openjtalk.exe"
fi

# Check if benchmark exists (not built in simplified CI build)
if [ -f "$BENCHMARK_PATH" ]; then
    if $BENCHMARK_PATH ../test_dictionary > benchmark_output.txt 2>&1; then
        echo "Benchmark completed successfully"
        cat benchmark_output.txt
        # Extract key metrics for CI
        grep -E "(Average processing time|All sentences)" benchmark_output.txt > bin/benchmark_results.txt || true
    else
        echo "Benchmark failed"
        cat benchmark_output.txt || true
        exit 1
    fi
else
    echo "Benchmark tool not built (using simplified CI build)"
    echo "Skipping performance benchmark"
fi

# Create output directory structure
OUTPUT_DIR="../output/$PLATFORM"
mkdir -p "$OUTPUT_DIR"

# Copy library to output
echo "=== Copying output files ==="
if [ "$PLATFORM" == "windows" ]; then
    cp bin/Release/openjtalk_wrapper.dll "$OUTPUT_DIR/" || cp bin/openjtalk_wrapper.dll "$OUTPUT_DIR/"
elif [ "$PLATFORM" == "macos" ]; then
    cp lib/libopenjtalk_wrapper.dylib "$OUTPUT_DIR/"
else
    cp lib/libopenjtalk_wrapper.so "$OUTPUT_DIR/"
fi

echo "=== Build completed successfully ==="
ls -la "$OUTPUT_DIR"