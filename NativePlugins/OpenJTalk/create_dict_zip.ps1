# Create compressed dictionary using .NET Framework
param()

Write-Host "Creating compressed dictionary file..."

# Remove old file
if (Test-Path "openjtalk_dict.zip") {
    Remove-Item "openjtalk_dict.zip" -Force
}

# Load compression assembly
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

# Create new ZIP archive
$compressionLevel = [System.IO.Compression.CompressionLevel]::Optimal
$includeBaseDirectory = $false

# Get all dictionary files
$sourceDir = Join-Path $PWD "dictionary"
$destFile = Join-Path $PWD "openjtalk_dict.zip"

# Create zip from directory
[System.IO.Compression.ZipFile]::CreateFromDirectory($sourceDir, $destFile, $compressionLevel, $includeBaseDirectory)

# Check result
if (Test-Path "openjtalk_dict.zip") {
    $zipInfo = Get-Item "openjtalk_dict.zip"
    $sizeMB = [Math]::Round($zipInfo.Length / 1MB, 2)
    Write-Host "Dictionary compressed successfully!"
    Write-Host "Original size: ~107 MB"
    Write-Host "Compressed size: $sizeMB MB"
    Write-Host "Compression ratio: $([Math]::Round((1 - $zipInfo.Length / 107335680) * 100, 1))%"
    
    # Copy to StreamingAssets
    $streamingAssets = Join-Path (Split-Path -Parent (Split-Path -Parent $PWD)) "Assets\StreamingAssets\uPiper\OpenJTalk"
    if (!(Test-Path $streamingAssets)) {
        New-Item -ItemType Directory -Path $streamingAssets -Force | Out-Null
    }
    
    Copy-Item "openjtalk_dict.zip" $streamingAssets -Force
    Write-Host "`nDictionary file copied to: $streamingAssets"
} else {
    Write-Error "Failed to create dictionary zip file"
    exit 1
}