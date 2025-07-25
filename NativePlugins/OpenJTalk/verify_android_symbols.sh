#!/bin/bash

# Verify JNI symbols in Android shared libraries
set -e

echo "=== Verifying Android JNI Symbols ==="

# Check if output directory exists
OUTPUT_DIR="output/android"
if [ ! -d "$OUTPUT_DIR" ]; then
    echo "Error: Output directory not found: $OUTPUT_DIR"
    exit 1
fi

# Function to check symbols for an ABI
check_symbols() {
    local ABI=$1
    local SO_FILE="$OUTPUT_DIR/$ABI/libopenjtalk_wrapper.so"
    
    echo ""
    echo "Checking $ABI..."
    
    if [ ! -f "$SO_FILE" ]; then
        echo "  Warning: Library not found: $SO_FILE"
        return
    fi
    
    # Get file size
    SIZE=$(ls -lh "$SO_FILE" | awk '{print $5}')
    echo "  File size: $SIZE"
    
    # Check if we have the Android NDK nm tool
    if [ -z "$ANDROID_NDK_HOME" ]; then
        export ANDROID_NDK_HOME=/opt/android-ndk
    fi
    
    # Use the appropriate nm for the architecture
    case $ABI in
        "arm64-v8a")
            NM="$ANDROID_NDK_HOME/toolchains/llvm/prebuilt/linux-x86_64/bin/llvm-nm"
            ;;
        "armeabi-v7a")
            NM="$ANDROID_NDK_HOME/toolchains/llvm/prebuilt/linux-x86_64/bin/llvm-nm"
            ;;
        "x86")
            NM="$ANDROID_NDK_HOME/toolchains/llvm/prebuilt/linux-x86_64/bin/llvm-nm"
            ;;
        "x86_64")
            NM="$ANDROID_NDK_HOME/toolchains/llvm/prebuilt/linux-x86_64/bin/llvm-nm"
            ;;
    esac
    
    # Check for exported symbols
    echo "  Exported symbols:"
    if [ -f "$NM" ]; then
        $NM -D "$SO_FILE" | grep -E "openjtalk_" | head -10 || echo "    No openjtalk symbols found"
    else
        # Fallback to system nm
        nm -D "$SO_FILE" 2>/dev/null | grep -E "openjtalk_" | head -10 || echo "    No openjtalk symbols found"
    fi
    
    # Check for JNI symbols
    echo "  JNI symbols:"
    if [ -f "$NM" ]; then
        $NM -D "$SO_FILE" | grep -E "Java_" | head -5 || echo "    No JNI symbols found (this is expected for P/Invoke)"
    else
        nm -D "$SO_FILE" 2>/dev/null | grep -E "Java_" | head -5 || echo "    No JNI symbols found (this is expected for P/Invoke)"
    fi
    
    # Check for undefined symbols
    echo "  Checking for undefined symbols:"
    if [ -f "$NM" ]; then
        UNDEFINED=$($NM -u "$SO_FILE" | grep -v "__" | head -5 || true)
    else
        UNDEFINED=$(nm -u "$SO_FILE" 2>/dev/null | grep -v "__" | head -5 || true)
    fi
    
    if [ -z "$UNDEFINED" ]; then
        echo "    No problematic undefined symbols found"
    else
        echo "    Found undefined symbols:"
        echo "$UNDEFINED" | sed 's/^/      /'
    fi
}

# Check all ABIs
ABIS=("arm64-v8a" "armeabi-v7a" "x86" "x86_64")
for ABI in "${ABIS[@]}"; do
    check_symbols $ABI
done

echo ""
echo "=== Symbol Verification Complete ==="
echo ""
echo "Note: P/Invoke uses direct symbol names (e.g., openjtalk_create)"
echo "      JNI would use Java_* prefixed symbols"