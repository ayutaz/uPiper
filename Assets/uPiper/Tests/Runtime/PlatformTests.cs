using NUnit.Framework;
using uPiper.Platform;
using UnityEngine;

namespace uPiper.Tests
{
    [TestFixture]
    public class PlatformTests
    {
        [Test]
        public void PlatformFactory_CreatesPlatform_ForCurrentRuntime()
        {
            var platform = PlatformFactory.CreatePlatform();
            
            Assert.IsNotNull(platform);
            Assert.IsTrue(platform is IPlatform);
        }

        [Test]
        public void WindowsPlatform_Properties_AreCorrect()
        {
            var platform = new WindowsPlatform();
            
            Assert.AreEqual(PlatformType.Windows, platform.Type);
            Assert.IsTrue(platform.SupportsNativePhonemization);
        }

        [Test]
        public void LinuxPlatform_Properties_AreCorrect()
        {
            var platform = new LinuxPlatform();
            
            Assert.AreEqual(PlatformType.Linux, platform.Type);
            Assert.IsTrue(platform.SupportsNativePhonemization);
        }

        [Test]
        public void UnsupportedPlatform_Properties_AreCorrect()
        {
            var platform = new UnsupportedPlatform();
            
            Assert.AreEqual(PlatformType.Unknown, platform.Type);
            Assert.IsFalse(platform.SupportsNativePhonemization);
        }

        [Test]
        public void WindowsPlatform_GetNativeLibraryPath_ReturnsDllPath()
        {
            var platform = new WindowsPlatform();
            
            var path = platform.GetNativeLibraryPath("testlib");
            
            // Should return null if file doesn't exist, but should contain .dll
            if (path != null)
            {
                Assert.IsTrue(path.Contains("testlib.dll"));
            }
        }

        [Test]
        public void LinuxPlatform_GetNativeLibraryPath_ReturnsSoPath()
        {
            var platform = new LinuxPlatform();
            
            var path = platform.GetNativeLibraryPath("testlib");
            
            // Should return null if file doesn't exist, but should contain lib*.so
            if (path != null)
            {
                Assert.IsTrue(path.Contains("libtestlib.so"));
            }
        }

        [Test]
        public void MacOSPlatform_Type_IsCorrect()
        {
            var platform = new MacOSPlatform();
            
            Assert.AreEqual(PlatformType.macOS, platform.Type);
            Assert.IsFalse(platform.SupportsNativePhonemization); // Currently unsupported
        }

        [Test]
        public void AndroidPlatform_Type_IsCorrect()
        {
            var platform = new AndroidPlatform();
            
            Assert.AreEqual(PlatformType.Android, platform.Type);
            Assert.IsFalse(platform.SupportsNativePhonemization); // Currently unsupported
        }

        [Test]
        public void IOSPlatform_Type_IsCorrect()
        {
            var platform = new IOSPlatform();
            
            Assert.AreEqual(PlatformType.iOS, platform.Type);
            Assert.IsFalse(platform.SupportsNativePhonemization); // Currently unsupported
        }

        [Test]
        public void WebGLPlatform_Type_IsCorrect()
        {
            var platform = new WebGLPlatform();
            
            Assert.AreEqual(PlatformType.WebGL, platform.Type);
            Assert.IsFalse(platform.SupportsNativePhonemization); // Currently unsupported
        }

        [Test]
        public void Platform_Initialize_DoesNotThrow()
        {
            var platforms = new IPlatform[]
            {
                new WindowsPlatform(),
                new LinuxPlatform(),
                new UnsupportedPlatform(),
                new MacOSPlatform(),
                new AndroidPlatform(),
                new IOSPlatform(),
                new WebGLPlatform()
            };

            foreach (var platform in platforms)
            {
                Assert.DoesNotThrow(() => platform.Initialize());
            }
        }

        [Test]
        public void Platform_Cleanup_DoesNotThrow()
        {
            var platforms = new IPlatform[]
            {
                new WindowsPlatform(),
                new LinuxPlatform(),
                new UnsupportedPlatform(),
                new MacOSPlatform(),
                new AndroidPlatform(),
                new IOSPlatform(),
                new WebGLPlatform()
            };

            foreach (var platform in platforms)
            {
                Assert.DoesNotThrow(() => platform.Cleanup());
            }
        }

        [Test]
        public void PlatformFactory_CreatesCorrectPlatform_ForEditor()
        {
            var platform = PlatformFactory.CreatePlatform();
            
            // In Unity Editor, it should create platform based on OS
#if UNITY_EDITOR_WIN
            Assert.IsTrue(platform is WindowsPlatform);
#elif UNITY_EDITOR_LINUX
            Assert.IsTrue(platform is LinuxPlatform);
#elif UNITY_EDITOR_OSX
            Assert.IsTrue(platform is MacOSPlatform);
#endif
        }
    }
}