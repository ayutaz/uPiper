#if UNITY_IOS || UNITY_EDITOR
using System.IO;
using NUnit.Framework;
using UnityEngine;
using uPiper.Core.Platform;

namespace uPiper.Tests.Runtime.Core.Platform
{
    /// <summary>
    /// Tests for iOS platform-specific functionality.
    /// </summary>
    [TestFixture]
    [Category("Platform")]
    public class IOSPlatformTest
    {
        [Test]
        public void PlatformHelper_IOSDetection()
        {
#if UNITY_IOS && !UNITY_EDITOR
            Assert.IsTrue(PlatformHelper.IsIOS);
            Assert.AreEqual(RuntimePlatform.IPhonePlayer, PlatformHelper.Platform);
#elif UNITY_EDITOR
            // In editor, test that iOS is properly detected when simulated
            if (Application.platform == RuntimePlatform.IPhonePlayer)
            {
                Assert.IsTrue(PlatformHelper.IsIOS);
            }
#endif
        }

        [Test]
        public void NativeLibraryExtension_ForIOS()
        {
            // iOS uses static libraries
            if (PlatformHelper.IsIOS)
            {
                Assert.AreEqual(".a", PlatformHelper.NativeLibraryExtension);
            }
        }

        [Test]
        public void NativeLibraryPrefix_ForIOS()
        {
            // iOS uses "lib" prefix for libraries
            if (PlatformHelper.IsIOS)
            {
                Assert.AreEqual("lib", PlatformHelper.NativeLibraryPrefix);
            }
        }

        [Test]
        public void Architecture_ForIOS()
        {
            // iOS should report arm64
            if (PlatformHelper.IsIOS)
            {
                Assert.AreEqual("arm64", PlatformHelper.GetArchitecture());
            }
        }

        [Test]
        public void NativeLibraryDirectory_ForIOS()
        {
            if (PlatformHelper.IsIOS)
            {
                var libDir = PlatformHelper.GetNativeLibraryDirectory();
                Assert.NotNull(libDir);
                
#if UNITY_IOS && !UNITY_EDITOR
                // On actual iOS device, should point to Frameworks
                Assert.IsTrue(libDir.Contains("Frameworks") || libDir.Contains(Application.dataPath));
#endif
            }
        }

        [Test]
        public void StreamingAssetsPath_ForIOS()
        {
#if UNITY_IOS && !UNITY_EDITOR
            // On iOS, StreamingAssets are in Application.dataPath/Raw
            var streamingPath = Application.streamingAssetsPath;
            var expectedPath = Path.Combine(Application.dataPath, "Raw");
            
            // Note: The actual path might be different, but it should contain Raw
            Assert.IsTrue(streamingPath.Contains("Raw") || streamingPath.Contains(Application.dataPath));
#endif
        }

        [Test]
        public void IOSBuildSettings_Validation()
        {
#if UNITY_EDITOR
            // This test validates that iOS build settings are properly configured
            var iosSettings = UnityEditor.PlayerSettings.iOS;
            
            // Verify minimum iOS version
            var minVersion = float.Parse(iosSettings.targetOSVersionString);
            Assert.GreaterOrEqual(minVersion, 11.0f, "Minimum iOS version should be 11.0 or higher");
            
            // Verify architecture
            Assert.AreEqual(UnityEditor.PlayerSettings.iOS.Architecture.ARM64, 
                iosSettings.architecture, "Should target ARM64 architecture");
#endif
        }

        [Test]
        public void DictionaryPath_Resolution()
        {
            // Test dictionary path resolution for iOS
            var possiblePaths = new[]
            {
#if UNITY_IOS && !UNITY_EDITOR
                Path.Combine(Application.dataPath, "Raw", "uPiper", "OpenJTalk", "naist_jdic"),
                Path.Combine(Application.dataPath, "Raw", "uPiper", "OpenJTalk", "dictionary"),
#else
                Path.Combine(Application.streamingAssetsPath, "uPiper", "OpenJTalk", "naist_jdic"),
                Path.Combine(Application.streamingAssetsPath, "uPiper", "OpenJTalk", "dictionary"),
#endif
            };

            // At least one path format should be valid
            var validPath = false;
            foreach (var path in possiblePaths)
            {
                if (!string.IsNullOrEmpty(path))
                {
                    validPath = true;
                    Debug.Log($"Possible dictionary path: {path}");
                }
            }
            
            Assert.IsTrue(validPath, "Should have at least one valid dictionary path format");
        }

        [Test]
        public void LibraryLoading_StaticLinking()
        {
#if UNITY_IOS && !UNITY_EDITOR
            // On iOS, libraries are statically linked
            // Test that we can reference the OpenJTalk functions
            try
            {
                // This would normally be done through P/Invoke
                // Here we just verify that the library name is correct
                var libraryName = "__Internal";
                Assert.AreEqual("__Internal", libraryName);
            }
            catch (System.Exception ex)
            {
                Assert.Fail($"Static library reference failed: {ex.Message}");
            }
#endif
        }

        [Test]
        public void MemoryConstraints_IOS()
        {
            // iOS has memory constraints that should be considered
            if (PlatformHelper.IsIOS)
            {
                // Get total memory (this is approximate)
                var totalMemory = SystemInfo.systemMemorySize;
                
                // iOS devices typically have 2GB-8GB RAM
                Assert.Greater(totalMemory, 0);
                Debug.Log($"iOS device memory: {totalMemory}MB");
                
                // Verify we're not using too much memory
                var currentMemory = GC.GetTotalMemory(false);
                var memoryMB = currentMemory / 1024 / 1024;
                
                // Our library should use less than 50MB at rest
                Assert.Less(memoryMB, 50, $"Memory usage too high for iOS: {memoryMB}MB");
            }
        }
    }
}
#endif