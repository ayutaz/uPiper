#!/bin/bash

# Download mecab-naist-jdic dictionary
# This script downloads the NAIST Japanese Dictionary for MeCab

set -e

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
DICT_DIR="$SCRIPT_DIR/../dictionary"
DICT_URL="https://github.com/tsukumijima/pyopenjtalk-plus/raw/master/pyopenjtalk/dictionary"

echo "Creating dictionary directory..."
mkdir -p "$DICT_DIR"

echo "Downloading mecab-naist-jdic dictionary files..."

# Download dictionary files
FILES=(
    "char.bin"
    "left-id.def"
    "matrix.bin"
    "pos-id.def"
    "rewrite.def"
    "right-id.def"
    "sys.dic"
    "unk.dic"
)

for file in "${FILES[@]}"; do
    echo "Downloading $file..."
    curl -L -o "$DICT_DIR/$file" "$DICT_URL/$file"
done

echo "Dictionary download complete!"
echo "Files saved to: $DICT_DIR"

# Check file sizes
echo ""
echo "Downloaded files:"
ls -lh "$DICT_DIR"