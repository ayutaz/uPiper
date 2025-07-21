#!/bin/bash
# Cross-compile build script for Windows using MinGW on Linux

set -e

echo "=== Building OpenJTalk Dependencies for Windows Cross-Compilation ==="

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_DIR="$SCRIPT_DIR/external/openjtalk_build"
INSTALL_DIR="$BUILD_DIR/install"

cd "$BUILD_DIR"

# MinGW toolchain setup
export CC=x86_64-w64-mingw32-gcc
export CXX=x86_64-w64-mingw32-g++
export AR=x86_64-w64-mingw32-ar
export RANLIB=x86_64-w64-mingw32-ranlib
export WINDRES=x86_64-w64-mingw32-windres
export STRIP=x86_64-w64-mingw32-strip

# Build flags for static linking
export CFLAGS="-O3 -static-libgcc -static-libstdc++"
export CXXFLAGS="-O3 -static-libgcc -static-libstdc++"
export LDFLAGS="-static-libgcc -static-libstdc++ -static"

# Number of parallel jobs
JOBS=$(nproc)

echo "Platform: Windows (cross-compile)"
echo "Using $JOBS parallel jobs"

# Build hts_engine for Windows
echo "=== Building hts_engine for Windows ==="
if [ -d "hts_engine_API-1.10" ]; then
    cd hts_engine_API-1.10
    
    # Clean previous builds
    if [ -f "Makefile" ]; then
        make clean || true
    fi
    
    # Configure for Windows cross-compilation
    ./configure \
        --host=x86_64-w64-mingw32 \
        --prefix="$INSTALL_DIR" \
        --enable-static \
        --disable-shared || {
        echo "ERROR: hts_engine configure failed"
        exit 1
    }
    
    echo "Building hts_engine..."
    make -j$JOBS || {
        echo "ERROR: hts_engine build failed"
        exit 1
    }
    make install || {
        echo "ERROR: hts_engine install failed"
        exit 1
    }
    cd ..
else
    echo "ERROR: hts_engine_API-1.10 directory not found!"
    exit 1
fi

# Build OpenJTalk for Windows
echo "=== Building OpenJTalk for Windows ==="
if [ -d "open_jtalk-1.11" ]; then
    cd open_jtalk-1.11
    
    # Copy dictionary files if they exist
    if [ -d "$SCRIPT_DIR/dictionary" ]; then
        echo "Using pre-built dictionary from repository"
        mkdir -p mecab-naist-jdic
        cp -r "$SCRIPT_DIR/dictionary"/* mecab-naist-jdic/ 2>/dev/null || true
    fi
    
    # Clean previous builds
    if [ -f "Makefile" ]; then
        make clean || true
    fi
    
    # Configure for Windows cross-compilation
    ./configure \
        --host=x86_64-w64-mingw32 \
        --prefix="$INSTALL_DIR" \
        --with-hts-engine-header-path="$INSTALL_DIR/include" \
        --with-hts-engine-library-path="$INSTALL_DIR/lib" \
        --with-charset=UTF-8 \
        --enable-static \
        --disable-shared || {
        echo "ERROR: OpenJTalk configure failed"
        exit 1
    }
    
    echo "Building OpenJTalk libraries..."
    # Build mecab library first (without the executable)
    if [ -d "mecab/src" ]; then
        echo "Building mecab library..."
        (cd mecab/src && make libmecab.a -j$JOBS) || {
            echo "ERROR: Failed to build mecab library"
            exit 1
        }
    fi
    
    # Build other libraries
    for dir in text2mecab mecab2njd njd njd_set_pronunciation njd_set_digit njd_set_accent_phrase njd_set_accent_type njd_set_unvoiced_vowel njd_set_long_vowel njd2jpcommon jpcommon; do
        if [ -d "$dir" ]; then
            echo "Building $dir..."
            (cd "$dir" && make -j$JOBS) || {
                echo "ERROR: Failed to build $dir"
                exit 1
            }
        fi
    done
    
    echo "=== Static libraries built successfully ==="
    cd ..
else
    echo "ERROR: open_jtalk-1.11 directory not found!"
    exit 1
fi

echo "=== Dependencies built successfully for Windows ==="
echo "Library structure:"
find "$BUILD_DIR" -name "*.a" -type f | head -20