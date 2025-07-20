#!/bin/bash
# CI build script with fallback options

set -e

echo "=== Building OpenJTalk wrapper for CI ==="
echo "Script started at: $(date)"

# Get script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
echo "Script directory: $SCRIPT_DIR"

# Check if we're in GitHub Actions
if [ -n "$GITHUB_ACTIONS" ]; then
    echo "Running in GitHub Actions"
    echo "Runner OS: $RUNNER_OS"
    echo "GitHub Workspace: $GITHUB_WORKSPACE"
fi

# Function to check if libraries exist
check_libraries() {
    local lib_dir="$SCRIPT_DIR/external/openjtalk_build/open_jtalk-1.11"
    if [ -d "$lib_dir" ] && [ -f "$lib_dir/mecab/src/libmecab.a" ]; then
        echo "Libraries found at: $lib_dir"
        return 0
    else
        echo "Libraries not found at: $lib_dir"
        return 1
    fi
}

# Try to use pre-built libraries first
if check_libraries; then
    echo "Using existing OpenJTalk libraries"
else
    echo "OpenJTalk libraries not found, attempting to build..."
    
    # Create external directory
    mkdir -p "$SCRIPT_DIR/external/openjtalk_build"
    
    # Check if we can download pre-built libraries (for CI speed)
    if [ -n "$GITHUB_ACTIONS" ] && [ -f "$SCRIPT_DIR/ci_prebuilt_libs.tar.gz" ]; then
        echo "Extracting pre-built libraries for CI..."
        cd "$SCRIPT_DIR/external/openjtalk_build"
        tar -xzf "$SCRIPT_DIR/ci_prebuilt_libs.tar.gz"
    else
        # Build from source
        echo "Building OpenJTalk from source..."
        
        # Use CI-specific scripts if available
        if [ -f "$SCRIPT_DIR/fetch_dependencies_ci.sh" ] && [ -f "$SCRIPT_DIR/build_dependencies_ci.sh" ]; then
            echo "Using CI-specific build scripts..."
            chmod +x "$SCRIPT_DIR/fetch_dependencies_ci.sh" "$SCRIPT_DIR/build_dependencies_ci.sh"
            
            # Run dependency fetching (timeout command is not available on Windows/macOS in CI)
            echo "Fetching dependencies..."
            "$SCRIPT_DIR/fetch_dependencies_ci.sh" || {
                echo "ERROR: Dependency fetch failed"
                exit 1
            }
            
            echo "Building dependencies..."
            "$SCRIPT_DIR/build_dependencies_ci.sh" || {
                echo "ERROR: Dependency build failed"
                exit 1
            }
        else
            echo "ERROR: CI build scripts not found"
            exit 1
        fi
    fi
    
    # Verify libraries were built
    if ! check_libraries; then
        echo "ERROR: Failed to build or extract OpenJTalk libraries"
        exit 1
    fi
fi

# Return to script directory
cd "$SCRIPT_DIR"

# Clean and create build directory
echo "Setting up build directory..."
rm -rf build
mkdir -p build
cd build

# Configure CMake
echo "Configuring CMake..."
cmake -DCMAKE_BUILD_TYPE=Release -DBUILD_TESTS=OFF -DBUILD_BENCHMARK=OFF .. || {
    echo "ERROR: CMake configuration failed"
    echo "CMake version: $(cmake --version | head -1)"
    exit 1
}

# Build
echo "Building library..."
if [ "$RUNNER_OS" = "Windows" ] || [ "$OSTYPE" = "msys" ] || [ "$OSTYPE" = "win32" ]; then
    cmake --build . --config Release --parallel || {
        echo "ERROR: Build failed"
        exit 1
    }
else
    make -j$(nproc 2>/dev/null || sysctl -n hw.ncpu 2>/dev/null || echo 2) || {
        echo "ERROR: Build failed"
        exit 1
    }
fi

# List build outputs
echo "Build completed. Output files:"
find . -type f \( -name "*.so" -o -name "*.dll" -o -name "*.dylib" \) -exec ls -la {} \;

# Create output directory for Unity
cd "$SCRIPT_DIR"
mkdir -p output
if [ "$RUNNER_OS" = "Windows" ] || [ "$OSTYPE" = "msys" ] || [ "$OSTYPE" = "win32" ]; then
    cp build/bin/Release/*.dll output/ 2>/dev/null || cp build/bin/*.dll output/ 2>/dev/null || true
elif [ "$RUNNER_OS" = "macOS" ] || [ "$OSTYPE" = "darwin"* ]; then
    cp build/lib/*.dylib output/ 2>/dev/null || true
else
    cp build/lib/*.so output/ 2>/dev/null || true
fi

echo "=== Build completed successfully at $(date) ==="