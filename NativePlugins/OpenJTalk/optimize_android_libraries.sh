#!/bin/bash

# Optimize Android libraries by stripping debug symbols
set -e

echo "=== Optimizing Android Libraries ==="

# Check for Android NDK
if [ -z "$ANDROID_NDK_HOME" ]; then
    export ANDROID_NDK_HOME=/opt/android-ndk
fi

# Function to strip a library
strip_library() {
    local ABI=$1
    local SO_FILE="output/android/$ABI/libopenjtalk_wrapper.so"
    local STRIP_TOOL="$ANDROID_NDK_HOME/toolchains/llvm/prebuilt/linux-x86_64/bin/llvm-strip"
    
    if [ ! -f "$SO_FILE" ]; then
        echo "Warning: Library not found: $SO_FILE"
        return
    fi
    
    echo ""
    echo "Processing $ABI..."
    
    # Get original size
    ORIG_SIZE=$(stat -c%s "$SO_FILE" 2>/dev/null || stat -f%z "$SO_FILE" 2>/dev/null)
    echo "Original size: $ORIG_SIZE bytes"
    
    # Backup original
    cp "$SO_FILE" "${SO_FILE}.orig"
    
    # Strip the library
    if [ -f "$STRIP_TOOL" ]; then
        $STRIP_TOOL --strip-unneeded "$SO_FILE"
    else
        # Fallback to system strip
        strip --strip-unneeded "$SO_FILE"
    fi
    
    # Get new size
    NEW_SIZE=$(stat -c%s "$SO_FILE" 2>/dev/null || stat -f%z "$SO_FILE" 2>/dev/null)
    echo "Optimized size: $NEW_SIZE bytes"
    
    # Calculate reduction
    REDUCTION=$((ORIG_SIZE - NEW_SIZE))
    PERCENT=$(echo "scale=1; ($REDUCTION * 100) / $ORIG_SIZE" | bc)
    echo "Size reduction: $REDUCTION bytes (${PERCENT}%)"
}

# Process all ABIs
ABIS=("arm64-v8a" "armeabi-v7a" "x86" "x86_64")
for ABI in "${ABIS[@]}"; do
    strip_library $ABI
done

echo ""
echo "=== Optimization Complete ==="