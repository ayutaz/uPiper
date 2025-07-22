@echo off
REM Build script for Android on Windows
REM Builds OpenJTalk library for multiple Android architectures

setlocal enabledelayedexpansion

REM Check for ANDROID_NDK_HOME
if "%ANDROID_NDK_HOME%"=="" (
    echo Error: ANDROID_NDK_HOME environment variable is not set
    echo Please set it to your Android NDK installation path
    exit /b 1
)

echo Using Android NDK at: %ANDROID_NDK_HOME%

REM Get script directory
set SCRIPT_DIR=%~dp0
cd /d "%SCRIPT_DIR%"

REM Android ABIs to build for
set ABIS=armeabi-v7a arm64-v8a x86 x86_64

REM Check if dependencies are built
if not exist "external\openjtalk_build" (
    echo Dependencies not found. Please run build_dependencies.sh first in WSL or Linux environment.
    echo Android cross-compilation requires Unix-like environment for dependency building.
    exit /b 1
)

REM Clean previous builds
for %%A in (%ABIS%) do (
    if exist "build_android_%%A" rmdir /s /q "build_android_%%A"
)
if not exist "output\android" mkdir "output\android"

REM Build wrapper library for each ABI
for %%A in (%ABIS%) do (
    echo Building OpenJTalk wrapper for Android %%A...
    
    REM Create build directory
    mkdir "build_android_%%A"
    cd "build_android_%%A"
    
    REM Configure with Android toolchain
    cmake -G "Ninja" ^
          -DCMAKE_TOOLCHAIN_FILE="%ANDROID_NDK_HOME%\build\cmake\android.toolchain.cmake" ^
          -DANDROID_ABI=%%A ^
          -DANDROID_PLATFORM=android-21 ^
          -DCMAKE_BUILD_TYPE=Release ^
          -DBUILD_TESTS=OFF ^
          -DBUILD_BENCHMARK=OFF ^
          ..
    
    if errorlevel 1 (
        echo Failed to configure for %%A
        cd ..
        exit /b 1
    )
    
    REM Build
    cmake --build . --config Release
    
    if errorlevel 1 (
        echo Failed to build for %%A
        cd ..
        exit /b 1
    )
    
    REM Copy output
    if not exist "..\output\android\%%A" mkdir "..\output\android\%%A"
    copy "lib\libopenjtalk_wrapper.so" "..\output\android\%%A\"
    
    cd ..
)

REM Create Unity plugin structure
echo Creating Unity plugin structure...
set UNITY_PLUGIN_DIR=..\..\Assets\uPiper\Plugins\Android
if not exist "%UNITY_PLUGIN_DIR%\libs" mkdir "%UNITY_PLUGIN_DIR%\libs"

for %%A in (%ABIS%) do (
    if not exist "%UNITY_PLUGIN_DIR%\libs\%%A" mkdir "%UNITY_PLUGIN_DIR%\libs\%%A"
    copy "output\android\%%A\libopenjtalk_wrapper.so" "%UNITY_PLUGIN_DIR%\libs\%%A\"
)

REM Copy dictionary files
echo Copying dictionary files...
xcopy /E /I /Y "dictionary" "%UNITY_PLUGIN_DIR%\dictionary"

echo Android build completed successfully!
echo Libraries are located in:
echo   - output\android\ (build output)
echo   - %UNITY_PLUGIN_DIR%\libs\ (Unity plugin structure)

endlocal