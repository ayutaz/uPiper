using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace uPiper.Editor
{
    /// <summary>
    /// Samples~ディレクトリ内のシーンを開くためのメニュー
    /// WebGLは現在サポートされていないため、関連メニューは削除されています
    /// </summary>
    public static class SampleSceneOpener
    {
        // WebGL関連の定数とメソッドは将来の実装のために保持（コメントアウト）
        
        /*
        private const string WEBGL_DEMO_SCENE_PATH = "Assets/uPiper/Samples~/WebGLDemo/WebGLDemoScene.unity";

        [MenuItem("uPiper/Demo/Open WebGL Demo Scene", false, 110)]
        public static void OpenWebGLDemoScene()
        {
            // 現在のシーンに変更がある場合は保存を促す
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            // 以下、WebGLデモシーンを開く処理（現在は無効）
        }
        */

        // 他のサンプルシーン用のメソッドをここに追加可能
    }
}