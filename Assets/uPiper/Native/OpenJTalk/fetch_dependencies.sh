#!/bin/bash

# OpenJTalk dependency fetcher script
# Downloads and prepares OpenJTalk, Mecab, and hts_engine_API

set -e

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
    curl -L "https://sourceforge.net/projects/hts-engine/files/hts_engine%20API/hts_engine_API-${HTS_ENGINE_VERSION}/hts_engine_API-${HTS_ENGINE_VERSION}.tar.gz/download" -o hts_engine_API.tar.gz
    tar xzf hts_engine_API.tar.gz
    rm hts_engine_API.tar.gz
fi

# Download OpenJTalk
if [ ! -d "open_jtalk-${OPENJTALK_VERSION}" ]; then
    echo "Downloading OpenJTalk ${OPENJTALK_VERSION}..."
    curl -L "https://sourceforge.net/projects/open-jtalk/files/Open%20JTalk/open_jtalk-${OPENJTALK_VERSION}/open_jtalk-${OPENJTALK_VERSION}.tar.gz/download" -o open_jtalk.tar.gz
    tar xzf open_jtalk.tar.gz
    rm open_jtalk.tar.gz
fi

# Download Mecab (without dictionary for now)
if [ ! -d "mecab-${MECAB_VERSION}" ]; then
    echo "Downloading Mecab ${MECAB_VERSION}..."
    curl -L "https://drive.google.com/uc?export=download&id=0B4y35FiV1wh7cENtOXlicTFaRUE" -o mecab.tar.gz || \
    curl -L "https://github.com/taku910/mecab/archive/refs/heads/master.tar.gz" -o mecab.tar.gz
    mkdir -p "mecab-${MECAB_VERSION}"
    tar xzf mecab.tar.gz -C "mecab-${MECAB_VERSION}" --strip-components=1
    rm mecab.tar.gz
fi

# Download mecab-naist-jdic dictionary
if [ ! -d "mecab-naist-jdic" ]; then
    echo "Downloading mecab-naist-jdic dictionary..."
    curl -L "https://drive.google.com/uc?export=download&id=0B4y35FiV1wh7MWVlSDBCSXZMTXM" -o mecab-naist-jdic.tar.gz || \
    curl -L "https://github.com/ayataka0nk/mecab-naist-jdic/archive/refs/heads/master.tar.gz" -o mecab-naist-jdic.tar.gz
    tar xzf mecab-naist-jdic.tar.gz
    rm mecab-naist-jdic.tar.gz
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