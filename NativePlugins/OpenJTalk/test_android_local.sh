#!/bin/bash

# Local Android testing script
set -e

echo "=== Local Android Library Testing ==="

# Check for Android SDK
if [ -z "$ANDROID_HOME" ]; then
    echo "Error: ANDROID_HOME not set"
    echo "Please set ANDROID_HOME to your Android SDK location"
    exit 1
fi

# Check for adb
if ! command -v adb &> /dev/null; then
    echo "Error: adb not found in PATH"
    exit 1
fi

# Function to test on connected device/emulator
test_on_device() {
    echo ""
    echo "=== Testing on Android Device/Emulator ==="
    
    # Check if device is connected
    if ! adb devices | grep -q "device$"; then
        echo "Error: No Android device/emulator connected"
        echo "Please connect a device or start an emulator"
        return 1
    fi
    
    # Get device info
    echo "Device info:"
    adb shell getprop ro.product.model
    adb shell getprop ro.build.version.release
    adb shell getprop ro.product.cpu.abi
    
    # Create test directory on device
    echo ""
    echo "Setting up test environment on device..."
    adb shell "mkdir -p /data/local/tmp/openjtalk_test"
    
    # Determine which ABI to use based on device
    DEVICE_ABI=$(adb shell getprop ro.product.cpu.abi)
    echo "Device ABI: $DEVICE_ABI"
    
    # Map device ABI to our library ABI
    case $DEVICE_ABI in
        "arm64-v8a")
            LIB_ABI="arm64-v8a"
            ;;
        "armeabi-v7a")
            LIB_ABI="armeabi-v7a"
            ;;
        "x86")
            LIB_ABI="x86"
            ;;
        "x86_64")
            LIB_ABI="x86_64"
            ;;
        *)
            echo "Warning: Unknown ABI $DEVICE_ABI, trying arm64-v8a"
            LIB_ABI="arm64-v8a"
            ;;
    esac
    
    # Check if library exists
    LIB_PATH="output/android/$LIB_ABI/libopenjtalk_wrapper.so"
    if [ ! -f "$LIB_PATH" ]; then
        echo "Error: Library not found for ABI $LIB_ABI"
        echo "Expected at: $LIB_PATH"
        return 1
    fi
    
    # Push library to device
    echo "Pushing library to device..."
    adb push "$LIB_PATH" /data/local/tmp/openjtalk_test/
    
    # Create and push test program
    echo ""
    echo "Creating test program..."
    cat > test_android_device.c << 'EOF'
#include <stdio.h>
#include <stdlib.h>
#include <dlfcn.h>
#include <android/log.h>

#define LOG_TAG "OpenJTalkTest"
#define LOGI(...) __android_log_print(ANDROID_LOG_INFO, LOG_TAG, __VA_ARGS__)
#define LOGE(...) __android_log_print(ANDROID_LOG_ERROR, LOG_TAG, __VA_ARGS__)

int main() {
    printf("OpenJTalk Android Test\n");
    printf("======================\n\n");
    
    // Load library
    printf("Loading library...\n");
    void* handle = dlopen("./libopenjtalk_wrapper.so", RTLD_NOW);
    if (!handle) {
        printf("Failed to load library: %s\n", dlerror());
        LOGE("Failed to load library: %s", dlerror());
        return 1;
    }
    
    printf("Library loaded successfully!\n");
    LOGI("Library loaded successfully!");
    
    // Get function pointers
    typedef void* (*create_fn)(const char*);
    typedef void (*destroy_fn)(void*);
    typedef const char* (*version_fn)();
    
    create_fn openjtalk_create = (create_fn)dlsym(handle, "openjtalk_create");
    destroy_fn openjtalk_destroy = (destroy_fn)dlsym(handle, "openjtalk_destroy");
    version_fn openjtalk_get_version = (version_fn)dlsym(handle, "openjtalk_get_version");
    
    printf("\nFunction lookup results:\n");
    printf("openjtalk_create: %s\n", openjtalk_create ? "FOUND" : "NOT FOUND");
    printf("openjtalk_destroy: %s\n", openjtalk_destroy ? "FOUND" : "NOT FOUND");
    printf("openjtalk_get_version: %s\n", openjtalk_get_version ? "FOUND" : "NOT FOUND");
    
    // Try to get version
    if (openjtalk_get_version) {
        const char* version = openjtalk_get_version();
        printf("\nOpenJTalk version: %s\n", version ? version : "NULL");
        LOGI("OpenJTalk version: %s", version ? version : "NULL");
    }
    
    // Try to create instance (will fail without dictionary)
    if (openjtalk_create) {
        printf("\nTrying to create OpenJTalk instance...\n");
        void* instance = openjtalk_create("/sdcard/dummy_dict");
        if (instance) {
            printf("Instance created successfully!\n");
            LOGI("Instance created successfully!");
            
            if (openjtalk_destroy) {
                openjtalk_destroy(instance);
                printf("Instance destroyed.\n");
            }
        } else {
            printf("Failed to create instance (expected without dictionary)\n");
            LOGI("Failed to create instance (expected without dictionary)");
        }
    }
    
    dlclose(handle);
    printf("\nTest completed successfully!\n");
    LOGI("Test completed successfully!");
    
    return 0;
}
EOF
    
    # Compile test program using NDK
    if [ -n "$ANDROID_NDK_HOME" ]; then
        echo "Compiling test program with NDK..."
        case $LIB_ABI in
            "arm64-v8a")
                TARGET="aarch64-linux-android"
                ;;
            "armeabi-v7a")
                TARGET="armv7a-linux-androideabi"
                ;;
            "x86")
                TARGET="i686-linux-android"
                ;;
            "x86_64")
                TARGET="x86_64-linux-android"
                ;;
        esac
        
        $ANDROID_NDK_HOME/toolchains/llvm/prebuilt/*/bin/clang \
            --target=${TARGET}21 \
            -o test_android_device \
            test_android_device.c \
            -ldl -llog
        
        # Push and run test
        adb push test_android_device /data/local/tmp/openjtalk_test/
        adb shell "cd /data/local/tmp/openjtalk_test && chmod +x test_android_device && ./test_android_device"
        
        # Check logcat for our messages
        echo ""
        echo "Checking logcat for test output..."
        adb logcat -d | grep "OpenJTalkTest" | tail -20
    else
        echo "Warning: ANDROID_NDK_HOME not set, skipping compilation test"
    fi
    
    # Cleanup
    echo ""
    echo "Cleaning up..."
    adb shell "rm -rf /data/local/tmp/openjtalk_test"
    rm -f test_android_device test_android_device.c
}

# Function to test with Docker (no device needed)
test_with_docker() {
    echo ""
    echo "=== Testing with Docker (no device needed) ==="
    
    if [ -f verify_android_symbols.sh ]; then
        ./verify_android_symbols.sh
    else
        echo "Warning: verify_android_symbols.sh not found"
    fi
}

# Main execution
echo "Choose test method:"
echo "1. Test on connected Android device/emulator"
echo "2. Test with Docker (symbol verification only)"
echo "3. Run both tests"
read -p "Enter choice (1-3): " choice

case $choice in
    1)
        test_on_device
        ;;
    2)
        test_with_docker
        ;;
    3)
        test_with_docker
        test_on_device
        ;;
    *)
        echo "Invalid choice"
        exit 1
        ;;
esac

echo ""
echo "=== Android Testing Complete ==="