# WebGL Troubleshooting Guide

## Quick Reference

This guide provides solutions for common WebGL issues encountered with uPiper.

## Common Issues and Solutions

### 1. 404 Error for OpenJTalk Data Files

**Symptom:**
```
GET https://[domain]/StreamingAssets/openjtalk-unity.data 404 (Not Found)
```

**Cause:** GitHub Pages has a 100MB file size limit.

**Solution:**
1. Ensure files are properly split:
   ```bash
   ls -lh Build/Web/StreamingAssets/openjtalk-unity.data.*
   ```
2. Verify split-file-loader.js is loaded before Unity framework
3. Check that both `.partaa` and `.partab` files exist

### 2. Audio Playing Too Fast or Distorted

**Symptom:**
- Audio plays at incorrect speed
- Speech is unintelligible
- Duration is much longer/shorter than expected

**Current Status:** Partially resolved with workaround.

**Workaround:**
The system automatically adjusts `length_scale` parameter for Unity WebGL environments:
```javascript
// In onnx-runtime-wrapper.js
if (isUnityWebGL) {
    lengthScale = lengthScale * 0.82;
}
```

**Root Cause:** Under investigation. Related to Unity WebGL's Float32Array marshalling.

### 3. ONNX Runtime Initialization Failure

**Symptom:**
```
[UnityONNXRuntime] Initialization failed: [error]
```

**Common Causes:**
- Missing ONNX model files
- Incorrect file paths
- CORS issues

**Solutions:**
1. Verify model files exist:
   ```bash
   ls Assets/StreamingAssets/*.onnx*
   ```
2. Check browser console for CORS errors
3. Ensure proper MIME types are configured

### 4. Memory Issues

**Symptom:**
- "Out of memory" errors
- Browser tab crashes
- Slow performance

**Solutions:**
1. Use smaller models for development
2. Enable browser memory flags (Chrome):
   ```
   --max-old-space-size=4096
   ```
3. Implement proper resource disposal:
   ```javascript
   window.UnityONNX.dispose();
   ```

## Debugging Steps

### 1. Enable Debug Logging

In browser console:
```javascript
window.UnityONNXRuntime.setDebugMode(true);
```

### 2. Check File Loading

Monitor Network tab in browser DevTools:
- Verify all required files are loaded
- Check response status codes
- Confirm file sizes match expectations

### 3. Analyze Tensor Output

Look for these log patterns:
```
[UnityONNXRuntime] Output tensor dims: [dimensions]
[UnityONNXRuntime] Expected: 3D tensor, got: 4D
```

### 4. Environment Detection

Verify Unity WebGL is properly detected:
```
[UnityONNXRuntime] [UNITY WEBGL FIX] Detected Unity WebGL environment
```

## Known Issues

### Issue #1: 4D Tensor Output
- **Status:** Unresolved
- **Impact:** Causes audio length inconsistencies
- **Workaround:** length_scale adjustment
- **Details:** [Full Investigation](docs/ja/guides/webgl/unity-webgl-audio-issue-complete-investigation.md)

### Issue #2: Inconsistent Audio Length
- **Status:** Partially resolved
- **Impact:** Audio duration varies between builds
- **Workaround:** Dynamic length_scale adjustment
- **Root Cause:** Unity WebGL data marshalling issue

### Issue #3: High Memory Usage
- **Status:** Ongoing optimization
- **Impact:** May cause browser tab crashes on low-end devices
- **Mitigation:** Use smaller models, implement aggressive cleanup

## Performance Optimization Tips

### 1. Model Selection
- Development: Use `*-small.onnx` models
- Production: Use `*-medium.onnx` models
- Avoid `*-large.onnx` models for WebGL

### 2. Caching Strategy
- Enable browser caching for model files
- Implement application-level audio caching
- Use IndexedDB for persistent storage

### 3. Loading Strategy
- Lazy load models on demand
- Show loading indicators during initialization
- Preload critical resources

## Testing Checklist

- [ ] Test on multiple browsers (Chrome, Firefox, Safari)
- [ ] Verify on both localhost and deployed URL
- [ ] Check console for errors
- [ ] Monitor memory usage
- [ ] Validate audio output quality
- [ ] Test with different text inputs
- [ ] Verify file loading sequence
- [ ] Check network requests

## Useful Commands

### Local Testing
```bash
# Python HTTP server
python -m http.server 8080 --directory Build/Web

# Node.js HTTP server
npx http-server Build/Web -p 8080
```

### File Verification
```bash
# Check file sizes
du -h Build/Web/StreamingAssets/*

# Verify split files
ls -la Build/Web/StreamingAssets/openjtalk-unity.data.*
```

### Browser Console Commands
```javascript
// Clear audio cache
window.clearAudioCache();

// Run minimal test
window.UnityONNX.runMinimalTest();

// Check initialization status
window.UnityONNXRuntime.isInitialized();
```

## Support Resources

- [Complete Investigation Report](docs/ja/guides/webgl/unity-webgl-audio-issue-complete-investigation.md)
- [WebGL Implementation Guide](docs/ja/guides/webgl/README.md)
- [ONNX Runtime Web Architecture](docs/ONNX_RUNTIME_WEB_ARCHITECTURE.md)
- [GitHub Issues](https://github.com/ayutaz/uPiper/issues)

## Reporting Issues

When reporting WebGL issues, please include:
1. Browser and version
2. Console error logs
3. Network tab screenshot
4. Steps to reproduce
5. Expected vs actual behavior

---

*Last updated: 2025-08-12*