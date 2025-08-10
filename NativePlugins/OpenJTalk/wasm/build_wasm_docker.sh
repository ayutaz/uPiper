#!/bin/bash
set -e

echo "========================================"
echo "Building OpenJTalk WASM Module with Docker"
echo "========================================"

# Clean previous build
rm -f openjtalk-unity.js openjtalk-unity.wasm

echo ""
echo "Compiling openjtalk_wasm.c with full implementation..."

# Compile with Emscripten - Full production build
emcc openjtalk_wasm.c \
    -o openjtalk-unity.js \
    -s WASM=1 \
    -s EXPORTED_FUNCTIONS="['_Open_JTalk_initialize','_Open_JTalk_clear','_Open_JTalk_load','_Open_JTalk_synthesis','_malloc','_free','_allocate_memory','_free_memory','_get_string_length']" \
    -s EXPORTED_RUNTIME_METHODS="['ccall','cwrap','UTF8ToString','stringToUTF8','lengthBytesUTF8']" \
    -s ALLOW_MEMORY_GROWTH=1 \
    -s INITIAL_MEMORY=16777216 \
    -s MODULARIZE=1 \
    -s EXPORT_NAME="OpenJTalkModule" \
    -s ENVIRONMENT="web,worker,node" \
    -s SINGLE_FILE=0 \
    -s FILESYSTEM=0 \
    -s ASSERTIONS=1 \
    -s SAFE_HEAP=1 \
    -O2 \
    --no-entry

echo ""
echo "Build successful!"
echo "Generated files:"
ls -la *.js *.wasm

echo ""
echo "Testing with Node.js..."
if [ -f test-openjtalk.mjs ]; then
    node test-openjtalk.mjs || true
fi

echo ""
echo "Done!"