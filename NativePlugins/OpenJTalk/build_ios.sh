#!/bin/bash

# iOS Build Script for OpenJTalk Unity Wrapper
# Supports: arm64 (device) and x86_64 (simulator, optional)

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}=== Building OpenJTalk for iOS ===${NC}"

# Configuration
CURRENT_DIR=$(pwd)
BUILD_DIR="${CURRENT_DIR}/build_ios"
INSTALL_DIR="${CURRENT_DIR}/install_ios"
OUTPUT_DIR="${CURRENT_DIR}/../../Assets/uPiper/Plugins/iOS"

# Clean previous builds
if [ -d "${BUILD_DIR}" ]; then
    echo -e "${YELLOW}Cleaning previous build...${NC}"
    rm -rf "${BUILD_DIR}"
fi

if [ -d "${INSTALL_DIR}" ]; then
    rm -rf "${INSTALL_DIR}"
fi

# Create directories
mkdir -p "${BUILD_DIR}"
mkdir -p "${INSTALL_DIR}"
mkdir -p "${OUTPUT_DIR}"

# Build function
build_ios() {
    local PLATFORM=$1
    local ARCH=$2
    local BUILD_TYPE="Release"
    
    echo -e "${GREEN}Building for ${PLATFORM} (${ARCH})...${NC}"
    
    local BUILD_PATH="${BUILD_DIR}/${PLATFORM}"
    mkdir -p "${BUILD_PATH}"
    cd "${BUILD_PATH}"
    
    # Configure with CMake
    cmake "${CURRENT_DIR}" \
        -G Xcode \
        -DCMAKE_TOOLCHAIN_FILE="${CURRENT_DIR}/ios.toolchain.cmake" \
        -DPLATFORM="${PLATFORM}" \
        -DCMAKE_BUILD_TYPE="${BUILD_TYPE}" \
        -DCMAKE_INSTALL_PREFIX="${INSTALL_DIR}/${PLATFORM}" \
        -DBUILD_SHARED_LIBS=OFF \
        -DENABLE_BITCODE=OFF \
        -DCMAKE_OSX_DEPLOYMENT_TARGET=11.0 \
        -DCMAKE_XCODE_ATTRIBUTE_ONLY_ACTIVE_ARCH=NO \
        -DCMAKE_IOS_INSTALL_COMBINED=YES
    
    # Build
    cmake --build . --config "${BUILD_TYPE}" --target install
    
    cd "${CURRENT_DIR}"
}

# Build for device (arm64)
build_ios "OS64" "arm64"

# Optional: Build for simulator (x86_64)
# Uncomment if needed for Unity Editor iOS simulator testing
# build_ios "SIMULATOR64" "x86_64"

# Copy the library to Unity Plugins folder
echo -e "${GREEN}Copying library to Unity...${NC}"
cp "${INSTALL_DIR}/OS64/lib/libopenjtalk_wrapper.a" "${OUTPUT_DIR}/"

# Create .meta file for Unity
echo -e "${GREEN}Creating Unity meta file...${NC}"
cat > "${OUTPUT_DIR}/libopenjtalk_wrapper.a.meta" << EOF
fileFormatVersion: 2
guid: $(uuidgen | tr '[:upper:]' '[:lower:]' | tr -d '-')
PluginImporter:
  externalObjects: {}
  serializedVersion: 2
  iconMap: {}
  executionOrder: {}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:
  - first:
      Any: 
    second:
      enabled: 0
      settings: {}
  - first:
      Editor: Editor
    second:
      enabled: 0
      settings:
        DefaultValueInitialized: true
  - first:
      iPhone: iOS
    second:
      enabled: 1
      settings:
        AddToEmbeddedBinaries: false
        CPU: ARM64
        CompileFlags: 
        FrameworkDependencies: 
  userData: 
  assetBundleName: 
  assetBundleVariant: 
EOF

# Copy dictionaries to StreamingAssets
DICT_SOURCE="${CURRENT_DIR}/dictionary"
DICT_DEST="${CURRENT_DIR}/../../Assets/StreamingAssets/uPiper/OpenJTalk/naist_jdic"

if [ -d "${DICT_SOURCE}" ]; then
    echo -e "${GREEN}Copying dictionary files...${NC}"
    mkdir -p "${DICT_DEST}"
    cp -r "${DICT_SOURCE}/"* "${DICT_DEST}/"
else
    echo -e "${YELLOW}Warning: Dictionary files not found. Please build OpenJTalk first.${NC}"
fi

echo -e "${GREEN}=== iOS Build Complete ===${NC}"
echo -e "Library location: ${OUTPUT_DIR}/libopenjtalk_wrapper.a"