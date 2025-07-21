#!/bin/bash

# Download and build OpenJTalk from source
# This script downloads OpenJTalk and its dependencies

set -e

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
BUILD_DIR="$SCRIPT_DIR/../external/openjtalk_build"
OPENJTALK_VERSION="1.11"
HTS_ENGINE_VERSION="1.10"

echo "Creating build directory..."
mkdir -p "$BUILD_DIR"
cd "$BUILD_DIR"

# Download HTS Engine API (required by OpenJTalk)
echo "Downloading HTS Engine API..."
if [ ! -f "hts_engine_API-${HTS_ENGINE_VERSION}.tar.gz" ]; then
    curl -L -o "hts_engine_API-${HTS_ENGINE_VERSION}.tar.gz" \
        "https://sourceforge.net/projects/hts-engine/files/hts_engine%20API/hts_engine_API-${HTS_ENGINE_VERSION}/hts_engine_API-${HTS_ENGINE_VERSION}.tar.gz/download"
fi

# Extract HTS Engine API
echo "Extracting HTS Engine API..."
tar -xzf "hts_engine_API-${HTS_ENGINE_VERSION}.tar.gz"

# Build HTS Engine API
echo "Building HTS Engine API..."
cd "hts_engine_API-${HTS_ENGINE_VERSION}"
./configure --prefix="$BUILD_DIR/install"
make -j8
make install
cd ..

# Download OpenJTalk
echo "Downloading OpenJTalk..."
if [ ! -f "open_jtalk-${OPENJTALK_VERSION}.tar.gz" ]; then
    curl -L -o "open_jtalk-${OPENJTALK_VERSION}.tar.gz" \
        "https://sourceforge.net/projects/open-jtalk/files/Open%20JTalk/open_jtalk-${OPENJTALK_VERSION}/open_jtalk-${OPENJTALK_VERSION}.tar.gz/download"
fi

# Extract OpenJTalk
echo "Extracting OpenJTalk..."
tar -xzf "open_jtalk-${OPENJTALK_VERSION}.tar.gz"

# Build OpenJTalk
echo "Building OpenJTalk..."
cd "open_jtalk-${OPENJTALK_VERSION}"

# Configure with HTS Engine API
export PKG_CONFIG_PATH="$BUILD_DIR/install/lib/pkgconfig:$PKG_CONFIG_PATH"
export CPPFLAGS="-I$BUILD_DIR/install/include"
export LDFLAGS="-L$BUILD_DIR/install/lib"

./configure --prefix="$BUILD_DIR/install" \
    --with-hts-engine-header-path="$BUILD_DIR/install/include" \
    --with-hts-engine-library-path="$BUILD_DIR/install/lib"

make -j8
make install

echo "OpenJTalk build complete!"
echo "Installation directory: $BUILD_DIR/install"
echo ""
echo "To use OpenJTalk:"
echo "  Include directory: $BUILD_DIR/install/include"
echo "  Library directory: $BUILD_DIR/install/lib"
echo "  Binary: $BUILD_DIR/install/bin/open_jtalk"