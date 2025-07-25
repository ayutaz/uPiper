@echo off
REM Build OpenJTalk for Android using Docker

echo === Docker Android Build ===
echo.

REM Change to script directory
cd /d "%~dp0"

REM Ensure Docker image is up to date
echo Updating Docker image...
docker-compose build android-build

if errorlevel 1 (
    echo.
    echo Error: Failed to build/update Docker image
    exit /b 1
)

echo.
echo Starting Android build...
echo This will build OpenJTalk for all Android architectures.
echo.

REM Run the build
docker-compose run --rm android-build ./build_android.sh

if errorlevel 1 (
    echo.
    echo Error: Android build failed
    echo Check the error messages above for details
    exit /b 1
)

echo.
echo === Build Complete ===
echo.
echo Build artifacts should be in:
echo - output/android/ (native libraries)
echo - ../../Assets/uPiper/Plugins/Android/ (Unity plugin structure)
echo.

REM Check if files were created
if exist "output\android\arm64-v8a\libopenjtalk_wrapper.so" (
    echo Verified: arm64-v8a library created
) else (
    echo Warning: arm64-v8a library not found
)

pause