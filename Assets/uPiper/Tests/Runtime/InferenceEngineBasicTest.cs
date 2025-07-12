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

        [UnityTest]
        public IEnumerator InferenceEngine_CanCreateModel()
        {
            // 簡単なモデルを作成できることを確認
            var inputs = FunctionalTensor.FromShape(new TensorShape(1, 3));
            var outputs = Unity.InferenceEngine.Functional.Dense(3, 2)(inputs);
            
            var model = Unity.InferenceEngine.Functional.Compile(
                inputs,
                outputs,
                Unity.InferenceEngine.InputDef.FromTensor(inputs)
            );

            Assert.IsNotNull(model, "Model should be created successfully");
            
            // モデルの入出力を確認
            var inputNames = model.inputs.Select(i => i.name).ToArray();
            var outputNames = model.outputs.Select(o => o.name).ToArray();
            
            Assert.AreEqual(1, inputNames.Length, "Model should have 1 input");
            Assert.AreEqual(1, outputNames.Length, "Model should have 1 output");
            
            model.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator InferenceEngine_CanRunInference()
        {
            // 簡単な推論を実行
            var inputs = FunctionalTensor.FromShape(new TensorShape(1, 3));
            var outputs = Unity.InferenceEngine.Functional.Dense(3, 2)(inputs);
            
            var model = Unity.InferenceEngine.Functional.Compile(
                inputs,
                outputs,
                Unity.InferenceEngine.InputDef.FromTensor(inputs)
            );

            // Workerを作成
            var worker = Unity.InferenceEngine.WorkerFactory.CreateWorker(
                Unity.InferenceEngine.BackendType.GPUCompute, 
                model
            );

            // 入力テンソルを作成
            var inputTensor = new Unity.InferenceEngine.Tensor(new TensorShape(1, 3), new float[] { 1f, 2f, 3f });
            
            // 推論実行
            worker.SetInput(model.inputs[0].name, inputTensor);
            worker.Schedule();
            
            // 結果を取得
            var outputTensor = worker.PeekOutput(model.outputs[0].name);
            Assert.IsNotNull(outputTensor, "Output tensor should not be null");
            Assert.AreEqual(2, outputTensor.shape[1], "Output should have 2 elements");
            
            // クリーンアップ
            inputTensor.Dispose();
            outputTensor.Dispose();
            worker.Dispose();
            model.Dispose();
            
            yield return null;
        }

        [Test]
        public void InferenceEngine_SupportedBackends()
        {
            // 利用可能なバックエンドを確認
            var backends = System.Enum.GetValues(typeof(Unity.InferenceEngine.BackendType));
            Assert.Greater(backends.Length, 0, "Should have at least one backend available");
            
            Debug.Log($"Available backends: {string.Join(", ", backends.Cast<Unity.InferenceEngine.BackendType>())}");
        }
    }
}