using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.TestTools;

namespace uPiper.Tests.Runtime
{
    /// <summary>
    /// Unity AI Interface (Inference Engine) の基本動作確認テスト
    /// </summary>
    public class InferenceEngineBasicTest
    {
        [Test]
        public void InferenceEngine_NamespaceExists()
        {
            // Inference Engine の名前空間が存在することを確認
            var type = typeof(Unity.InferenceEngine.Model);
            Assert.IsNotNull(type, "Unity.InferenceEngine namespace should exist");
        }

        [Test]
        public void InferenceEngine_CanCreateTensor()
        {
            // Tensor を作成できることを確認
            var tensorShape = new TensorShape(1, 3);
            var tensor = new Tensor<float>(tensorShape);
            
            Assert.IsNotNull(tensor, "Tensor should be created successfully");
            Assert.AreEqual(3, tensor.shape[1], "Tensor should have correct shape");
            
            tensor.Dispose();
        }

        [Test]
        public void InferenceEngine_CanCreateWorker()
        {
            // Worker の作成テスト（実際のモデルなしで）
            // 注意: 実際の推論には有効なONNXモデルが必要
            var tensorShape = new TensorShape(1, 3);
            var tensor = new Tensor<float>(tensorShape, new float[] { 1f, 2f, 3f });
            
            Assert.IsNotNull(tensor, "Tensor should be created with data");
            Assert.AreEqual(1f, tensor[0], "First element should be 1");
            
            tensor.Dispose();
        }

        [Test]
        public void InferenceEngine_SupportedBackends()
        {
            // 利用可能なバックエンドを確認
            var backends = System.Enum.GetValues(typeof(Unity.InferenceEngine.BackendType));
            Assert.Greater(backends.Length, 0, "Should have at least one backend available");
            
            Debug.Log($"Available backends: {string.Join(", ", backends.Cast<Unity.InferenceEngine.BackendType>())}");
        }

        [UnityTest]
        public IEnumerator InferenceEngine_TensorOperations()
        {
            // Tensor の基本操作をテスト
            var shape = new TensorShape(2, 3);
            var data = new float[] { 1f, 2f, 3f, 4f, 5f, 6f };
            var tensor = new Tensor<float>(shape, data);

            Assert.AreEqual(2, tensor.shape[0], "Batch size should be 2");
            Assert.AreEqual(3, tensor.shape[1], "Feature size should be 3");
            
            // データアクセス
            Assert.AreEqual(1f, tensor[0], "First element should be 1");
            Assert.AreEqual(6f, tensor[5], "Last element should be 6");

            tensor.Dispose();
            yield return null;
        }
    }
}