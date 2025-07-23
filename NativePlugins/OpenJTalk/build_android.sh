#!/bin/bash

# Build script for Android platforms
# Builds OpenJTalk library for multiple Android architectures
# Can be run directly or inside Docker container

set -e

# Script directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
cd "$SCRIPT_DIR"

# Check if running in Docker
if [ -f /.dockerenv ]; then
    echo "Running in Docker container"
    IN_DOCKER=1
else
    echo "Running on host system"
    IN_DOCKER=0
fi

# Check for ANDROID_NDK_HOME
if [ -z "$ANDROID_NDK_HOME" ]; then
    if [ "$IN_DOCKER" -eq 1 ]; then
        # In Docker, NDK should be pre-installed
        export ANDROID_NDK_HOME=/opt/android-ndk
    else
        echo "Error: ANDROID_NDK_HOME environment variable is not set"
        echo "Please set it to your Android NDK installation path"
        exit 1
    fi
fi

echo "Using Android NDK at: $ANDROID_NDK_HOME"

# Android ABIs to build for
ABIS=("armeabi-v7a" "arm64-v8a" "x86" "x86_64")

# Build dependencies if not already built
if [ ! -d "external/open_jtalk-1.11" ]; then
    echo "Fetching dependencies first..."
    ./fetch_dependencies.sh
fi

# Function to build dependencies for Android
build_android_dependencies() {
    echo "Building dependencies for Android..."
    
    # Create build script inline
    cat > build_dependencies_android.sh << 'EOF'
#!/bin/bash
set -e

# Build HTSEngine for Android
build_hts_engine() {
    local ABI=$1
    echo "Building HTSEngine for Android $ABI..."
    
    cd external/hts_engine_API-1.10
    mkdir -p build_android_$ABI
    cd build_android_$ABI
    
    cmake -DCMAKE_TOOLCHAIN_FILE=$ANDROID_NDK_HOME/build/cmake/android.toolchain.cmake \
          -DANDROID_ABI=$ABI \
          -DANDROID_PLATFORM=android-21 \
          -DCMAKE_BUILD_TYPE=Release \
          -DCMAKE_INSTALL_PREFIX=../../openjtalk_build/install_android_$ABI \
          ..
    
    make -j$(nproc)
    make install
    cd ../../..
}

# Build OpenJTalk for Android
build_openjtalk() {
    local ABI=$1
    echo "Building OpenJTalk for Android $ABI..."
    
    cd external/open_jtalk-1.11
    
    # Configure for Android
    export CC=$ANDROID_NDK_HOME/toolchains/llvm/prebuilt/linux-x86_64/bin/clang
    export CXX=$ANDROID_NDK_HOME/toolchains/llvm/prebuilt/linux-x86_64/bin/clang++
    export AR=$ANDROID_NDK_HOME/toolchains/llvm/prebuilt/linux-x86_64/bin/llvm-ar
    export RANLIB=$ANDROID_NDK_HOME/toolchains/llvm/prebuilt/linux-x86_64/bin/llvm-ranlib
    
    # Set target based on ABI
    case $ABI in
        "armeabi-v7a")
            export TARGET=armv7a-linux-androideabi21
            ;;
        "arm64-v8a")
            export TARGET=aarch64-linux-android21
            ;;
        "x86")
            export TARGET=i686-linux-android21
            ;;
        "x86_64")
            export TARGET=x86_64-linux-android21
            ;;
    esac
    
    export CC="$CC --target=$TARGET"
    export CXX="$CXX --target=$TARGET"
    
    # Clean previous builds
    make clean || true
    
    # Configure
    ./configure --host=$TARGET \
                --prefix=$PWD/../openjtalk_build/install_android_$ABI \
                --with-hts-engine-header-path=$PWD/../openjtalk_build/install_android_$ABI/include \
                --with-hts-engine-library-path=$PWD/../openjtalk_build/install_android_$ABI/lib \
                --with-charset=UTF-8
    
    # Build
    make -j$(nproc)
    
    # Build static libraries only (no executables)
    for dir in mecab/src text2mecab mecab2njd njd njd_set_pronunciation njd_set_digit njd_set_accent_phrase njd_set_accent_type njd_set_unvoiced_vowel njd_set_long_vowel njd2jpcommon jpcommon; do
        (cd $dir && make)
    done
    
    cd ../..
}

# Download dependencies
if [ ! -d "external/open_jtalk-1.11" ]; then
    ./scripts/download_openjtalk_source.sh
fi

if [ ! -d "external/hts_engine_API-1.10" ]; then
    mkdir -p external
    cd external
    wget https://sourceforge.net/projects/hts-engine/files/hts_engine%20API/hts_engine_API-1.10/hts_engine_API-1.10.tar.gz
    tar xzf hts_engine_API-1.10.tar.gz
    cd ..
fi

# Build for each ABI
for ABI in "${ABIS[@]}"; do
    build_hts_engine $ABI
    build_openjtalk $ABI
done

echo "Android dependencies built successfully!"
EOF
    
    chmod +x build_dependencies_android.sh
    ./build_dependencies_android.sh

# Clean previous builds
rm -rf build_android_*
mkdir -p output/android

# Build wrapper library for each ABI
for ABI in "${ABIS[@]}"; do
    echo "Building OpenJTalk wrapper for Android $ABI..."
    
    # Create build directory
    mkdir -p build_android_$ABI
    cd build_android_$ABI
    
    # Configure with Android toolchain
    cmake -DCMAKE_TOOLCHAIN_FILE=$ANDROID_NDK_HOME/build/cmake/android.toolchain.cmake \
          -DANDROID_ABI=$ABI \
          -DANDROID_PLATFORM=android-21 \
          -DANDROID_STL=c++_shared \
          -DCMAKE_BUILD_TYPE=Release \
          -DBUILD_TESTS=OFF \
          -DBUILD_BENCHMARK=OFF \
          ..
    
    # Build
    make -j$(nproc)
    
    # Copy output
    mkdir -p ../output/android/$ABI
    cp lib/libopenjtalk_wrapper.so ../output/android/$ABI/
    
    cd ..
done

# Create Unity plugin structure
echo "Creating Unity plugin structure..."
UNITY_PLUGIN_DIR="../../Assets/uPiper/Plugins/Android"
mkdir -p "$UNITY_PLUGIN_DIR/libs"

for ABI in "${ABIS[@]}"; do
    mkdir -p "$UNITY_PLUGIN_DIR/libs/$ABI"
    cp output/android/$ABI/libopenjtalk_wrapper.so "$UNITY_PLUGIN_DIR/libs/$ABI/"
done

# Copy dictionary files
echo "Copying dictionary files..."
cp -r dictionary "$UNITY_PLUGIN_DIR/"

echo "Android build completed successfully!"
echo "Libraries are located in:"
echo "  - output/android/ (build output)"
echo "  - $UNITY_PLUGIN_DIR/libs/ (Unity plugin structure)"