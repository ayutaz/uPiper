@echo off
echo Building OpenJTalk for WebAssembly using Docker...

REM Build Docker image
echo Building Docker image...
docker build -f Dockerfile.wasm -t openjtalk-wasm-builder .

REM Run the build
echo Running build in Docker container...
docker run --rm -v "%CD%:/output" openjtalk-wasm-builder sh -c "cp -r /build/build_wasm/lib/* /output/"

REM Copy output files to Unity
echo Copying files to Unity StreamingAssets...
if exist "openjtalk_wrapper.js" (
    copy /Y "openjtalk_wrapper.js" "..\..\Assets\StreamingAssets\openjtalk.js"
    copy /Y "openjtalk_wrapper.wasm" "..\..\Assets\StreamingAssets\openjtalk.wasm"
    echo Build successful! Files copied to Unity.
) else (
    echo Error: Build files not found.
    exit /b 1
)

echo Done!