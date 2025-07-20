#!/bin/bash

# Download NAIST Japanese Dictionary for OpenJTalk
# This downloads the same dictionary used by pyopenjtalk

set -e

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
DICT_DIR="$SCRIPT_DIR/../naist_jdic"
DICT_URL="https://github.com/r9y9/open_jtalk/releases/download/v1.11.1/open_jtalk_dic_utf_8-1.11.tar.gz"
DICT_FILENAME="open_jtalk_dic_utf_8-1.11.tar.gz"
DICT_SHA256="fe6ba0e43542cef98339abdffd903e062008ea170b04e7e2a35da805902f382a"

echo "Creating dictionary directory..."
mkdir -p "$DICT_DIR"

echo "Downloading NAIST Japanese Dictionary..."
cd "$DICT_DIR"

# Download dictionary archive
if command -v curl &> /dev/null; then
    curl -L -o "$DICT_FILENAME" "$DICT_URL"
elif command -v wget &> /dev/null; then
    wget -O "$DICT_FILENAME" "$DICT_URL"
else
    echo "Error: Neither curl nor wget is available for downloading"
    exit 1
fi

# Verify checksum
echo "Verifying checksum..."
if command -v sha256sum &> /dev/null; then
    echo "$DICT_SHA256  $DICT_FILENAME" | sha256sum -c -
elif command -v shasum &> /dev/null; then
    echo "$DICT_SHA256  $DICT_FILENAME" | shasum -a 256 -c -
else
    echo "Warning: No checksum tool available, skipping verification"
fi

# Extract dictionary
echo "Extracting dictionary..."
tar -xzf "$DICT_FILENAME"

# Clean up
rm "$DICT_FILENAME"

echo "NAIST dictionary downloaded successfully!"
echo "Dictionary path: $DICT_DIR/open_jtalk_dic_utf_8-1.11"

# Show dictionary contents
echo ""
echo "Dictionary contents:"
ls -lh "$DICT_DIR/open_jtalk_dic_utf_8-1.11/"