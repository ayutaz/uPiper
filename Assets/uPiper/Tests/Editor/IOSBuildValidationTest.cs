#if UNITY_EDITOR
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace uPiper.Tests.Editor
{
    /// <summary>
    /// Editor tests to validate iOS build configuration and assets.
    /// </summary>
    [TestFixture]
    [Category("Editor")]
    [Category("iOS")]
    public class IOSBuildValidationTest
    {
        private const string IOS_PLUGIN_PATH = "Assets/uPiper/Plugins/iOS";
        private const string NATIVE_LIB_NAME = "libopenjtalk_wrapper.a";
        private const string STREAMING_ASSETS_PATH = "Assets/StreamingAssets/uPiper/OpenJTalk";

        [Test]
        public void IOSNativeLibrary_Exists()
        {
            var libraryPath = Path.Combine(IOS_PLUGIN_PATH, NATIVE_LIB_NAME);
            Assert.IsTrue(File.Exists(libraryPath), 
                $"iOS native library not found at: {libraryPath}");
            
            // Check file size (should be reasonable)
            var fileInfo = new FileInfo(libraryPath);
            Assert.Greater(fileInfo.Length, 100 * 1024, // At least 100KB
                $"iOS library seems too small: {fileInfo.Length} bytes");
            Assert.Less(fileInfo.Length, 50 * 1024 * 1024, // Less than 50MB
                $"iOS library seems too large: {fileInfo.Length} bytes");
        }

        [Test]
        public void IOSNativeLibrary_HasCorrectMetaFile()
        {
            var metaPath = Path.Combine(IOS_PLUGIN_PATH, NATIVE_LIB_NAME + ".meta");
            Assert.IsTrue(File.Exists(metaPath), 
                $"iOS native library meta file not found at: {metaPath}");
            
            // Read and validate meta file
            var metaContent = File.ReadAllText(metaPath);
            Assert.IsTrue(metaContent.Contains("iOS"), 
                "Meta file should have iOS platform settings");
            Assert.IsTrue(metaContent.Contains("enabled: 1"), 
                "iOS platform should be enabled in meta file");
            Assert.IsTrue(metaContent.Contains("CPU: ARM64"), 
                "Should target ARM64 architecture");
        }

        [Test]
        public void IOSPluginImporter_ConfiguredCorrectly()
        {
            var libraryPath = Path.Combine(IOS_PLUGIN_PATH, NATIVE_LIB_NAME);
            var importer = AssetImporter.GetAtPath(libraryPath) as PluginImporter;
            
            Assert.NotNull(importer, "Could not get PluginImporter for iOS library");
            
            // Verify iOS settings
            Assert.IsTrue(importer.GetCompatibleWithPlatform(BuildTarget.iOS), 
                "Library should be compatible with iOS platform");
            
            var iosSettings = importer.GetPlatformData(BuildTarget.iOS, "CPU");
            Assert.AreEqual("ARM64", iosSettings, "Should target ARM64 CPU");
            
            // Verify other platforms are disabled
            Assert.IsFalse(importer.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows64), 
                "Should not be compatible with Windows");
            Assert.IsFalse(importer.GetCompatibleWithPlatform(BuildTarget.Android), 
                "Should not be compatible with Android");
        }

        [Test]
        public void DictionaryFiles_InStreamingAssets()
        {
            var dictionaryPath = Path.Combine(STREAMING_ASSETS_PATH, "naist_jdic");
            
            // Check if dictionary exists (either naist_jdic or dictionary folder)
            var exists = Directory.Exists(dictionaryPath) || 
                        Directory.Exists(Path.Combine(STREAMING_ASSETS_PATH, "dictionary"));
            
            Assert.IsTrue(exists, 
                $"Dictionary files not found in StreamingAssets at: {dictionaryPath}");
            
            if (Directory.Exists(dictionaryPath))
            {
                // Dictionary files are in open_jtalk_dic_utf_8-1.11 subdirectory
                var dictSubPath = Path.Combine(dictionaryPath, "open_jtalk_dic_utf_8-1.11");

                // Verify essential dictionary files
                var essentialFiles = new[] { "sys.dic", "unk.dic", "matrix.bin", "char.bin" };
                foreach (var file in essentialFiles)
                {
                    var filePath = Path.Combine(dictSubPath, file);
                    Assert.IsTrue(File.Exists(filePath),
                        $"Essential dictionary file missing: {file}");
                }
            }
        }

        [Test]
        public void PlayerSettings_IOSConfiguration()
        {
            // Verify iOS player settings
            var targetOSVersion = PlayerSettings.iOS.targetOSVersionString;
            if (!string.IsNullOrEmpty(targetOSVersion))
            {
                var versionParts = targetOSVersion.Split('.');
                if (versionParts.Length >= 2)
                {
                    var majorVersion = int.Parse(versionParts[0]);
                    Assert.GreaterOrEqual(majorVersion, 11,
                        $"iOS minimum version should be 11.0 or higher, but is: {targetOSVersion}");
                }
            }
            
            // Verify SDK version
            Assert.AreEqual(iOSSdkVersion.DeviceSDK, 
                PlayerSettings.iOS.sdkVersion, 
                "Should use Device SDK (not Simulator SDK)");
        }

        [Test]
        public void BuildScript_Exists()
        {
            var buildScriptPath = Path.Combine(Application.dataPath, 
                "../NativePlugins/OpenJTalk/build_ios.sh");
            
            Assert.IsTrue(File.Exists(buildScriptPath), 
                $"iOS build script not found at: {buildScriptPath}");
            
            // Verify script is executable (has shebang)
            var firstLine = File.ReadLines(buildScriptPath).First();
            Assert.IsTrue(firstLine.StartsWith("#!/bin/bash"), 
                "Build script should start with bash shebang");
        }

        [Test]
        public void CMakeLists_HasIOSSupport()
        {
            var cmakePath = Path.Combine(Application.dataPath, 
                "../NativePlugins/OpenJTalk/CMakeLists.txt");
            
            if (File.Exists(cmakePath))
            {
                var content = File.ReadAllText(cmakePath);
                Assert.IsTrue(content.Contains("IOS"), 
                    "CMakeLists.txt should have iOS platform detection");
                Assert.IsTrue(content.Contains("STATIC") || content.Contains("static"), 
                    "CMakeLists.txt should build static library for iOS");
            }
        }

        [Test]
        public void IOSCodeSupport_InPhonemizer()
        {
            var phonemizerPath = "Assets/uPiper/Runtime/Core/Phonemizers/Implementations/OpenJTalkPhonemizer.cs";
            Assert.IsTrue(File.Exists(phonemizerPath),
                $"OpenJTalkPhonemizer not found at: {phonemizerPath}");

            var content = File.ReadAllText(phonemizerPath);

            // Verify iOS support in phonemizer
            Assert.IsTrue(content.Contains("UNITY_IOS"),
                "Phonemizer should have iOS platform support");
            Assert.IsTrue(content.Contains("__Internal"),
                "Phonemizer should use __Internal for iOS DllImport");

            // iOS platform checking is now in NativeLibraryResolver (shared path resolution)
            var resolverPath = "Assets/uPiper/Runtime/Core/Platform/NativeLibraryResolver.cs";
            Assert.IsTrue(File.Exists(resolverPath),
                $"NativeLibraryResolver not found at: {resolverPath}");

            var resolverContent = File.ReadAllText(resolverPath);
            Assert.IsTrue(resolverContent.Contains("PlatformHelper.IsIOS"),
                "NativeLibraryResolver should check for iOS platform");
        }

        [Test]
        public void TestFiles_ForIOS_Exist()
        {
            var testFiles = new[]
            {
                "Assets/uPiper/Tests/Runtime/Core/Phonemizers/OpenJTalkPhonemizerIOSTest.cs",
                "Assets/uPiper/Tests/Runtime/Core/Platform/IOSPlatformTest.cs",
                "Assets/uPiper/Tests/Runtime/Integration/IOSIntegrationTest.cs"
            };
            
            foreach (var testFile in testFiles)
            {
                Assert.IsTrue(File.Exists(testFile), 
                    $"iOS test file not found: {testFile}");
                
                // Verify test has iOS conditional compilation
                var content = File.ReadAllText(testFile);
                Assert.IsTrue(content.Contains("#if UNITY_IOS") || content.Contains("#if UNITY_EDITOR"), 
                    $"Test file should have iOS conditional compilation: {testFile}");
            }
        }

        [Test]
        public void NoConflictingLibraries()
        {
            // Ensure no other platform libraries are in iOS folder
            var iosFiles = Directory.GetFiles(IOS_PLUGIN_PATH, "*", SearchOption.AllDirectories);
            
            foreach (var file in iosFiles)
            {
                var fileName = Path.GetFileName(file);
                
                // Skip meta files and our library
                if (fileName.EndsWith(".meta") || fileName == NATIVE_LIB_NAME)
                    continue;
                
                // Check for wrong platform libraries
                Assert.IsFalse(fileName.EndsWith(".dll"), 
                    $"Windows DLL found in iOS folder: {fileName}");
                Assert.IsFalse(fileName.EndsWith(".so"), 
                    $"Linux SO found in iOS folder: {fileName}");
                Assert.IsFalse(fileName.EndsWith(".dylib"), 
                    $"macOS dylib found in iOS folder: {fileName}");
            }
        }
    }
}
#endif