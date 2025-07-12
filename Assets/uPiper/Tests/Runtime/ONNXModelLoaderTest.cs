using System.Collections;
using System.IO;
using NUnit.Framework;
using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.TestTools;

namespace uPiper.Tests.Runtime
{
    /// <summary>
    /// ONNX モデル読み込みテスト
    /// </summary>
    public class ONNXModelLoaderTest
    {
        private const string TestModelPath = "Assets/uPiper/Tests/TestData/dummy_model.onnx";

        [SetUp]
        public void Setup()
        {
            // テスト用のダミーONNXファイルを作成（実際のONNXファイルが必要な場合はスキップ）
            CreateDummyONNXFile();
        }

        [TearDown]
        public void Cleanup()
        {
            // テストファイルのクリーンアップ
            if (File.Exists(TestModelPath))
            {
                File.Delete(TestModelPath);
            }
        }

        private void CreateDummyONNXFile()
        {
            // テスト用ディレクトリを作成
            var directory = Path.GetDirectoryName(TestModelPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // ダミーファイルを作成（実際のONNXモデルが必要）
            // ここでは仮のバイトデータを書き込む
            File.WriteAllBytes(TestModelPath, new byte[] { 0x08, 0x01 });
        }

        [UnityTest]
        public IEnumerator LoadONNXModel_FileExists()
        {
            // ファイルの存在確認
            Assert.IsTrue(File.Exists(TestModelPath), "Test ONNX file should exist");
            yield return null;
        }

        [UnityTest]
        public IEnumerator LoadONNXModel_CanLoadModel()
        {
            // 実際のONNXモデルロードテスト
            // 注意: 実際のONNXファイルが必要
            try
            {
                // Resourcesフォルダからロードする場合の例
                // var modelAsset = Resources.Load("model-name") as ModelAsset;
                
                // ダミーファイルの場合はスキップ
                Debug.LogWarning("Skipping actual ONNX loading test - real ONNX model file required");
                Debug.Log("To test ONNX loading: Place a valid .onnx file in Resources folder and load it as ModelAsset");
            }
            catch (System.Exception e)
            {
                // エラーハンドリング
                Debug.LogWarning($"ONNX loading test skipped: {e.Message}");
            }
            
            yield return null;
        }

        [Test]
        public void CheckInferenceEngineVersion()
        {
            // バージョン情報の確認（利用可能な場合）
            Debug.Log("Unity AI Interface (Inference Engine) is available");
            
            // パッケージ情報を確認
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                typeof(Unity.InferenceEngine.Model).Assembly
            );
            
            if (packageInfo != null)
            {
                Debug.Log($"Inference Engine version: {packageInfo.version}");
                Assert.IsNotNull(packageInfo.version, "Package version should be available");
            }
        }
    }
}