#!/bin/bash

# Generate API documentation using Doxygen

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
cd "$SCRIPT_DIR"

# Check if Doxygen is installed
if ! command -v doxygen &> /dev/null; then
    echo "Error: Doxygen is not installed"
    echo "Please install Doxygen:"
    echo "  macOS: brew install doxygen"
    echo "  Ubuntu: sudo apt-get install doxygen"
    echo "  Windows: Download from https://www.doxygen.nl/download.html"
    exit 1
fi

# Clean previous documentation
if [ -d "docs" ]; then
    echo "Cleaning previous documentation..."
    rm -rf docs
fi

# Generate documentation
echo "Generating API documentation..."
doxygen Doxyfile

if [ $? -eq 0 ]; then
    echo "Documentation generated successfully!"
    echo "Open docs/html/index.html to view the documentation"
    
    # Open documentation in default browser (macOS)
    if [[ "$OSTYPE" == "darwin"* ]]; then
        open docs/html/index.html
    fi
else
    echo "Error: Failed to generate documentation"
    exit 1
fi