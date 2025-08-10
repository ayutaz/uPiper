@echo off
echo ============================================
echo Building Full OpenJTalk WASM with Dictionary (Fixed)
echo ============================================

cd /d %~dp0

echo.
echo Step 1: Building Docker image...
docker build -f Dockerfile.full2 -t openjtalk-full2-builder .
if %ERRORLEVEL% neq 0 (
    echo ERROR: Docker build failed
    exit /b 1
)

echo.
echo Step 2: Running build in container...
docker run --rm -v "%cd%\output:/output" openjtalk-full2-builder
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
) else (
    echo [WARNING] openjtalk-unity-full.data not found
)

echo.
echo Step 4: Copying to StreamingAssets...
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