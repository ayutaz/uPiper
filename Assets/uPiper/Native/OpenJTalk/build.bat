@echo off
setlocal EnableDelayedExpansion

:: OpenJTalk Wrapper Build Script for Windows
:: Supports Visual Studio 2019 and 2022

echo ===================================
echo OpenJTalk Wrapper Build for Windows
echo ===================================

:: Check for command line arguments
set BUILD_TYPE=Release
set ARCH=x64
set VS_VERSION=

if "%1"=="Debug" set BUILD_TYPE=Debug
if "%1"=="debug" set BUILD_TYPE=Debug
if "%2"=="x86" set ARCH=x86
if "%2"=="Win32" set ARCH=x86

:: Detect Visual Studio
echo.
echo Detecting Visual Studio installation...

:: Check for VS2022
if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvarsall.bat" (
    set VS_PATH=%ProgramFiles%\Microsoft Visual Studio\2022\Community
    set VS_VERSION=2022
    goto :VS_FOUND
)
if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Professional\VC\Auxiliary\Build\vcvarsall.bat" (
    set VS_PATH=%ProgramFiles%\Microsoft Visual Studio\2022\Professional
    set VS_VERSION=2022
    goto :VS_FOUND
)
if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Enterprise\VC\Auxiliary\Build\vcvarsall.bat" (
    set VS_PATH=%ProgramFiles%\Microsoft Visual Studio\2022\Enterprise
    set VS_VERSION=2022
    goto :VS_FOUND
)

:: Check for VS2019
if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Community\VC\Auxiliary\Build\vcvarsall.bat" (
    set VS_PATH=%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Community
    set VS_VERSION=2019
    goto :VS_FOUND
)
if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Professional\VC\Auxiliary\Build\vcvarsall.bat" (
    set VS_PATH=%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Professional
    set VS_VERSION=2019
    goto :VS_FOUND
)
if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Enterprise\VC\Auxiliary\Build\vcvarsall.bat" (
    set VS_PATH=%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Enterprise
    set VS_VERSION=2019
    goto :VS_FOUND
)

echo ERROR: Visual Studio 2019 or 2022 not found!
echo Please install Visual Studio with C++ development tools.
exit /b 1

:VS_FOUND
echo Found Visual Studio %VS_VERSION% at: %VS_PATH%

:: Set up Visual Studio environment
echo.
echo Setting up build environment for %ARCH%...
call "%VS_PATH%\VC\Auxiliary\Build\vcvarsall.bat" %ARCH%
if errorlevel 1 (
    echo ERROR: Failed to set up Visual Studio environment
    exit /b 1
)

:: Create build directory
set BUILD_DIR=build\windows\%ARCH%\%BUILD_TYPE%
if not exist %BUILD_DIR% mkdir %BUILD_DIR%
cd %BUILD_DIR%

:: Configure with CMake
echo.
echo Configuring with CMake...
cmake -G "NMake Makefiles" ^
    -DCMAKE_BUILD_TYPE=%BUILD_TYPE% ^
    -DCMAKE_INSTALL_PREFIX=..\..\..\..\output\windows\%ARCH% ^
    ..\..\..\..
if errorlevel 1 (
    echo ERROR: CMake configuration failed
    cd ..\..\..\..
    exit /b 1
)

:: Build
echo.
echo Building OpenJTalk Wrapper...
nmake
if errorlevel 1 (
    echo ERROR: Build failed
    cd ..\..\..\..
    exit /b 1
)

:: Install
echo.
echo Installing files...
nmake install
if errorlevel 1 (
    echo ERROR: Installation failed
    cd ..\..\..\..
    exit /b 1
)

:: Return to original directory
cd ..\..\..\..

:: Copy to Unity plugin directory
echo.
echo Copying to Unity plugin directory...
set UNITY_PLUGIN_DIR=..\..\Plugins\Windows\%ARCH%
if not exist %UNITY_PLUGIN_DIR% mkdir %UNITY_PLUGIN_DIR%

copy output\windows\%ARCH%\bin\openjtalk_wrapper.dll %UNITY_PLUGIN_DIR%\
if errorlevel 1 (
    echo ERROR: Failed to copy DLL to Unity plugin directory
    exit /b 1
)

:: Success
echo.
echo ===================================
echo Build completed successfully!
echo ===================================
echo.
echo Output files:
echo   DLL: %UNITY_PLUGIN_DIR%\openjtalk_wrapper.dll
echo   Header: output\windows\%ARCH%\include\openjtalk_wrapper.h
echo.
echo Build configuration:
echo   Type: %BUILD_TYPE%
echo   Architecture: %ARCH%
echo   Visual Studio: %VS_VERSION%
echo.

exit /b 0