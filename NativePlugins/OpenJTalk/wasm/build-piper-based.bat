@echo off
echo ============================================
echo Building Full OpenJTalk WASM (piper-plus approach)
echo Using C++14 to avoid std::binary_function issues
echo ============================================

cd /d %~dp0

echo.
echo Step 1: Building Docker image...
docker build -f Dockerfile.piper-based -t openjtalk-piper-builder .
if %ERRORLEVEL% neq 0 (
    echo ERROR: Docker build failed
    exit /b 1
)

echo.
echo Step 2: Running build in container...
docker run --rm -v "%cd%\output:/output" openjtalk-piper-builder
if %ERRORLEVEL% neq 0 (
    echo ERROR: Build failed
    exit /b 1
)

echo.
echo Step 3: Checking output files...
if exist "output\openjtalk-unity-full.js" (
    echo [OK] openjtalk-unity-full.js created
) else (
    echo [ERROR] openjtalk-unity-full.js not found
    exit /b 1
)

if exist "output\openjtalk-unity-full.wasm" (
    echo [OK] openjtalk-unity-full.wasm created
) else (
    echo [ERROR] openjtalk-unity-full.wasm not found
    exit /b 1
)

if exist "output\openjtalk-unity-full.data" (
    echo [OK] openjtalk-unity-full.data created (dictionary embedded)
)

echo.
echo Step 4: Testing with Node.js...
cd output
node test-full.mjs
if %ERRORLEVEL% neq 0 (
    echo WARNING: Node.js test failed or not available
)
cd ..

echo.
echo Step 5: Copying to StreamingAssets...
copy /Y "output\openjtalk-unity-full.js" "..\..\..\Assets\StreamingAssets\openjtalk-unity.js"
copy /Y "output\openjtalk-unity-full.wasm" "..\..\..\Assets\StreamingAssets\openjtalk-unity.wasm"
if exist "output\openjtalk-unity-full.data" (
    copy /Y "output\openjtalk-unity-full.data" "..\..\..\Assets\StreamingAssets\openjtalk-unity.data"
)

echo.
echo ============================================
echo Build completed successfully!
echo ============================================
echo Output files in: %cd%\output
echo.
dir output\openjtalk-unity-full.*
echo.