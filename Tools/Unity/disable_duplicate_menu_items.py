#!/usr/bin/env python3
"""Disable duplicate menu items in existing files"""

import os
import re

def comment_out_menu_item(file_path, menu_path):
    """Comment out a specific MenuItem attribute in a file"""
    
    if not os.path.exists(file_path):
        print(f"File not found: {file_path}")
        return False
    
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Find and comment out the specific MenuItem
    pattern = rf'(\s*)\[MenuItem\("{re.escape(menu_path)}"[^\]]*\)\]'
    
    def replace_func(match):
        indent = match.group(1)
        return f'{indent}// [MenuItem("{menu_path}"...)] // Moved to uPiperMenuItems'
    
    new_content = re.sub(pattern, replace_func, content)
    
    if new_content != content:
        with open(file_path, 'w', encoding='utf-8') as f:
            f.write(new_content)
        print(f"Disabled menu item in {os.path.basename(file_path)}: {menu_path}")
        return True
    
    return False

def main():
    base_path = r"C:\Users\yuta\Desktop\Private\uPiper\Assets\uPiper\Editor"
    
    # List of duplicate menu items to disable
    duplicates = [
        ("CheckCompilation.cs", "uPiper/Debug/Check Compilation"),
        ("CheckDLLArchitecture.cs", "uPiper/Debug/DLL/Check Architecture"),
        ("CheckDLLSearchPath.cs", "uPiper/Debug/DLL/Check Search Path"),
        ("CheckDLLSearchPath.cs", "uPiper/Debug/DLL/Force Reimport"),
        ("Build/AndroidEncodingChecker.cs", "uPiper/Android/Check Encoding Settings"),
        ("Build/AndroidEncodingChecker.cs", "uPiper/Android/Fix Text Asset Encoding"),
        ("Build/AndroidBuildHelper.cs", "uPiper/Android/Setup Android Libraries"),
        ("Build/AndroidBuildHelper.cs", "uPiper/Android/Verify Android Setup"),
        ("DebugONNXModel.cs", "uPiper/Debug/ONNX/Inspect Model"),
        ("DebugONNXModel.cs", "uPiper/Debug/ONNX/Test Simple Inference"),
        ("BuildSettings/PiperBuildProcessor.cs", "uPiper/Build/Configure Build Settings"),
        ("BuildSettings/PiperBuildProcessor.cs", "uPiper/Build/Build All Platforms"),
        ("OpenJTalkStatus.cs", "uPiper/Debug/OpenJTalk/Show Status"),
        ("ToggleOpenJTalk.cs", "uPiper/Debug/OpenJTalk/Toggle Enabled State"),
        ("OpenJTalkLibraryChecker.cs", "uPiper/Debug/OpenJTalk/Check Library"),
        ("Build/AndroidLibraryValidator.cs", "uPiper/Android/Validate Native Libraries"),
        ("Build/AndroidLibraryValidator.cs", "uPiper/Android/Fix Library Import Settings"),
        ("OpenJTalkPhonemizerDemo.cs", "uPiper/Tools/OpenJTalk Phonemizer Test"),
        # These should remain as they are the targets
        # ("IL2CPPBuildSettings.cs", "uPiper/Configure IL2CPP Settings"),
        # ("IL2CPPBuildSettings.cs", "uPiper/Verify IL2CPP Configuration"),
        # ("IL2CPPBenchmarkRunner.cs", "uPiper/IL2CPP Benchmark Runner"),
        # ("GPUInferenceTest.cs", "Window/uPiper/GPU Inference Test"),
        # ("CreateInferenceDemoScene.cs", "uPiper/Scenes/Open Inference Demo Scene"),
        # ("CreateInferenceDemoScene.cs", "uPiper/Scenes/Create Inference Demo Scene"),
        # ("SampleSceneOpener.cs", "uPiper/Samples/Open WebGL Demo Scene"),
        # ("SampleSceneOpener.cs", "uPiper/Samples/Copy All Samples to Assets"),
        # ("SampleSceneOpener.cs", "uPiper/Samples/Add All Scenes to Build Settings"),
    ]
    
    disabled_count = 0
    for relative_path, menu_path in duplicates:
        full_path = os.path.join(base_path, relative_path)
        if comment_out_menu_item(full_path, menu_path):
            disabled_count += 1
    
    print(f"\nDisabled {disabled_count} duplicate menu items")

if __name__ == "__main__":
    main()