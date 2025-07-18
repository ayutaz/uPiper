# uPiper TTS Samples

This folder contains sample implementations demonstrating how to use uPiper TTS in Unity.

## Sample Scene

### TTSSample.unity
A complete demonstration of text-to-speech functionality including:
- Text input field for entering text to synthesize
- Language selection dropdown
- Generate button to create audio
- Play/Stop controls for audio playback
- Progress indicator during generation
- Performance metrics display
- Status messages

### Setup Instructions

1. Open the `TTSSample.unity` scene
2. Ensure you have an AudioSource component in the scene
3. The TTSSampleController script should be attached to a GameObject
4. Link all UI elements in the inspector:
   - Text Input Field
   - Generate Button
   - Play Button
   - Stop Button
   - Language Dropdown
   - Progress Slider
   - Status Text
   - Performance Text
   - Audio Source

### Features Demonstrated

- **Text-to-Speech Generation**: Convert text to audio using various languages
- **Async/Await Pattern**: Non-blocking audio generation
- **Progress Tracking**: Real-time progress updates during generation
- **Performance Metrics**: Generation time and real-time factor display
- **Cache System**: Demonstrates phoneme caching for improved performance
- **Error Handling**: Graceful error handling with user feedback
- **Multi-language Support**: Switch between different languages

### Code Examples

#### Basic Usage
```csharp
// Create configuration
var config = new PiperConfig
{
    DefaultLanguage = "ja",
    SampleRate = 22050,
    EnablePhonemeCache = true
};

// Create TTS instance
var piperTTS = new PiperTTS(config);

// Initialize
await piperTTS.InitializeAsync();

// Generate audio
var audioClip = await piperTTS.GenerateAudioAsync("こんにちは");

// Play audio
audioSource.clip = audioClip;
audioSource.Play();
```

#### Streaming Audio
```csharp
await foreach (var chunk in piperTTS.StreamAudioAsync("長いテキスト"))
{
    // Process audio chunks as they arrive
    ProcessAudioChunk(chunk);
}
```

#### Event Handling
```csharp
piperTTS.OnProcessingProgress += (progress) => 
{
    Debug.Log($"Progress: {progress * 100}%");
};

piperTTS.OnError += (error) => 
{
    Debug.LogError($"TTS Error: {error.Message}");
};
```

## Performance Considerations

- **Caching**: Enable phoneme caching for frequently used text
- **Threading**: Use multi-threaded inference for better performance
- **Memory**: Monitor cache size to prevent excessive memory usage
- **Model Loading**: ONNX models are loaded on demand from StreamingAssets

## Troubleshooting

1. **No Audio Generated**: Check that the ONNX model file exists in StreamingAssets/uPiper/Models
2. **Initialization Failed**: Verify that all required native libraries are present
3. **Poor Performance**: Try enabling multi-threaded inference and adjusting worker thread count
4. **Memory Issues**: Reduce cache size or disable caching entirely

## Additional Resources

- [uPiper Documentation](../../README.md)
- [Unity Audio Documentation](https://docs.unity3d.com/Manual/Audio.html)
- [Piper TTS Project](https://github.com/rhasspy/piper)