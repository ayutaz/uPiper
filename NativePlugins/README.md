# Native Plugins Source Code

This directory contains the source code for native plugins used by uPiper.
These are kept outside of the Unity Assets folder to avoid:
- Unnecessary Unity import processing
- Excessive meta file generation  
- Large build artifacts being included in Unity builds

## OpenJTalk

Japanese text-to-phoneme conversion library.

> **Note (legacy):** The native OpenJTalk phonemizer is **no longer used** by the current
> architecture. Japanese G2P is now handled by the pure C# `dot-net-g2p` package
> (`com.dotnetg2p.mecab`, see `Packages/manifest.json`), so no native `openjtalk_wrapper`
> binary is built or referenced by the Unity build. The build steps below are retained for
> historical reference. The **naist_jdic dictionary** (see "Dictionary Files" below) is still
> used at runtime by `dot-net-g2p` (MeCab) and remains current.

### Building (legacy / unused)

```bash
cd OpenJTalk
./build.sh     # macOS/Linux
# or
./build.bat    # Windows
```

The built library should be copied to:
- macOS: `Assets/uPiper/Plugins/macOS/libopenjtalk_wrapper.dylib`
- Windows: `Assets/uPiper/Plugins/Windows/openjtalk_wrapper.dll`
- Linux: `Assets/uPiper/Plugins/Linux/libopenjtalk_wrapper.so`

### Dictionary Files

The NAIST Japanese dictionary files are stored in:
`Assets/StreamingAssets/uPiper/OpenJTalk/naist_jdic/`

This location allows the dictionary to be accessed at runtime on all platforms.