@echo off
echo === Building OpenJTalk for Windows with MSVC ===

rem Setup MSVC environment
call "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat"

rem Set build directories
set SCRIPT_DIR=%~dp0
set BUILD_DIR=%SCRIPT_DIR%external\openjtalk_build
set INSTALL_DIR=%BUILD_DIR%\install

echo Script directory: %SCRIPT_DIR%
echo Build directory: %BUILD_DIR%
echo Install directory: %INSTALL_DIR%

cd /d "%BUILD_DIR%"

rem Build hts_engine with CMake (MSVC compatible)
echo === Building hts_engine with CMake ===
if exist "hts_engine_API-1.10" (
    cd hts_engine_API-1.10
    if exist build rmdir /s /q build
    mkdir build
    cd build
    
    cmake -G "Visual Studio 17 2022" -A x64 ^
          -DCMAKE_INSTALL_PREFIX="%INSTALL_DIR%" ^
          -DCMAKE_BUILD_TYPE=Release ^
          ..
    
    cmake --build . --config Release
    cmake --install . --config Release
    
    cd ..\..
) else (
    echo ERROR: hts_engine_API-1.10 directory not found
    exit /b 1
)

rem Build OpenJTalk
echo === Building OpenJTalk ===
if exist "open_jtalk-1.11" (
    cd open_jtalk-1.11
    
    rem Create a simple CMakeLists.txt for OpenJTalk components
    echo project(openjtalk_components^) > CMakeLists.txt
    echo cmake_minimum_required(VERSION 3.10^) >> CMakeLists.txt
    echo set(CMAKE_C_STANDARD 99^) >> CMakeLists.txt
    echo find_path(HTS_ENGINE_INCLUDE_DIR HTS_engine.h PATHS "%INSTALL_DIR%/include"^) >> CMakeLists.txt
    echo find_library(HTS_ENGINE_LIB HTSEngine PATHS "%INSTALL_DIR%/lib"^) >> CMakeLists.txt
    echo include_directories(${HTS_ENGINE_INCLUDE_DIR}^) >> CMakeLists.txt
    
    rem Add subdirectories for each component
    echo add_subdirectory(mecab/src^) >> CMakeLists.txt
    echo add_subdirectory(text2mecab^) >> CMakeLists.txt
    echo add_subdirectory(mecab2njd^) >> CMakeLists.txt
    echo add_subdirectory(njd^) >> CMakeLists.txt
    echo add_subdirectory(njd_set_pronunciation^) >> CMakeLists.txt
    echo add_subdirectory(njd_set_digit^) >> CMakeLists.txt
    echo add_subdirectory(njd_set_accent_phrase^) >> CMakeLists.txt
    echo add_subdirectory(njd_set_accent_type^) >> CMakeLists.txt
    echo add_subdirectory(njd_set_unvoiced_vowel^) >> CMakeLists.txt
    echo add_subdirectory(njd_set_long_vowel^) >> CMakeLists.txt
    echo add_subdirectory(njd2jpcommon^) >> CMakeLists.txt
    echo add_subdirectory(jpcommon^) >> CMakeLists.txt
    
    rem Use traditional make approach instead
    rem Build each component manually
    for %%d in (mecab\src text2mecab mecab2njd njd njd_set_pronunciation njd_set_digit njd_set_accent_phrase njd_set_accent_type njd_set_unvoiced_vowel njd_set_long_vowel njd2jpcommon jpcommon) do (
        echo Building %%d...
        cd %%d
        if exist *.c (
            for %%f in (*.c) do (
                cl /c /nologo /MD /O2 /I. /I"%INSTALL_DIR%\include" %%f
            )
            lib /nologo *.obj /out:lib%%~nd.lib
            del *.obj
        )
        cd ..
    )
    
    cd ..
) else (
    echo ERROR: open_jtalk-1.11 directory not found
    exit /b 1
)

echo === Dependencies built successfully ===

rem Now build the wrapper
echo === Building OpenJTalk wrapper ===
cd /d "%SCRIPT_DIR%"
if exist build rmdir /s /q build
mkdir build
cd build

cmake -G "Visual Studio 17 2022" -A x64 ^
      -DCMAKE_BUILD_TYPE=Release ^
      ..

cmake --build . --config Release

echo === Build completed ===
pause