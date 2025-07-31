# PowerShell script to download Flite source code for Windows

Write-Host "====================================="
Write-Host "Downloading Flite v2.1 source code..."
Write-Host "====================================="

# Create external directory if not exists
$externalDir = Join-Path $PSScriptRoot "external"
if (!(Test-Path $externalDir)) {
    New-Item -ItemType Directory -Path $externalDir | Out-Null
}

# Check if already downloaded
$fliteDir = Join-Path $externalDir "flite"
if (Test-Path $fliteDir) {
    Write-Host "Flite already downloaded at: $fliteDir"
    Write-Host "To re-download, delete the existing directory first."
    exit 0
}

# Download Flite source
try {
    Write-Host "Downloading from GitHub..."
    $url = "https://github.com/festvox/flite/archive/refs/tags/v2.1-release.tar.gz"
    $tempFile = Join-Path $env:TEMP "flite-2.1.tar.gz"
    
    # Download file
    Invoke-WebRequest -Uri $url -OutFile $tempFile -UseBasicParsing
    
    Write-Host "Extracting archive..."
    # Extract using tar (available in Windows 10+)
    Push-Location $externalDir
    tar -xzf $tempFile
    
    # Rename extracted directory
    if (Test-Path "flite-2.1-release") {
        Rename-Item "flite-2.1-release" "flite"
    }
    
    Pop-Location
    
    # Clean up
    Remove-Item $tempFile -Force
    
    Write-Host "====================================="
    Write-Host "Flite source code downloaded successfully!"
    Write-Host "Location: $fliteDir"
    Write-Host "====================================="
}
catch {
    Write-Error "Failed to download Flite: $_"
    exit 1
}