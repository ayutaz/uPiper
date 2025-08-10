@echo off
echo ============================================
echo Building Simplified OpenJTalk WASM
echo ============================================

cd /d %~dp0

echo.
echo Step 1: Building Docker image...
docker build -f Dockerfile.simple -t openjtalk-simple-builder .
if %ERRORLEVEL% neq 0 (
    echo ERROR: Docker build failed
    exit /b 1
)

echo.
echo Step 2: Running build in container...
docker run --rm -v "%cd%\output:/output" openjtalk-simple-builder
if %ERRORLEVEL% neq 0 (
    echo ERROR: Build failed
    exit /b 1
)

echo.
echo Step 3: Checking output files...
if exist "output\openjtalk-unity-dict.js" (
    echo [OK] openjtalk-unity-dict.js created
) else (
    echo [ERROR] openjtalk-unity-dict.js not found
    exit /b 1
)

if exist "output\openjtalk-unity-dict.wasm" (
    echo [OK] openjtalk-unity-dict.wasm created
) else (
    echo [ERROR] openjtalk-unity-dict.wasm not found
    exit /b 1
)

echo.
echo Step 4: Copying files to StreamingAssets...
copy /Y "output\openjtalk-unity-dict.js" "..\..\..\Assets\StreamingAssets\openjtalk-unity.js"
copy /Y "output\openjtalk-unity-dict.wasm" "..\..\..\Assets\StreamingAssets\openjtalk-unity.wasm"
if exist "output\openjtalk-unity-dict.data" (
    copy /Y "output\openjtalk-unity-dict.data" "..\..\..\Assets\StreamingAssets\openjtalk-unity.data"
)

echo.
echo ============================================
echo Build completed successfully!
echo ============================================
echo Output files in: %cd%\output
echo Copied to: Assets\StreamingAssets\
echo.
dir output\openjtalk-unity-dict.*
echo.