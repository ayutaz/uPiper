# uPiper Tests

This directory contains unit and integration tests for the uPiper TTS system.

## Test Structure

- **Runtime/** - Unit tests for core functionality
  - `PiperConfigTests.cs` - Configuration validation tests
  - `BasePhonemizer Tests.cs` - Phonemizer base class tests
  - **Integration/** - Integration tests with actual models
    - `PiperIntegrationTests.cs` - End-to-end TTS tests

- **Editor/** - Editor utilities for test execution
  - `TestRunnerSetup.cs` - Menu items and test utilities

## Running Tests

### Using Unity Test Runner

1. Open Unity Test Runner: `Window > General > Test Runner`
2. Switch to "PlayMode" tab for integration tests
3. Click "Run All" or select specific tests

### Using Menu Items

- `uPiper > Tests > Run All Tests` - Runs all unit and integration tests
- `uPiper > Tests > Run Integration Tests` - Runs only integration tests
- `uPiper > Tests > Validate Test Models` - Checks if test models are present

## Test Models

Integration tests require the following models in `Assets/StreamingAssets/uPiper/Models/`:

- `ja_JP-test-medium.onnx` - Japanese TTS model (63MB)
- `ja_JP-test-medium.onnx.json` - Japanese model configuration
- `test_voice.onnx` - English TTS model (27MB)
- `test_voice.onnx.json` - English model configuration

These models are copied from the Piper test directory.

## Test Requirements

- Unity 6000.0.35f1 or later
- Unity Test Framework package
- Unity Sentis 2.1.3 or later
- Test models in StreamingAssets

## Writing New Tests

### Unit Tests
```csharp
[Test]
public void MyComponent_WhenCondition_ShouldExpectedBehavior()
{
    // Arrange
    var component = new MyComponent();
    
    // Act
    var result = component.DoSomething();
    
    // Assert
    Assert.AreEqual(expected, result);
}
```

### Integration Tests
```csharp
[UnityTest]
public IEnumerator MyIntegration_ShouldWork()
{
    // Setup
    var tts = new PiperTTS();
    
    // Async operations
    var task = tts.InitializeAsync(config);
    while (!task.IsCompleted) yield return null;
    
    // Assertions
    Assert.IsTrue(tts.IsInitialized);
}
```

## Continuous Integration

These tests can be run in batch mode for CI/CD:

```bash
Unity -batchmode -projectPath . -runTests -testPlatform PlayMode -testFilter "uPiper.Tests"
```