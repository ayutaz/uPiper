# uPiper Basic TTS Demo

This sample demonstrates text-to-speech functionality using uPiper with multilingual text support (Japanese, English, Chinese, Spanish, French, Portuguese).

## Getting Started

1. Import this sample through Unity Package Manager
2. Open the `BasicTTSDemo.unity` scene
3. Press Play to run the demo
4. The multilingual model supports 6 languages (ja/en/zh/es/fr/pt)
5. Choose a preset phrase or enter custom text
6. Click "Generate Speech" to synthesize audio
7. The generated audio will play automatically

## Features

- Multi-language support (6 languages: ja/en/zh/es/fr/pt)
- dot-net-g2p integration for accurate Japanese phonemization (pure C#)
- Preset phrases for quick testing
- Real-time phoneme display
- Model selection dropdown
- Status display for feedback

## Requirements

- Unity 6000.0.58f2 or later
- Unity AI Inference Engine 2.2.x
- Multilingual ONNX model (multilingual-test-medium.onnx)
- dot-net-g2p dictionary (included)

## Included Assets

このサンプルには以下のアセットが含まれています：

- **日本語フォント (NotoSansJP-Regular)**: 日本語テキスト表示用
  - SIL Open Font License v1.1
  - TextMeshPro用SDFアセット付属
- **英語フォント (LiberationSans)**: 英語テキスト表示用
  - SIL Open Font License v1.1
  - TextMeshPro用SDFアセット付属

## Architecture

The demo uses the complete TTS pipeline:
1. Text Input → Phonemization (dot-net-g2p/Flite LTS)
2. Phoneme Encoding → PUA character mapping
3. Neural Synthesis → VITS model inference
4. Audio Output → Unity AudioSource

## Scripts

- Uses `InferenceEngineDemo.cs` from the main package
- Scene is a copy of the main InferenceEngineDemo scene
- All functionality is identical to the main demo