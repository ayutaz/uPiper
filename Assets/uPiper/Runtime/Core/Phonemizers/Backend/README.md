# Phonemizer Backend Directory

This directory contains phonemizer backend implementations for uPiper.

## Important Files

### Core Backends
- **FliteLTSPhonemizerBackend.cs** - Letter-to-Sound phonemizer for English using Flite (pure C#)
- **RuleBased/** - Rule-based phonemizer components

### Supporting Components
- **IPhonemizerBackend.cs** - Interface for all phonemizer backends
- **PhonemizerBackendBase.cs** - Base class for backend implementations

## Japanese Phonemization

Japanese phonemization is handled by `DotNetG2PPhonemizer` (located in `../Implementations/`), which uses dot-net-g2p (pure C# MeCab implementation). No native libraries are required.

## Unity Compilation Issues

If Unity doesn't recognize the backend classes:

1. **Reimport Assets**
   - Right-click on this folder in Unity
   - Select "Reimport"

2. **Clear Library Cache**
   - Close Unity Editor
   - Delete the `Library` folder in project root
   - Reopen Unity (it will rebuild)

3. **Check Assembly Definition**
   - Ensure `uPiper.Runtime.asmdef` includes this directory
   - No circular dependencies exist

## License
All backends use commercial-friendly licenses (MIT/BSD).
