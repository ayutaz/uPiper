# WebGL Implementation Guide

## Overview

uPiper's WebGL implementation provides full Japanese speech synthesis functionality using ONNX Runtime Web. It bypasses Unity AI Inference Engine's WebGL limitations and achieves the same accuracy as piper-plus.

## Key Features

- ✅ **Full Dictionary Support**: Uses NAIST Japanese Dictionary (102.33MB)
- ✅ **High-Precision Phonemization**: Same accuracy as Windows/Android versions
- ✅ **GitHub Pages Compatible**: Automatic file splitting for 100MB limit
- ✅ **ONNX Runtime Web Integration**: Browser-native fast inference

## Architecture

```
Unity C# Layer
    ↓
JSLib Bridge (ONNXRuntimeBridge.jslib)
    ↓
JavaScript Runtime (onnx-runtime-wrapper.js)
    ↓
ONNX Runtime Web + OpenJTalk WASM
```

## Required Files

WebGL builds require the following files:

### OpenJTalk WASM
- `openjtalk-unity.js` - OpenJTalk JavaScript interface
- `openjtalk-unity.wasm` - OpenJTalk WebAssembly binary
- `openjtalk-unity.data` - NAIST dictionary data (split)
  - `openjtalk-unity.data.part000` (90MB)
  - `openjtalk-unity.data.part001` (13MB)
  - `openjtalk-unity.data.manifest.json`

### ONNX Runtime Web
- `onnx-runtime-wrapper.js` - ONNX Runtime Web wrapper
- `ONNXRuntimeBridge.jslib` - Unity-JavaScript bridge

### Integration Scripts
- `github-pages-adapter.js` - GitHub Pages path resolution and file reconstruction
- `openjtalk-unity-wrapper.js` - OpenJTalk integration wrapper

## Build Instructions

### 1. Preparation

```bash
# Split large files (first time only)
uv run python split-openjtalk-data.py
```

### 2. Unity Settings

Player Settings:
- **Target Platform**: WebGL
- **Compression Format**: Gzip or Disabled (for development)
- **Decompression Fallback**: Enabled (fixed in Unity 6000.0.55f1)

### 3. Build Execution

1. Open `File > Build Settings` in Unity Editor
2. Select WebGL platform
3. Click `Build` or `Build And Run`

### 4. Post-Build Processing

`WebGLPostBuildProcessor` automatically:
- Copies required files to StreamingAssets
- Adds script tags to index.html
- Places split files

## Local Testing

```bash
# Run with Python server
python -m http.server 8080 --directory Build

# Access in browser
http://localhost:8080
```

## GitHub Pages Deployment

### 1. Repository Settings

1. GitHub repository Settings > Pages
2. Source: Deploy from a branch
3. Branch: gh-pages (or any branch)
4. Folder: / (root)

### 2. File Upload

```bash
# Push build results to gh-pages branch
git checkout -b gh-pages
cp -r Build/* .
git add -A
git commit -m "Deploy WebGL build"
git push origin gh-pages
```

### 3. Access

```
https://[username].github.io/[repository]/
```

## Troubleshooting

### Error: "Failed to load openjtalk-unity.data"

**Cause**: File exceeds 100MB

**Solution**: 
```bash
# Verify file splitting
ls -lh Assets/StreamingAssets/openjtalk-unity.data.*
```

### Error: "ONNX Runtime initialization failed"

**Cause**: ONNX model files not found

**Solution**:
```bash
# Check model files
ls Assets/StreamingAssets/*.onnx*
```

### Error: "Cannot read properties of undefined"

**Cause**: Unity 6000.0.35f1 WebGL bug

**Solution**: Update to Unity 6000.0.55f1 or later

## Performance Optimization

### 1. Model Size Optimization

Use smaller models:
- `ja_JP-test-small.onnx` (15MB) - For development
- `ja_JP-test-medium.onnx` (63MB) - For production

### 2. Caching Strategy

```javascript
// Enable caching in github-pages-adapter.js
const CACHE_NAME = 'upiper-v1';
const CACHE_DURATION = 7 * 24 * 60 * 60 * 1000; // 7 days
```

### 3. Lazy Loading

```javascript
// Load ONNX model only when needed
async function loadModelOnDemand() {
    if (!window.onnxModel) {
        window.onnxModel = await loadONNXModel();
    }
    return window.onnxModel;
}
```

## Technical Details

### Memory Management

WebGL operates with limited memory, so the following measures are implemented:

1. **Automatic Memory Release**
```csharp
[DllImport("__Internal")]
private static extern void ONNXRuntime_FreeMemory(IntPtr ptr);
```

2. **Streaming Processing**
- Split processing of large audio data
- Immediate release of unnecessary buffers

### Asynchronous Processing

Asynchronous processing using JavaScript Promises:

```csharp
public async Task<float[]> GenerateAudioAsync(int[] phonemeIds)
{
    var tcs = new TaskCompletionSource<float[]>();
    ONNXRuntime_Synthesize(phonemeIds, callback);
    return await tcs.Task;
}
```

## Related Documentation

- [ONNX Runtime Web Architecture](../../../ONNX_RUNTIME_WEB_ARCHITECTURE.md)
- [WebGL Build Checklist](../../../../WEBGL_BUILD_CHECKLIST.md)
- [Unity WebGL Implementation Plan](../../../../UNITY_WEBGL_OPENJTALK_IMPLEMENTATION_PLAN.md)