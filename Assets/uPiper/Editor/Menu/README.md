# uPiper Menu System

## Overview

This directory contains the centralized menu system for uPiper. All menu items are organized under a single `uPiper` top-level menu for better organization and accessibility.

## Menu Structure

```
uPiper/
├── Demo/
│   ├── Open Inference Demo Scene
│   ├── Create Inference Demo Scene
│   ├── Open WebGL Demo Scene
│   ├── Copy All Samples to Assets
│   └── Add All Scenes to Build Settings
├── Build/
│   ├── Configure Build Settings
│   ├── Build All Platforms
│   ├── Configure IL2CPP Settings
│   └── Verify IL2CPP Configuration
├── Tools/
│   ├── OpenJTalk Phonemizer Test
│   ├── GPU Inference Test
│   └── IL2CPP Benchmark Runner
├── Debug/
│   ├── Check Compilation
│   ├── OpenJTalk/
│   │   ├── Show Status
│   │   ├── Toggle Enabled State
│   │   └── Check Library
│   ├── DLL/
│   │   ├── Check Search Path
│   │   ├── Check Architecture
│   │   └── Force Reimport
│   └── ONNX/
│       ├── Inspect Model
│       └── Test Simple Inference
├── Android/
│   ├── Setup Android Libraries
│   ├── Verify Android Setup
│   ├── Validate Native Libraries
│   ├── Fix Library Import Settings
│   ├── Check Encoding Settings
│   └── Fix Text Asset Encoding
├── Settings/
│   └── (Reserved for future use)
└── Help/
    ├── Documentation
    ├── Report Issue
    └── About uPiper
```

## Implementation

The menu system is implemented in `uPiperMenuItems.cs` which:

1. Defines all menu paths and priorities
2. Wraps existing functionality by calling the original menu items
3. Provides a consistent structure and organization

## Menu Priorities

Menu items are grouped by priority ranges:
- 100-199: Demo & Samples
- 200-299: Build
- 300-399: Tools
- 400-499: Debug
- 500-599: Android
- 600-699: Settings
- 700-799: Help

## Migration from Old Structure

The old menu items are preserved for backward compatibility. They are called internally by the new menu system using `EditorApplication.ExecuteMenuItem()`.

### Old Menu Locations:
- `Window/uPiper/` - Previously contained GPU Inference Test
- Various scattered `uPiper/` items without consistent organization

### Benefits of New Structure:
1. **Organization**: All items under one top-level menu
2. **Discoverability**: Logical grouping makes features easier to find
3. **Consistency**: Unified naming and structure
4. **Extensibility**: Easy to add new menu items in appropriate sections

## Adding New Menu Items

To add a new menu item:

1. Choose the appropriate section (Demo, Build, Tools, Debug, etc.)
2. Add a new MenuItem attribute with the correct path and priority
3. Implement the functionality or call existing methods
4. Follow the naming convention: `SECTION_[CATEGORY] + "Item Name"`

Example:
```csharp
[MenuItem(SECTION_TOOLS + "New Tool", false, PRIORITY_TOOLS + 30)]
private static void NewTool()
{
    // Implementation or ExecuteMenuItem() call
}
```

## Notes

- The system maintains backward compatibility by keeping original menu items
- Menu separators appear between different priority groups
- Submenu items (e.g., Debug/OpenJTalk/) group related functionality
- The Help section includes quick access to documentation and support