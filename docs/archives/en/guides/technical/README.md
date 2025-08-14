# Technical Documentation

This directory contains documentation about technical details of uPiper.

## Document List

### 1. [il2cpp.md](il2cpp.md)
**IL2CPP Support Comprehensive Guide**
- IL2CPP overview, benefits and limitations
- Build configuration (automatic/manual)
- Platform-specific settings
- Native library placement
- Troubleshooting
- Optimization tips
- Performance expectations

### 2. [il2cpp-compatibility.md](il2cpp-compatibility.md)
**IL2CPP Compatibility Report**
- P/Invoke implementation verification results
- Marshaling attribute analysis
- AOT constraint compliance status
- Unity AI Inference Engine compatibility
- CI/CD environment limitations
- Actual code analysis results

### 3. [gpu-inference.md](gpu-inference.md)
**GPU Inference Guide**
- Backend type selection
- Platform-specific GPU settings
- Performance optimization
- Troubleshooting

### 4. [webgl-investigation.md](webgl-investigation.md)
**WebGL Support Technical Investigation Report**
- Discovery of OpenJTalk WebAssembly implementation
- Unity WebGL technical advantages
- Comparison with piper-plus project
- Implementation approach and recommendations
- Feasibility of high-precision phonemization with full dictionary

## How to Use Documentation

### For New Developers
1. First understand IL2CPP basics with [il2cpp.md](il2cpp.md)
2. Performance optimization with [gpu-inference.md](gpu-inference.md)
3. See [CI/CD Guide](../ci-cd/) for CI/CD configuration

### IL2CPP Integration Work
1. Check configuration methods in [il2cpp.md](il2cpp.md)
2. Check known issues in [il2cpp-compatibility.md](il2cpp-compatibility.md)
3. See "Build Procedure" section in [il2cpp.md](il2cpp.md) for build configuration

## Maintenance Guidelines

### Update Frequency
- **il2cpp.md**: When Unity/uPiper has major updates
- **il2cpp-compatibility.md**: When testing new platforms, Unity updates
- **gpu-inference.md**: When Unity AI Inference Engine updates
- **webgl-investigation.md**: When WebGL implementation progresses

### Documentation Creation Rules
1. Don't create design documents before implementation (code is truth)
2. Document based on actual behavior after implementation
3. Include specific code examples
4. Clearly distinguish platform-specific information

## Related Resources

- [Unity IL2CPP Official Documentation](https://docs.unity3d.com/Manual/IL2CPP.html)
- [Unity AI Inference Engine](https://docs.unity3d.com/Packages/com.unity.sentis@latest)
- [GitHub Actions](https://docs.github.com/en/actions)