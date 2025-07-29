@echo off
echo === Creating dictionary ZIP file for Android ===
cd Assets\StreamingAssets\uPiper\OpenJTalk

if exist naist_jdic.zip (
    echo Removing old ZIP file...
    del naist_jdic.zip
)

echo Creating naist_jdic.zip from naist_jdic folder...
powershell -Command "Compress-Archive -Path 'naist_jdic\*' -DestinationPath 'naist_jdic.zip' -Force"

if exist naist_jdic.zip (
    echo ZIP file created successfully!
    echo Location: Assets\StreamingAssets\uPiper\OpenJTalk\naist_jdic.zip
    powershell -Command "Get-Item naist_jdic.zip | Select-Object Name, Length"
) else (
    echo Failed to create ZIP file!
)

cd ..\..\..\..\
pause