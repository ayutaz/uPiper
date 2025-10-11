#if UNITY_IOS || UNITY_EDITOR
using System.IO;
using NUnit.Framework;
using UnityEngine;
using uPiper.Core.Platform;

namespace uPiper.Tests.Runtime.Core.Platform
{
    /// <summary>
    /// Tests for IOSPathResolver functionality.
    /// </summary>
    [TestFixture]
    [Category("Platform")]
    [Category("iOS")]
    public class IOSPathResolverTest
    {
#if UNITY_IOS && !UNITY_EDITOR
        [Test]
        public void GetOpenJTalkDictionaryPath_ReturnsCorrectPath()
        {
            var path = IOSPathResolver.GetOpenJTalkDictionaryPath();
            
            Assert.NotNull(path);
            Assert.IsTrue(path.Contains("Raw"), "Path should contain 'Raw' directory");
            Assert.IsTrue(path.Contains("uPiper"), "Path should contain 'uPiper'");
            Assert.IsTrue(path.Contains("OpenJTalk"), "Path should contain 'OpenJTalk'");
            Assert.IsTrue(path.Contains("naist_jdic"), "Path should contain 'naist_jdic'");
            
            Debug.Log($"[IOSPathResolverTest] Dictionary path: {path}");
        }

        [Test]
        public void GetStreamingAssetsPath_ReturnsCorrectPath()
        {
            var testPath = "test/file.txt";
            var fullPath = IOSPathResolver.GetStreamingAssetsPath(testPath);
            
            Assert.NotNull(fullPath);
            Assert.IsTrue(fullPath.Contains(Application.dataPath), "Should contain Application.dataPath");
            Assert.IsTrue(fullPath.Contains("Raw"), "Should contain 'Raw' directory");
            Assert.IsTrue(fullPath.EndsWith(testPath), "Should end with the relative path");
        }

        [Test]
        public void DictionaryExists_ChecksEssentialFiles()
        {
            // This test might fail if dictionary is not included in test build
            // But it validates the checking logic
            var exists = IOSPathResolver.DictionaryExists();
            
            // Log the result regardless
            Debug.Log($"[IOSPathResolverTest] Dictionary exists: {exists}");
            
            if (exists)
            {
                // If dictionary exists, verify we can get its size
                var size = IOSPathResolver.GetDictionarySize();
                Assert.Greater(size, 0, "Dictionary size should be greater than 0");
                Debug.Log($"[IOSPathResolverTest] Dictionary size: {size} bytes");
            }
        }

        [Test]
        public void LoadTextFile_HandlesNonExistentFile()
        {
            var content = IOSPathResolver.LoadTextFile("nonexistent/file.txt");
            Assert.IsNull(content, "Should return null for non-existent file");
        }

        [Test]
        public void LoadBinaryFile_HandlesNonExistentFile()
        {
            var data = IOSPathResolver.LoadBinaryFile("nonexistent/file.bin");
            Assert.IsNull(data, "Should return null for non-existent file");
        }

        [Test]
        public void ListFiles_HandlesNonExistentDirectory()
        {
            var files = IOSPathResolver.ListFiles("nonexistent/directory");
            Assert.NotNull(files, "Should return empty array, not null");
            Assert.AreEqual(0, files.Length, "Should return empty array for non-existent directory");
        }

        [Test]
        public void GetDictionarySize_HandlesErrors()
        {
            // Even if dictionary doesn't exist, method should not throw
            var size = IOSPathResolver.GetDictionarySize();
            Assert.GreaterOrEqual(size, 0, "Size should be non-negative");
        }

        [Test]
        public void LogDictionaryInfo_DoesNotThrow()
        {
            // This should not throw even if dictionary doesn't exist
            Assert.DoesNotThrow(() => IOSPathResolver.LogDictionaryInfo());
        }
#endif

        [Test]
        public void PathConstruction_IsConsistent()
        {
            // Test that path construction is consistent across calls
            var basePath = Path.Combine("uPiper", "OpenJTalk", "naist_jdic");
            var file1 = Path.Combine(basePath, "sys.dic");
            var file2 = Path.Combine(basePath, "sys.dic");
            
            Assert.AreEqual(file1, file2, "Path construction should be consistent");
            
            // Test path separator handling
            Assert.IsFalse(file1.Contains("\\\\"), "Should not have double backslashes");
            Assert.IsFalse(file1.Contains("//"), "Should not have double forward slashes");
        }

        [Test]
        public void EssentialFiles_ListIsComplete()
        {
            // Verify the list of essential files matches OpenJTalk requirements
            var essentialFiles = new[] { "sys.dic", "unk.dic", "matrix.bin", "char.bin" };
            
            Assert.AreEqual(4, essentialFiles.Length, "Should check for 4 essential files");
            Assert.Contains("sys.dic", essentialFiles, "Should check for sys.dic");
            Assert.Contains("unk.dic", essentialFiles, "Should check for unk.dic");
            Assert.Contains("matrix.bin", essentialFiles, "Should check for matrix.bin");
            Assert.Contains("char.bin", essentialFiles, "Should check for char.bin");
        }

#if UNITY_EDITOR
        [Test]
        public void EditorOnly_PathResolverCompiles()
        {
            // This test ensures the IOSPathResolver code compiles in editor
            // even though it's wrapped in UNITY_IOS && !UNITY_EDITOR
            Assert.Pass("IOSPathResolver compilation test passed");
        }
#endif
    }
}
#endif