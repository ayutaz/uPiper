#!/bin/bash

# Fix macOS library loading issues

echo "Fixing macOS library loading issues..."

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PLUGINS_DIR="$SCRIPT_DIR/../../Plugins/macOS"

# Find the library
LIBRARY_PATH="$PLUGINS_DIR/libopenjtalk_wrapper.dylib"

if [ ! -f "$LIBRARY_PATH" ]; then
    echo "Error: Library not found at $LIBRARY_PATH"
    exit 1
fi

echo "Found library at: $LIBRARY_PATH"

# Remove quarantine attribute (for downloaded files)
xattr -d com.apple.quarantine "$LIBRARY_PATH" 2>/dev/null

# Check library architecture
echo "Library architecture:"
file "$LIBRARY_PATH"
lipo -info "$LIBRARY_PATH"

# Check library dependencies
echo -e "\nLibrary dependencies:"
otool -L "$LIBRARY_PATH"

# Make sure the library is executable
chmod +x "$LIBRARY_PATH"

# For development, you might need to sign the library
# codesign --force --sign - "$LIBRARY_PATH"

echo -e "\nLibrary permissions:"
ls -la "$LIBRARY_PATH"

echo -e "\nDone! Please restart Unity Editor if it's running."