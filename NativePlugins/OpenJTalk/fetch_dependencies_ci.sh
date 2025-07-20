#!/bin/bash
# CI-specific dependency fetcher with reliable mirrors

set -e

echo "=== Fetching OpenJTalk Dependencies for CI ==="

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
EXTERNAL_DIR="$SCRIPT_DIR/external"
BUILD_DIR="$EXTERNAL_DIR/openjtalk_build"

# Create directories
mkdir -p "$BUILD_DIR"
cd "$BUILD_DIR"

# Download with retry function
download_with_retry() {
    local url="$1"
    local output="$2"
    local max_retries=3
    local retry_count=0
    
    while [ $retry_count -lt $max_retries ]; do
        echo "Downloading from $url (attempt $((retry_count + 1))/$max_retries)..."
        if curl -L --fail --connect-timeout 30 --max-time 300 "$url" -o "$output"; then
            echo "Download successful!"
            return 0
        fi
        retry_count=$((retry_count + 1))
        if [ $retry_count -lt $max_retries ]; then
            echo "Download failed, retrying in 5 seconds..."
            sleep 5
        fi
    done
    echo "ERROR: Failed to download after $max_retries attempts"
    return 1
}

# Download OpenJTalk
if [ ! -f "open_jtalk-1.11.tar.gz" ] && [ ! -d "open_jtalk-1.11" ]; then
    echo "Downloading OpenJTalk 1.11..."
    if ! download_with_retry "https://sourceforge.net/projects/open-jtalk/files/Open%20JTalk/open_jtalk-1.11/open_jtalk-1.11.tar.gz/download" "open_jtalk-1.11.tar.gz"; then
        echo "ERROR: Failed to download OpenJTalk"
        exit 1
    fi
fi

if [ ! -d "open_jtalk-1.11" ]; then
    echo "Extracting OpenJTalk..."
    tar -xzf open_jtalk-1.11.tar.gz || {
        echo "ERROR: Failed to extract OpenJTalk"
        exit 1
    }
fi

# Download hts_engine
if [ ! -f "hts_engine_API-1.10.tar.gz" ] && [ ! -d "hts_engine_API-1.10" ]; then
    echo "Downloading hts_engine API 1.10..."
    if ! download_with_retry "https://sourceforge.net/projects/hts-engine/files/hts_engine%20API/hts_engine_API-1.10/hts_engine_API-1.10.tar.gz/download" "hts_engine_API-1.10.tar.gz"; then
        echo "ERROR: Failed to download hts_engine"
        exit 1
    fi
fi

if [ ! -d "hts_engine_API-1.10" ]; then
    echo "Extracting hts_engine..."
    tar -xzf hts_engine_API-1.10.tar.gz || {
        echo "ERROR: Failed to extract hts_engine"
        exit 1
    }
fi

# For CI, we'll skip the mecab dictionary download since it often fails
# The dictionary is already included in the repository at NativePlugins/OpenJTalk/dictionary

echo "=== Dependencies fetched successfully ==="
echo "Contents of build directory:"
ls -la
echo ""
echo "Moving up to external directory:"
cd ..
ls -la