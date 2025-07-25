#!/bin/bash

# Build x86 dependencies only
set -e

echo "=== Building x86 Dependencies for Android ==="

# Check for ANDROID_NDK_HOME
if [ -z "$ANDROID_NDK_HOME" ]; then
    export ANDROID_NDK_HOME=/opt/android-ndk
fi

# Function to set up Android compiler for x86
setup_android_compiler_x86() {
    # Base paths
    export TOOLCHAIN=$ANDROID_NDK_HOME/toolchains/llvm/prebuilt/linux-x86_64
    export CC=$TOOLCHAIN/bin/clang
    export CXX=$TOOLCHAIN/bin/clang++
    export AR=$TOOLCHAIN/bin/llvm-ar
    export AS=$TOOLCHAIN/bin/llvm-as
    export LD=$TOOLCHAIN/bin/ld
    export RANLIB=$TOOLCHAIN/bin/llvm-ranlib
    export STRIP=$TOOLCHAIN/bin/llvm-strip
    
    # x86 specific flags
    export TARGET="i686-linux-android"
    export API=21
    export CC="$CC -target $TARGET$API"
    export CXX="$CXX -target $TARGET$API"
    export CFLAGS="-O2 -fPIC -DANDROID"
    export CXXFLAGS="$CFLAGS"
    export LDFLAGS=""
}

# Build HTS Engine for x86
build_hts_engine_x86() {
    echo "Building HTS Engine for x86..."
    
    cd external/hts_engine_API-1.10
    
    # Clean previous builds
    make clean || true
    rm -rf build_android_x86
    
    # Configure for Android x86
    ./configure \
        --host=i686-linux-android \
        --prefix="$PWD/../openjtalk_build/android_x86" \
        --enable-static \
        --disable-shared \
        CC="$CC" \
        CXX="$CXX" \
        AR="$AR" \
        RANLIB="$RANLIB" \
        CFLAGS="$CFLAGS" \
        CXXFLAGS="$CXXFLAGS" \
        LDFLAGS="$LDFLAGS"
    
    # Build and install
    make -j$(nproc)
    make install
    
    cd ../..
}

# Build OpenJTalk for x86
build_openjtalk_x86() {
    echo "Building OpenJTalk for x86..."
    
    cd external/open_jtalk-1.11
    
    # Clean previous builds
    make clean || true
    
    # Set HTS Engine paths
    HTS_ENGINE_HEADER_PATH="$PWD/../openjtalk_build/android_x86/include"
    HTS_ENGINE_LIBRARY_PATH="$PWD/../openjtalk_build/android_x86/lib"
    
    # Configure for Android x86
    ./configure \
        --host=i686-linux-android \
        --prefix="$PWD/../openjtalk_build/android_x86" \
        --with-hts-engine-header-path="$HTS_ENGINE_HEADER_PATH" \
        --with-hts-engine-library-path="$HTS_ENGINE_LIBRARY_PATH" \
        --with-charset=UTF-8 \
        CC="$CC" \
        CXX="$CXX" \
        AR="$AR" \
        RANLIB="$RANLIB" \
        CFLAGS="$CFLAGS -finput-charset=UTF-8 -fexec-charset=UTF-8" \
        CXXFLAGS="$CXXFLAGS -finput-charset=UTF-8 -fexec-charset=UTF-8" \
        LDFLAGS="$LDFLAGS"
    
    # Build only the libraries we need
    echo "Building mecab..."
    cd mecab/src
    make libmecab.a -j$(nproc)
    cd ../..
    
    echo "Building text2mecab..."
    cd text2mecab
    make libtext2mecab.a -j$(nproc)
    cd ..
    
    echo "Building mecab2njd..."
    cd mecab2njd
    make libmecab2njd.a -j$(nproc)
    cd ..
    
    echo "Building njd..."
    cd njd
    make libnjd.a -j$(nproc)
    cd ..
    
    echo "Building njd_set_pronunciation..."
    cd njd_set_pronunciation
    make libnjd_set_pronunciation.a -j$(nproc)
    cd ..
    
    echo "Building njd_set_digit..."
    cd njd_set_digit
    make libnjd_set_digit.a -j$(nproc)
    cd ..
    
    echo "Building njd_set_accent_phrase..."
    cd njd_set_accent_phrase
    make libnjd_set_accent_phrase.a -j$(nproc)
    cd ..
    
    echo "Building njd_set_accent_type..."
    cd njd_set_accent_type
    make libnjd_set_accent_type.a -j$(nproc)
    cd ..
    
    echo "Building njd_set_unvoiced_vowel..."
    cd njd_set_unvoiced_vowel
    make libnjd_set_unvoiced_vowel.a -j$(nproc)
    cd ..
    
    echo "Building njd_set_long_vowel..."
    cd njd_set_long_vowel
    make libnjd_set_long_vowel.a -j$(nproc)
    cd ..
    
    echo "Building njd2jpcommon..."
    cd njd2jpcommon
    make libnjd2jpcommon.a -j$(nproc)
    cd ..
    
    echo "Building jpcommon..."
    cd jpcommon
    make libjpcommon.a -j$(nproc)
    cd ..
    
    # Copy libraries to install location
    echo "Installing libraries..."
    INSTALL_DIR="../openjtalk_build/android_x86"
    mkdir -p "$INSTALL_DIR/lib"
    
    cp mecab/src/libmecab.a "$INSTALL_DIR/lib/"
    cp text2mecab/libtext2mecab.a "$INSTALL_DIR/lib/"
    cp mecab2njd/libmecab2njd.a "$INSTALL_DIR/lib/"
    cp njd/libnjd.a "$INSTALL_DIR/lib/"
    cp njd_set_pronunciation/libnjd_set_pronunciation.a "$INSTALL_DIR/lib/"
    cp njd_set_digit/libnjd_set_digit.a "$INSTALL_DIR/lib/"
    cp njd_set_accent_phrase/libnjd_set_accent_phrase.a "$INSTALL_DIR/lib/"
    cp njd_set_accent_type/libnjd_set_accent_type.a "$INSTALL_DIR/lib/"
    cp njd_set_unvoiced_vowel/libnjd_set_unvoiced_vowel.a "$INSTALL_DIR/lib/"
    cp njd_set_long_vowel/libnjd_set_long_vowel.a "$INSTALL_DIR/lib/"
    cp njd2jpcommon/libnjd2jpcommon.a "$INSTALL_DIR/lib/"
    cp jpcommon/libjpcommon.a "$INSTALL_DIR/lib/"
    
    cd ../..
}

# Main build process
setup_android_compiler_x86
build_hts_engine_x86
build_openjtalk_x86

echo ""
echo "=== x86 Dependencies Build Complete ==="
echo "Libraries built in: external/openjtalk_build/android_x86/"