using NUnit.Framework;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace uPiper.Tests.Editor
{
    /// <summary>
    /// WebGL implementation verification tests that can run in Unity Editor
    /// </summary>
    public class WebGLImplementationVerificationTest
    {
        [Test]
        public void WebGL_JavaScriptFiles_Exist()
        {
            // Verify .jslib files exist
            string[] expectedJsLibFiles = new[]
            {
                "Assets/uPiper/Plugins/WebGL/openjtalk_wrapper.jslib",
                "Assets/uPiper/Plugins/WebGL/espeak_wrapper.jslib",
                "Assets/uPiper/Plugins/WebGL/indexeddb_cache.jslib"
            };

            foreach (var file in expectedJsLibFiles)
            {
                Assert.IsTrue(File.Exists(file), $"WebGL JavaScript library file missing: {file}");
            }
        }

        [Test]
        public void WebGL_CSharpFiles_Exist()
        {
            // Verify WebGL C# implementation files exist
            string[] expectedCsFiles = new[]
            {
                "Assets/uPiper/Runtime/Core/Phonemizers/WebGL/WebGLInterop.cs",
                "Assets/uPiper/Runtime/Core/Phonemizers/WebGL/WebGLOpenJTalkPhonemizer.cs",
                "Assets/uPiper/Runtime/Core/Phonemizers/WebGL/WebGLESpeakPhonemizer.cs",
                "Assets/uPiper/Runtime/Core/Phonemizers/WebGL/WebGLCacheManager.cs"
            };

            foreach (var file in expectedCsFiles)
            {
                Assert.IsTrue(File.Exists(file), $"WebGL C# implementation file missing: {file}");
            }
        }

        [Test]
        public void WebGL_Template_Exists()
        {
            // Verify WebGL template exists
            string templatePath = "Assets/WebGLTemplates/uPiper";
            Assert.IsTrue(Directory.Exists(templatePath), "WebGL template directory missing");

            string indexHtml = Path.Combine(templatePath, "index.html");
            Assert.IsTrue(File.Exists(indexHtml), "WebGL template index.html missing");
        }

        [Test]
        public void WebGL_Demo_Scene_Exists()
        {
            // Verify WebGL demo files exist
            string demoPath = "Assets/uPiper/Samples~/WebGLDemo";
            Assert.IsTrue(Directory.Exists(demoPath), "WebGL demo directory missing");

            string[] expectedDemoFiles = new[]
            {
                "WebGLTTSDemo.cs",
                "WebGLDemoScene.unity",
                "WebGLDemo.asmdef",
                "README.md"
            };

            foreach (var file in expectedDemoFiles)
            {
                string fullPath = Path.Combine(demoPath, file);
                Assert.IsTrue(File.Exists(fullPath), $"WebGL demo file missing: {file}");
            }
        }

        [Test]
        public void WebGL_JavaScriptLibrary_Syntax_Valid()
        {
            // Basic syntax validation for .jslib files
            string[] jsLibFiles = Directory.GetFiles("Assets/uPiper/Plugins/WebGL", "*.jslib");
            
            foreach (var file in jsLibFiles)
            {
                string content = File.ReadAllText(file);
                
                // Check for basic structure
                Assert.IsTrue(content.Contains("mergeInto(LibraryManager.library"), 
                    $"Invalid jslib structure in {Path.GetFileName(file)}");
                
                // Check for balanced braces
                int openBraces = 0;
                foreach (char c in content)
                {
                    if (c == '{') openBraces++;
                    if (c == '}') openBraces--;
                }
                Assert.AreEqual(0, openBraces, 
                    $"Unbalanced braces in {Path.GetFileName(file)}");
            }
        }

        [Test]
        public void PiperTTS_Has_WebGL_Conditional_Compilation()
        {
            string piperTTSPath = "Assets/uPiper/Runtime/Core/PiperTTS.cs";
            Assert.IsTrue(File.Exists(piperTTSPath), "PiperTTS.cs not found");

            string content = File.ReadAllText(piperTTSPath);
            
            // Check for WebGL conditional compilation
            Assert.IsTrue(content.Contains("#if UNITY_WEBGL"), 
                "PiperTTS.cs missing WebGL conditional compilation");
            
            // Check for WebGL phonemizer usage
            Assert.IsTrue(content.Contains("WebGLOpenJTalkPhonemizer"), 
                "PiperTTS.cs missing WebGLOpenJTalkPhonemizer reference");
            Assert.IsTrue(content.Contains("WebGLESpeakPhonemizer"), 
                "PiperTTS.cs missing WebGLESpeakPhonemizer reference");
        }

        [Test]
        public void WebGL_Build_Settings_Configured()
        {
            string buildProcessorPath = "Assets/uPiper/Editor/BuildSettings/PiperBuildProcessor.cs";
            Assert.IsTrue(File.Exists(buildProcessorPath), "PiperBuildProcessor.cs not found");

            string content = File.ReadAllText(buildProcessorPath);
            
            // Check for WebGL build configuration
            Assert.IsTrue(content.Contains("ConfigureWebGLBuild"), 
                "PiperBuildProcessor missing WebGL configuration method");
            
            // Check for memory size configuration
            Assert.IsTrue(content.Contains("PlayerSettings.WebGL.memorySize = 1024"), 
                "WebGL memory size not set to 1GB");
            
            // Check for custom template usage
            Assert.IsTrue(content.Contains("APPLICATION:uPiper"), 
                "WebGL custom template not configured");
        }

        [Test]
        public void Package_Json_Has_WebGL_Sample()
        {
            string packageJsonPath = "Assets/uPiper/package.json";
            Assert.IsTrue(File.Exists(packageJsonPath), "package.json not found");

            string content = File.ReadAllText(packageJsonPath);
            
            // Check for WebGL demo sample
            Assert.IsTrue(content.Contains("WebGL Demo"), 
                "package.json missing WebGL Demo sample");
            Assert.IsTrue(content.Contains("Samples~/WebGLDemo"), 
                "package.json missing WebGL Demo path");
        }

        [Test]
        public void WebGL_Memory_Management_Implemented()
        {
            // Check for memory management functions in jslib files
            string cacheLibPath = "Assets/uPiper/Plugins/WebGL/indexeddb_cache.jslib";
            Assert.IsTrue(File.Exists(cacheLibPath), "indexeddb_cache.jslib not found");

            string content = File.ReadAllText(cacheLibPath);
            
            // Check for IndexedDB functions
            Assert.IsTrue(content.Contains("InitializeIndexedDBCache"), 
                "Missing IndexedDB initialization");
            Assert.IsTrue(content.Contains("CachePhonemes"), 
                "Missing phoneme caching function");
            Assert.IsTrue(content.Contains("GetCachedPhonemes"), 
                "Missing phoneme retrieval function");
            Assert.IsTrue(content.Contains("ClearOldCache"), 
                "Missing cache cleanup function");
        }

        [Test]
        public void WebGL_Platform_Detection_Implemented()
        {
            string platformHelperPath = "Assets/uPiper/Runtime/Core/Platform/PlatformHelper.cs";
            Assert.IsTrue(File.Exists(platformHelperPath), "PlatformHelper.cs not found");

            string content = File.ReadAllText(platformHelperPath);
            
            // Check for WebGL platform detection
            Assert.IsTrue(content.Contains("IsWebGL"), 
                "PlatformHelper missing WebGL detection");
            Assert.IsTrue(content.Contains("RuntimePlatform.WebGLPlayer"), 
                "PlatformHelper missing WebGL platform check");
        }
    }
}