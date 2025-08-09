using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;

namespace uPiper.Tests.Editor
{
    /// <summary>
    /// M3: Unity統合ラッパー実装の検証テスト
    /// </summary>
    public class M3IntegrationTest
    {
        private const string WRAPPER_FILE = "Assets/StreamingAssets/openjtalk-unity-wrapper.js";
        private const string JSLIB_FILE = "Assets/uPiper/Plugins/WebGL/openjtalk_unity.jslib";
        private const string PHONEMIZER_FILE = "Assets/uPiper/Runtime/Core/Phonemizers/WebGL/WebGLOpenJTalkUnityPhonemizer.cs";

        [Test]
        public void M3_Unity統合ラッパーが存在する()
        {
            Assert.IsTrue(File.Exists(WRAPPER_FILE), 
                $"openjtalk-unity-wrapper.js が見つかりません。\nパス: {WRAPPER_FILE}");
            
            var fileInfo = new FileInfo(WRAPPER_FILE);
            Debug.Log($"✓ openjtalk-unity-wrapper.js が存在: {fileInfo.Length:N0} bytes");
        }

        [Test]
        public void M3_JSLibファイルが存在する()
        {
            Assert.IsTrue(File.Exists(JSLIB_FILE), 
                $"openjtalk_unity.jslib が見つかりません。\nパス: {JSLIB_FILE}");
            
            var fileInfo = new FileInfo(JSLIB_FILE);
            Debug.Log($"✓ openjtalk_unity.jslib が存在: {fileInfo.Length:N0} bytes");
        }

        [Test]
        public void M3_WebGL音素化クラスが存在する()
        {
            Assert.IsTrue(File.Exists(PHONEMIZER_FILE), 
                $"WebGLOpenJTalkUnityPhonemizer.cs が見つかりません。\nパス: {PHONEMIZER_FILE}");
            
            var fileInfo = new FileInfo(PHONEMIZER_FILE);
            Debug.Log($"✓ WebGLOpenJTalkUnityPhonemizer.cs が存在: {fileInfo.Length:N0} bytes");
        }

        [Test]
        public void M3_ラッパーが必要な関数を含む()
        {
            if (!File.Exists(WRAPPER_FILE))
            {
                Assert.Ignore("ラッパーファイルが存在しません");
                return;
            }

            var content = File.ReadAllText(WRAPPER_FILE);
            
            // 必要な関数の確認
            Assert.IsTrue(content.Contains("initialize"), "initialize関数が見つかりません");
            Assert.IsTrue(content.Contains("phonemize"), "phonemize関数が見つかりません");
            Assert.IsTrue(content.Contains("isReady"), "isReady関数が見つかりません");
            Assert.IsTrue(content.Contains("dispose"), "dispose関数が見つかりません");
            Assert.IsTrue(content.Contains("isGitHubPages"), "isGitHubPages関数が見つかりません");
            Assert.IsTrue(content.Contains("adjustPathForGitHubPages"), "adjustPathForGitHubPages関数が見つかりません");
            
            Debug.Log("✓ ラッパーに必要な関数が全て含まれています");
        }

        [Test]
        public void M3_JSLibが必要な関数をエクスポート()
        {
            if (!File.Exists(JSLIB_FILE))
            {
                Assert.Ignore("JSLibファイルが存在しません");
                return;
            }

            var content = File.ReadAllText(JSLIB_FILE);
            
            // エクスポート関数の確認
            Assert.IsTrue(content.Contains("InitializeOpenJTalkUnity"), "InitializeOpenJTalkUnity関数が見つかりません");
            Assert.IsTrue(content.Contains("IsOpenJTalkUnityInitialized"), "IsOpenJTalkUnityInitialized関数が見つかりません");
            Assert.IsTrue(content.Contains("PhonemizeWithOpenJTalk"), "PhonemizeWithOpenJTalk関数が見つかりません");
            Assert.IsTrue(content.Contains("FreeOpenJTalkMemory"), "FreeOpenJTalkMemory関数が見つかりません");
            Assert.IsTrue(content.Contains("DisposeOpenJTalkUnity"), "DisposeOpenJTalkUnity関数が見つかりません");
            Assert.IsTrue(content.Contains("GetOpenJTalkDebugInfo"), "GetOpenJTalkDebugInfo関数が見つかりません");
            
            Debug.Log("✓ JSLibに必要な関数が全てエクスポートされています");
        }

        [Test]
        public void M3_PUA文字マッピングが実装されている()
        {
            if (!File.Exists(WRAPPER_FILE))
            {
                Assert.Ignore("ラッパーファイルが存在しません");
                return;
            }

            var content = File.ReadAllText(WRAPPER_FILE);
            
            // PUA文字マッピングの確認
            Assert.IsTrue(content.Contains("MULTI_CHAR_PHONEMES"), "MULTI_CHAR_PHONEMESマッピングが見つかりません");
            Assert.IsTrue(content.Contains("'ch': '\\ue001'"), "chのPUAマッピングが見つかりません");
            Assert.IsTrue(content.Contains("'ky': '\\ue006'"), "kyのPUAマッピングが見つかりません");
            Assert.IsTrue(content.Contains("'sh': '\\ue00b'"), "shのPUAマッピングが見つかりません");
            Assert.IsTrue(content.Contains("'ts': '\\ue00c'"), "tsのPUAマッピングが見つかりません");
            
            Debug.Log("✓ PUA文字マッピングが正しく実装されています");
        }

        [Test]
        [Category("Summary")]
        public void M3成果物検証サマリー()
        {
            Debug.Log("\n=== M3 Unity統合ラッパー 検証結果 ===");
            
            bool wrapperExists = File.Exists(WRAPPER_FILE);
            bool jslibExists = File.Exists(JSLIB_FILE);
            bool phonemizerExists = File.Exists(PHONEMIZER_FILE);
            
            if (wrapperExists && jslibExists && phonemizerExists)
            {
                var wrapperInfo = new FileInfo(WRAPPER_FILE);
                var jslibInfo = new FileInfo(JSLIB_FILE);
                var phonemizerInfo = new FileInfo(PHONEMIZER_FILE);
                
                Debug.Log($"✅ openjtalk-unity-wrapper.js: {wrapperInfo.Length:N0} bytes");
                Debug.Log($"✅ openjtalk_unity.jslib: {jslibInfo.Length:N0} bytes");
                Debug.Log($"✅ WebGLOpenJTalkUnityPhonemizer.cs: {phonemizerInfo.Length:N0} bytes");
                Debug.Log($"✅ Unity統合レイヤー準備完了");
                
                Debug.Log("\n📝 M3完了内容:");
                Debug.Log("1. JavaScript統合ラッパー実装");
                Debug.Log("2. Unity JSLib（C#-JS橋渡し）実装");
                Debug.Log("3. WebGL音素化クラス実装");
                Debug.Log("4. PUA文字マッピング実装");
                Debug.Log("5. GitHub Pages対応実装");
                
                Debug.Log("\n📝 次のステップ（M4以降）:");
                Debug.Log("1. Unity WebGLビルドの実行");
                Debug.Log("2. ローカルサーバーでのテスト");
                Debug.Log("3. GitHub Pagesへのデプロイ");
            }
            else
            {
                Debug.LogError("❌ 必要なファイルが不足しています");
                if (!wrapperExists) Debug.LogError("  - openjtalk-unity-wrapper.js");
                if (!jslibExists) Debug.LogError("  - openjtalk_unity.jslib");
                if (!phonemizerExists) Debug.LogError("  - WebGLOpenJTalkUnityPhonemizer.cs");
            }
        }
    }
}