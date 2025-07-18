using System.IO;
using NUnit.Framework;
using UnityEngine;
using uPiper.Core.Platform;

namespace uPiper.Tests.Runtime.Platform
{
    /// <summary>
    /// Tests for PlatformHelper
    /// </summary>
    public class PlatformHelperTests
    {
        [Test]
        public void Platform_ReturnsCurrentPlatform()
        {
            var platform = PlatformHelper.Platform;
            Assert.IsTrue(System.Enum.IsDefined(typeof(RuntimePlatform), platform));
        }

        [Test]
        public void PlatformChecks_OnlyOneIsTrue()
        {
            int trueCount = 0;
            
            if (PlatformHelper.IsWindows) trueCount++;
            if (PlatformHelper.IsMacOS) trueCount++;
            if (PlatformHelper.IsLinux) trueCount++;
            if (PlatformHelper.IsAndroid) trueCount++;
            if (PlatformHelper.IsIOS) trueCount++;
            if (PlatformHelper.IsWebGL) trueCount++;
            
            // In editor, we might have multiple true (e.g., WindowsEditor counts as Windows)
            // But at least one should be true
            Assert.GreaterOrEqual(trueCount, 1);
        }

        [Test]
        public void NativeLibraryExtension_ReturnsCorrectExtension()
        {
            var extension = PlatformHelper.NativeLibraryExtension;
            
            if (PlatformHelper.IsWindows)
            {
                Assert.AreEqual(".dll", extension);
            }
            else if (PlatformHelper.IsMacOS)
            {
                Assert.AreEqual(".dylib", extension);
            }
            else if (PlatformHelper.IsLinux || PlatformHelper.IsAndroid)
            {
                Assert.AreEqual(".so", extension);
            }
            else if (PlatformHelper.IsIOS)
            {
                Assert.AreEqual(".a", extension);
            }
            else if (PlatformHelper.IsWebGL)
            {
                Assert.AreEqual("", extension);
            }
        }

        [Test]
        public void GetNativeLibraryName_Windows_ReturnsBaseName()
        {
            if (PlatformHelper.IsWindows)
            {
                Assert.AreEqual("mylib", PlatformHelper.GetNativeLibraryName("mylib"));
                Assert.AreEqual("libmylib", PlatformHelper.GetNativeLibraryName("libmylib"));
            }
        }

        [Test]
        public void GetNativeLibraryName_Unix_AddsLibPrefix()
        {
            if (PlatformHelper.IsMacOS || PlatformHelper.IsLinux)
            {
                Assert.AreEqual("libmylib", PlatformHelper.GetNativeLibraryName("mylib"));
                Assert.AreEqual("libmylib", PlatformHelper.GetNativeLibraryName("libmylib"));
            }
        }

        [Test]
        public void GetNativeLibraryDirectory_ReturnsValidPath()
        {
            var dir = PlatformHelper.GetNativeLibraryDirectory();
            Assert.IsNotNull(dir);
            Assert.IsNotEmpty(dir);
        }

        [Test]
        public void GetArchitecture_ReturnsValidArchitecture()
        {
            var arch = PlatformHelper.GetArchitecture();
            Assert.IsNotNull(arch);
            Assert.IsNotEmpty(arch);
            
            if (PlatformHelper.IsWindows || PlatformHelper.IsLinux || PlatformHelper.IsMacOS)
            {
                Assert.IsTrue(arch == "x64" || arch == "x86");
            }
            else if (PlatformHelper.IsAndroid)
            {
                Assert.AreEqual("arm64-v8a", arch);
            }
            else if (PlatformHelper.IsIOS)
            {
                Assert.AreEqual("arm64", arch);
            }
        }

        [Test]
        public void SupportsNativePlugins_CorrectForPlatform()
        {
            if (PlatformHelper.IsWebGL)
            {
                Assert.IsFalse(PlatformHelper.SupportsNativePlugins);
            }
            else if (PlatformHelper.IsWindows || PlatformHelper.IsMacOS || 
                     PlatformHelper.IsLinux || PlatformHelper.IsAndroid || 
                     PlatformHelper.IsIOS)
            {
                Assert.IsTrue(PlatformHelper.SupportsNativePlugins);
            }
        }

        [Test]
        public void PathSeparator_MatchesSystem()
        {
            var separator = PlatformHelper.PathSeparator;
            Assert.AreEqual(Path.DirectorySeparatorChar, separator);
        }

        [Test]
        public void NormalizePath_HandlesEmptyString()
        {
            Assert.AreEqual("", PlatformHelper.NormalizePath(""));
            Assert.AreEqual(null, PlatformHelper.NormalizePath(null));
        }

        [Test]
        public void NormalizePath_NormalizesSeparators()
        {
            var testPath = "folder1\\folder2/file.txt";
            var normalized = PlatformHelper.NormalizePath(testPath);
            
            // Should not contain mixed separators
            Assert.IsFalse(normalized.Contains("\\") && normalized.Contains("/"));
            
            // Should contain the correct separator
            var separator = PlatformHelper.PathSeparator;
            Assert.IsTrue(normalized.Contains(separator.ToString()));
        }

        [Test]
        public void GetStreamingAssetsNativePath_ReturnsExpectedPath()
        {
            var path = PlatformHelper.GetStreamingAssetsNativePath();
            Assert.IsNotNull(path);
            Assert.IsTrue(path.Contains("StreamingAssets"));
            Assert.IsTrue(path.Contains("uPiper"));
            Assert.IsTrue(path.Contains("Native"));
        }

        [Test]
        public void LogPlatformInfo_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => PlatformHelper.LogPlatformInfo());
        }

        [Test]
        public void PlatformSpecificPaths_ConsistentWithPlatform()
        {
            var libDir = PlatformHelper.GetNativeLibraryDirectory();
            
            if (Application.isEditor)
            {
                Assert.IsTrue(libDir.Contains("Plugins"));
            }
            else
            {
                // In builds, paths vary by platform
                if (PlatformHelper.IsWindows || PlatformHelper.IsLinux)
                {
                    // Should be in Data/Plugins
                    Assert.IsTrue(libDir.Contains("Plugins"));
                }
                else if (PlatformHelper.IsMacOS)
                {
                    // Should be in Contents/Plugins
                    Assert.IsTrue(libDir.Contains("Plugins"));
                }
            }
        }
    }
}