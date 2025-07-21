@echo off
echo Building OpenJTalk for Windows using Docker...

REM Build Docker image
docker build -f Dockerfile.windows-cross -t openjtalk-windows-cross .

REM Run build in Docker container
docker run --rm -v "%CD%:/workspace" openjtalk-windows-cross /bin/bash -c "cd /workspace && ./build_full_windows.sh"

echo Build completed. Check the output directory for the DLL.