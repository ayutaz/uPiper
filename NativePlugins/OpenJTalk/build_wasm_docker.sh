#!/bin/bash

echo "Building OpenJTalk for WebAssembly using Docker..."

# Build Docker image
echo "Building Docker image..."
docker build -f Dockerfile.wasm -t openjtalk-wasm-builder .

# Run the build
echo "Running build in Docker container..."
docker run --rm -v "$(pwd):/output" openjtalk-wasm-builder sh -c "cp -r /build/build_wasm/lib/* /output/"

# Copy output files to Unity
echo "Copying files to Unity StreamingAssets..."
if [ -f "openjtalk_wrapper.js" ]; then
    cp -f "openjtalk_wrapper.js" "../../Assets/StreamingAssets/openjtalk.js"
    cp -f "openjtalk_wrapper.wasm" "../../Assets/StreamingAssets/openjtalk.wasm"
    echo "Build successful! Files copied to Unity."
else
    echo "Error: Build files not found."
    exit 1
fi

echo "Done!"