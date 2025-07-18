using System;
using NUnit.Framework;
using uPiper.Core.Platform;

namespace uPiper.Tests.Runtime.Platform
{
    /// <summary>
    /// Tests for NativeLibraryLoader
    /// </summary>
    public class NativeLibraryLoaderTests
    {
        [TearDown]
        public void TearDown()
        {
            // Clean up any loaded libraries
            NativeLibraryLoader.UnloadAll();
        }

        [Test]
        public void LoadLibrary_NullName_ThrowsException()
        {
            Assert.Throws<ArgumentNullException>(() => 
                NativeLibraryLoader.LoadLibrary(null));
        }

        [Test]
        public void LoadLibrary_EmptyName_ThrowsException()
        {
            Assert.Throws<ArgumentNullException>(() => 
                NativeLibraryLoader.LoadLibrary(""));
        }

        [Test]
        public void LoadLibrary_NonExistentLibrary_ThrowsException()
        {
            if (!PlatformHelper.SupportsNativePlugins)
            {
                Assert.Ignore("Platform does not support native plugins");
            }

            Assert.Throws<DllNotFoundException>(() => 
                NativeLibraryLoader.LoadLibrary("nonexistent_library_12345"));
        }

        [Test]
        public void IsLibraryLoaded_NotLoaded_ReturnsFalse()
        {
            Assert.IsFalse(NativeLibraryLoader.IsLibraryLoaded("test_library"));
        }

        [Test]
        public void UnloadLibrary_NotLoaded_ReturnsFalse()
        {
            Assert.IsFalse(NativeLibraryLoader.UnloadLibrary("test_library"));
        }

        [Test]
        public void GetFunctionPointer_LibraryNotLoaded_ThrowsException()
        {
            Assert.Throws<InvalidOperationException>(() => 
                NativeLibraryLoader.GetFunctionPointer("test_library", "test_function"));
        }

        [Test]
        public void UnloadAll_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => NativeLibraryLoader.UnloadAll());
        }

        [Test]
        [Category("RequiresNativeLibrary")]
        public void LoadLibrary_ValidSystemLibrary_Succeeds()
        {
            if (!PlatformHelper.SupportsNativePlugins)
            {
                Assert.Ignore("Platform does not support native plugins");
            }

            // Try to load a common system library
            string libraryName = null;
            
            if (PlatformHelper.IsWindows)
            {
                libraryName = "kernel32"; // Always available on Windows
            }
            else if (PlatformHelper.IsMacOS)
            {
                libraryName = "c"; // libc
            }
            else if (PlatformHelper.IsLinux)
            {
                libraryName = "c"; // libc
            }
            
            if (libraryName == null)
            {
                Assert.Ignore("No suitable system library for testing on this platform");
            }

            // This might fail in some Unity environments due to sandboxing
            try
            {
                var handle = NativeLibraryLoader.LoadLibrary(libraryName);
                Assert.AreNotEqual(IntPtr.Zero, handle);
                Assert.IsTrue(NativeLibraryLoader.IsLibraryLoaded(libraryName));
            }
            catch (DllNotFoundException)
            {
                // Expected in some environments
                Assert.Ignore("Could not load system library in this environment");
            }
        }
    }
}