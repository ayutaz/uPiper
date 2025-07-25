@echo off
REM Docker environment test for Android build

echo === Docker Android Build Environment Test ===
echo.

REM Change to script directory
cd /d "%~dp0"

echo Building Docker image...
docker-compose build android-build

if errorlevel 1 (
    echo.
    echo Error: Failed to build Docker image
    echo Please make sure Docker is running and docker-compose is installed
    exit /b 1
)

echo.
echo Running Android NDK environment test...
docker-compose run --rm android-build ./test_android_env.sh

if errorlevel 1 (
    echo.
    echo Error: Android NDK environment test failed
    exit /b 1
)

echo.
echo Running dependency analysis...
docker-compose run --rm android-build ./analyze_android_dependencies.sh

echo.
echo === Test Complete ===
echo.
echo Next steps:
echo 1. If all tests passed, you can proceed with the Android build
echo 2. To build OpenJTalk for Android, run: docker_build_android.bat
echo 3. To enter the build environment interactively: docker-compose run --rm android-build