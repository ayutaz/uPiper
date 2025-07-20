#!/bin/bash
# CI用の依存関係取得スクリプト（最小限の依存関係のみ）

set -e

echo "=== Fetching OpenJTalk Dependencies for CI ==="

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
EXTERNAL_DIR="$SCRIPT_DIR/external"
BUILD_DIR="$EXTERNAL_DIR/openjtalk_build"

# Create directories
mkdir -p "$BUILD_DIR"
cd "$BUILD_DIR"

# Download pre-built OpenJTalk libraries (if available) or build minimal version
if [ ! -f "open_jtalk-1.11.tar.gz" ]; then
    echo "Downloading OpenJTalk 1.11..."
    curl -L -o open_jtalk-1.11.tar.gz https://sourceforge.net/projects/open-jtalk/files/Open%20JTalk/open_jtalk-1.11/open_jtalk-1.11.tar.gz/download
fi

if [ ! -d "open_jtalk-1.11" ]; then
    echo "Extracting OpenJTalk..."
    tar -xzf open_jtalk-1.11.tar.gz
fi

# Download hts_engine
if [ ! -f "hts_engine_API-1.10.tar.gz" ]; then
    echo "Downloading hts_engine API 1.10..."
    curl -L -o hts_engine_API-1.10.tar.gz https://sourceforge.net/projects/hts-engine/files/hts_engine%20API/hts_engine_API-1.10/hts_engine_API-1.10.tar.gz/download
fi

if [ ! -d "hts_engine_API-1.10" ]; then
    echo "Extracting hts_engine..."
    tar -xzf hts_engine_API-1.10.tar.gz
fi

echo "=== Dependencies fetched successfully ==="