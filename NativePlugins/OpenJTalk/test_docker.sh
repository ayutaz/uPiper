#!/bin/bash
# Simple test script to verify Docker environment

echo "=== Test Docker Environment ==="
echo "Current directory: $(pwd)"
echo "User: $(whoami)"
echo "Home: $HOME"
echo ""

echo "=== Directory contents ==="
ls -la
echo ""

echo "=== Check for required tools ==="
which gcc || echo "gcc not found"
which x86_64-w64-mingw32-gcc || echo "mingw gcc not found"
which cmake || echo "cmake not found"
which make || echo "make not found"
echo ""

echo "=== Environment variables ==="
echo "CC=$CC"
echo "CXX=$CXX"
echo ""

echo "=== Test complete ==="