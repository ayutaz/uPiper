using UnityEngine;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace uPiper.Tests.Editor
{
    /// <summary>
    /// Setup for running uPiper tests in Unity Test Runner
    /// </summary>
    public static class TestRunnerSetup
    {
        [MenuItem("uPiper/Tests/Run All Tests")]
        public static void RunAllTests()
        {
            Debug.Log("[uPiper] Starting all tests...");
            
            var testRunnerApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            var filter = new Filter()
            {
                testMode = TestMode.PlayMode | TestMode.EditMode,
                assemblyNames = new[] { "uPiper.Tests" }
            };
            
            testRunnerApi.Execute(new ExecutionSettings(filter));
        }
        
        [MenuItem("uPiper/Tests/Run Integration Tests")]
        public static void RunIntegrationTests()
        {
            Debug.Log("[uPiper] Starting integration tests...");
            
            var testRunnerApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            var filter = new Filter()
            {
                testMode = TestMode.PlayMode,
                testNames = new[] { "uPiper.Tests.Integration" }
            };
            
            testRunnerApi.Execute(new ExecutionSettings(filter));
        }
        
        [MenuItem("uPiper/Tests/Open Test Runner")]
        public static void OpenTestRunner()
        {
            EditorApplication.ExecuteMenuItem("Window/General/Test Runner");
        }
        
        [MenuItem("uPiper/Tests/Validate Test Models")]
        public static void ValidateTestModels()
        {
            var modelsPath = System.IO.Path.Combine(Application.streamingAssetsPath, "uPiper", "Models");
            
            if (!System.IO.Directory.Exists(modelsPath))
            {
                Debug.LogError($"[uPiper] Models directory not found: {modelsPath}");
                return;
            }
            
            var jaModel = System.IO.Path.Combine(modelsPath, "ja_JP-test-medium.onnx");
            var enModel = System.IO.Path.Combine(modelsPath, "test_voice.onnx");
            
            bool allValid = true;
            
            if (!System.IO.File.Exists(jaModel))
            {
                Debug.LogError($"[uPiper] Japanese model not found: {jaModel}");
                allValid = false;
            }
            else
            {
                var size = new System.IO.FileInfo(jaModel).Length / 1024 / 1024;
                Debug.Log($"[uPiper] Japanese model found: {jaModel} ({size}MB)");
            }
            
            if (!System.IO.File.Exists(enModel))
            {
                Debug.LogError($"[uPiper] English model not found: {enModel}");
                allValid = false;
            }
            else
            {
                var size = new System.IO.FileInfo(enModel).Length / 1024 / 1024;
                Debug.Log($"[uPiper] English model found: {enModel} ({size}MB)");
            }
            
            if (allValid)
            {
                Debug.Log("[uPiper] All test models validated successfully!");
            }
        }
    }
}