#!/bin/bash

# Fix rpath issues for macOS library

echo "Fixing rpath for OpenJTalk library..."

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PLUGINS_DIR="$SCRIPT_DIR/../../Plugins/macOS"
LIBRARY_PATH="$PLUGINS_DIR/libopenjtalk_wrapper.dylib"

if [ ! -f "$LIBRARY_PATH" ]; then
    echo "Error: Library not found at $LIBRARY_PATH"
    exit 1
fi

echo "Current library ID and dependencies:"
otool -L "$LIBRARY_PATH"

# Change the library ID to use @loader_path instead of @rpath
echo -e "\nChanging library ID..."
install_name_tool -id "@loader_path/libopenjtalk_wrapper.dylib" "$LIBRARY_PATH"

# If there are any other dependencies using @rpath, fix them too
# For now, let's just show the updated dependencies
echo -e "\nUpdated library dependencies:"
otool -L "$LIBRARY_PATH"

# Also check if we need to add rpath
echo -e "\nCurrent rpaths:"
otool -l "$LIBRARY_PATH" | grep -A2 LC_RPATH || echo "No rpaths found"

# Sign the library for macOS
echo -e "\nSigning library..."
codesign --force --sign - "$LIBRARY_PATH"

echo -e "\nDone! Library has been fixed."
echo "Please restart Unity Editor to apply changes."