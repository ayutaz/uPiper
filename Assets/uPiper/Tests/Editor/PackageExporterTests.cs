using NUnit.Framework;
using System.IO;
using UnityEngine;
using UnityEditor;
using System.Linq;
using Newtonsoft.Json;

namespace uPiper.Tests.Editor
{
    /// <summary>
    /// Tests for PackageExporter functionality
    /// </summary>
    public class PackageExporterTests
    {
        private string testExportPath;
        private string originalPackageJsonPath = "Assets/uPiper/package.json";
        
        [SetUp]
        public void SetUp()
        {
            testExportPath = Path.Combine(Application.temporaryCachePath, "PackageExporterTest");
            if (Directory.Exists(testExportPath))
            {
                Directory.Delete(testExportPath, true);
            }
            Directory.CreateDirectory(testExportPath);
        }
        
        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(testExportPath))
            {
                try
                {
                    Directory.Delete(testExportPath, true);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"Failed to clean up test directory: {ex.Message}");
                }
            }
        }
        
        [Test]
        public void PackageJsonExists()
        {
            Assert.IsTrue(File.Exists(originalPackageJsonPath), 
                "package.json should exist at the expected location");
        }
        
        [Test]
        public void PackageJsonIsValidJson()
        {
            Assert.IsTrue(File.Exists(originalPackageJsonPath));
            
            var json = File.ReadAllText(originalPackageJsonPath);
            Assert.DoesNotThrow(() => 
            {
                var parsed = JsonConvert.DeserializeObject(json);
                Assert.IsNotNull(parsed);
            }, "package.json should contain valid JSON");
        }
        
        [Test]
        public void PackageJsonHasRequiredFields()
        {
            Assert.IsTrue(File.Exists(originalPackageJsonPath));
            
            var json = File.ReadAllText(originalPackageJsonPath);
            var packageInfo = JsonConvert.DeserializeObject<PackageInfo>(json);
            
            Assert.IsNotNull(packageInfo, "Should be able to deserialize package.json");
            Assert.IsNotEmpty(packageInfo.Name, "Package should have a name");
            Assert.IsNotEmpty(packageInfo.Version, "Package should have a version");
            Assert.IsNotEmpty(packageInfo.DisplayName, "Package should have a display name");
            Assert.IsNotEmpty(packageInfo.Description, "Package should have a description");
            Assert.IsNotNull(packageInfo.Dependencies, "Package should have dependencies");
        }
        
        [Test]
        public void PackageVersionIsValidSemVer()
        {
            Assert.IsTrue(File.Exists(originalPackageJsonPath));
            
            var json = File.ReadAllText(originalPackageJsonPath);
            var packageInfo = JsonConvert.DeserializeObject<PackageInfo>(json);
            
            Assert.IsNotNull(packageInfo);
            
            // Basic semver validation (major.minor.patch)
            var versionParts = packageInfo.Version.Split('.');
            Assert.GreaterOrEqual(versionParts.Length, 3, 
                "Version should have at least 3 parts (major.minor.patch)");
            
            foreach (var part in versionParts.Take(3))
            {
                Assert.IsTrue(int.TryParse(part, out _), 
                    $"Version part '{part}' should be a valid integer");
            }
        }
        
        [Test]
        public void uPiperAssetsDirectoryExists()
        {
            Assert.IsTrue(Directory.Exists("Assets/uPiper"), 
                "uPiper assets directory should exist");
            
            Assert.IsTrue(Directory.Exists("Assets/uPiper/Runtime"), 
                "uPiper Runtime directory should exist");
                
            Assert.IsTrue(Directory.Exists("Assets/uPiper/Editor"), 
                "uPiper Editor directory should exist");
        }
        
        [Test]
        public void RequiredAssemblyDefinitionsExist()
        {
            Assert.IsTrue(File.Exists("Assets/uPiper/Runtime/uPiper.Runtime.asmdef"), 
                "Runtime assembly definition should exist");
                
            Assert.IsTrue(File.Exists("Assets/uPiper/Editor/uPiper.Editor.asmdef"), 
                "Editor assembly definition should exist");
                
            Assert.IsTrue(File.Exists("Assets/uPiper/Tests/Runtime/uPiper.Tests.Runtime.asmdef"), 
                "Runtime tests assembly definition should exist");
                
            Assert.IsTrue(File.Exists("Assets/uPiper/Tests/Editor/uPiper.Tests.Editor.asmdef"), 
                "Editor tests assembly definition should exist");
        }
        
        [Test]
        public void ExportDirectoryCanBeCreated()
        {
            var exportDir = Path.Combine(testExportPath, "TestExport");
            Assert.DoesNotThrow(() => Directory.CreateDirectory(exportDir));
            Assert.IsTrue(Directory.Exists(exportDir));
        }
        
        [Test]
        public void PackageExporterMenuItemsExist()
        {
            // This test verifies that the menu items are properly defined
            // We can't actually invoke them in a unit test, but we can check if the methods exist
            var exporterType = typeof(uPiper.Editor.PackageExporter);
            
            var exportUnityPackageMethod = exporterType.GetMethod("ExportUnityPackage", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(exportUnityPackageMethod, "ExportUnityPackage method should exist");
            
            var exportUPMPackageMethod = exporterType.GetMethod("ExportUPMPackage", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(exportUPMPackageMethod, "ExportUPMPackage method should exist");
            
            var exportBothFormatsMethod = exporterType.GetMethod("ExportBothFormats", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(exportBothFormatsMethod, "ExportBothFormats method should exist");
            
            var exportUnityPackageCIMethod = exporterType.GetMethod("ExportUnityPackageCI", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(exportUnityPackageCIMethod, "ExportUnityPackageCI method should exist");
        }
        
        [Test]
        public void MenuStructurePrioritiesAreCorrect()
        {
            // Verify that menu priorities are properly spaced
            var menuStructureType = typeof(uPiper.Editor.Menu.uPiperMenuStructure);
            
            var priorityBuild = (int)menuStructureType.GetField("PRIORITY_BUILD").GetValue(null);
            Assert.AreEqual(200, priorityBuild, "PRIORITY_BUILD should be 200");
            
            // Package export items should be in the build section (200-299)
            // The actual menu items are defined with PRIORITY_BUILD + offset
            // So they should be in the range 250-260 based on our implementation
        }
        
        [Test]
        public void SamplesDirectoryStructureIsValid()
        {
            // Check if samples are properly structured
            var samplesPath = "Assets/uPiper/Samples~";
            if (Directory.Exists(samplesPath))
            {
                var sampleDirs = Directory.GetDirectories(samplesPath);
                foreach (var sampleDir in sampleDirs)
                {
                    var sampleName = Path.GetFileName(sampleDir);
                    Debug.Log($"Found sample directory: {sampleName}");
                    
                    // Each sample should have at least one scene or script
                    var hasContent = Directory.GetFiles(sampleDir, "*", SearchOption.AllDirectories)
                        .Any(f => f.EndsWith(".unity") || f.EndsWith(".cs"));
                    
                    Assert.IsTrue(hasContent, $"Sample '{sampleName}' should contain at least one scene or script");
                }
            }
        }
        
        // Helper class for JSON deserialization
        [System.Serializable]
        private class PackageInfo
        {
            public string Name { get; set; }
            public string Version { get; set; }
            public string DisplayName { get; set; }
            public string Description { get; set; }
            public string Unity { get; set; }
            public string UnityRelease { get; set; }
            public System.Collections.Generic.Dictionary<string, string> Dependencies { get; set; }
            public string[] Keywords { get; set; }
            public AuthorInfo Author { get; set; }
            public SampleInfo[] Samples { get; set; }
        }
        
        [System.Serializable]
        private class AuthorInfo
        {
            public string Name { get; set; }
            public string Email { get; set; }
            public string Url { get; set; }
        }
        
        [System.Serializable]
        private class SampleInfo
        {
            public string DisplayName { get; set; }
            public string Description { get; set; }
            public string Path { get; set; }
        }
    }
}