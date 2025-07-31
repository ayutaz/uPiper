@echo off
setlocal enabledelayedexpansion

echo ===================================
echo Building Flite for Unity (Windows)
echo ===================================

:: Check for CMake
where cmake >nul 2>nul
if %errorlevel% neq 0 (
    echo Error: CMake not found in PATH
    echo Please install CMake from https://cmake.org/
    exit /b 1
)

:: Create build directory
if not exist build mkdir build
cd build

:: Configure
echo Configuring...
cmake .. -G "Visual Studio 17 2022" -A x64 -DCMAKE_BUILD_TYPE=MinSizeRel

if %errorlevel% neq 0 (
    echo Error: CMake configuration failed
    exit /b 1
)

:: Build
echo Building...
cmake --build . --config MinSizeRel

if %errorlevel% neq 0 (
    echo Error: Build failed
    exit /b 1
)

:: Copy to Unity plugins folder
echo Copying to Unity plugins folder...
if not exist "..\..\..\..\Assets\uPiper\Plugins\Windows\x86_64" (
    mkdir "..\..\..\..\Assets\uPiper\Plugins\Windows\x86_64"
)

copy /Y MinSizeRel\flite_unity.dll "..\..\..\..\Assets\uPiper\Plugins\Windows\x86_64\"

echo ===================================
echo Build completed successfully!
echo ===================================

cd ..
endlocal