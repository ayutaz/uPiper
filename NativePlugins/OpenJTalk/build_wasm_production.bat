@echo off
echo Building production OpenJTalk WebAssembly module...

REM Build using Docker
docker build -f Dockerfile.wasm.production -t openjtalk-wasm-prod .
if %ERRORLEVEL% neq 0 (
    echo Docker build failed!
    exit /b 1
)

REM Create output directory
if not exist "build_wasm_output" mkdir build_wasm_output

REM Copy built files from container
docker create --name openjtalk-wasm-temp openjtalk-wasm-prod
docker cp openjtalk-wasm-temp:/build/openjtalk.js build_wasm_output/
docker cp openjtalk-wasm-temp:/build/openjtalk.wasm build_wasm_output/
docker rm openjtalk-wasm-temp

REM Copy to Unity StreamingAssets
echo Copying files to Unity StreamingAssets...
copy /Y build_wasm_output\openjtalk.js ..\..\Assets\StreamingAssets\
copy /Y build_wasm_output\openjtalk.wasm ..\..\Assets\StreamingAssets\

echo Build complete!
echo Files copied to Assets/StreamingAssets/