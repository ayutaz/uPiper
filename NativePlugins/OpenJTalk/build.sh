#!/bin/bash

# OpenJTalk Wrapper Build Script for Unix-like systems (Linux/macOS)

echo "==================================="
echo "OpenJTalk Wrapper Build for Unix"
echo "==================================="

# Default values
BUILD_TYPE="Release"
ARCH=$(uname -m)
PLATFORM=$(uname -s)

# Parse command line arguments
if [ "$1" = "Debug" ] || [ "$1" = "debug" ]; then
    BUILD_TYPE="Debug"
fi

if [ "$2" != "" ]; then
    ARCH=$2
fi

# Detect platform
if [ "$PLATFORM" = "Darwin" ]; then
    PLATFORM_NAME="macos"
    SHARED_LIB_EXT="dylib"
    echo "Detected platform: macOS"
elif [ "$PLATFORM" = "Linux" ]; then
    PLATFORM_NAME="linux"
    SHARED_LIB_EXT="so"
    echo "Detected platform: Linux"
else
    echo "ERROR: Unsupported platform: $PLATFORM"
    exit 1
fi

echo "Architecture: $ARCH"
echo "Build type: $BUILD_TYPE"

# Check for required tools
echo ""
echo "Checking for required tools..."

if ! command -v cmake &> /dev/null; then
    echo "ERROR: CMake not found. Please install CMake."
    exit 1
fi

if ! command -v make &> /dev/null; then
    echo "ERROR: Make not found. Please install build tools."
    exit 1
fi

if [ "$PLATFORM" = "Darwin" ]; then
    if ! command -v clang &> /dev/null; then
        echo "ERROR: Clang not found. Please install Xcode Command Line Tools."
        exit 1
    fi
else
    if ! command -v gcc &> /dev/null; then
        echo "ERROR: GCC not found. Please install build-essential."
        exit 1
    fi
fi

echo "All required tools found."

# Create build directory
BUILD_DIR="build/$PLATFORM_NAME/$ARCH/$BUILD_TYPE"
mkdir -p "$BUILD_DIR"
cd "$BUILD_DIR" || exit 1

# Configure with CMake
echo ""
echo "Configuring with CMake..."
cmake -DCMAKE_BUILD_TYPE="$BUILD_TYPE" \
      -DCMAKE_INSTALL_PREFIX="../../../../output/$PLATFORM_NAME/$ARCH" \
      -DENABLE_DEBUG_LOG=ON \
      ../../../..

if [ $? -ne 0 ]; then
    echo "ERROR: CMake configuration failed"
    cd ../../../.. || exit 1
    exit 1
fi

# Build
echo ""
echo "Building OpenJTalk Wrapper..."
make -j$(nproc 2>/dev/null || sysctl -n hw.ncpu 2>/dev/null || echo 2)

if [ $? -ne 0 ]; then
    echo "ERROR: Build failed"
    cd ../../../.. || exit 1
    exit 1
fi

# Install
echo ""
echo "Installing files..."
make install

if [ $? -ne 0 ]; then
    echo "ERROR: Installation failed"
    cd ../../../.. || exit 1
    exit 1
fi

# Return to original directory
cd ../../../.. || exit 1

# Copy to Unity plugin directory
echo ""
echo "Copying to Unity plugin directory..."

if [ "$PLATFORM" = "Darwin" ]; then
    UNITY_PLUGIN_DIR="../../Assets/uPiper/Plugins/macOS"
else
    UNITY_PLUGIN_DIR="../../Assets/uPiper/Plugins/Linux/$ARCH"
fi

mkdir -p "$UNITY_PLUGIN_DIR"

if [ "$PLATFORM" = "Darwin" ]; then
    cp "output/$PLATFORM_NAME/$ARCH/lib/libopenjtalk_wrapper.$SHARED_LIB_EXT" "$UNITY_PLUGIN_DIR/"
    
    # Create bundle structure for macOS
    BUNDLE_DIR="$UNITY_PLUGIN_DIR/openjtalk_wrapper.bundle"
    mkdir -p "$BUNDLE_DIR/Contents/MacOS"
    cp "output/$PLATFORM_NAME/$ARCH/lib/libopenjtalk_wrapper.$SHARED_LIB_EXT" "$BUNDLE_DIR/Contents/MacOS/openjtalk_wrapper"
    
    # Create Info.plist
    cat > "$BUNDLE_DIR/Contents/Info.plist" << EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDevelopmentRegion</key>
    <string>English</string>
    <key>CFBundleExecutable</key>
    <string>openjtalk_wrapper</string>
    <key>CFBundleIdentifier</key>
    <string>com.upiper.openjtalk</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleName</key>
    <string>OpenJTalk Wrapper</string>
    <key>CFBundlePackageType</key>
    <string>BNDL</string>
    <key>CFBundleVersion</key>
    <string>1.0.0</string>
</dict>
</plist>
EOF
else
    cp "output/$PLATFORM_NAME/$ARCH/lib/libopenjtalk_wrapper.$SHARED_LIB_EXT" "$UNITY_PLUGIN_DIR/"
fi

if [ $? -ne 0 ]; then
    echo "ERROR: Failed to copy library to Unity plugin directory"
    exit 1
fi

# Success
echo ""
echo "==================================="
echo "Build completed successfully!"
echo "==================================="
echo ""
echo "Output files:"
if [ "$PLATFORM" = "Darwin" ]; then
    echo "  Library: $UNITY_PLUGIN_DIR/openjtalk_wrapper.bundle"
else
    echo "  Library: $UNITY_PLUGIN_DIR/libopenjtalk_wrapper.$SHARED_LIB_EXT"
fi
echo "  Header: output/$PLATFORM_NAME/$ARCH/include/openjtalk_wrapper.h"
echo ""
echo "Build configuration:"
echo "  Type: $BUILD_TYPE"
echo "  Architecture: $ARCH"
echo "  Platform: $PLATFORM_NAME"
echo ""

exit 0