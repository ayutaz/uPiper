#!/bin/bash

# Setup Unity Android plugin structure
set -e

echo "=== Setting up Unity Android Plugin Structure ==="

# Unity plugin base path
UNITY_PLUGIN_PATH="../../Assets/uPiper/Plugins/Android"

# Create directory structure
echo "Creating Unity plugin directories..."
mkdir -p "$UNITY_PLUGIN_PATH/libs/arm64-v8a"
mkdir -p "$UNITY_PLUGIN_PATH/libs/armeabi-v7a"
mkdir -p "$UNITY_PLUGIN_PATH/libs/x86"
mkdir -p "$UNITY_PLUGIN_PATH/libs/x86_64"

# Copy libraries if they exist
if [ -d "output/android" ]; then
    echo "Copying Android libraries to Unity..."
    
    for ABI in arm64-v8a armeabi-v7a x86 x86_64; do
        if [ -f "output/android/$ABI/libopenjtalk_wrapper.so" ]; then
            echo "Copying $ABI library..."
            cp "output/android/$ABI/libopenjtalk_wrapper.so" "$UNITY_PLUGIN_PATH/libs/$ABI/"
            
            # Create .meta file for Unity
            cat > "$UNITY_PLUGIN_PATH/libs/$ABI/libopenjtalk_wrapper.so.meta" << EOF
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
      Android: Android
    second:
      enabled: 1
      settings:
        CPU: $ABI
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
  userData: 
  assetBundleName: 
  assetBundleVariant: 
EOF
        else
            echo "Warning: $ABI library not found"
        fi
    done
    
    # Copy C++ shared library (needed for all ABIs)
    echo ""
    echo "Copying C++ shared libraries..."
    for ABI in arm64-v8a armeabi-v7a x86 x86_64; do
        if [ "$ABI" == "arm64-v8a" ]; then
            ARCH_DIR="aarch64-linux-android"
        elif [ "$ABI" == "armeabi-v7a" ]; then
            ARCH_DIR="arm-linux-androideabi"
        elif [ "$ABI" == "x86" ]; then
            ARCH_DIR="i686-linux-android"
        elif [ "$ABI" == "x86_64" ]; then
            ARCH_DIR="x86_64-linux-android"
        fi
        
        CPP_LIB="$ANDROID_NDK_HOME/toolchains/llvm/prebuilt/linux-x86_64/sysroot/usr/lib/$ARCH_DIR/libc++_shared.so"
        if [ -f "$CPP_LIB" ]; then
            echo "Copying libc++_shared.so for $ABI..."
            cp "$CPP_LIB" "$UNITY_PLUGIN_PATH/libs/$ABI/"
            
            # Create .meta file for libc++_shared.so
            cat > "$UNITY_PLUGIN_PATH/libs/$ABI/libc++_shared.so.meta" << EOF
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
      Android: Android
    second:
      enabled: 1
      settings:
        CPU: $ABI
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
  userData: 
  assetBundleName: 
  assetBundleVariant: 
EOF
        fi
    done
else
    echo "Error: output/android directory not found. Please build the libraries first."
    exit 1
fi

echo ""
echo "Unity Android plugin structure created successfully!"
echo "Plugin location: $UNITY_PLUGIN_PATH"