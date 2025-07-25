@echo off
echo === Copying Android libraries to Unity ===

set SRC_DIR=output\android
set DST_DIR=..\..\Assets\uPiper\Plugins\Android\libs

echo.
echo Backing up existing libraries...
for %%A in (arm64-v8a armeabi-v7a x86 x86_64) do (
    if exist "%DST_DIR%\%%A\libopenjtalk_wrapper.so" (
        echo Backing up %%A library...
        copy /Y "%DST_DIR%\%%A\libopenjtalk_wrapper.so" "%DST_DIR%\%%A\libopenjtalk_wrapper.so.bak"
    )
)

echo.
echo Copying new libraries...
for %%A in (arm64-v8a armeabi-v7a x86 x86_64) do (
    if exist "%SRC_DIR%\%%A\libopenjtalk_wrapper.so" (
        echo Copying %%A library...
        copy /Y "%SRC_DIR%\%%A\libopenjtalk_wrapper.so" "%DST_DIR%\%%A\"
        if errorlevel 1 (
            echo ERROR: Failed to copy %%A library
        ) else (
            echo Successfully copied %%A library
        )
    ) else (
        echo WARNING: %%A library not found in source
    )
)

echo.
echo === Library copy complete ===
echo Please rebuild your Android APK in Unity
pause