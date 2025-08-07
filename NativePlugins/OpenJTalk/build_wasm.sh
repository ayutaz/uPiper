#!/bin/bash

# Build OpenJTalk for WebAssembly (Emscripten)

echo "Building OpenJTalk for WebAssembly..."

# Check if emcc is available
if ! command -v emcc &> /dev/null; then
    echo "Error: Emscripten (emcc) not found. Please install Emscripten SDK."
    exit 1
fi

# Set build directory
BUILD_DIR="build_wasm"
INSTALL_DIR="install_wasm"

# Clean previous build
rm -rf "$BUILD_DIR" "$INSTALL_DIR"
mkdir -p "$BUILD_DIR"
cd "$BUILD_DIR"

# Configure with Emscripten
echo "Configuring with Emscripten..."
emcmake cmake .. \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_INSTALL_PREFIX="../$INSTALL_DIR" \
    -DBUILD_TESTS=OFF \
    -DBUILD_BENCHMARK=OFF \
    -DEMSCRIPTEN=ON

# Add Emscripten-specific flags to CMakeLists.txt if not present
if ! grep -q "EMSCRIPTEN" ../CMakeLists.txt; then
    echo "Adding Emscripten support to CMakeLists.txt..."
    cat >> ../CMakeLists.txt << 'EOF'

# Emscripten (WebAssembly) specific settings
if(EMSCRIPTEN)
    set(CMAKE_EXECUTABLE_SUFFIX ".js")
    
    # Emscripten link flags
    set(EMSCRIPTEN_LINK_FLAGS
        "-sEXPORTED_RUNTIME_METHODS=['cwrap','ccall','getValue','setValue','allocate','allocateUTF8','UTF8ToString','lengthBytesUTF8','stringToUTF8','_malloc','_free']"
        "-sEXPORTED_FUNCTIONS=['_malloc','_free','_openjtalk_initialize','_openjtalk_synthesis_labels','_openjtalk_free_string','_openjtalk_clear']"
        "-sALLOW_MEMORY_GROWTH=1"
        "-sINITIAL_MEMORY=67108864"  # 64MB
        "-sSTACK_SIZE=16777216"       # 16MB
        "-sMODULARIZE=1"
        "-sEXPORT_NAME='OpenJTalkModule'"
        "-sEXPORT_ES6=0"              # Use ES5 format
        "-sUSE_ES6_IMPORT_META=0"
        "-sENVIRONMENT='web,worker'"
        "-sFILESYSTEM=1"
        "-sWASM=1"
        "-sWASM_ASYNC_COMPILATION=1"
    )
    
    # Apply flags to the library
    string(REPLACE ";" " " EMSCRIPTEN_LINK_FLAGS_STR "${EMSCRIPTEN_LINK_FLAGS}")
    set_target_properties(openjtalk_wrapper PROPERTIES
        LINK_FLAGS "${EMSCRIPTEN_LINK_FLAGS_STR}"
        SUFFIX ".js"
    )
    
    # Change library type to STATIC for Emscripten
    set_target_properties(openjtalk_wrapper PROPERTIES
        POSITION_INDEPENDENT_CODE OFF
    )
endif()
EOF
fi

# Build
echo "Building..."
emmake make -j$(nproc)

# Install
echo "Installing..."
make install

# Copy output files to Unity StreamingAssets
echo "Copying files to Unity..."
UNITY_DIR="../../Assets/StreamingAssets"
mkdir -p "$UNITY_DIR"

if [ -f "lib/openjtalk_wrapper.js" ]; then
    cp "lib/openjtalk_wrapper.js" "$UNITY_DIR/openjtalk.js"
    cp "lib/openjtalk_wrapper.wasm" "$UNITY_DIR/openjtalk.wasm"
    echo "WebAssembly build successful!"
else
    echo "Error: Build files not found"
    exit 1
fi

cd ..
echo "Done!"