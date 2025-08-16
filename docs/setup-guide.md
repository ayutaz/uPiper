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

Download and import the latest release from [GitHub Releases](https://github.com/ayutaz/uPiper/releases)

## Required Data Setup

uPiper requires dictionary data and voice models to function. These are provided as **Samples** in the Package Manager to keep the core package lightweight.

### Step 1: Import Required Samples

After installing uPiper:

1. Open **Window > Package Manager**
2. Select **In Project** from the dropdown
3. Find and select **uPiper**
4. In the package details, locate the **Samples** section
5. Import the following samples:

   - **OpenJTalk Dictionary Data** (103MB)
     - Required for Japanese text-to-speech
     - Contains NAIST Japanese Dictionary
   
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

### Step 3: Verify Installation

Check that everything is installed correctly:

1. Go to **uPiper > Setup > Check Setup Status**
2. Ensure all components show "✓ Installed"

## Alternative: Manual Setup

If you prefer to manage data files manually:

### Dictionary Files
Place dictionary files in `Assets/StreamingAssets/uPiper/`:
- OpenJTalk: `OpenJTalk/naist_jdic/`
- CMU Dictionary: `Phonemizers/cmudict-0.7b.txt`

### Voice Models
Place ONNX models in `Assets/uPiper/Resources/Models/`:
- `ja_JP-test-medium.onnx` and `.onnx.json`
- `en_US-ljspeech-medium.onnx` and `.onnx.json`

## Important Notes

### Dictionary Requirements

**Full dictionary data is required for uPiper to function properly.** The package does not include fallback dictionaries.

- **Japanese TTS**: Requires OpenJTalk Dictionary Data
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
- StreamingAssets access may be limited
- Consider using Resources folder for critical data
- Voice models may need optimization for web deployment

### Android
- Dictionary files are automatically copied from StreamingAssets
- Ensure adequate device storage for data files

### iOS
- Similar to Android, files are bundled with the app
- No special configuration required

## Next Steps

After setup is complete:
1. Check out the sample scene in the imported **Basic TTS Demo**
2. See the [API Documentation](api-reference.md) for usage examples
3. Explore the [Configuration Guide](configuration.md) for customization options