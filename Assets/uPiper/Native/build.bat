@echo off
REM Build script for uPiper native libraries on Windows

setlocal enabledelayedexpansion

set SCRIPT_DIR=%~dp0
set BUILD_DIR=%SCRIPT_DIR%Build
set OUTPUT_BASE=%SCRIPT_DIR%..\Plugins

echo [INFO] Starting uPiper native library build for Windows...

REM Check for required tools
where cmake >nul 2>nul
if errorlevel 1 (
    echo [ERROR] CMake not found. Please install CMake and add it to PATH.
    exit /b 1
)

REM Clean build directory
echo [INFO] Cleaning build directory...
if exist "%BUILD_DIR%" rmdir /s /q "%BUILD_DIR%"
mkdir "%BUILD_DIR%"

REM Create output directories
mkdir "%OUTPUT_BASE%\Windows\x86_64" 2>nul

REM Change to build directory
cd /d "%BUILD_DIR%"

REM Configure with CMake
echo [INFO] Configuring with CMake...

REM Try different generators
set CMAKE_GENERATOR=
set BUILD_COMMAND=

REM Check for Visual Studio
where cl >nul 2>nul
if not errorlevel 1 (
    echo [INFO] Found Visual Studio compiler
    set CMAKE_GENERATOR="Visual Studio 17 2022"
    set BUILD_COMMAND=cmake --build . --config Release
    goto :configure
)

REM Check for MinGW
where mingw32-make >nul 2>nul
if not errorlevel 1 (
    echo [INFO] Found MinGW
    set CMAKE_GENERATOR="MinGW Makefiles"
    set BUILD_COMMAND=mingw32-make
    goto :configure
)

REM Check for Ninja
where ninja >nul 2>nul
if not errorlevel 1 (
    echo [INFO] Found Ninja
    set CMAKE_GENERATOR="Ninja"
    set BUILD_COMMAND=ninja
    goto :configure
)

echo [ERROR] No suitable build system found. Please install Visual Studio, MinGW, or Ninja.
exit /b 1

:configure
echo [INFO] Using generator: %CMAKE_GENERATOR%
cmake .. -G %CMAKE_GENERATOR% -DCMAKE_BUILD_TYPE=Release
if errorlevel 1 (
    echo [ERROR] CMake configuration failed
    exit /b 1
)

REM Build
echo [INFO] Building...
%BUILD_COMMAND%
if errorlevel 1 (
    echo [ERROR] Build failed
    exit /b 1
)

REM Copy the built library
echo [INFO] Copying library...
if exist "%OUTPUT_BASE%\Windows\openjtalk_wrapper.dll" (
    copy /y "%OUTPUT_BASE%\Windows\openjtalk_wrapper.dll" "%OUTPUT_BASE%\Windows\x86_64\"
    echo [SUCCESS] Library copied to %OUTPUT_BASE%\Windows\x86_64\
) else (
    echo [ERROR] Built library not found
    exit /b 1
)

echo [SUCCESS] Build completed successfully!
echo [INFO] Library location: %OUTPUT_BASE%\Windows\x86_64\openjtalk_wrapper.dll

endlocal