# uPiper Setup Guide

## Installation Methods

uPiper can be installed in two ways:

### 1. Unity Package Manager (Recommended)

Add uPiper to your project via Package Manager:

1. Open **Window > Package Manager**
2. Click the **+** button and select **Add package from git URL**
3. Enter: `https://github.com/ayutaz/uPiper.git#main`
4. Click **Add**

### 2. Unity Package (.unitypackage)

1. Download `uPiper-vX.X.X.unitypackage` from [GitHub Releases](https://github.com/ayutaz/uPiper/releases)
2. In Unity Editor, go to **Assets > Import Package > Custom Package**
3. Select the downloaded `.unitypackage` file and click **Import**
4. Add the DotNetG2P dependency packages to your `Packages/manifest.json`

The `.unitypackage` does not include DotNetG2P packages. Add the following entries to the `"dependencies"` section of your `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.dotnetg2p.chinese": "https://github.com/ayutaz/dot-net-g2p.git?path=src/DotNetG2P.Chinese#v1.8.2",
    "com.dotnetg2p.core": "https://github.com/ayutaz/dot-net-g2p.git?path=src/DotNetG2P.Core#v1.8.2",
    "com.dotnetg2p.english": "https://github.com/ayutaz/dot-net-g2p.git?path=src/DotNetG2P.English#v1.8.2",
    "com.dotnetg2p.french": "https://github.com/ayutaz/dot-net-g2p.git?path=src/DotNetG2P.French#v1.8.2",
    "com.dotnetg2p.korean": "https://github.com/ayutaz/dot-net-g2p.git?path=src/DotNetG2P.Korean#v1.8.2",
    "com.dotnetg2p.mecab": "https://github.com/ayutaz/dot-net-g2p.git?path=src/DotNetG2P.MeCab#v1.8.2",
    "com.dotnetg2p.portuguese": "https://github.com/ayutaz/dot-net-g2p.git?path=src/DotNetG2P.Portuguese#v1.8.2",
    "com.dotnetg2p.spanish": "https://github.com/ayutaz/dot-net-g2p.git?path=src/DotNetG2P.Spanish#v1.8.2"
  }
}
```

## Required Data Setup

uPiper requires dictionary data and voice models to function. These are provided as **Samples** in the Package Manager to keep the core package lightweight.

### Step 1: Import Required Samples

After installing uPiper:

1. Open **Window > Package Manager**
2. Select **In Project** from the dropdown
3. Find and select **uPiper**
4. In the package details, locate the **Samples** section
5. Import the following samples:

   - **MeCab Dictionary Data** (103MB)
     - Required for Japanese text-to-speech
     - Contains NAIST Japanese Dictionary (used by dot-net-g2p)
   
   - **CMU Pronouncing Dictionary** (3.5MB)
     - Required for English text-to-speech
     - Contains pronunciation data for English words
   
   - **Voice Models (Optional)** (127MB)
     - High-quality ONNX voice models
     - Includes Japanese and English voices
     - Can be downloaded separately if needed

### Step 2: Install Imported Samples

After importing the samples:

1. Go to **uPiper > Setup > Install from Samples**
2. The setup wizard will automatically copy the imported data to the correct locations
3. Wait for the installation to complete

> **Note**: The previous "Run Initial Setup" menu option has been removed. "Install from Samples" is now the only setup method.

### Step 3: Verify Installation

Check that everything is installed correctly:

1. Go to **uPiper > Setup > Check Setup Status**
2. Ensure all components show "✓ Installed"

## Alternative: Manual Setup

If you prefer to manage data files manually:

### Dictionary Files
Place dictionary files in `Assets/StreamingAssets/uPiper/`:
- MeCab Dictionary: `MeCab/naist_jdic/`
- CMU Dictionary: `Phonemizers/cmudict-0.7b.txt`

### Voice Models
Place ONNX models in `Assets/uPiper/Resources/Models/`:
- `multilingual-test-medium.onnx` and `.onnx.json`

## Important Notes

### Dictionary Requirements

**Full dictionary data is required for uPiper to function properly.** The package does not include fallback dictionaries.

- **Japanese TTS**: Requires MeCab Dictionary Data (NAIST Japanese Dictionary)
- **English TTS**: Requires CMU Pronouncing Dictionary

Without these dictionaries, the phonemizers will throw errors and TTS functionality will not work.

## Development Mode

For developers working on uPiper itself:

1. Define `UPIPER_DEVELOPMENT` in **Project Settings > Player > Scripting Define Symbols**
2. This disables automatic setup prompts
3. Assumes all files are already in the correct locations

## Troubleshooting

### "No Samples Found" Error
- Make sure you've imported the samples from Package Manager first
- Samples should appear in `Assets/Samples/uPiper/`

### Dictionary Not Found
- Run **uPiper > Setup > Install from Samples** after importing
- Check that files exist in `Assets/StreamingAssets/uPiper/`

### Voice Models Not Loading
- Ensure models are in `Assets/uPiper/Resources/Models/`
- Check that both `.onnx` and `.onnx.json` files are present

## Platform-Specific Notes

### WebGL
- StreamingAssets are loaded asynchronously via `UnityWebRequest`
- Custom dictionaries are loaded asynchronously during initialization
- Large model files (100MB+) are automatically split for GitHub Pages deployment
- WebGPU browsers use GPUCompute, WebGL2 browsers use GPUPixel
- [Demo Page](https://ayutaz.github.io/uPiper/)

### Android
- Dictionary files are automatically copied from StreamingAssets
- Ensure adequate device storage for data files

### iOS
- Similar to Android, files are bundled with the app
- No special configuration required

## Next Steps

After setup is complete:
1. Check out the sample scene in the imported **Basic TTS Demo**
2. See the [Architecture Documentation](ARCHITECTURE_en.md) for technical details and usage examples
3. Explore the [GPU Inference Guide](features/gpu/gpu-inference.md) for performance customization