@echo off
echo === Building Android libraries with Docker ===
echo Current directory: %cd%
docker run --rm -v "%cd%:/workspace" -w /workspace android-openjtalk-builder sh -c "cd /workspace && ./build_android.sh"
if errorlevel 1 (
    echo Build failed!
    exit /b 1
)
echo === Build complete ===