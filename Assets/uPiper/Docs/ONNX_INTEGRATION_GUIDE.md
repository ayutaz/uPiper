# ONNX Model Integration Guide for Unity.InferenceEngine

## Overview
Unity.InferenceEngine (formerly Sentis) requires specific model formats to work properly. This guide explains how to integrate ONNX models with uPiper.

## Model Format Requirements

### Unity.InferenceEngine 2.2.1
- Unity.InferenceEngine expects `.sentis` format models
- `.onnx` files need to be imported into Unity and converted
- The conversion happens automatically when you import an ONNX file into Unity

### ONNX to Sentis Conversion Process

1. **Import ONNX Model into Unity**
   - Place your `.onnx` file in the Unity project (e.g., `Assets/Models/`)
   - Unity will automatically create a `.sentis` asset

2. **Use ModelAsset in Code**
   ```csharp
   // Instead of loading from file path:
   // ModelLoader.Load("path/to/model.onnx")
   
   // Use ModelAsset reference:
   public ModelAsset myModel; // Assign in Inspector
   ModelLoader.Load(myModel);
   ```

3. **Alternative: Runtime Loading**
   - For runtime loading, the ONNX file must be pre-converted to `.sentis`
   - Use Unity's Model Importer settings to configure the conversion

## Current Implementation Status

### Working Features
- ✅ ModelLoader supports both file paths and ModelAssets
- ✅ Mock mode for testing without actual models
- ✅ Voice-specific model loading
- ✅ Audio caching system

### Limitations
- ⚠️ Direct ONNX file loading at runtime is not supported
- ⚠️ Models must be imported into Unity project first
- ⚠️ WebGL platform requires special handling

## Integration Steps

### 1. Prepare Your ONNX Model
```bash
# Ensure your Piper ONNX model is compatible
# Typical Piper model structure:
# - Input: phoneme_ids (int32[1, ?])
# - Output: audio (float32[1, ?])
```

### 2. Import into Unity
1. Copy your `.onnx` file to `Assets/StreamingAssets/uPiper/Models/`
2. Unity will create a corresponding `.sentis` asset
3. Configure import settings if needed

### 3. Update Voice Configuration
```csharp
var voice = new PiperVoiceConfig
{
    VoiceId = "ja-JP-test",
    Language = "ja",
    ModelPath = "path/to/model.sentis", // Use .sentis extension
    // Or use ModelAsset reference
    ModelAsset = myModelAsset
};
```

### 4. Test the Integration
```csharp
// Initialize PiperTTS
var config = new PiperConfig { DefaultLanguage = "ja" };
var piperTTS = new PiperTTS(config);
await piperTTS.InitializeAsync();

// Load voice
await piperTTS.LoadVoiceAsync(voice);

// Generate audio
var audioClip = await piperTTS.GenerateAudioAsync("こんにちは");
```

## Troubleshooting

### "Failed to load serialized .sentis model" Error
- Ensure the ONNX file is imported into Unity first
- Check that the model format is compatible with Unity.InferenceEngine
- Verify the model path points to the `.sentis` asset, not the `.onnx` file

### Mock Mode Testing
Set environment variable to bypass model loading:
```csharp
System.Environment.SetEnvironmentVariable("PIPER_MOCK_MODE", "1");
```

## Next Steps
1. Import actual Piper ONNX models into Unity
2. Configure voice settings for each model
3. Test audio generation with real models
4. Optimize performance and memory usage