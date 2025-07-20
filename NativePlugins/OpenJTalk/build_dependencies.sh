#!/bin/bash

# Build script for OpenJTalk dependencies
# Builds hts_engine_API and OpenJTalk in the correct order

set -e

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
EXTERNAL_DIR="$SCRIPT_DIR/external"
BUILD_DIR="$SCRIPT_DIR/external/openjtalk_build"

# Create build directory
mkdir -p "$BUILD_DIR"

# Platform detection
PLATFORM=$(uname -s)
if [ "$PLATFORM" = "Darwin" ]; then
    JOBS=$(sysctl -n hw.ncpu)
else
    JOBS=$(nproc)
fi

echo "Building OpenJTalk dependencies..."
echo "Platform: $PLATFORM"
echo "Jobs: $JOBS"

# Build hts_engine_API
echo "=== Building hts_engine_API ==="
cd "$EXTERNAL_DIR/hts_engine_API-1.10"
if [ ! -f "Makefile" ]; then
    ./configure --prefix="$BUILD_DIR/install"
fi
make -j$JOBS
make install

# Build OpenJTalk with integrated Mecab
echo "=== Building OpenJTalk ==="
cd "$EXTERNAL_DIR/open_jtalk-1.11"

# Configure OpenJTalk with embedded Mecab
if [ ! -f "Makefile" ]; then
    # Set HTS engine paths
    export CPPFLAGS="-I$BUILD_DIR/install/include"
    export LDFLAGS="-L$BUILD_DIR/install/lib"
    
    ./configure \
        --prefix="$BUILD_DIR/install" \
        --with-hts-engine-header-path="$BUILD_DIR/install/include" \
        --with-hts-engine-library-path="$BUILD_DIR/install/lib" \
        --with-charset=UTF-8
fi

# Build
make -j$JOBS

# Don't install OpenJTalk itself, we just need the static libraries
# Copy the static libraries to a known location
echo "=== Copying static libraries ==="
mkdir -p "$BUILD_DIR/open_jtalk-1.11"

# Copy all static libraries
find . -name "*.a" -type f | while read -r lib; do
    dir=$(dirname "$lib")
    mkdir -p "$BUILD_DIR/open_jtalk-1.11/$dir"
    cp "$lib" "$BUILD_DIR/open_jtalk-1.11/$dir/"
done

echo "=== Dependencies built successfully ==="
echo "Static libraries available at: $BUILD_DIR/open_jtalk-1.11"