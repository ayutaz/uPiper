@echo off
setlocal enabledelayedexpansion

echo Building OpenJTalk dependencies with MSVC...

cd /d "%~dp0"

:: Check if dependencies exist
if not exist external\openjtalk_build\open_jtalk-1.11 (
    echo Error: Dependencies not found. Run fetch_dependencies.sh first.
    exit /b 1
)

cd external\openjtalk_build

:: Build hts_engine_API
echo.
echo Building hts_engine_API...
cd hts_engine_API-1.10
if exist build rmdir /s /q build
mkdir build
cd build
cmake .. -G "Visual Studio 17 2022" -A x64 -DCMAKE_BUILD_TYPE=Release -DCMAKE_INSTALL_PREFIX=../../install
if errorlevel 1 (
    echo CMake configuration failed for hts_engine_API
    exit /b 1
)
cmake --build . --config Release
if errorlevel 1 (
    echo Build failed for hts_engine_API
    exit /b 1
)
cmake --build . --config Release --target install
cd ..\..

:: Build open_jtalk
echo.
echo Building open_jtalk...
cd open_jtalk-1.11

:: Build each component separately with MSVC
set COMPONENTS=text2mecab mecab mecab2njd njd njd_set_pronunciation njd_set_digit njd_set_accent_phrase njd_set_accent_type njd_set_unvoiced_vowel njd_set_long_vowel njd2jpcommon jpcommon

for %%C in (%COMPONENTS%) do (
    echo.
    echo Building %%C...
    cd %%C
    
    :: Create simple CMakeLists.txt for MSVC if it doesn't exist
    if not exist CMakeLists.txt (
        echo cmake_minimum_required^(VERSION 3.10^) > CMakeLists.txt
        echo project^(%%C^) >> CMakeLists.txt
        echo file^(GLOB SOURCES "*.c"^) >> CMakeLists.txt
        echo add_library^(%%C STATIC ${SOURCES}^) >> CMakeLists.txt
        echo target_include_directories^(%%C PUBLIC ${CMAKE_CURRENT_SOURCE_DIR}^) >> CMakeLists.txt
        
        :: Add specific includes for components that need them
        if "%%C"=="mecab" (
            echo target_include_directories^(%%C PUBLIC ../text2mecab^) >> CMakeLists.txt
        )
        if "%%C"=="mecab2njd" (
            echo target_include_directories^(%%C PUBLIC ../mecab/src ../njd^) >> CMakeLists.txt
        )
        if "%%C"=="njd_set_pronunciation" (
            echo target_include_directories^(%%C PUBLIC ../njd ../mecab/src^) >> CMakeLists.txt
        )
        if "%%C"=="njd_set_digit" (
            echo target_include_directories^(%%C PUBLIC ../njd ../mecab/src^) >> CMakeLists.txt
        )
        if "%%C"=="njd_set_accent_phrase" (
            echo target_include_directories^(%%C PUBLIC ../njd ../mecab/src^) >> CMakeLists.txt
        )
        if "%%C"=="njd_set_accent_type" (
            echo target_include_directories^(%%C PUBLIC ../njd ../mecab/src^) >> CMakeLists.txt
        )
        if "%%C"=="njd_set_unvoiced_vowel" (
            echo target_include_directories^(%%C PUBLIC ../njd ../mecab/src^) >> CMakeLists.txt
        )
        if "%%C"=="njd_set_long_vowel" (
            echo target_include_directories^(%%C PUBLIC ../njd ../mecab/src^) >> CMakeLists.txt
        )
        if "%%C"=="njd2jpcommon" (
            echo target_include_directories^(%%C PUBLIC ../njd ../jpcommon ../mecab/src^) >> CMakeLists.txt
        )
        if "%%C"=="jpcommon" (
            echo target_include_directories^(%%C PUBLIC ../njd ../mecab/src^) >> CMakeLists.txt
            echo target_include_directories^(%%C PUBLIC ../../../install/include^) >> CMakeLists.txt
        )
    )
    
    if exist build rmdir /s /q build
    mkdir build
    cd build
    cmake .. -G "Visual Studio 17 2022" -A x64
    cmake --build . --config Release
    
    :: Copy the lib file
    copy /Y Release\%%C.lib ..\..\lib%%C.a
    
    cd ..\..
)

cd ..

echo.
echo Dependencies built successfully!
echo You can now build the main project with CMake using Visual Studio generator.

endlocal