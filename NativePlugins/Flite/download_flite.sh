#!/bin/bash
# Script to download and prepare Flite source code

set -e

echo "Downloading Flite v2.3..."

# Move to external directory
cd external

# Check if already downloaded
if [ -d "flite" ]; then
    echo "Flite already downloaded. Skipping..."
    exit 0
fi

# Download Flite
if command -v git &> /dev/null; then
    git clone https://github.com/festvox/flite.git
    cd flite
    git checkout v2.3
else
    # Alternative: download tarball
    curl -L https://github.com/festvox/flite/archive/refs/tags/v2.3.tar.gz -o flite-2.3.tar.gz
    tar -xzf flite-2.3.tar.gz
    mv flite-2.3 flite
    rm flite-2.3.tar.gz
fi

echo "Flite source code downloaded successfully!"