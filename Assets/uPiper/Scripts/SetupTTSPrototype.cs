using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace uPiper.Runtime
{
    public static class SetupTTSPrototype
    {
#if UNITY_EDITOR
        [MenuItem("uPiper/Setup Minimal TTS Prototype")]
        public static void SetupMinimalTTSScene()
        {
            // MinimalTTSPrototype を作成
            var minimalObject = new GameObject("MinimalTTSPrototype");
            var minimalTTS = minimalObject.AddComponent<MinimalTTSPrototype>();
            
            Debug.Log("Created MinimalTTSPrototype GameObject");
            Selection.activeGameObject = minimalObject;
            
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene()
            );
        }
        
        [MenuItem("uPiper/Setup Piper TTS Prototype")]
        public static void SetupPiperTTSScene()
        {
            // PiperTTSPrototype を作成
            var piperObject = new GameObject("PiperTTSPrototype");
            var piperTTS = piperObject.AddComponent<PiperTTSPrototype>();
            
            Debug.Log("Created PiperTTSPrototype GameObject");
            Debug.Log("Note: Make sure the ONNX model is in Resources folder");
            Selection.activeGameObject = piperObject;
            
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene()
            );
        }
#endif
    }
}