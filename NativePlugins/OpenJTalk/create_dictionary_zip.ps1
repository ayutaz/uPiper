# Create compressed dictionary file for Android
Write-Host "Creating compressed dictionary file..."

# Remove old zip if exists
if (Test-Path "openjtalk_dict.zip") {
    Remove-Item "openjtalk_dict.zip" -Force
}

# Add files to zip
Add-Type -Assembly "System.IO.Compression.FileSystem"

$zipFile = [System.IO.Compression.ZipFile]::Open("$PWD\openjtalk_dict.zip", 'Create')

# Add dictionary files (excluding .meta files)
$files = Get-ChildItem "dictionary" -Include "*.bin", "*.def", "*.dic" -File
foreach ($file in $files) {
    Write-Host "Adding $($file.Name)..."
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zipFile, $file.FullName, $file.Name, 'Optimal') | Out-Null
}

$zipFile.Dispose()

# Check result
if (Test-Path "openjtalk_dict.zip") {
    $zipInfo = Get-Item "openjtalk_dict.zip"
    Write-Host "Dictionary compressed successfully!"
    Write-Host "Size: $([Math]::Round($zipInfo.Length / 1MB, 2)) MB"
    
    # Copy to Unity StreamingAssets
    $streamingAssets = "..\..\Assets\StreamingAssets\uPiper\OpenJTalk"
    if (!(Test-Path $streamingAssets)) {
        New-Item -ItemType Directory -Path $streamingAssets -Force | Out-Null
    }
    
    Copy-Item "openjtalk_dict.zip" $streamingAssets -Force
    Write-Host "Dictionary file copied to StreamingAssets"
} else {
    Write-Host "Failed to create dictionary zip file"
    exit 1
}