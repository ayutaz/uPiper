using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;

namespace uPiper.Tests.Editor
{
    /// <summary>
    /// M2: Unity WebGL OpenJTalkモジュールの検証テスト
    /// Unity Editorで実行して、ビルド成果物の配置と内容を確認
    /// </summary>
    public class WebGLModuleValidationTest
    {
        private const string JS_FILE = "Assets/StreamingAssets/openjtalk-unity.js";
        private const string WASM_FILE = "Assets/StreamingAssets/openjtalk-unity.wasm";

        [Test]
        public void OpenJTalkUnity_JSファイルが存在する()
        {
            Assert.IsTrue(File.Exists(JS_FILE), 
                $"openjtalk-unity.js が見つかりません。\nパス: {JS_FILE}\n" +
                "piper-plus/dist/ からコピーしてください。");
            
            var fileInfo = new FileInfo(JS_FILE);
            Debug.Log($"✓ openjtalk-unity.js が存在: {fileInfo.Length:N0} bytes");
        }

        [Test]
        public void OpenJTalkUnity_WASMファイルが存在する()
        {
            Assert.IsTrue(File.Exists(WASM_FILE), 
                $"openjtalk-unity.wasm が見つかりません。\nパス: {WASM_FILE}\n" +
                "piper-plus/dist/ からコピーしてください。");
            
            var fileInfo = new FileInfo(WASM_FILE);
            Debug.Log($"✓ openjtalk-unity.wasm が存在: {fileInfo.Length:N0} bytes");
        }

        [Test]
        public void OpenJTalkUnity_JSファイルサイズが適切()
        {
            if (!File.Exists(JS_FILE))
            {
                Assert.Ignore("JSファイルが存在しません");
                return;
            }

            var fileInfo = new FileInfo(JS_FILE);
            var sizeKB = fileInfo.Length / 1024.0;
            
            Assert.Greater(sizeKB, 50, "JSファイルが小さすぎます（50KB未満）");
            Assert.Less(sizeKB, 500, "JSファイルが大きすぎます（500KB超）");
            
            Debug.Log($"✓ JSファイルサイズ: {sizeKB:F1} KB（適切な範囲内）");
        }

        [Test]
        public void OpenJTalkUnity_WASMファイルサイズが適切()
        {
            if (!File.Exists(WASM_FILE))
            {
                Assert.Ignore("WASMファイルが存在しません");
                return;
            }

            var fileInfo = new FileInfo(WASM_FILE);
            var sizeKB = fileInfo.Length / 1024.0;
            
            Assert.Greater(sizeKB, 5, "WASMファイルが小さすぎます（5KB未満）");
            Assert.Less(sizeKB, 1000, "WASMファイルが大きすぎます（1MB超）");
            
            Debug.Log($"✓ WASMファイルサイズ: {sizeKB:F1} KB（適切な範囲内）");
        }

        [Test]
        public void OpenJTalkUnity_必要なエクスポートが含まれている()
        {
            if (!File.Exists(JS_FILE))
            {
                Assert.Ignore("JSファイルが存在しません");
                return;
            }

            var jsContent = File.ReadAllText(JS_FILE);
            
            // モジュール名の確認
            Assert.IsTrue(jsContent.Contains("OpenJTalkModule"), 
                "OpenJTalkModule が見つかりません");
            
            // HEAP配列の確認（Emscripten 3.1.39では自動的に利用可能）
            Assert.IsTrue(jsContent.Contains("HEAP"), 
                "HEAP配列の参照が見つかりません");
            
            // 必要な関数の確認
            var requiredFunctions = new[]
            {
                "_Open_JTalk_initialize",
                "_Open_JTalk_clear",
                "_Open_JTalk_load",
                "_Open_JTalk_synthesis",
                "_malloc",
                "_free"
            };
            
            foreach (var func in requiredFunctions)
            {
                Assert.IsTrue(jsContent.Contains(func), 
                    $"必要な関数 {func} が見つかりません");
            }
            
            Debug.Log("✓ 必要なエクスポートが全て含まれています");
        }

        [Test]
        public void OpenJTalkUnity_Unityモジュールとの競合がない()
        {
            if (!File.Exists(JS_FILE))
            {
                Assert.Ignore("JSファイルが存在しません");
                return;
            }

            var jsContent = File.ReadAllText(JS_FILE);
            
            // グローバルなModuleを上書きしていないことを確認
            Assert.IsFalse(jsContent.StartsWith("Module = {") || 
                          jsContent.StartsWith("var Module = {") ||
                          jsContent.Contains("\nModule = {"),
                "グローバルなModuleを上書きしています（Unity互換性問題）");
            
            // OpenJTalkModuleという独自の名前空間を使用
            Assert.IsTrue(jsContent.Contains("OpenJTalkModule"), 
                "OpenJTalkModule名前空間が見つかりません");
            
            Debug.Log("✓ Unity Moduleとの競合なし（OpenJTalkModule名前空間を使用）");
        }

        [Test]
        public void StreamingAssetsメタファイルが存在する()
        {
            var metaFiles = new[]
            {
                JS_FILE + ".meta",
                WASM_FILE + ".meta"
            };
            
            foreach (var metaFile in metaFiles)
            {
                if (!File.Exists(metaFile))
                {
                    Debug.LogWarning($"メタファイルが存在しません: {metaFile}\n" +
                                   "Unity Editorでファイルをリフレッシュしてください");
                }
                else
                {
                    Debug.Log($"✓ メタファイル存在: {Path.GetFileName(metaFile)}");
                }
            }
        }

        [Test]
        [Category("Summary")]
        public void M2成果物検証サマリー()
        {
            Debug.Log("\n=== M2 Unity Editor 検証結果 ===");
            
            bool jsExists = File.Exists(JS_FILE);
            bool wasmExists = File.Exists(WASM_FILE);
            
            if (jsExists && wasmExists)
            {
                var jsInfo = new FileInfo(JS_FILE);
                var wasmInfo = new FileInfo(WASM_FILE);
                
                Debug.Log($"✅ openjtalk-unity.js: {jsInfo.Length:N0} bytes");
                Debug.Log($"✅ openjtalk-unity.wasm: {wasmInfo.Length:N0} bytes");
                Debug.Log($"✅ 配置場所: Assets/StreamingAssets/");
                Debug.Log($"✅ Unity WebGL互換モジュール準備完了");
                
                Debug.Log("\n📝 次のステップ（M3）:");
                Debug.Log("1. Unity統合ラッパー（openjtalk-unity-wrapper.js）の作成");
                Debug.Log("2. Unity JSLib（.jslib）の実装");
                Debug.Log("3. C#インターフェースクラスの実装");
            }
            else
            {
                Debug.LogError("❌ 必要なファイルが不足しています");
                if (!jsExists) Debug.LogError("  - openjtalk-unity.js");
                if (!wasmExists) Debug.LogError("  - openjtalk-unity.wasm");
                Debug.LogError("\npiper-plus/dist/ からファイルをコピーしてください");
            }
        }
    }
}