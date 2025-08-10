@echo off
REM Build OpenJTalk WASM module with Emscripten
REM Requires: Emscripten SDK installed and activated

echo ======================================
echo Building OpenJTalk WASM Module
echo ======================================

REM Check if emcc is available
where emcc >nul 2>nul
if %errorlevel% neq 0 (
    echo Error: emcc not found. Please install and activate Emscripten SDK.
    echo Visit: https://emscripten.org/docs/getting_started/downloads.html
    exit /b 1
)

REM Clean previous build
if exist openjtalk-unity.js del openjtalk-unity.js
if exist openjtalk-unity.wasm del openjtalk-unity.wasm

echo.
echo Compiling openjtalk_wasm.c...

REM Compile with Emscripten
emcc openjtalk_wasm.c ^
    -o openjtalk-unity.js ^
    -s WASM=1 ^
    -s EXPORTED_FUNCTIONS="['_Open_JTalk_initialize','_Open_JTalk_clear','_Open_JTalk_load','_Open_JTalk_synthesis','_malloc','_free','_allocate_memory','_free_memory','_get_string_length']" ^
    -s EXPORTED_RUNTIME_METHODS="['ccall','cwrap','UTF8ToString','stringToUTF8','lengthBytesUTF8']" ^
    -s ALLOW_MEMORY_GROWTH=1 ^
    -s INITIAL_MEMORY=16777216 ^
    -s MODULARIZE=1 ^
    -s EXPORT_NAME="OpenJTalkModule" ^
    -s ENVIRONMENT="web,worker" ^
    -s SINGLE_FILE=0 ^
    -O2 ^
    --no-entry

if %errorlevel% neq 0 (
    echo.
    echo Error: Compilation failed
    exit /b 1
)

echo.
echo Build successful!
echo Generated files:
dir /b *.js *.wasm

echo.
echo Copying to StreamingAssets...
copy /Y openjtalk-unity.js ..\..\..\Assets\StreamingAssets\
copy /Y openjtalk-unity.wasm ..\..\..\Assets\StreamingAssets\

echo.
echo Done! Files copied to Assets/StreamingAssets/
echo.