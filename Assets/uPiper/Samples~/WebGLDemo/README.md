# uPiper WebGL Demo

This demo showcases the uPiper text-to-speech system running in WebGL with multilingual support.

## Features

- **Multi-language Support**: Switch between Japanese, English, and Chinese
- **Real-time Generation**: Generate speech directly in the browser
- **Speed Control**: Adjust playback speed from 0.5x to 2.0x
- **WebAssembly Integration**: Uses wasm_open_jtalk for Japanese and eSpeak-ng for other languages

## Setup

1. Import this sample through the Package Manager
2. Open the `WebGLDemoScene`
3. Configure voice models in the WebGLTTSDemo component:
   - Add Japanese voice config (ja_JP-test-medium)
   - Add English voice config (en_US-amy-medium)
   - Add Chinese voice config (zh_CN-huayan-medium)

## Building for WebGL

1. Switch platform to WebGL in Build Settings
2. Select the uPiper WebGL template in Player Settings
3. Set memory size to at least 512MB (1GB recommended)
4. Build and deploy to a web server with proper CORS headers

## Browser Requirements

- Chrome 90+ (recommended)
- Firefox 89+
- Safari 15+ (limited WebAssembly SIMD support)
- Edge 90+

## Performance Tips

- Use GPUPixel backend (auto-selected for WebGL)
- Enable phoneme caching to reduce computation
- Consider using shorter text inputs for better responsiveness
- Monitor browser memory usage, especially with large models

## Known Limitations

- Initial load time may be longer due to WebAssembly modules
- Some browsers may have memory limitations
- Audio playback may have slight delays on first generation