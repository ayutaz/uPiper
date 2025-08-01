# iOS Demo Sample

This sample demonstrates uPiper functionality on iOS devices.

## Features

- Dictionary access verification
- OpenJTalk phonemizer testing
- Complete TTS pipeline testing
- Performance monitoring
- Memory usage tracking

## Usage

1. Import the iOS Demo sample from Package Manager
2. Open the `IOSTestScene` in Unity
3. Configure iOS build settings:
   - Minimum iOS Version: 11.0
   - Architecture: ARM64
   - Graphics API: Metal (with CPU fallback)
4. Build and deploy to iOS device

## Test Functions

### Dictionary Test
- Verifies OpenJTalk dictionary files are accessible
- Checks file paths and sizes
- Lists available dictionary files

### Phonemizer Test
- Tests Japanese text phonemization
- Displays phoneme output
- Measures processing time
- Shows cache statistics

### TTS Test
- Loads ONNX model
- Generates audio from text
- Plays generated audio
- Displays audio properties

## Requirements

- iOS 11.0 or later
- ARM64 device (iPhone 6s or newer)
- At least 200MB free storage
- Japanese language support

## Troubleshooting

### Dictionary Not Found
- Ensure StreamingAssets are properly included in build
- Check that dictionary path uses Application.dataPath + "/Raw"

### Phonemizer Fails
- Verify native library is included (libopenjtalk_wrapper.a)
- Check console logs for detailed error messages

### Audio Generation Fails
- Ensure model file exists in StreamingAssets
- Check available memory
- Try smaller model if memory is limited

## Performance Notes

- First phonemization may be slower due to dictionary loading
- Subsequent calls use cache for better performance
- GPU inference falls back to CPU on iOS automatically