# uPiper Menu System

## Overview

This directory contains the centralized menu system for uPiper. All menu items are organized under a single `uPiper` top-level menu for better organization and accessibility.

## Menu Structure

```
uPiper/
├── Setup/
│   ├── Install from Samples
│   └── Check Setup Status
├── Build/
│   ├── Configure Build Settings
│   ├── Build All Platforms
│   ├── Configure IL2CPP Settings
│   └── Verify IL2CPP Configuration
├── Package/
│   ├── Export Unity Package (.unitypackage)
│   ├── Export UPM Package (.tgz)
│   ├── Export Unity Package (No Dependencies)
│   ├── Export Both Formats
│   └── Open Export Directory
├── Tools/
│   ├── GPU Inference Test
│   ├── IL2CPP Benchmark Runner
│   └── Check DLL Architecture
├── Dictionary Manager
├── Development/
│   ├── Extract Dictionary from Zip
│   └── Prepare WebGL for GitHub Pages
├── Documentation
└── Report Issue
```

## Implementation

The menu system is implemented as follows:

1. `uPiperMenuStructure.cs` defines the shared priority constants (`PRIORITY_DEMO`, `PRIORITY_BUILD`, `PRIORITY_TOOLS`, `PRIORITY_DEBUG`, `PRIORITY_ANDROID`, `PRIORITY_HELP`) and the Help menu items (Documentation, Report Issue).
2. Each individual menu item is declared via `[MenuItem("uPiper/...")]` attributes in its own Editor script (e.g. `DictionaryManagerWindow.cs`, `GPUInferenceTest.cs`, `IL2CPPBuildSettings.cs`, `PackageExporter.cs`), referencing the shared priority constants where appropriate.

> Note: `uPiperMenuItems.cs` is intentionally empty/disabled. Menu items are no longer centralized there; they live in their respective Editor scripts.

## Menu Priorities

Menu items are grouped by priority ranges (constants defined in `uPiperMenuStructure.cs`):
- 100-199: Demo & Samples (`PRIORITY_DEMO`)
- 200-299: Build (`PRIORITY_BUILD`)
- 300-399: Tools (`PRIORITY_TOOLS`)
- 400-499: Debug (`PRIORITY_DEBUG`)
- 500-599: Android (`PRIORITY_ANDROID`)
- 600-699: Help (`PRIORITY_HELP`)

## Migration from Old Structure

Scattered menu items were consolidated under the single `uPiper/` top-level menu. Each item is now declared directly in its own Editor script with a `[MenuItem("uPiper/...")]` attribute, rather than being wrapped or re-routed through a central dispatcher.

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
2. Add a `[MenuItem]` attribute in the relevant Editor script with the full `"uPiper/Section/Item Name"` path
3. Set the priority by referencing the matching constant from `uPiperMenuStructure` (with an optional offset)
4. Implement the functionality directly in the method

Example:
```csharp
[MenuItem("uPiper/Tools/New Tool", false, uPiperMenuStructure.PRIORITY_TOOLS + 30)]
private static void NewTool()
{
    // Implementation
}
```

## Notes

- Visual menu separators are not used; gaps in priority values group items instead (Unity menu separators are unreliable with caching, see `uPiperMenuStructure.cs`)
- Submenu items (e.g., `uPiper/Tools/...`, `uPiper/Build/...`) group related functionality
- The Help section (`uPiper/Documentation`, `uPiper/Report Issue`) provides quick access to documentation and issue reporting