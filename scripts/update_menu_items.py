#!/usr/bin/env python3
"""Update existing MenuItem attributes to use centralized menu system"""

import os
import re

# Files to update and their menu mappings
files_to_update = {
    # Build
    "BuildSettings/PiperBuildProcessor.cs": [
        ("uPiper/Build/Configure Build Settings", "// Moved to uPiperMenuItems"),
        ("uPiper/Build/Build All Platforms", "// Moved to uPiperMenuItems")
    ],
    # Scenes/Demo
    "CreateInferenceDemoScene.cs": [
        ("uPiper/Scenes/Open Inference Demo Scene", "// Moved to uPiperMenuItems"),
        ("uPiper/Scenes/Create Inference Demo Scene", "// Moved to uPiperMenuItems")
    ],
    # Debug
    "CheckDLLSearchPath.cs": [
        ("uPiper/Debug/DLL/Check Search Path", "// Moved to uPiperMenuItems"),
        ("uPiper/Debug/DLL/Force Reimport", "// Moved to uPiperMenuItems")
    ],
    "DebugONNXModel.cs": [
        ("uPiper/Debug/ONNX/Inspect Model", "// Moved to uPiperMenuItems"),
        ("uPiper/Debug/ONNX/Test Simple Inference", "// Moved to uPiperMenuItems")
    ],
    "CheckDLLArchitecture.cs": [
        ("uPiper/Debug/DLL/Check Architecture", "// Moved to uPiperMenuItems")
    ],
    "CheckCompilation.cs": [
        ("uPiper/Debug/Check Compilation", "// Moved to uPiperMenuItems")
    ],
    "OpenJTalkStatus.cs": [
        ("uPiper/Debug/OpenJTalk/Show Status", "// Moved to uPiperMenuItems")
    ],
    "ToggleOpenJTalk.cs": [
        ("uPiper/Debug/OpenJTalk/Toggle Enabled State", "// Moved to uPiperMenuItems")
    ],
    "OpenJTalkLibraryChecker.cs": [
        ("uPiper/Debug/OpenJTalk/Check Library", "// Moved to uPiperMenuItems")
    ],
    # Tools
    "IL2CPPBenchmarkRunner.cs": [
        ("uPiper/IL2CPP Benchmark Runner", "// Moved to uPiperMenuItems")
    ],
    "GPUInferenceTest.cs": [
        ("Window/uPiper/GPU Inference Test", "// Moved to uPiperMenuItems")
    ],
    "IL2CPPBuildSettings.cs": [
        ("uPiper/Configure IL2CPP Settings", "// Moved to uPiperMenuItems"),
        ("uPiper/Verify IL2CPP Configuration", "// Moved to uPiperMenuItems")
    ],
    "OpenJTalkPhonemizerDemo.cs": [
        ("uPiper/Tools/OpenJTalk Phonemizer Test", "// Moved to uPiperMenuItems")
    ],
    # Samples
    "SampleSceneOpener.cs": [
        ("uPiper/Samples/Open WebGL Demo Scene", "// Moved to uPiperMenuItems"),
        ("uPiper/Samples/Copy All Samples to Assets", "// Moved to uPiperMenuItems"),
        ("uPiper/Samples/Add All Scenes to Build Settings", "// Moved to uPiperMenuItems")
    ],
    # Android
    "Build/AndroidBuildHelper.cs": [
        ("uPiper/Android/Setup Android Libraries", "// Moved to uPiperMenuItems"),
        ("uPiper/Android/Verify Android Setup", "// Moved to uPiperMenuItems")
    ],
    "Build/AndroidEncodingChecker.cs": [
        ("uPiper/Android/Check Encoding Settings", "// Moved to uPiperMenuItems"),
        ("uPiper/Android/Fix Text Asset Encoding", "// Moved to uPiperMenuItems")
    ],
    "Build/AndroidLibraryValidator.cs": [
        ("uPiper/Android/Validate Native Libraries", "// Moved to uPiperMenuItems"),
        ("uPiper/Android/Fix Library Import Settings", "// Moved to uPiperMenuItems")
    ]
}

def update_file(filepath, replacements):
    """Update MenuItem attributes in a file"""
    
    if not os.path.exists(filepath):
        print(f"File not found: {filepath}")
        return False
    
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()
    
    original_content = content
    
    for menu_path, comment in replacements:
        # Find and comment out MenuItem attributes
        pattern = rf'\[MenuItem\("{re.escape(menu_path)}".*?\)\]'
        replacement = f'// {comment}\n        // [MenuItem("{menu_path}")]'
        content = re.sub(pattern, replacement, content)
    
    if content != original_content:
        # Make methods public static instead of private static
        content = re.sub(r'private static void (\w+)\(', r'public static void \1(', content)
        
        with open(filepath, 'w', encoding='utf-8') as f:
            f.write(content)
        
        print(f"Updated: {filepath}")
        return True
    
    return False

def main():
    base_path = r"C:\Users\yuta\Desktop\Private\uPiper\Assets\uPiper\Editor"
    
    updated_count = 0
    for relative_path, replacements in files_to_update.items():
        full_path = os.path.join(base_path, relative_path)
        if update_file(full_path, replacements):
            updated_count += 1
    
    print(f"\nUpdated {updated_count} files")

if __name__ == "__main__":
    main()