using UnityEditor;
using UnityEngine;

namespace uPiper.Editor
{
    /// <summary>
    /// OpenJTalkの有効/無効を切り替えるユーティリティ
    /// </summary>
    public static class ToggleOpenJTalk
    {
        private const string DISABLE_OPENJTALK_KEY = "uPiper_DisableOpenJTalk";
        
        [MenuItem("uPiper/Debug/Toggle OpenJTalk (Currently: Enabled)", true)]
        private static bool ValidateToggleOpenJTalkEnabled()
        {
            return !IsOpenJTalkDisabled();
        }
        
        [MenuItem("uPiper/Debug/Toggle OpenJTalk (Currently: Enabled)")]
        private static void ToggleOpenJTalkEnabled()
        {
            SetOpenJTalkDisabled(true);
        }
        
        [MenuItem("uPiper/Debug/Toggle OpenJTalk (Currently: Disabled)", true)]
        private static bool ValidateToggleOpenJTalkDisabled()
        {
            return IsOpenJTalkDisabled();
        }
        
        [MenuItem("uPiper/Debug/Toggle OpenJTalk (Currently: Disabled)")]
        private static void ToggleOpenJTalkDisabled()
        {
            SetOpenJTalkDisabled(false);
        }
        
        private static bool IsOpenJTalkDisabled()
        {
            return EditorPrefs.GetBool(DISABLE_OPENJTALK_KEY, false);
        }
        
        private static void SetOpenJTalkDisabled(bool disabled)
        {
            EditorPrefs.SetBool(DISABLE_OPENJTALK_KEY, disabled);
            
            if (disabled)
            {
                Debug.Log("[uPiper] OpenJTalk disabled - Using simple phoneme mapping (Phase 1.9 mode)");
            }
            else
            {
                Debug.Log("[uPiper] OpenJTalk enabled - Using OpenJTalk for Japanese text (Phase 1.10 mode)");
            }
        }
        
        [MenuItem("uPiper/Debug/Force Regenerate InferenceEngineDemo Scene")]
        private static void ForceRegenerateScene()
        {
            // Delete the current scene if it exists
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/uPiper/Scenes/InferenceEngineDemo.unity") != null)
            {
                AssetDatabase.DeleteAsset("Assets/uPiper/Scenes/InferenceEngineDemo.unity");
                AssetDatabase.Refresh();
                Debug.Log("Deleted existing InferenceEngineDemo scene");
            }
            
            // Trigger scene creation
            CreateInferenceDemoScene.CreateDemoScene();
            Debug.Log("InferenceEngineDemo scene regenerated");
        }
    }
}