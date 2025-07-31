# Phonemizer Backend Directory

This directory contains phonemizer backend implementations for uPiper.

## Important Files

### Core Backends
- **OpenJTalkBackendAdapter.cs** - Adapter for Japanese phonemization using OpenJTalk
- **SimpleLTSPhonemizer.cs** - Simple Letter-to-Sound phonemizer for English

### Supporting Components
- **IPhonemizerBackend.cs** - Interface for all phonemizer backends
- **PhonemizerBackendBase.cs** - Base class for backend implementations

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

## Backend Registration

Backends are loaded dynamically using reflection to avoid compilation order issues:
```csharp
var type = System.Type.GetType("uPiper.Core.Phonemizers.Backend.OpenJTalkBackendAdapter, uPiper.Runtime");
var backend = Activator.CreateInstance(type) as IPhonemizerBackend;
```

## License
All backends use commercial-friendly licenses (MIT/BSD).