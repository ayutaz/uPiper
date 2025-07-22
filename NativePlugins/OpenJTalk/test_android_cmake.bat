@echo off
REM Test Android CMake configuration without full dependencies

setlocal

REM Check for ANDROID_NDK_HOME
if "%ANDROID_NDK_HOME%"=="" (
    echo Error: ANDROID_NDK_HOME environment variable is not set
    echo Please download Android NDK from: https://developer.android.com/ndk/downloads
    echo And set ANDROID_NDK_HOME to the installation path
    exit /b 1
)

echo Using Android NDK at: %ANDROID_NDK_HOME%

REM Test CMake configuration for arm64-v8a
echo Testing CMake configuration for Android arm64-v8a...
if exist "test_build_android" rmdir /s /q "test_build_android"
mkdir test_build_android
cd test_build_android

cmake -G "Ninja" ^
      -DCMAKE_TOOLCHAIN_FILE="%ANDROID_NDK_HOME%\build\cmake\android.toolchain.cmake" ^
      -DANDROID_ABI=arm64-v8a ^
      -DANDROID_PLATFORM=android-21 ^
      -DCMAKE_BUILD_TYPE=Release ^
      ..

if errorlevel 1 (
    echo CMake configuration failed!
    echo Please check:
    echo 1. Android NDK is properly installed
    echo 2. Ninja is available (usually comes with Android SDK)
    echo 3. CMakeLists.txt is compatible with Android
    cd ..
    exit /b 1
)

echo CMake configuration successful!
cd ..
rmdir /s /q "test_build_android"

endlocal