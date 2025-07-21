@echo off
REM OpenJTalk dependency fetcher script for Windows
REM Downloads OpenJTalk, Mecab, and hts_engine_API

setlocal enabledelayedexpansion

set SCRIPT_DIR=%~dp0
set EXTERNAL_DIR=%SCRIPT_DIR%external

REM Create external directory
if not exist "%EXTERNAL_DIR%" mkdir "%EXTERNAL_DIR%"
cd /d "%EXTERNAL_DIR%"

REM Version definitions
set OPENJTALK_VERSION=1.11
set HTS_ENGINE_VERSION=1.10
set MECAB_VERSION=0.996

echo Fetching OpenJTalk dependencies for Windows...

REM Check for curl or use PowerShell
where curl >nul 2>&1
if %errorlevel% == 0 (
    set DOWNLOAD_CMD=curl -L -o
) else (
    echo Using PowerShell for downloads...
    set DOWNLOAD_CMD=powershell -Command "Invoke-WebRequest -Uri 
    set DOWNLOAD_CMD_END= -OutFile"
)

REM Download hts_engine_API
if not exist "hts_engine_API-%HTS_ENGINE_VERSION%" (
    echo Downloading hts_engine_API %HTS_ENGINE_VERSION%...
    if defined DOWNLOAD_CMD_END (
        powershell -Command "Invoke-WebRequest -Uri 'https://sourceforge.net/projects/hts-engine/files/hts_engine%%20API/hts_engine_API-%HTS_ENGINE_VERSION%/hts_engine_API-%HTS_ENGINE_VERSION%.tar.gz/download' -OutFile 'hts_engine_API.tar.gz'"
    ) else (
        curl -L -o hts_engine_API.tar.gz "https://sourceforge.net/projects/hts-engine/files/hts_engine%%20API/hts_engine_API-%HTS_ENGINE_VERSION%/hts_engine_API-%HTS_ENGINE_VERSION%.tar.gz/download"
    )
    
    echo Extracting hts_engine_API...
    tar xzf hts_engine_API.tar.gz
    del hts_engine_API.tar.gz
)

REM Download OpenJTalk
if not exist "open_jtalk-%OPENJTALK_VERSION%" (
    echo Downloading OpenJTalk %OPENJTALK_VERSION%...
    if defined DOWNLOAD_CMD_END (
        powershell -Command "Invoke-WebRequest -Uri 'https://sourceforge.net/projects/open-jtalk/files/Open%%20JTalk/open_jtalk-%OPENJTALK_VERSION%/open_jtalk-%OPENJTALK_VERSION%.tar.gz/download' -OutFile 'open_jtalk.tar.gz'"
    ) else (
        curl -L -o open_jtalk.tar.gz "https://sourceforge.net/projects/open-jtalk/files/Open%%20JTalk/open_jtalk-%OPENJTALK_VERSION%/open_jtalk-%OPENJTALK_VERSION%.tar.gz/download"
    )
    
    echo Extracting OpenJTalk...
    tar xzf open_jtalk.tar.gz
    del open_jtalk.tar.gz
)

echo Dependencies fetched successfully!

endlocal