@echo off
REM Build script for OpenJTalk dependencies on Windows
REM Builds hts_engine_API and OpenJTalk in the correct order

setlocal enabledelayedexpansion

set SCRIPT_DIR=%~dp0
set EXTERNAL_DIR=%SCRIPT_DIR%external
set BUILD_DIR=%SCRIPT_DIR%external\openjtalk_build

REM Create build directory
if not exist "%BUILD_DIR%" mkdir "%BUILD_DIR%"

echo Building OpenJTalk dependencies for Windows...

REM Build hts_engine_API
echo === Building hts_engine_API ===
cd /d "%EXTERNAL_DIR%\hts_engine_API-1.10"

if not exist "build" mkdir build
cd build

REM Configure with CMake
cmake .. -DCMAKE_INSTALL_PREFIX="%BUILD_DIR%\install"
if errorlevel 1 (
    echo Failed to configure hts_engine_API
    exit /b 1
)

REM Build
cmake --build . --config Release
if errorlevel 1 (
    echo Failed to build hts_engine_API
    exit /b 1
)

REM Install
cmake --build . --config Release --target install
if errorlevel 1 (
    echo Failed to install hts_engine_API
    exit /b 1
)

REM Build OpenJTalk
echo === Building OpenJTalk ===
cd /d "%EXTERNAL_DIR%\open_jtalk-1.11"

if not exist "build" mkdir build
cd build

REM Configure with CMake (if CMakeLists.txt exists)
if exist "..\CMakeLists.txt" (
    cmake .. -DCMAKE_INSTALL_PREFIX="%BUILD_DIR%\install" ^
             -DHTS_ENGINE_INCLUDE_DIR="%BUILD_DIR%\install\include" ^
             -DHTS_ENGINE_LIB="%BUILD_DIR%\install\lib\HTSEngine.lib"
    cmake --build . --config Release
) else (
    REM Use Visual Studio project if available
    echo Note: OpenJTalk Windows build requires manual setup
    echo Please build OpenJTalk manually or use pre-built libraries
)

echo === Dependencies build completed ===
echo Libraries location: %BUILD_DIR%

endlocal