# Native Plugins Source Code

This directory contains the source code for native plugins used by uPiper.
These are kept outside of the Unity Assets folder to avoid:
- Unnecessary Unity import processing
- Excessive meta file generation  
- Large build artifacts being included in Unity builds

## OpenJTalk

Japanese text-to-phoneme conversion library.

### Building

```bash
cd OpenJTalk
mkdir build
cd build
cmake ..
make
```

The built library should be copied to:
- macOS: `Assets/uPiper/Plugins/macOS/libopenjtalk_wrapper.dylib`
- Windows: `Assets/uPiper/Plugins/Windows/openjtalk_wrapper.dll`
- Linux: `Assets/uPiper/Plugins/Linux/libopenjtalk_wrapper.so`

### Dictionary Files

The NAIST Japanese dictionary files are stored in:
`Assets/StreamingAssets/uPiper/OpenJTalk/naist_jdic/`

This location allows the dictionary to be accessed at runtime on all platforms.