using System.Collections;
using Unity.InferenceEngine;
using UnityEngine;

namespace uPiper.Tests
{
    /// <summary>
    /// Inference Engine の動作確認用マネージャー
    /// シーンで実行してテストする
    /// </summary>
    public class InferenceEngineTestManager : MonoBehaviour
    {
        [Header("Test Settings")]
        [SerializeField] private bool runTestOnStart = true;
        [SerializeField] private BackendType preferredBackend = BackendType.GPUCompute;
        
        [Header("Test Results")]
        [SerializeField] private bool testPassed = false;
        [SerializeField] private string testStatus = "Not started";
        
        private Model testModel;
        private Worker testWorker;

        void Start()
        {
            if (runTestOnStart)
            {
                StartCoroutine(RunInferenceEngineTest());
            }
        }

        void OnDestroy()
        {
            CleanupResources();
        }

        [ContextMenu("Run Test")]
        public void RunTest()
        {
            StartCoroutine(RunInferenceEngineTest());
        }

        private IEnumerator RunInferenceEngineTest()
        {
            testStatus = "Starting Inference Engine test...";
            Debug.Log("=== Unity AI Interface (Inference Engine) Test ===");
            
            yield return new WaitForSeconds(0.5f);
            
            try
            {
                // Step 1: Test Tensor creation
                testStatus = "Testing Tensor creation...";
                Debug.Log("Step 1: Creating test tensors");
                
                var tensorShape = new TensorShape(1, 4);
                var inputData = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
                
                using (var testTensor = new Tensor<float>(tensorShape, inputData))
                {
                    Debug.Log($"Tensor created successfully with shape: {testTensor.shape}");
                    Debug.Log($"Tensor data: [{testTensor[0]}, {testTensor[1]}, {testTensor[2]}, {testTensor[3]}]");
                }
                
                yield return new WaitForSeconds(0.5f);
                
                // Step 2: Check available backends
                testStatus = "Checking available backends...";
                Debug.Log("Step 2: Enumerating available backends");
                
                var backends = System.Enum.GetValues(typeof(BackendType));
                foreach (BackendType backend in backends)
                {
                    Debug.Log($"Available backend: {backend}");
                }
                
                yield return new WaitForSeconds(0.5f);
                
                // Step 3: Model loading test (requires actual ONNX model)
                testStatus = "Model loading test...";
                Debug.Log("Step 3: Model loading test");
                
                // Note: Actual model loading requires a valid ONNX file in Resources folder
                // Example: var modelAsset = Resources.Load("model-name") as ModelAsset;
                //         testModel = ModelLoader.Load(modelAsset);
                //         testWorker = new Worker(testModel, preferredBackend);
                
                Debug.LogWarning("Model loading skipped - requires valid ONNX model in Resources folder");
                Debug.Log("To test full inference: Add a valid .onnx file to Resources folder");
                
                testStatus = "Basic tests completed!";
                testPassed = true;
                Debug.Log("=== Basic Tests Passed! Inference Engine package is properly installed ===");
            }
            catch (System.Exception e)
            {
                testStatus = $"Test failed: {e.Message}";
                testPassed = false;
                Debug.LogError($"Test failed with error: {e}");
            }
            
            yield return new WaitForSeconds(1f);
        }

        private void CleanupResources()
        {
            testWorker?.Dispose();
            testWorker = null;
            
            testModel?.Dispose();
            testModel = null;
        }

        void OnGUI()
        {
            // Simple GUI for test status
            GUILayout.BeginArea(new Rect(10, 10, 400, 200));
            GUILayout.Label("Inference Engine Test Status", GUI.skin.box);
            GUILayout.Space(10);
            
            GUILayout.Label($"Status: {testStatus}");
            GUILayout.Label($"Test Passed: {(testPassed ? "Yes" : "No")}");
            GUILayout.Label($"Backend: {preferredBackend}");
            
            GUILayout.Space(10);
            
            if (GUILayout.Button("Run Test"))
            {
                RunTest();
            }
            
            GUILayout.EndArea();
        }
    }
}