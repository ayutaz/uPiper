#!/bin/bash
# Test CI environment

echo "=== Testing CI Environment ==="
echo "Current directory: $(pwd)"
echo "OS Type: $OSTYPE"
echo "Shell: $SHELL"
echo "User: $(whoami)"

echo ""
echo "=== Directory structure ==="
ls -la

echo ""
echo "=== Checking script files ==="
ls -la *.sh 2>/dev/null || echo "No .sh files found"

echo ""
echo "=== Checking CI-specific scripts ==="
if [ -f "fetch_dependencies_ci.sh" ]; then
    echo "fetch_dependencies_ci.sh exists"
else
    echo "fetch_dependencies_ci.sh NOT FOUND"
fi

if [ -f "build_dependencies_ci.sh" ]; then
    echo "build_dependencies_ci.sh exists"
else
    echo "build_dependencies_ci.sh NOT FOUND"
fi

echo ""
echo "=== Environment complete ==="