@echo off
echo Building simple OpenJTalk WebAssembly module...

REM Simple build script for minimal OpenJTalk WASM
docker run --rm -v "%cd%:/src" -w /src emscripten/emsdk:3.1.50 emcc ^
    src/openjtalk_wasm_wrapper.c ^
    -o output/openjtalk_simple.js ^
    -s EXPORTED_FUNCTIONS="['_openjtalk_test','_malloc','_free']" ^
    -s EXPORTED_RUNTIME_METHODS="['ccall','cwrap','UTF8ToString','stringToUTF8','lengthBytesUTF8']" ^
    -s MODULARIZE=1 ^
    -s EXPORT_NAME="OpenJTalkModule" ^
    -s ENVIRONMENT="web" ^
    -s ALLOW_MEMORY_GROWTH=1 ^
    -O2

if %ERRORLEVEL% EQU 0 (
    echo Build successful!
    echo Output files: output/openjtalk_simple.js and output/openjtalk_simple.wasm
) else (
    echo Build failed!
)