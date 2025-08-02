# Unity Tuple Syntax Warning

Unity's NUnit implementation has issues with C# 7.0 tuple syntax, causing Unity Editor to freeze.

## Affected Files:
- ChinesePhase2IntegrationTests.cs
- ChineseTextNormalizerTests.cs  
- ChineseWordSegmenterTests.cs
- IntegratedMultiToneTests.cs
- MultiToneProcessorTests.cs

## Problem Pattern:
```csharp
// This causes Unity Editor freeze:
var testCases = new[] { ("text", "expected") };
foreach (var (text, expected) in testCases) { }
```

## Solution:
Replace with individual test cases:
```csharp
var result1 = TestMethod("text");
Assert.AreEqual("expected", result1);
```

## Status:
- TraditionalChineseConverterTests.cs - FIXED ✓
- TraditionalChineseIntegrationTests.cs - FIXED ✓
- ChineseWordSegmentationIntegrationTests.cs - PARTIALLY FIXED
- Others - PENDING

To run all tests safely, the remaining tuple syntax must be removed.