# uPiper Basic Example

This sample demonstrates basic usage of the uPiper TTS (Text-to-Speech) system in Unity.

## Setup

1. Import the uPiper package into your Unity project
2. Import this sample from the Package Manager
3. Open the `BasicExample` scene
4. Ensure you have placed ONNX model files in the appropriate location

## Usage

1. Enter text in the input field
2. Select the language from the dropdown
3. Click "Generate" to create speech audio
4. Click "Play" to hear the generated speech

## Requirements

- Unity 6000.0.35f1 or later
- Unity Sentis 2.1.2 or later
- ONNX voice model files
- Platform-specific phonemization libraries (for full functionality)

## Model Files

Place your ONNX model files in:
- `StreamingAssets/uPiper/Models/` (recommended)
- Or specify a custom path in the TTS Example component

## Supported Languages

- Japanese (ja) - Default
- English (en) - Requires appropriate model

Additional languages can be supported by adding corresponding ONNX models and phonemization support.