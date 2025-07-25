#!/bin/bash

# Android environment verification script
# Tests NDK setup and basic cross-compilation

set -e

echo "=== Android NDK Environment Verification ==="
echo ""

# Check if running in Docker
if [ -f /.dockerenv ]; then
    echo "✓ Running in Docker container"
    IN_DOCKER=1
else
    echo "✗ Not running in Docker container"
    IN_DOCKER=0
fi

# Check ANDROID_NDK_HOME
if [ -z "$ANDROID_NDK_HOME" ]; then
    if [ "$IN_DOCKER" -eq 1 ]; then
        export ANDROID_NDK_HOME=/opt/android-ndk
    else
        echo "✗ ANDROID_NDK_HOME not set"
        exit 1
    fi
fi

if [ -d "$ANDROID_NDK_HOME" ]; then
    echo "✓ ANDROID_NDK_HOME: $ANDROID_NDK_HOME"
else
    echo "✗ ANDROID_NDK_HOME directory not found: $ANDROID_NDK_HOME"
    exit 1
fi

# Check for required tools
echo ""
echo "Checking for required tools:"

# Check CMake
if command -v cmake &> /dev/null; then
    CMAKE_VERSION=$(cmake --version | head -n1)
    echo "✓ CMake: $CMAKE_VERSION"
else
    echo "✗ CMake not found"
    exit 1
fi

# Check for Android toolchain file
TOOLCHAIN_FILE="$ANDROID_NDK_HOME/build/cmake/android.toolchain.cmake"
if [ -f "$TOOLCHAIN_FILE" ]; then
    echo "✓ Android toolchain file found"
else
    echo "✗ Android toolchain file not found: $TOOLCHAIN_FILE"
    exit 1
fi

# Test compilation for each ABI
echo ""
echo "Testing compilation for each ABI:"

# Create test program
cat > test_android.c << 'EOF'
#include <stdio.h>
#include <jni.h>

#ifdef __ANDROID__
const char* get_platform() { return "Android"; }
#else
const char* get_platform() { return "Unknown"; }
#endif

JNIEXPORT jstring JNICALL
Java_com_example_Test_getPlatform(JNIEnv* env, jobject thiz) {
    return (*env)->NewStringUTF(env, get_platform());
}

int main() {
    printf("Platform: %s\n", get_platform());
    #ifdef __arm__
    printf("Architecture: ARM 32-bit\n");
    #elif defined(__aarch64__)
    printf("Architecture: ARM 64-bit\n");
    #elif defined(__i386__)
    printf("Architecture: x86 32-bit\n");
    #elif defined(__x86_64__)
    printf("Architecture: x86 64-bit\n");
    #endif
    return 0;
}
EOF

# Test compilation for each ABI
ABIS=("arm64-v8a" "armeabi-v7a" "x86" "x86_64")
FAILED_ABIS=()

for ABI in "${ABIS[@]}"; do
    echo ""
    echo "Testing $ABI..."
    
    # Create build directory
    rm -rf test_build_$ABI
    mkdir test_build_$ABI
    cd test_build_$ABI
    
    # Configure with CMake
    if cmake -DCMAKE_TOOLCHAIN_FILE="$TOOLCHAIN_FILE" \
             -DANDROID_ABI=$ABI \
             -DANDROID_PLATFORM=android-21 \
             -DCMAKE_BUILD_TYPE=Release \
             .. &> /dev/null; then
        
        # Try to compile test program directly
        COMPILER=""
        case $ABI in
            "armeabi-v7a")
                COMPILER="$ANDROID_NDK_HOME/toolchains/llvm/prebuilt/linux-x86_64/bin/armv7a-linux-androideabi21-clang"
                ;;
            "arm64-v8a")
                COMPILER="$ANDROID_NDK_HOME/toolchains/llvm/prebuilt/linux-x86_64/bin/aarch64-linux-android21-clang"
                ;;
            "x86")
                COMPILER="$ANDROID_NDK_HOME/toolchains/llvm/prebuilt/linux-x86_64/bin/i686-linux-android21-clang"
                ;;
            "x86_64")
                COMPILER="$ANDROID_NDK_HOME/toolchains/llvm/prebuilt/linux-x86_64/bin/x86_64-linux-android21-clang"
                ;;
        esac
        
        if [ -n "$COMPILER" ] && [ -f "$COMPILER" ]; then
            if $COMPILER ../test_android.c -shared -o libtest.so &> /dev/null; then
                echo "✓ $ABI: Compilation successful"
            else
                echo "✗ $ABI: Compilation failed"
                FAILED_ABIS+=("$ABI")
            fi
        else
            echo "✗ $ABI: Compiler not found"
            FAILED_ABIS+=("$ABI")
        fi
    else
        echo "✗ $ABI: CMake configuration failed"
        FAILED_ABIS+=("$ABI")
    fi
    
    cd ..
done

# Clean up
rm -rf test_build_* test_android.c

# Summary
echo ""
echo "=== Summary ==="
echo "Tested ABIs: ${#ABIS[@]}"
echo "Successful: $((${#ABIS[@]} - ${#FAILED_ABIS[@]}))"
echo "Failed: ${#FAILED_ABIS[@]}"

if [ ${#FAILED_ABIS[@]} -gt 0 ]; then
    echo ""
    echo "Failed ABIs: ${FAILED_ABIS[*]}"
    exit 1
else
    echo ""
    echo "✓ All Android NDK environment checks passed!"
fi