#!/bin/bash
# CI用の依存関係ビルドスクリプト（最小限のビルド）

set -e

echo "=== Building OpenJTalk Dependencies for CI ==="

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_DIR="$SCRIPT_DIR/external/openjtalk_build"
INSTALL_DIR="$BUILD_DIR/install"

cd "$BUILD_DIR"

# Build hts_engine
echo "Building hts_engine..."
cd hts_engine_API-1.10
if [ ! -f "Makefile" ]; then
    ./configure --prefix="$INSTALL_DIR" --enable-static --disable-shared
fi
make -j$(nproc || echo 4)
make install
cd ..

# Build OpenJTalk
echo "Building OpenJTalk..."
cd open_jtalk-1.11
if [ ! -f "Makefile" ]; then
    ./configure --prefix="$INSTALL_DIR" \
        --with-hts-engine-header-path="$INSTALL_DIR/include" \
        --with-hts-engine-library-path="$INSTALL_DIR/lib" \
        --enable-static --disable-shared
fi
make -j$(nproc || echo 4)
make install
cd ..

echo "=== Dependencies built successfully ==="