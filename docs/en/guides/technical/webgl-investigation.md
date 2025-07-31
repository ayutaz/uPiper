# WebGL Support Technical Investigation Report

## Overview

This document summarizes the technical investigation results for uPiper's WebGL support. It details the technical feasibility and implementation approaches for achieving high-quality Japanese speech synthesis in Unity WebGL environments.

## Investigation Background

- **Investigation Period**: January 2025
- **Objective**: Achieve high-precision Japanese speech synthesis with full dictionary support in browser environments
- **Related Project**: WebAssembly implementation investigation of [piper-plus](https://github.com/ayutaz/piper-plus)

## Key Findings

### 1. Existence of OpenJTalk WebAssembly Implementation

The npm package `wasm_open_jtalk` already exists, providing a WebAssembly implementation of OpenJTalk:

```bash
npm install wasm_open_jtalk
```

- Built using Emscripten
- Dictionary compatibility issues resolved
- Verified to work in Node.js environment

### 2. Unity WebGL Technical Advantages

#### Memory Management
- Unity WebGL provides 2-4GB heap
- Capable of processing large dictionary files (100MB)
- Automatic caching system via IndexedDB

#### JavaScript Integration
Unity WebGL has established JavaScript integration using `.jslib` files:

```javascript
// sample.jslib
mergeInto(LibraryManager.library, {
    InitOpenJTalkWasm: function() {
        // Initialize OpenJTalk WebAssembly
    },
    PhonemizeText: function(textPtr) {
        var text = UTF8ToString(textPtr);
        var phonemes = openjtalk_wasm.phonemize(text);
        return allocateUTF8(phonemes);
    }
});
```

```csharp
// Unity C# side
[DllImport("__Internal")]
private static extern void InitOpenJTalkWasm();

[DllImport("__Internal")]
private static extern string PhonemizeText(string text);
```

## Comparison with piper-plus Project

### piper-plus Challenges (PR #118)

The piper-plus project abandoned implementation due to MeCab dictionary format incompatibility:

- **Issue**: DARTS (Double Array Trie) implementation incompatibility
- **Result**: 0% phonemization accuracy
- **Cause**: MeCab's proprietary binary format

### uPiper Solution

1. **Leverage Existing Implementation**: Use `wasm_open_jtalk` package
2. **Utilize Unity Foundation**: Strong support from WebGL runtime
3. **Phased Implementation**: Build from verified components

## Implementation Approach

### Phase 1: Foundation Setup (1-2 weeks)

1. **WebGL Detection and Fallback**
```csharp
#if UNITY_WEBGL && !UNITY_EDITOR
public class WebGLPhonemizer : IPhonmizer
{
    // WebGL-specific implementation
}
#endif
```

2. **External Dictionary Distribution**
- Dynamic loading from CDN
- Asynchronous retrieval via UnityWebRequest
- IndexedDB caching

### Phase 2: OpenJTalk Integration (2-3 weeks)

1. **wasm_open_jtalk Integration**
- Adaptation to browser environment
- Unity jslib integration implementation

2. **Performance Optimization**
- Initial load optimization
- Memory usage management

### Phase 3: Quality Assurance (1 week)

1. **Accuracy Verification**
- Comparison with desktop version
- Test case execution

2. **Usability Improvements**
- Loading progress display
- Error handling

## Technical Constraints and Solutions

### Constraints

1. **Initial Load Time**: 10-20 seconds for dictionary download (100MB)
2. **Memory Usage**: 300-400MB for dictionary + models
3. **Browser Requirements**: WebAssembly support mandatory

### Solutions

1. **Progressive Loading**: Load necessary parts sequentially
2. **Caching Strategy**: Persistence via IndexedDB
3. **Fallback**: Appropriate guidance for unsupported environments

## Recommendations

### Implementation Priority: **High**

WebGL support enables:

- ✅ Installation-free demo environment
- ✅ Phonemization accuracy equivalent to pyopenjtalk
- ✅ Cross-platform support
- ✅ Easy distribution and access

### Next Steps

1. **Implementation in piper-plus**: Start with a lighter implementation
2. **Integration into uPiper**: Leverage piper-plus results
3. **Continuous Improvement**: Performance and UX enhancement

## Conclusion

High-precision Japanese speech synthesis with full dictionary support in Unity WebGL environments is **technically feasible**. By utilizing `wasm_open_jtalk` and Unity WebGL's robust foundation, we can provide desktop-equivalent quality in browsers.

## References

- [wasm_open_jtalk NPM Package](https://www.npmjs.com/package/wasm_open_jtalk)
- [Unity WebGL: Browser Scripting](https://docs.unity3d.com/Manual/webgl-interactingwithbrowserscripting.html)
- [piper-plus PR #118](https://github.com/ayutaz/piper-plus/pull/118)