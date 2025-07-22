#!/bin/bash

# Analyze dependencies for Android portability
# This script checks OpenJTalk and its dependencies for Android compatibility

set -e

echo "=== Android Dependency Analysis ==="
echo ""

# Function to check for Android-incompatible features
check_android_compatibility() {
    local src_dir=$1
    local lib_name=$2
    
    echo "Analyzing $lib_name..."
    
    # Check for problematic system calls
    ISSUES=()
    
    # Check for file I/O that might need adaptation
    if grep -r "fopen\|freopen\|tmpfile" "$src_dir" --include="*.c" --include="*.h" &> /dev/null; then
        ISSUES+=("Uses file I/O (may need path adaptation)")
    fi
    
    # Check for locale dependencies
    if grep -r "setlocale\|locale\.h" "$src_dir" --include="*.c" --include="*.h" &> /dev/null; then
        ISSUES+=("Uses locale functions (limited on Android)")
    fi
    
    # Check for signal handling
    if grep -r "signal\.h\|sigaction" "$src_dir" --include="*.c" --include="*.h" &> /dev/null; then
        ISSUES+=("Uses signal handling (limited on Android)")
    fi
    
    # Check for dynamic loading
    if grep -r "dlopen\|dlsym" "$src_dir" --include="*.c" --include="*.h" &> /dev/null; then
        ISSUES+=("Uses dynamic loading (requires special handling)")
    fi
    
    # Check for threading
    if grep -r "pthread\.h\|thread\.h" "$src_dir" --include="*.c" --include="*.h" &> /dev/null; then
        ISSUES+=("Uses threading (supported but needs -pthread)")
    fi
    
    # Check for system/exec calls
    if grep -r "system\|exec\|fork" "$src_dir" --include="*.c" --include="*.h" &> /dev/null; then
        ISSUES+=("Uses process spawning (not supported on Android)")
    fi
    
    # Report findings
    if [ ${#ISSUES[@]} -eq 0 ]; then
        echo "  ✓ No major compatibility issues found"
    else
        echo "  ⚠ Potential issues:"
        for issue in "${ISSUES[@]}"; do
            echo "    - $issue"
        done
    fi
    echo ""
}

# Check if dependencies exist
if [ ! -d "external" ]; then
    echo "Dependencies not found. Running fetch_dependencies.sh first..."
    ./fetch_dependencies.sh
fi

# Analyze each component
echo "=== Component Analysis ==="
echo ""

# HTS Engine
if [ -d "external/hts_engine_API-1.10" ]; then
    check_android_compatibility "external/hts_engine_API-1.10" "HTS Engine"
else
    echo "⚠ HTS Engine source not found"
fi

# OpenJTalk mecab
if [ -d "external/open_jtalk-1.11/mecab" ]; then
    check_android_compatibility "external/open_jtalk-1.11/mecab" "MeCab (in OpenJTalk)"
else
    echo "⚠ OpenJTalk MeCab source not found"
fi

# OpenJTalk text processing
if [ -d "external/open_jtalk-1.11" ]; then
    check_android_compatibility "external/open_jtalk-1.11/text2mecab" "OpenJTalk Text Processing"
fi

# Check memory requirements
echo "=== Memory Requirements Analysis ==="
echo ""

# Estimate dictionary size
if [ -d "dictionary" ]; then
    DICT_SIZE=$(du -sh dictionary | cut -f1)
    echo "Dictionary size: $DICT_SIZE"
    
    # Check individual files
    echo "Dictionary components:"
    for file in dictionary/*.bin dictionary/*.dic dictionary/*.def; do
        if [ -f "$file" ]; then
            SIZE=$(du -sh "$file" | cut -f1)
            echo "  - $(basename $file): $SIZE"
        fi
    done
else
    echo "⚠ Dictionary not found"
fi

echo ""
echo "=== Build Configuration Recommendations ==="
echo ""

# Generate recommended CMake flags
cat << EOF
Recommended CMake configuration for Android:

cmake -DCMAKE_TOOLCHAIN_FILE=\$ANDROID_NDK_HOME/build/cmake/android.toolchain.cmake \\
      -DANDROID_ABI=arm64-v8a \\
      -DANDROID_PLATFORM=android-21 \\
      -DANDROID_STL=c++_static \\
      -DCMAKE_BUILD_TYPE=Release \\
      -DCMAKE_C_FLAGS="-ffunction-sections -fdata-sections" \\
      -DCMAKE_CXX_FLAGS="-ffunction-sections -fdata-sections" \\
      -DCMAKE_SHARED_LINKER_FLAGS="-Wl,--gc-sections -Wl,--strip-all" \\
      ..

Additional considerations:
1. Use static STL (c++_static) to avoid STL version conflicts
2. Enable function/data sections for size optimization
3. Strip symbols in release builds
4. Consider using LTO (Link Time Optimization) for further size reduction
EOF

echo ""
echo "=== Android-specific Implementation Notes ==="
echo ""

cat << EOF
1. File Access:
   - Use Android asset manager for dictionary files
   - Paths will need adjustment for APK packaging
   
2. Memory Management:
   - Implement dictionary compression (zlib/lz4)
   - Consider memory mapping for large files
   
3. JNI Considerations:
   - Keep JNI calls minimal (batch operations)
   - Use Direct ByteBuffers for large data transfers
   
4. Testing Requirements:
   - Test on both 32-bit and 64-bit devices
   - Verify on Android 5.0 (API 21) minimum
   - Profile memory usage on low-end devices
EOF

echo ""
echo "=== Summary ==="
echo "Analysis complete. Most components appear Android-compatible with minor adaptations needed."