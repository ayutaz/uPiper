#if UNITY_EDITOR
using NUnit.Framework;
using uPiper.Core.Platform;

namespace uPiper.Tests.Editor
{
    /// <summary>
    /// Tests for NativeLibraryResolver functionality.
    /// </summary>
    [TestFixture]
    [Category("Editor")]
    [Category("Platform")]
    public class NativeLibraryResolverTest
    {
        #region IsCIEnvironment Tests

        [Test]
        public void IsCIEnvironment_ReturnsBoolValue()
        {
            // IsCIEnvironment should return a valid boolean
            var result = NativeLibraryResolver.IsCIEnvironment;
            Assert.That(result, Is.TypeOf<bool>());
        }

        [Test]
        public void IsCIEnvironment_ConsistentAcrossCalls()
        {
            var result1 = NativeLibraryResolver.IsCIEnvironment;
            var result2 = NativeLibraryResolver.IsCIEnvironment;
            Assert.AreEqual(result1, result2, "IsCIEnvironment should be consistent");
        }

        #endregion

        #region GetExpectedLibraryPath Tests

        [Test]
        public void GetExpectedLibraryPath_ReturnsNonEmptyString()
        {
            var path = NativeLibraryResolver.GetExpectedLibraryPath();
            Assert.IsNotNull(path);
            Assert.IsNotEmpty(path);
        }

        [Test]
        public void GetExpectedLibraryPath_ContainsPlatformSpecificExtension()
        {
            var path = NativeLibraryResolver.GetExpectedLibraryPath();

            if (PlatformHelper.IsWindows)
            {
                Assert.IsTrue(path.EndsWith(".dll"),
                    $"Windows path should end with .dll: {path}");
            }
            else if (PlatformHelper.IsMacOS)
            {
                Assert.IsTrue(path.EndsWith(".bundle") || path.EndsWith(".dylib"),
                    $"macOS path should end with .bundle or .dylib: {path}");
            }
            else if (PlatformHelper.IsLinux)
            {
                Assert.IsTrue(path.EndsWith(".so"),
                    $"Linux path should end with .so: {path}");
            }
        }

        [Test]
        public void GetExpectedLibraryPath_ContainsLibraryName()
        {
            var path = NativeLibraryResolver.GetExpectedLibraryPath();
            Assert.IsTrue(path.Contains("openjtalk_wrapper"),
                $"Path should contain library name: {path}");
        }

        #endregion

        #region GetAlternativeLibraryPaths Tests

        [Test]
        public void GetAlternativeLibraryPaths_ReturnsNonNullList()
        {
            var paths = NativeLibraryResolver.GetAlternativeLibraryPaths();
            Assert.IsNotNull(paths);
        }

        [Test]
        public void GetAlternativeLibraryPaths_ContainsValidPaths()
        {
            var paths = NativeLibraryResolver.GetAlternativeLibraryPaths();
            foreach (var path in paths)
            {
                Assert.IsNotNull(path, "Path should not be null");
                Assert.IsNotEmpty(path, "Path should not be empty");
            }
        }

        [Test]
        public void GetAlternativeLibraryPaths_PathsContainLibraryName()
        {
            var paths = NativeLibraryResolver.GetAlternativeLibraryPaths();
            foreach (var path in paths)
            {
                Assert.IsTrue(path.Contains("openjtalk_wrapper"),
                    $"Path should contain library name: {path}");
            }
        }

        #endregion

        #region IsNativeLibraryAvailable Tests

        [Test]
        public void IsNativeLibraryAvailable_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => NativeLibraryResolver.IsNativeLibraryAvailable());
        }

        [Test]
        public void IsNativeLibraryAvailable_ReturnsBoolValue()
        {
            var result = NativeLibraryResolver.IsNativeLibraryAvailable();
            Assert.That(result, Is.TypeOf<bool>());
        }

        #endregion

        #region GetPackagePath Tests

        [Test]
        public void GetPackagePath_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => NativeLibraryResolver.GetPackagePath());
        }

        [Test]
        public void GetPackagePath_ReturnsNullOrValidPath()
        {
            var path = NativeLibraryResolver.GetPackagePath();

            // GetPackagePath returns null when not installed via UPM
            // or a valid path when installed via UPM
            if (path != null)
            {
                Assert.IsNotEmpty(path, "If not null, path should not be empty");
                Assert.IsTrue(path.Contains("com.ayutaz.upiper"),
                    $"Package path should contain package name: {path}");
            }
        }

        #endregion

        #region Logging Helper Tests

        [Test]
        public void LogEnvironmentInfo_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => NativeLibraryResolver.LogEnvironmentInfo());
        }

        [Test]
        public void LogPluginDirectoryContents_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => NativeLibraryResolver.LogPluginDirectoryContents());
        }

        #endregion

        #region Path Consistency Tests

        [Test]
        public void GetExpectedLibraryPath_ConsistentAcrossCalls()
        {
            var path1 = NativeLibraryResolver.GetExpectedLibraryPath();
            var path2 = NativeLibraryResolver.GetExpectedLibraryPath();
            Assert.AreEqual(path1, path2, "Path should be consistent across calls");
        }

        [Test]
        public void GetAlternativeLibraryPaths_ConsistentAcrossCalls()
        {
            var paths1 = NativeLibraryResolver.GetAlternativeLibraryPaths();
            var paths2 = NativeLibraryResolver.GetAlternativeLibraryPaths();

            Assert.AreEqual(paths1.Count, paths2.Count,
                "Alternative paths count should be consistent");

            for (int i = 0; i < paths1.Count; i++)
            {
                Assert.AreEqual(paths1[i], paths2[i],
                    $"Path at index {i} should be consistent");
            }
        }

        #endregion

        #region Path Format Tests

        [Test]
        public void GetExpectedLibraryPath_NoDoubleSlashes()
        {
            var path = NativeLibraryResolver.GetExpectedLibraryPath();
            Assert.IsFalse(path.Contains("//"),
                $"Path should not have double forward slashes: {path}");
            Assert.IsFalse(path.Contains("\\\\"),
                $"Path should not have double backslashes: {path}");
        }

        [Test]
        public void GetAlternativeLibraryPaths_NoDoubleSlashes()
        {
            var paths = NativeLibraryResolver.GetAlternativeLibraryPaths();
            foreach (var path in paths)
            {
                Assert.IsFalse(path.Contains("//"),
                    $"Path should not have double forward slashes: {path}");
                Assert.IsFalse(path.Contains("\\\\"),
                    $"Path should not have double backslashes: {path}");
            }
        }

        #endregion
    }
}
#endif
