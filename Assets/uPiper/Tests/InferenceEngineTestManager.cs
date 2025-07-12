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
        private IWorker testWorker;

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
                // Step 1: Create a simple model
                testStatus = "Creating model...";
                Debug.Log("Step 1: Creating a simple neural network model");
                
                var inputs = FunctionalTensor.FromShape(new TensorShape(1, 4));
                var layer1 = Functional.Dense(4, 8)(inputs);
                var activation1 = Functional.Relu()(layer1);
                var outputs = Functional.Dense(8, 2)(activation1);
                
                testModel = Functional.Compile(
                    inputs,
                    outputs,
                    InputDef.FromTensor(inputs)
                );
                
                Debug.Log($"Model created successfully with {testModel.inputs.Count} inputs and {testModel.outputs.Count} outputs");
                
                yield return new WaitForSeconds(0.5f);
                
                // Step 2: Create worker
                testStatus = "Creating worker...";
                Debug.Log($"Step 2: Creating worker with backend: {preferredBackend}");
                
                testWorker = WorkerFactory.CreateWorker(preferredBackend, testModel);
                Debug.Log("Worker created successfully");
                
                yield return new WaitForSeconds(0.5f);
                
                // Step 3: Run inference
                testStatus = "Running inference...";
                Debug.Log("Step 3: Running inference with test data");
                
                var inputData = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
                using (var inputTensor = new Tensor(new TensorShape(1, 4), inputData))
                {
                    testWorker.SetInput(testModel.inputs[0].name, inputTensor);
                    testWorker.Schedule();
                    
                    // Get output
                    var outputTensor = testWorker.PeekOutput(testModel.outputs[0].name);
                    
                    // Log results
                    var outputData = outputTensor.ReadbackAndClone();
                    Debug.Log($"Input: [{string.Join(", ", inputData)}]");
                    Debug.Log($"Output shape: {outputTensor.shape}");
                    Debug.Log($"Output values: [{outputData[0]}, {outputData[1]}]");
                    
                    outputTensor.Dispose();
                }
                
                testStatus = "Test completed successfully!";
                testPassed = true;
                Debug.Log("=== Test Passed! Inference Engine is working correctly ===");
            }
            catch (System.Exception e)
            {
                testStatus = $"Test failed: {e.Message}";
                testPassed = false;
                Debug.LogError($"Test failed with error: {e}");
            }
            
            yield return new WaitForSeconds(1f);
            
            // Cleanup
            CleanupResources();
        }

        private void CleanupResources()
        {
            if (testWorker != null)
            {
                testWorker.Dispose();
                testWorker = null;
            }
            
            if (testModel != null)
            {
                testModel.Dispose();
                testModel = null;
            }
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