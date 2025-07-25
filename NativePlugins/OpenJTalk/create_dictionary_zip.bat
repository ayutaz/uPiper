@echo off
REM Create compressed dictionary file for Android

setlocal

echo Creating compressed dictionary file...

REM Remove old zip if exists
if exist openjtalk_dict.zip del openjtalk_dict.zip

REM Create zip file (excluding .meta files)
powershell -Command "Compress-Archive -Path 'dictionary\*.bin', 'dictionary\*.def', 'dictionary\*.dic' -DestinationPath openjtalk_dict.zip -CompressionLevel Optimal"

REM Check result
if exist openjtalk_dict.zip (
    echo Dictionary compressed successfully!
    powershell -Command "Get-Item openjtalk_dict.zip | Select-Object Name, @{Name='Size (MB)';Expression={[Math]::Round($_.Length / 1MB, 2)}}"
) else (
    echo Failed to create dictionary zip file
    exit /b 1
)

REM Copy to Unity StreamingAssets
set STREAMING_ASSETS=..\..\Assets\StreamingAssets\uPiper\OpenJTalk
if not exist "%STREAMING_ASSETS%" mkdir "%STREAMING_ASSETS%"

copy openjtalk_dict.zip "%STREAMING_ASSETS%\"

echo Dictionary file copied to StreamingAssets

endlocal