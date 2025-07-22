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

        [MenuItem("uPiper/Debug/OpenJTalk/Toggle Enabled State")]
        private static void ToggleEnabledState()
        {
            SetOpenJTalkDisabled(!IsOpenJTalkDisabled());
        }

        public static bool IsOpenJTalkEnabled()
        {
            return !IsOpenJTalkDisabled();
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

    }
}