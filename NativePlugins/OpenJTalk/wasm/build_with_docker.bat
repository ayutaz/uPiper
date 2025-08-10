@echo off
echo ========================================
echo Building OpenJTalk WASM with Docker
echo ========================================

REM Create output directory
if not exist output mkdir output

REM Check if Docker is installed
where docker >nul 2>nul
if %errorlevel% neq 0 (
    echo Error: Docker is not installed or not in PATH
    echo Please install Docker Desktop from https://www.docker.com/products/docker-desktop
    exit /b 1
)

REM Check if Docker is running
docker info >nul 2>nul
if %errorlevel% neq 0 (
    echo Error: Docker is not running
    echo Please start Docker Desktop
    exit /b 1
)

echo.
echo Building Docker image...
docker-compose build

echo.
echo Running build in Docker container...
docker-compose up

echo.
echo Copying built files to Unity Assets...
if exist output\openjtalk-unity.js (
    copy /Y output\openjtalk-unity.js ..\..\..\Assets\StreamingAssets\
    copy /Y output\openjtalk-unity.wasm ..\..\..\Assets\StreamingAssets\
    echo Files copied to Assets\StreamingAssets\
) else (
    echo Error: Build files not found in output directory
    exit /b 1
)

echo.
echo Build complete!
echo.
echo You can now test with:
echo   node test-openjtalk.mjs
echo.