#!/bin/bash
# iOS-specific dependency build script for OpenJTalk

set -e

echo "=== Building OpenJTalk Dependencies for iOS ==="

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_DIR="$SCRIPT_DIR/external/openjtalk_build"
INSTALL_DIR="$BUILD_DIR/install_ios"

# iOS toolchain path
TOOLCHAIN_FILE="$SCRIPT_DIR/ios.toolchain.cmake"

# Check if toolchain exists
if [ ! -f "$TOOLCHAIN_FILE" ]; then
    echo "ERROR: iOS toolchain not found at $TOOLCHAIN_FILE"
    echo "Please run build_ios.sh first to download the toolchain"
    exit 1
fi

cd "$BUILD_DIR"

# iOS specific settings
export PLATFORM="OS64"
export DEPLOYMENT_TARGET="11.0"
export ARCH="arm64"

# Get the iOS SDK path
XCODE_PATH=$(xcode-select -p)
SDK_PATH="${XCODE_PATH}/Platforms/iPhoneOS.platform/Developer/SDKs/iPhoneOS.sdk"

if [ ! -d "$SDK_PATH" ]; then
    echo "ERROR: iOS SDK not found at $SDK_PATH"
    echo "Please install Xcode and run: sudo xcode-select --switch /Applications/Xcode.app"
    exit 1
fi

echo "Using iOS SDK: $SDK_PATH"

# iOS compiler flags
export CC="xcrun -sdk iphoneos clang"
export CXX="xcrun -sdk iphoneos clang++"
export AR="xcrun -sdk iphoneos ar"
export RANLIB="xcrun -sdk iphoneos ranlib"
export CFLAGS="-arch arm64 -isysroot $SDK_PATH -mios-version-min=$DEPLOYMENT_TARGET -fembed-bitcode"
export CXXFLAGS="$CFLAGS"
export LDFLAGS="-arch arm64 -isysroot $SDK_PATH -mios-version-min=$DEPLOYMENT_TARGET"

# Build hts_engine
echo "=== Building hts_engine for iOS ==="
if [ -d "hts_engine_API-1.10" ]; then
    cd hts_engine_API-1.10
    
    # Clean previous builds
    if [ -f "Makefile" ]; then
        make clean || true
    fi
    
    # Configure for iOS
    ./configure \
        --prefix="$INSTALL_DIR" \
        --host=arm-apple-darwin \
        --enable-static \
        --disable-shared \
        --disable-dependency-tracking || {
        echo "ERROR: hts_engine configure failed"
        exit 1
    }
    
    echo "Starting hts_engine build..."
    make -j$(sysctl -n hw.ncpu) || {
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
    echo "Please run fetch_dependencies_ci.sh first"
    exit 1
fi

# Build OpenJTalk
echo "=== Building OpenJTalk for iOS ==="
if [ -d "open_jtalk-1.11" ]; then
    cd open_jtalk-1.11
    
    # Copy dictionary files if they exist in the repo
    if [ -d "$SCRIPT_DIR/dictionary" ]; then
        echo "Using pre-built dictionary from repository"
        mkdir -p mecab-naist-jdic
        cp -r "$SCRIPT_DIR/dictionary"/* mecab-naist-jdic/ 2>/dev/null || true
    fi
    
    # Clean previous builds
    if [ -f "Makefile" ]; then
        make clean || true
    fi
    
    # Configure for iOS
    ./configure \
        --prefix="$INSTALL_DIR" \
        --host=arm-apple-darwin \
        --with-hts-engine-header-path="$INSTALL_DIR/include" \
        --with-hts-engine-library-path="$INSTALL_DIR/lib" \
        --enable-static \
        --disable-shared \
        --disable-dependency-tracking || {
        echo "ERROR: OpenJTalk configure failed"
        exit 1
    }
    
    echo "Starting OpenJTalk build..."
    
    # Build libraries only (not tools)
    for dir in mecab/src text2mecab mecab2njd njd njd_set_pronunciation njd_set_digit njd_set_accent_phrase njd_set_accent_type njd_set_unvoiced_vowel njd_set_long_vowel njd2jpcommon jpcommon; do
        if [ -d "$dir" ]; then
            echo "Building $dir..."
            (cd "$dir" && make -j$(sysctl -n hw.ncpu)) || {
                echo "ERROR: Failed to build $dir"
                exit 1
            }
        fi
    done
    
    echo "=== Static libraries built successfully ==="
    
    cd ..
else
    echo "ERROR: open_jtalk-1.11 directory not found!"
    echo "Please run fetch_dependencies_ci.sh first"
    exit 1
fi

echo "=== iOS Dependencies built successfully ==="
echo "Library structure:"
find "$BUILD_DIR/open_jtalk-1.11" -name "*.a" -type f
echo ""
echo "HTSEngine library:"
find "$INSTALL_DIR" -name "*.a" -type f