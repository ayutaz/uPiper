using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace uPiper.Tests
{
    /// <summary>
    /// テストシーンのセットアップヘルパー
    /// </summary>
    public static class SetupTestScene
    {
#if UNITY_EDITOR
        [MenuItem("uPiper/Setup Test Scene")]
        public static void SetupInferenceEngineTestScene()
        {
            // 既存の InferenceEngineTestManager を探す
            var existing = GameObject.FindObjectOfType<InferenceEngineTestManager>();
            if (existing != null)
            {
                Debug.Log("InferenceEngineTestManager already exists in the scene.");
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            // 新しい GameObject を作成
            var testManagerObject = new GameObject("InferenceEngineTestManager");
            var testManager = testManagerObject.AddComponent<InferenceEngineTestManager>();
            
            // デフォルト設定
            // runTestOnStart = true (自動実行)
            // preferredBackend = BackendType.GPUCompute
            
            Debug.Log("Created InferenceEngineTestManager GameObject.");
            Selection.activeGameObject = testManagerObject;
            
            // シーンを dirty にマーク
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene()
            );
        }
        
        [MenuItem("uPiper/Run Inference Engine Test")]
        public static void RunInferenceEngineTest()
        {
            var testManager = GameObject.FindObjectOfType<InferenceEngineTestManager>();
            if (testManager == null)
            {
                Debug.LogError("InferenceEngineTestManager not found in scene. Please run 'uPiper/Setup Test Scene' first.");
                return;
            }
            
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Please enter Play Mode to run the test.");
                return;
            }
            
            testManager.RunTest();
        }
#endif
    }
}