using NUnit.Framework;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace uPiper.Tests.Editor
{
    /// <summary>
    /// Static analysis tests for WebGL implementation that don't require compilation
    /// </summary>
    public class WebGLStaticAnalysisTest
    {
        [Test]
        public void WebGL_PhonemizersHaveProperConditionalCompilation()
        {
            string[] webglPhonemizerFiles = Directory.GetFiles(
                "Assets/uPiper/Runtime/Core/Phonemizers/WebGL", 
                "*.cs"
            );

            foreach (var file in webglPhonemizerFiles)
            {
                string content = File.ReadAllText(file);
                string fileName = Path.GetFileName(file);
                
                // Should have UNITY_WEBGL conditional compilation
                Assert.IsTrue(
                    content.Contains("#if UNITY_WEBGL") || content.Contains("#if UNITY_WEBGL && !UNITY_EDITOR"),
                    $"{fileName} missing UNITY_WEBGL conditional compilation"
                );
                
                // Should have matching #endif
                int ifCount = Regex.Matches(content, @"#if\s+UNITY_WEBGL").Count;
                int endifCount = Regex.Matches(content, @"#endif").Count;
                Assert.GreaterOrEqual(endifCount, ifCount, 
                    $"{fileName} has unmatched #if/#endif directives");
            }
        }

        [Test]
        public void WebGL_JavaScriptLibraries_HaveValidStructure()
        {
            string[] jsLibFiles = Directory.GetFiles(
                "Assets/uPiper/Plugins/WebGL", 
                "*.jslib"
            );

            Assert.Greater(jsLibFiles.Length, 0, "No .jslib files found");

            foreach (var file in jsLibFiles)
            {
                string content = File.ReadAllText(file);
                string fileName = Path.GetFileName(file);
                
                // Check basic structure
                Assert.IsTrue(
                    content.Contains("mergeInto(LibraryManager.library"),
                    $"{fileName} missing mergeInto declaration"
                );
                
                // Check for function definitions
                var functionMatches = Regex.Matches(content, @"(\w+):\s*function\s*\(");
                Assert.Greater(functionMatches.Count, 0, 
                    $"{fileName} contains no function definitions");
                
                // Verify each function has proper error handling
                foreach (Match match in functionMatches)
                {
                    string functionName = match.Groups[1].Value;
                    Assert.IsTrue(
                        content.Contains($"console.log") || content.Contains($"console.error"),
                        $"{fileName}: Function {functionName} should have logging"
                    );
                }
            }
        }

        [Test]
        public void WebGL_CacheManager_ImplementsAllRequiredMethods()
        {
            string cacheManagerPath = "Assets/uPiper/Runtime/Core/Phonemizers/WebGL/WebGLCacheManager.cs";
            Assert.IsTrue(File.Exists(cacheManagerPath), "WebGLCacheManager.cs not found");

            string content = File.ReadAllText(cacheManagerPath);
            
            // Required methods
            string[] requiredMethods = new[]
            {
                "Initialize",
                "CachePhonemesForText",
                "GetCachedPhonemesForText",
                "ClearOldEntries",
                "GetStats"
            };

            foreach (var method in requiredMethods)
            {
                Assert.IsTrue(
                    content.Contains($"public static") && content.Contains(method),
                    $"WebGLCacheManager missing required method: {method}"
                );
            }
        }

        [Test]
        public void WebGL_Template_HasRequiredScriptLoading()
        {
            string templatePath = "Assets/WebGLTemplates/uPiper/index.html";
            Assert.IsTrue(File.Exists(templatePath), "WebGL template index.html not found");

            string content = File.ReadAllText(templatePath);
            
            // Check for required script elements
            Assert.IsTrue(
                content.Contains("window.wasmOpenJTalk"),
                "Template missing wasmOpenJTalk initialization"
            );
            
            Assert.IsTrue(
                content.Contains("window.espeakNG"),
                "Template missing espeakNG initialization"
            );
            
            Assert.IsTrue(
                content.Contains("window.uPiper"),
                "Template missing uPiper namespace"
            );
        }

        [Test]
        public void WebGL_DemoScene_ReferencesCorrectComponents()
        {
            string demoScriptPath = "Assets/uPiper/Samples~/WebGLDemo/WebGLTTSDemo.cs";
            Assert.IsTrue(File.Exists(demoScriptPath), "WebGLTTSDemo.cs not found");

            string content = File.ReadAllText(demoScriptPath);
            
            // Check for required UI components
            string[] requiredComponents = new[]
            {
                "TMP_InputField",
                "TMP_Dropdown",
                "Button",
                "Slider",
                "AudioSource"
            };

            foreach (var component in requiredComponents)
            {
                Assert.IsTrue(
                    content.Contains($"[SerializeField] private {component}"),
                    $"WebGLTTSDemo missing required component: {component}"
                );
            }
            
            // Check for language support
            Assert.IsTrue(content.Contains("Japanese"), "Demo missing Japanese support");
            Assert.IsTrue(content.Contains("English"), "Demo missing English support");
            Assert.IsTrue(content.Contains("Chinese"), "Demo missing Chinese support");
        }

        [Test]
        public void WebGL_BuildProcessor_ConfiguresMemoryCorrectly()
        {
            string buildProcessorPath = "Assets/uPiper/Editor/BuildSettings/PiperBuildProcessor.cs";
            string content = File.ReadAllText(buildProcessorPath);
            
            // Check memory configuration
            var memoryMatch = Regex.Match(content, @"PlayerSettings\.WebGL\.memorySize\s*=\s*(\d+)");
            Assert.IsTrue(memoryMatch.Success, "WebGL memory size not configured");
            
            int memorySize = int.Parse(memoryMatch.Groups[1].Value);
            Assert.GreaterOrEqual(memorySize, 512, "WebGL memory size should be at least 512MB");
            Assert.AreEqual(1024, memorySize, "WebGL memory size should be 1024MB (1GB)");
        }

        [Test]
        public void WebGL_InteropMethods_MatchJavaScriptDefinitions()
        {
            string interopPath = "Assets/uPiper/Runtime/Core/Phonemizers/WebGL/WebGLInterop.cs";
            string content = File.ReadAllText(interopPath);
            
            // Extract DllImport method names
            var dllImportMatches = Regex.Matches(content, @"\[DllImport\(""__Internal""\)\]\s*\n\s*public static extern \w+ (\w+)");
            var csharpMethods = dllImportMatches.Cast<Match>()
                .Select(m => m.Groups[1].Value)
                .ToList();

            // Check each corresponding jslib file
            string[] jsLibFiles = Directory.GetFiles("Assets/uPiper/Plugins/WebGL", "*.jslib");
            var jsMethods = new List<string>();

            foreach (var jsFile in jsLibFiles)
            {
                string jsContent = File.ReadAllText(jsFile);
                var jsMethodMatches = Regex.Matches(jsContent, @"(\w+):\s*function\s*\(");
                jsMethods.AddRange(jsMethodMatches.Cast<Match>().Select(m => m.Groups[1].Value));
            }

            // Verify each C# method has corresponding JavaScript implementation
            foreach (var method in csharpMethods)
            {
                if (method != "FreeWebGLMemory") // This is a common utility method
                {
                    Assert.IsTrue(
                        jsMethods.Contains(method),
                        $"C# DllImport method '{method}' has no corresponding JavaScript implementation"
                    );
                }
            }
        }

        [Test]
        public void WebGL_PhonemizerClasses_ImplementRequiredInterface()
        {
            string[] phonemizerFiles = new[]
            {
                "Assets/uPiper/Runtime/Core/Phonemizers/WebGL/WebGLOpenJTalkPhonemizer.cs",
                "Assets/uPiper/Runtime/Core/Phonemizers/WebGL/WebGLESpeakPhonemizer.cs"
            };

            foreach (var file in phonemizerFiles)
            {
                Assert.IsTrue(File.Exists(file), $"{Path.GetFileName(file)} not found");
                
                string content = File.ReadAllText(file);
                string className = Path.GetFileNameWithoutExtension(file);
                
                // Check inheritance
                Assert.IsTrue(
                    content.Contains($"class {className} : PhonemizerBackendBase"),
                    $"{className} should inherit from PhonemizerBackendBase"
                );
                
                // Check required method overrides
                string[] requiredOverrides = new[]
                {
                    "InitializeInternalAsync",
                    "PhonemizeAsync",
                    "GetMemoryUsage",
                    "GetCapabilities",
                    "DisposeInternal"
                };

                foreach (var method in requiredOverrides)
                {
                    Assert.IsTrue(
                        content.Contains($"override") && content.Contains(method),
                        $"{className} missing required override: {method}"
                    );
                }
            }
        }

        [Test]
        public void WebGL_SamplePackageJson_IsValid()
        {
            string packageJsonPath = "Assets/uPiper/package.json";
            string content = File.ReadAllText(packageJsonPath);
            
            // Parse samples section
            var samplesMatch = Regex.Match(content, @"""samples""\s*:\s*\[(.*?)\]", RegexOptions.Singleline);
            Assert.IsTrue(samplesMatch.Success, "package.json missing samples section");
            
            string samplesContent = samplesMatch.Groups[1].Value;
            
            // Check for WebGL demo
            Assert.IsTrue(
                samplesContent.Contains("WebGL Demo"),
                "package.json missing WebGL Demo sample"
            );
            
            Assert.IsTrue(
                samplesContent.Contains("Samples~/WebGLDemo"),
                "package.json WebGL Demo has incorrect path"
            );
        }

        [Test]
        public void WebGL_AllMetaFilesExist()
        {
            // Check that all created files have corresponding .meta files
            string[] foldersToCheck = new[]
            {
                "Assets/uPiper/Plugins/WebGL",
                "Assets/uPiper/Runtime/Core/Phonemizers/WebGL",
                "Assets/WebGLTemplates/uPiper"
            };

            foreach (var folder in foldersToCheck)
            {
                if (!Directory.Exists(folder)) continue;
                
                var files = Directory.GetFiles(folder, "*", SearchOption.TopDirectoryOnly)
                    .Where(f => !f.EndsWith(".meta"));

                foreach (var file in files)
                {
                    string metaFile = file + ".meta";
                    Assert.IsTrue(
                        File.Exists(metaFile),
                        $"Missing .meta file for: {file}"
                    );
                }
            }
        }
    }
}