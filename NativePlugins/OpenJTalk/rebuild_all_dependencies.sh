#!/bin/bash

# Rebuild all Android dependencies
set -e

echo "=== Rebuilding All Android Dependencies ==="

# Clean previous builds
echo "Cleaning previous builds..."
rm -rf external/openjtalk_build/android_*

# Build dependencies for all ABIs
./build_dependencies_android.sh

echo ""
echo "=== All Dependencies Rebuilt ==="
echo ""
echo "Built ABIs:"
ls -la external/openjtalk_build/