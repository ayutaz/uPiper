#!/bin/bash

# Build OpenJTalk dependencies for Android
# This script builds HTS Engine and OpenJTalk for all Android ABIs

set -e

echo "=== Building OpenJTalk Dependencies for Android ==="
echo ""

# Script directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
cd "$SCRIPT_DIR"

# Check for ANDROID_NDK_HOME
if [ -z "$ANDROID_NDK_HOME" ]; then
    if [ -f /.dockerenv ]; then
        export ANDROID_NDK_HOME=/opt/android-ndk
    else
        echo "Error: ANDROID_NDK_HOME not set"
        exit 1
    fi
fi

echo "Using Android NDK at: $ANDROID_NDK_HOME"

# Android ABIs to build for
if [ -z "$ABIS" ]; then
    ABIS=("arm64-v8a" "armeabi-v7a" "x86" "x86_64")
fi

# Ensure dependencies are fetched
if [ ! -d "external/open_jtalk-1.11" ]; then
    echo "Fetching dependencies..."
    ./fetch_dependencies.sh
fi

# Function to set up Android compiler for specific ABI
setup_android_compiler() {
    local ABI=$1
    
    # Base paths
    export TOOLCHAIN=$ANDROID_NDK_HOME/toolchains/llvm/prebuilt/linux-x86_64
    export CC=$TOOLCHAIN/bin/clang
    export CXX=$TOOLCHAIN/bin/clang++
    export AR=$TOOLCHAIN/bin/llvm-ar
    export AS=$TOOLCHAIN/bin/llvm-as
    export LD=$TOOLCHAIN/bin/ld
    export RANLIB=$TOOLCHAIN/bin/llvm-ranlib
    export STRIP=$TOOLCHAIN/bin/llvm-strip
    
    # Set target based on ABI
    case $ABI in
        "armeabi-v7a")
            export TARGET=armv7a-linux-androideabi
            export TARGET_HOST=arm-linux-androideabi
            export API=21
            ;;
        "arm64-v8a")
            export TARGET=aarch64-linux-android
            export TARGET_HOST=aarch64-linux-android
            export API=21
            ;;
        "x86")
            export TARGET=i686-linux-android
            export TARGET_HOST=i686-linux-android
            export API=21
            ;;
        "x86_64")
            export TARGET=x86_64-linux-android
            export TARGET_HOST=x86_64-linux-android
            export API=21
            ;;
    esac
    
    # Set compiler flags
    export CC="$CC -target ${TARGET}${API}"
    export CXX="$CXX -target ${TARGET}${API}"
    export CFLAGS="-fPIC -O2 -DANDROID"
    export CXXFLAGS="-fPIC -O2 -DANDROID"
    export LDFLAGS="-fPIC"
}

# Build HTS Engine for Android
build_hts_engine_android() {
    local ABI=$1
    echo ""
    echo "================================================"
    echo "Building HTS Engine for Android $ABI..."
    echo "================================================"
    
    setup_android_compiler $ABI
    
    cd external/hts_engine_API-1.10
    
    # Clean previous builds
    make clean || true
    rm -rf build_android_$ABI
    
    # Configure for Android
    ./configure --host=$TARGET_HOST \
                --prefix=$SCRIPT_DIR/external/openjtalk_build/android_$ABI \
                --enable-static \
                --disable-shared \
                --with-audio=no
    
    # Build
    make -j$(nproc)
    make install
    
    cd ../..
}

# Build OpenJTalk for Android
build_openjtalk_android() {
    local ABI=$1
    echo ""
    echo "================================================"
    echo "Building OpenJTalk for Android $ABI..."
    echo "================================================"
    
    setup_android_compiler $ABI
    
    cd external/open_jtalk-1.11
    
    # Clean previous builds
    make clean || true
    
    # Configure for Android
    ./configure --host=$TARGET_HOST \
                --prefix=$SCRIPT_DIR/external/openjtalk_build/android_$ABI \
                --with-hts-engine-header-path=$SCRIPT_DIR/external/openjtalk_build/android_$ABI/include \
                --with-hts-engine-library-path=$SCRIPT_DIR/external/openjtalk_build/android_$ABI/lib \
                --with-charset=UTF-8 \
                --enable-static \
                --disable-shared
    
    # Build only the libraries (not the executables)
    for dir in mecab/src text2mecab mecab2njd njd njd_set_pronunciation njd_set_digit njd_set_accent_phrase njd_set_accent_type njd_set_unvoiced_vowel njd_set_long_vowel njd2jpcommon jpcommon; do
        echo "Building $dir..."
        (cd $dir && make -j$(nproc))
    done
    
    # Manually copy the static libraries
    mkdir -p $SCRIPT_DIR/external/openjtalk_build/android_$ABI/lib
    find . -name "*.a" -exec cp {} $SCRIPT_DIR/external/openjtalk_build/android_$ABI/lib/ \;
    
    cd ../..
}

# Build for each ABI
for ABI in "${ABIS[@]}"; do
    build_hts_engine_android $ABI
    build_openjtalk_android $ABI
done

echo ""
echo "=== Android Dependencies Build Complete ==="
echo ""
echo "Libraries built for ABIs: ${ABIS[*]}"
echo "Location: external/openjtalk_build/android_*/"