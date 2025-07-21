@echo off
setlocal

echo === Building test_kyou_simple ===

REM Build test program
gcc -o test_kyou_simple.exe test\test_kyou_simple.c -L. -lopenjtalk_wrapper -I.\include

if %errorlevel% neq 0 (
    echo Build failed!
    exit /b 1
)

echo === Running test ===

REM Get dictionary path
set DICT_PATH=%~dp0..\..\Assets\StreamingAssets\uPiper\OpenJTalk\naist_jdic\open_jtalk_dic_utf_8-1.11

REM Copy DLL to current directory
copy /Y ..\..\Assets\uPiper\Plugins\Windows\x86_64\openjtalk_wrapper.dll . >nul

REM Run test
test_kyou_simple.exe "%DICT_PATH%"

echo.
echo === Test completed ===
pause