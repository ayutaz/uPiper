using UnityEditor;
using UnityEngine;

namespace uPiper.Editor
{
    /// <summary>
    /// プロジェクト起動時にデモシーンの存在を確認
    /// </summary>
    [InitializeOnLoad]
    public static class InitializeScenes
    {
        private const string ScenePath = "Assets/uPiper/Scenes/InferenceEngineDemo.unity";
        private const string CheckedKey = "uPiper_InferenceDemoScene_Checked";

        static InitializeScenes()
        {
            // 一度チェック済みなら何もしない
            if (EditorPrefs.GetBool(CheckedKey, false))
                return;

            // 少し遅延してからチェック（エディタの初期化を待つ）
            EditorApplication.delayCall += CheckDemoScene;
        }

        private static void CheckDemoScene()
        {
            if (!System.IO.File.Exists(ScenePath))
            {
                var result = EditorUtility.DisplayDialogComplex(
                    "uPiper デモシーン",
                    "Inference Engine デモシーンがまだ作成されていません。\n今すぐ作成しますか？",
                    "作成する",
                    "後で",
                    "今後表示しない"
                );

                switch (result)
                {
                    case 0: // 作成する
                        Debug.LogError("デモシーンの自動作成は無効化されました。シーンは手動で管理してください。");
                        EditorPrefs.SetBool(CheckedKey, true);
                        break;
                    case 1: // 後で
                        // 何もしない（次回起動時に再度確認）
                        break;
                    case 2: // 今後表示しない
                        EditorPrefs.SetBool(CheckedKey, true);
                        break;
                }
            }
            else
            {
                // シーンが存在する場合はチェック済みにする
                EditorPrefs.SetBool(CheckedKey, true);
            }
        }

    }
}