using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;

namespace uPiper.Tests.Editor
{
    /// <summary>
    /// M5: Unity WebGLビルドとローカルテストの検証
    /// </summary>
    public class M5WebGLBuildTest
    {
        private const string PHONEMIZER_FILE = "Assets/uPiper/Runtime/Core/Phonemizers/WebGL/WebGLOpenJTalkUnityPhonemizer.cs";
        private const string JSLIB_FILE = "Assets/uPiper/Plugins/WebGL/openjtalk_unity.jslib";
        private const string WRAPPER_JS = "Assets/StreamingAssets/openjtalk-unity-wrapper.js";
        private const string MODULE_JS = "Assets/StreamingAssets/openjtalk-unity.js";
        private const string MODULE_WASM = "Assets/StreamingAssets/openjtalk-unity.wasm";

        [Test]
        public void M5_WebGL音素化クラスが存在する()
        {
            Assert.IsTrue(File.Exists(PHONEMIZER_FILE), 
                $"WebGLOpenJTalkUnityPhonemizer.cs が見つかりません。\nパス: {PHONEMIZER_FILE}");
            
            var fileInfo = new FileInfo(PHONEMIZER_FILE);
            Debug.Log($"✓ WebGLOpenJTalkUnityPhonemizer.cs が存在: {fileInfo.Length:N0} bytes");
        }

        [Test]
        public void M5_WebGL音素化クラスが必要なメソッドを含む()
        {
            if (!File.Exists(PHONEMIZER_FILE))
            {
                Assert.Ignore("音素化クラスファイルが存在しません");
                return;
            }

            var content = File.ReadAllText(PHONEMIZER_FILE);
            
            // 必要なメソッドの確認
            Assert.IsTrue(content.Contains("InitializeAsync"), "InitializeAsyncメソッドが見つかりません");
            Assert.IsTrue(content.Contains("TextToPhonemesAsync"), "TextToPhonemesAsyncメソッドが見つかりません");
            Assert.IsTrue(content.Contains("IsInitialized"), "IsInitializedメソッドが見つかりません");
            Assert.IsTrue(content.Contains("Dispose"), "Disposeメソッドが見つかりません");
            Assert.IsTrue(content.Contains("LogDebugInfo"), "LogDebugInfoメソッドが見つかりません");
            
            // DllImport宣言の確認
            Assert.IsTrue(content.Contains("InitializeOpenJTalkUnity"), "InitializeOpenJTalkUnity DllImportが見つかりません");
            Assert.IsTrue(content.Contains("PhonemizeWithOpenJTalk"), "PhonemizeWithOpenJTalk DllImportが見つかりません");
            Assert.IsTrue(content.Contains("GetOpenJTalkDebugInfo"), "GetOpenJTalkDebugInfo DllImportが見つかりません");
            
            Debug.Log("✓ WebGL音素化クラスに必要なメソッドが全て含まれています");
        }

        [Test]
        public void M5_JSLibファイルが存在する()
        {
            Assert.IsTrue(File.Exists(JSLIB_FILE), 
                $"openjtalk_unity.jslib が見つかりません。\nパス: {JSLIB_FILE}");
            
            var fileInfo = new FileInfo(JSLIB_FILE);
            Debug.Log($"✓ openjtalk_unity.jslib が存在: {fileInfo.Length:N0} bytes");
        }

        [Test]
        public void M5_StreamingAssetsにモジュールファイルが存在する()
        {
            Assert.IsTrue(File.Exists(MODULE_JS), 
                $"openjtalk-unity.js が見つかりません。\nパス: {MODULE_JS}");
            Assert.IsTrue(File.Exists(MODULE_WASM), 
                $"openjtalk-unity.wasm が見つかりません。\nパス: {MODULE_WASM}");
            Assert.IsTrue(File.Exists(WRAPPER_JS), 
                $"openjtalk-unity-wrapper.js が見つかりません。\nパス: {WRAPPER_JS}");
            
            var jsInfo = new FileInfo(MODULE_JS);
            var wasmInfo = new FileInfo(MODULE_WASM);
            var wrapperInfo = new FileInfo(WRAPPER_JS);
            
            Debug.Log($"✓ openjtalk-unity.js: {jsInfo.Length:N0} bytes");
            Debug.Log($"✓ openjtalk-unity.wasm: {wasmInfo.Length:N0} bytes");
            Debug.Log($"✓ openjtalk-unity-wrapper.js: {wrapperInfo.Length:N0} bytes");
        }

        [Test]
        public void M5_WebGLビルド設定が正しい()
        {
            var buildTarget = EditorUserBuildSettings.activeBuildTarget;
            var buildTargetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);
            
            Debug.Log($"現在のビルドターゲット: {buildTarget}");
            Debug.Log($"ビルドターゲットグループ: {buildTargetGroup}");
            
            // WebGLビルド設定の推奨
            if (buildTarget == BuildTarget.WebGL)
            {
                Debug.Log("✓ WebGLビルドターゲットが選択されています");
                
                // Player設定の確認
                var compressionFormat = PlayerSettings.WebGL.compressionFormat;
                var template = PlayerSettings.WebGL.template;
                
                Debug.Log($"  圧縮形式: {compressionFormat}");
                Debug.Log($"  テンプレート: {template}");
                
                // 推奨設定
                if (compressionFormat == WebGLCompressionFormat.Gzip)
                {
                    Debug.Log("  ✓ Gzip圧縮が有効です（推奨）");
                }
                else
                {
                    Debug.LogWarning($"  ⚠ 圧縮形式が{compressionFormat}です。Gzipが推奨されます");
                }
            }
            else
            {
                Debug.LogWarning($"⚠ 現在のビルドターゲットは{buildTarget}です。WebGLに切り替えてください");
            }
        }

        [Test]
        public void M5_モジュールサイズが適切()
        {
            if (!File.Exists(MODULE_JS) || !File.Exists(MODULE_WASM))
            {
                Assert.Ignore("モジュールファイルが存在しません");
                return;
            }

            var jsSize = new FileInfo(MODULE_JS).Length;
            var wasmSize = new FileInfo(MODULE_WASM).Length;
            
            // サイズチェック（最小実装なので小さいはず）
            Assert.Less(jsSize, 100 * 1024, "JSファイルが100KB以下であること");
            Assert.Less(wasmSize, 20 * 1024, "WASMファイルが20KB以下であること");
            
            Debug.Log($"✓ モジュールサイズが適切です");
            Debug.Log($"  JS: {jsSize:N0} bytes");
            Debug.Log($"  WASM: {wasmSize:N0} bytes");
        }

        [Test]
        [Category("Summary")]
        public void M5_WebGLビルド準備状況サマリー()
        {
            Debug.Log("\n=== M5 Unity WebGLビルド準備状況 ===");
            
            bool phonemizerExists = File.Exists(PHONEMIZER_FILE);
            bool jslibExists = File.Exists(JSLIB_FILE);
            bool moduleJsExists = File.Exists(MODULE_JS);
            bool moduleWasmExists = File.Exists(MODULE_WASM);
            bool wrapperExists = File.Exists(WRAPPER_JS);
            
            if (phonemizerExists && jslibExists && moduleJsExists && moduleWasmExists && wrapperExists)
            {
                Debug.Log("✅ 全ファイル準備完了");
                
                Debug.Log("\n📝 WebGLビルド手順:");
                Debug.Log("1. File > Build Settings を開く");
                Debug.Log("2. Platform: WebGL を選択");
                Debug.Log("3. Switch Platform をクリック");
                Debug.Log("4. Player Settings で以下を確認:");
                Debug.Log("   - Compression Format: Gzip");
                Debug.Log("   - Publishing Settings > Compression Format: Gzip");
                Debug.Log("5. Build をクリック");
                
                Debug.Log("\n📝 ローカルテスト手順:");
                Debug.Log("1. ビルド出力フォルダで以下を実行:");
                Debug.Log("   python -m http.server 8000");
                Debug.Log("2. ブラウザで http://localhost:8000 を開く");
                Debug.Log("3. Developer Consoleで以下を確認:");
                Debug.Log("   - [Unity] Initializing OpenJTalk Unity integration...");
                Debug.Log("   - [Unity] OpenJTalk Unity integration ready");
                Debug.Log("4. コンソールで音素化テスト:");
                Debug.Log("   window.OpenJTalkUnityAPI.phonemize('こんにちは')");
                
                Debug.Log("\n✅ M5準備完了 - WebGLビルドを実行できます");
            }
            else
            {
                Debug.LogError("❌ 必要なファイルが不足しています");
                if (!phonemizerExists) Debug.LogError("  - WebGLOpenJTalkUnityPhonemizer.cs");
                if (!jslibExists) Debug.LogError("  - openjtalk_unity.jslib");
                if (!moduleJsExists) Debug.LogError("  - openjtalk-unity.js");
                if (!moduleWasmExists) Debug.LogError("  - openjtalk-unity.wasm");
                if (!wrapperExists) Debug.LogError("  - openjtalk-unity-wrapper.js");
            }
        }
    }
}