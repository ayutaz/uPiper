#!/bin/bash

# Analyze library sizes and suggest optimizations
set -e

echo "=== Android Library Size Analysis ==="
echo ""

# Function to analyze a library
analyze_library() {
    local ABI=$1
    local SO_FILE="output/android/$ABI/libopenjtalk_wrapper.so"
    
    if [ ! -f "$SO_FILE" ]; then
        echo "Warning: Library not found: $SO_FILE"
        return
    fi
    
    echo "## $ABI Analysis"
    
    # Get file size
    SIZE=$(stat -c%s "$SO_FILE" 2>/dev/null || stat -f%z "$SO_FILE" 2>/dev/null)
    SIZE_MB=$(echo "scale=2; $SIZE / 1048576" | bc)
    echo "File size: $SIZE bytes (${SIZE_MB} MB)"
    
    # Check if stripped
    if file "$SO_FILE" | grep -q "not stripped"; then
        echo "Status: NOT STRIPPED (can be optimized)"
    else
        echo "Status: Already stripped"
    fi
    
    # Analyze sections if we have the NDK tools
    if [ -n "$ANDROID_NDK_HOME" ] && [ -f "$ANDROID_NDK_HOME/toolchains/llvm/prebuilt/linux-x86_64/bin/llvm-size" ]; then
        echo ""
        echo "Section sizes:"
        $ANDROID_NDK_HOME/toolchains/llvm/prebuilt/linux-x86_64/bin/llvm-size "$SO_FILE" | head -5
    fi
    
    echo ""
}

# Check all ABIs
ABIS=("arm64-v8a" "armeabi-v7a" "x86" "x86_64")
for ABI in "${ABIS[@]}"; do
    analyze_library $ABI
done

# Total size calculation
echo "## Total Size Summary"
TOTAL_SIZE=0
for ABI in "${ABIS[@]}"; do
    SO_FILE="output/android/$ABI/libopenjtalk_wrapper.so"
    if [ -f "$SO_FILE" ]; then
        SIZE=$(stat -c%s "$SO_FILE" 2>/dev/null || stat -f%z "$SO_FILE" 2>/dev/null)
        TOTAL_SIZE=$((TOTAL_SIZE + SIZE))
    fi
done

TOTAL_MB=$(echo "scale=2; $TOTAL_SIZE / 1048576" | bc)
echo "Total size for all ABIs: $TOTAL_SIZE bytes (${TOTAL_MB} MB)"
echo ""

# Optimization suggestions
echo "## Optimization Recommendations"
echo ""
echo "1. **Strip Debug Symbols**"
echo "   - Use: \$STRIP --strip-unneeded <library.so>"
echo "   - Typically reduces size by 20-40%"
echo ""
echo "2. **Compiler Optimizations**"
echo "   - Use -Os instead of -O2 for size optimization"
echo "   - Add -ffunction-sections -fdata-sections"
echo "   - Link with -Wl,--gc-sections"
echo ""
echo "3. **LTO (Link Time Optimization)**"
echo "   - Add -flto to both compile and link flags"
echo "   - Can reduce size by 5-15%"
echo ""
echo "4. **Selective ABI Support**"
echo "   - Consider supporting only arm64-v8a for modern devices"
echo "   - This would reduce APK size by ~75%"
echo ""
echo "5. **Dictionary Compression**"
echo "   - OpenJTalk dictionary can be compressed"
echo "   - Load and decompress at runtime"