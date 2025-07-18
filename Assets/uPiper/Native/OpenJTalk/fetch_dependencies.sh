#!/bin/bash

# OpenJTalk dependency fetcher script
# Downloads and prepares OpenJTalk, Mecab, and hts_engine_API

set -e

# Error handling function
error_exit() {
    echo "Error: $1" >&2
    exit 1
}

# Download with retry function
download_with_retry() {
    local url="$1"
    local output="$2"
    local max_retries=3
    local retry_count=0
    
    while [ $retry_count -lt $max_retries ]; do
        echo "Downloading from $url (attempt $((retry_count + 1))/$max_retries)..."
        if curl -L --fail --connect-timeout 30 --max-time 300 "$url" -o "$output"; then
            return 0
        fi
        retry_count=$((retry_count + 1))
        if [ $retry_count -lt $max_retries ]; then
            echo "Download failed, retrying in 5 seconds..."
            sleep 5
        fi
    done
    return 1
}

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
EXTERNAL_DIR="$SCRIPT_DIR/external"

# Create external directory
mkdir -p "$EXTERNAL_DIR"
cd "$EXTERNAL_DIR"

# Version definitions
OPENJTALK_VERSION="1.11"
HTS_ENGINE_VERSION="1.10"
MECAB_VERSION="0.996"

echo "Fetching OpenJTalk dependencies..."

# Download hts_engine_API
if [ ! -d "hts_engine_API-${HTS_ENGINE_VERSION}" ]; then
    echo "Downloading hts_engine_API ${HTS_ENGINE_VERSION}..."
    download_with_retry "https://sourceforge.net/projects/hts-engine/files/hts_engine%20API/hts_engine_API-${HTS_ENGINE_VERSION}/hts_engine_API-${HTS_ENGINE_VERSION}.tar.gz/download" "hts_engine_API.tar.gz" || \
        error_exit "Failed to download hts_engine_API after multiple attempts"
    
    echo "Extracting hts_engine_API..."
    tar xzf hts_engine_API.tar.gz || error_exit "Failed to extract hts_engine_API"
    rm hts_engine_API.tar.gz
fi

# Download OpenJTalk
if [ ! -d "open_jtalk-${OPENJTALK_VERSION}" ]; then
    echo "Downloading OpenJTalk ${OPENJTALK_VERSION}..."
    download_with_retry "https://sourceforge.net/projects/open-jtalk/files/Open%20JTalk/open_jtalk-${OPENJTALK_VERSION}/open_jtalk-${OPENJTALK_VERSION}.tar.gz/download" "open_jtalk.tar.gz" || \
        error_exit "Failed to download OpenJTalk after multiple attempts"
    
    echo "Extracting OpenJTalk..."
    tar xzf open_jtalk.tar.gz || error_exit "Failed to extract OpenJTalk"
    rm open_jtalk.tar.gz
fi

# Download Mecab (without dictionary for now)
if [ ! -d "mecab-${MECAB_VERSION}" ]; then
    echo "Downloading Mecab ${MECAB_VERSION}..."
    # Try Google Drive first, then GitHub
    if ! download_with_retry "https://drive.google.com/uc?export=download&id=0B4y35FiV1wh7cENtOXlicTFaRUE" "mecab.tar.gz"; then
        echo "Google Drive download failed, trying GitHub..."
        download_with_retry "https://github.com/taku910/mecab/archive/refs/heads/master.tar.gz" "mecab.tar.gz" || \
            error_exit "Failed to download Mecab from all sources"
    fi
    
    echo "Extracting Mecab..."
    mkdir -p "mecab-${MECAB_VERSION}"
    tar xzf mecab.tar.gz -C "mecab-${MECAB_VERSION}" --strip-components=1 || error_exit "Failed to extract Mecab"
    rm mecab.tar.gz
fi

# Download mecab-naist-jdic dictionary
if [ ! -d "mecab-naist-jdic" ]; then
    echo "Downloading mecab-naist-jdic dictionary..."
    # Try Google Drive first, then GitHub mirror
    if ! download_with_retry "https://drive.google.com/uc?export=download&id=0B4y35FiV1wh7MWVlSDBCSXZMTXM" "mecab-naist-jdic.tar.gz"; then
        echo "Google Drive download failed, trying GitHub mirror..."
        download_with_retry "https://github.com/ayataka0nk/mecab-naist-jdic/archive/refs/heads/master.tar.gz" "mecab-naist-jdic.tar.gz" || \
            error_exit "Failed to download mecab-naist-jdic dictionary from all sources"
    fi
    
    echo "Extracting mecab-naist-jdic dictionary..."
    tar xzf mecab-naist-jdic.tar.gz || error_exit "Failed to extract mecab-naist-jdic dictionary"
    rm mecab-naist-jdic.tar.gz
    
    # Verify dictionary files exist
    if [ ! -f "mecab-naist-jdic/sys.dic" ] && [ ! -f "mecab-naist-jdic-master/sys.dic" ]; then
        error_exit "Dictionary files not found after extraction"
    fi
fi

echo "Creating license directory..."
mkdir -p licenses

# Copy licenses
if [ -f "hts_engine_API-${HTS_ENGINE_VERSION}/COPYING" ]; then
    cp "hts_engine_API-${HTS_ENGINE_VERSION}/COPYING" licenses/hts_engine_API_LICENSE
fi

if [ -f "open_jtalk-${OPENJTALK_VERSION}/COPYING" ]; then
    cp "open_jtalk-${OPENJTALK_VERSION}/COPYING" licenses/OpenJTalk_LICENSE
fi

if [ -f "mecab-${MECAB_VERSION}/COPYING" ]; then
    cp "mecab-${MECAB_VERSION}/COPYING" licenses/Mecab_LICENSE
elif [ -f "mecab-${MECAB_VERSION}/BSD" ]; then
    cp "mecab-${MECAB_VERSION}/BSD" licenses/Mecab_LICENSE
fi

echo "Dependencies fetched successfully!"
echo "Next steps:"
echo "1. Build hts_engine_API"
echo "2. Build Mecab"
echo "3. Build OpenJTalk with dependencies"