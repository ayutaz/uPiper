#!/bin/bash
echo "Building production OpenJTalk WebAssembly module..."

# Build using Docker
docker build -f Dockerfile.wasm.production -t openjtalk-wasm-prod .
if [ $? -ne 0 ]; then
    echo "Docker build failed!"
    exit 1
fi

# Create output directory
mkdir -p build_wasm_output

# Copy built files from container
docker create --name openjtalk-wasm-temp openjtalk-wasm-prod
docker cp openjtalk-wasm-temp:/build/openjtalk.js build_wasm_output/
docker cp openjtalk-wasm-temp:/build/openjtalk.wasm build_wasm_output/
docker rm openjtalk-wasm-temp

# Copy to Unity StreamingAssets
echo "Copying files to Unity StreamingAssets..."
cp -f build_wasm_output/openjtalk.js ../../Assets/StreamingAssets/
cp -f build_wasm_output/openjtalk.wasm ../../Assets/StreamingAssets/

echo "Build complete!"
echo "Files copied to Assets/StreamingAssets/"