# uPiper Basic TTS Demo

This sample demonstrates text-to-speech functionality using uPiper with Japanese and English text support.

## Getting Started

1. Import this sample through Unity Package Manager
2. Use the menu: `uPiper/Samples/Create Basic TTS Demo Scene` to create a new demo scene
3. Select a model from the dropdown (ja_JP for Japanese, test_voice for English)
4. Choose a preset phrase or enter custom text
5. Click "Generate" to synthesize audio
6. The generated audio will play automatically

## Features

- Multi-language support (Japanese/English)
- OpenJTalk integration for accurate Japanese phonemization
- Preset phrases for quick testing
- Real-time phoneme display
- Model selection dropdown
- Status display for feedback

## Requirements

- Unity 6000.0.55f1 or later
- Unity AI Inference Engine 2.2.x
- Japanese ONNX model (ja_JP-test-medium.onnx)
- OpenJTalk native libraries (included)

## Architecture

The demo uses the complete TTS pipeline:
1. Text Input → Phonemization (OpenJTalk/Simple)
2. Phoneme Encoding → PUA character mapping
3. Neural Synthesis → VITS model inference
4. Audio Output → Unity AudioSource

## Scripts

- `BasicTTSDemo.cs` - Main demo controller for the sample
- `CreateBasicTTSDemoScene.cs` - Editor script to create the demo scene
- Located in the scene on the `BasicTTSDemo` GameObject