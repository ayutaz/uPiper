# OpenJTalk WebAssembly Build Guide

This guide explains how to build OpenJTalk for WebAssembly using Docker.

## Prerequisites

- Docker Desktop installed and running
- Git for Windows (if on Windows)

## Building with Docker

### Option 1: Using Docker Compose (Recommended)

```bash
# From the NativePlugins/OpenJTalk directory
docker-compose -f docker-compose.wasm.yml up --build
```

The output files will be in the `output` directory.

### Option 2: Using build script

#### Windows:
```cmd
build_wasm_docker.bat
```

#### macOS/Linux:
```bash
chmod +x build_wasm_docker.sh
./build_wasm_docker.sh
```

### Option 3: Manual Docker commands

```bash
# Build the Docker image
docker build -f Dockerfile.wasm -t openjtalk-wasm-builder .

# Run the build
docker run --rm -v "$(pwd)/output:/output" openjtalk-wasm-builder sh -c "cp /build/build_wasm/lib/* /output/"

# Copy to Unity
cp output/openjtalk_wrapper.js ../../Assets/StreamingAssets/openjtalk.js
cp output/openjtalk_wrapper.wasm ../../Assets/StreamingAssets/openjtalk.wasm
```

## Expected Output

After a successful build, you should have:
- `openjtalk_wrapper.js`: JavaScript module with proper HEAP exports
- `openjtalk_wrapper.wasm`: WebAssembly binary

These files will have:
- HEAP8, HEAPU8, etc. properly exported
- ES5 module format (no ES6 imports)
- All OpenJTalk functions accessible

## Troubleshooting

### Docker not found
- Ensure Docker Desktop is installed and running
- On Windows, you may need to restart your terminal

### Build fails
- Check Docker logs: `docker logs <container_id>`
- Ensure you have enough disk space (build requires ~2GB)
- Try cleaning Docker cache: `docker system prune`

### Files not copied to Unity
- Check the `output` directory for the built files
- Manually copy them to `Assets/StreamingAssets/`

## Build Time

The complete build process takes approximately:
- First build: 20-30 minutes (downloading dependencies)
- Subsequent builds: 5-10 minutes (using cache)