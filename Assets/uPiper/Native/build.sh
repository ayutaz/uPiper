#!/bin/bash

# Build script for uPiper native libraries
# Supports: Linux, macOS, Windows (via MinGW or WSL)

set -e

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
BUILD_DIR="$SCRIPT_DIR/Build"
OUTPUT_BASE="$SCRIPT_DIR/../Plugins"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Detect platform
detect_platform() {
    case "$(uname -s)" in
        Linux*)     PLATFORM="Linux";;
        Darwin*)    PLATFORM="macOS";;
        CYGWIN*|MINGW*|MSYS*) PLATFORM="Windows";;
        *)          print_error "Unknown platform"; exit 1;;
    esac
    print_info "Detected platform: $PLATFORM"
}

# Clean build directory
clean_build() {
    print_info "Cleaning build directory..."
    rm -rf "$BUILD_DIR"
    mkdir -p "$BUILD_DIR"
}

# Build for current platform
build_native() {
    local platform=$1
    print_info "Building for $platform..."
    
    cd "$BUILD_DIR"
    
    # Configure CMake based on platform
    case "$platform" in
        Linux)
            cmake .. \
                -DCMAKE_BUILD_TYPE=Release \
                -DCMAKE_C_COMPILER=gcc \
                -DCMAKE_CXX_COMPILER=g++
            ;;
        macOS)
            cmake .. \
                -DCMAKE_BUILD_TYPE=Release \
                -DCMAKE_OSX_ARCHITECTURES="x86_64;arm64" \
                -DCMAKE_OSX_DEPLOYMENT_TARGET=10.15
            ;;
        Windows)
            if command -v x86_64-w64-mingw32-gcc &> /dev/null; then
                # Cross-compile for Windows on Linux/macOS
                cmake .. \
                    -DCMAKE_BUILD_TYPE=Release \
                    -DCMAKE_TOOLCHAIN_FILE="$SCRIPT_DIR/cmake/mingw-w64-x86_64.cmake"
            else
                # Native Windows build
                cmake .. -G "MinGW Makefiles" -DCMAKE_BUILD_TYPE=Release
            fi
            ;;
    esac
    
    # Build
    cmake --build . --config Release -j$(nproc 2>/dev/null || echo 4)
    
    print_success "Build completed for $platform"
}

# Copy libraries to Unity plugin folders
copy_libraries() {
    local platform=$1
    print_info "Copying libraries for $platform..."
    
    case "$platform" in
        Linux)
            local src="$OUTPUT_BASE/Linux/libopenjtalk_wrapper.so"
            local dst="$OUTPUT_BASE/Linux/x86_64/"
            mkdir -p "$dst"
            if [ -f "$src" ]; then
                cp "$src" "$dst"
                print_success "Copied Linux library"
            fi
            ;;
        macOS)
            local src="$OUTPUT_BASE/macOS/libopenjtalk_wrapper.dylib"
            if [ -f "$src" ]; then
                print_success "Copied macOS library"
            fi
            ;;
        Windows)
            local src="$OUTPUT_BASE/Windows/openjtalk_wrapper.dll"
            local dst="$OUTPUT_BASE/Windows/x86_64/"
            mkdir -p "$dst"
            if [ -f "$src" ]; then
                cp "$src" "$dst"
                print_success "Copied Windows library"
            fi
            ;;
    esac
}

# Main build process
main() {
    print_info "Starting uPiper native library build..."
    
    # Detect platform
    detect_platform
    
    # Parse arguments
    BUILD_ALL=false
    if [ "$1" == "--all" ]; then
        BUILD_ALL=true
    fi
    
    # Clean build directory
    clean_build
    
    if [ "$BUILD_ALL" == true ]; then
        print_info "Building for all platforms..."
        
        # Build for current platform first
        build_native "$PLATFORM"
        copy_libraries "$PLATFORM"
        
        # Cross-compile for other platforms if possible
        if [ "$PLATFORM" == "Linux" ] || [ "$PLATFORM" == "macOS" ]; then
            # Try to cross-compile for Windows
            if command -v x86_64-w64-mingw32-gcc &> /dev/null; then
                print_info "Cross-compiling for Windows..."
                clean_build
                build_native "Windows"
                copy_libraries "Windows"
            else
                print_info "MinGW not found, skipping Windows cross-compilation"
            fi
        fi
    else
        # Build only for current platform
        build_native "$PLATFORM"
        copy_libraries "$PLATFORM"
    fi
    
    print_success "Build process completed!"
    print_info "Libraries are located in: $OUTPUT_BASE"
}

# Run main function
main "$@"