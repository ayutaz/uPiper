#!/bin/bash
set -e

# Build full production OpenJTalk WebAssembly module
echo "Building full production OpenJTalk WebAssembly module..."

# Build the Docker image
docker build -f Dockerfile.wasm.full -t openjtalk-wasm-full-builder .

# Run the container and copy the output files
docker run --rm -v "$(pwd)/output:/output" openjtalk-wasm-full-builder sh -c "
    cp /build/openjtalk.js /output/
    cp /build/openjtalk.wasm /output/
    echo 'Build artifacts copied to output directory'
"

echo "Full production build complete!"
echo "Output files:"
echo "  - output/openjtalk.js"
echo "  - output/openjtalk.wasm"