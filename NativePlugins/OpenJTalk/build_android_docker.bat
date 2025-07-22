@echo off
REM Build Android libraries using Docker

setlocal

REM Build Docker image
echo Building Docker image for Android build...
docker build -f Dockerfile.android -t upiper-android-build .

if errorlevel 1 (
    echo Failed to build Docker image
    exit /b 1
)

REM Run build in Docker container
echo Running Android build in Docker...
docker run --rm -v "%cd%:/workspace" upiper-android-build /workspace/build_android.sh

if errorlevel 1 (
    echo Failed to run Android build
    exit /b 1
)

echo Android build completed successfully!

endlocal