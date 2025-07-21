using System.Linq;
using Unity.InferenceEngine;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace uPiper.Editor
{
    /// <summary>
    /// ONNXモデルの詳細情報を確認するデバッグツール
    /// </summary>
    public static class DebugONNXModel
    {
        [MenuItem("uPiper/Debug/ONNX/Inspect Model")]
        public static void InspectJapaneseModel()
        {
            InspectModel("Models/ja_JP-test-medium");
        }
        
        private static void InspectModel(string modelPath)
        {
            Debug.Log($"=== Inspecting ONNX Model: {modelPath} ===");
            
            var modelAsset = Resources.Load<ModelAsset>(modelPath);
            if (modelAsset == null)
            {
                Debug.LogError($"Model not found: {modelPath}");
                return;
            }
            
            var model = ModelLoader.Load(modelAsset);
            if (model == null)
            {
                Debug.LogError("Failed to load model");
                return;
            }
            
            Debug.Log($"\n--- Model Information ---");
            Debug.Log($"Model loaded successfully");
            
            // 入力情報
            Debug.Log($"\n--- Inputs ({model.inputs.Count}) ---");
            for (int i = 0; i < model.inputs.Count; i++)
            {
                var input = model.inputs[i];
                Debug.Log($"Input[{i}]:");
                Debug.Log($"  Name: {input.name}");
                // DynamicTensorShapeを文字列に変換
                var shapeStr = input.shape.ToString();
                Debug.Log($"  Shape: {shapeStr}");
                Debug.Log($"  DataType: {input.dataType}");
            }
            
            // 出力情報
            Debug.Log($"\n--- Outputs ({model.outputs.Count}) ---");
            for (int i = 0; i < model.outputs.Count; i++)
            {
                var output = model.outputs[i];
                Debug.Log($"Output[{i}]:");
                Debug.Log($"  Name: {output.name}");
                // Model.Outputには shape プロパティがない可能性があるため、名前のみ表示
            }
            
            // レイヤー情報（最初の10個）
            Debug.Log($"\n--- Layers (first 10 of {model.layers.Count}) ---");
            for (int i = 0; i < Mathf.Min(10, model.layers.Count); i++)
            {
                var layer = model.layers[i];
                Debug.Log($"Layer[{i}]: Type = {layer.GetType().Name}");
            }
            
            // Modelは手動でDisposeする必要がない
            Debug.Log("\n=== Inspection Complete ===");
        }
        
        [MenuItem("uPiper/Debug/ONNX/Test Simple Inference")]
        public static void TestSimpleInference()
        {
            Debug.Log("=== Testing Simple Inference ===");
            
            var modelAsset = Resources.Load<ModelAsset>("Models/ja_JP-test-medium");
            if (modelAsset == null)
            {
                Debug.LogError("Model not found");
                return;
            }
            
            var model = ModelLoader.Load(modelAsset);
            var worker = new Worker(model, BackendType.GPUCompute);
            
            // こんにちはの音素ID（デバッグツールから）
            int[] phonemeIds = { 25, 11, 50, 50, 8, 39, 8, 56, 7 };
            
            // 入力テンソル作成
            var inputTensor = new Tensor<int>(new TensorShape(1, phonemeIds.Length), phonemeIds);
            var lengthTensor = new Tensor<int>(new TensorShape(1), new[] { phonemeIds.Length });
            var scalesTensor = new Tensor<float>(new TensorShape(3), new[] { 0.667f, 1.0f, 0.8f });
            
            Debug.Log($"Input shape: {inputTensor.shape}");
            Debug.Log($"Input IDs: [{string.Join(", ", phonemeIds)}]");
            
            try
            {
                // 入力設定
                worker.SetInput(model.inputs[0].name, inputTensor);
                worker.SetInput(model.inputs[1].name, lengthTensor);
                worker.SetInput(model.inputs[2].name, scalesTensor);
                
                // 推論実行
                worker.Schedule();
                
                // 出力取得
                var output = worker.PeekOutput() as Tensor<float>;
                if (output != null)
                {
                    var shape = output.shape;
                    Debug.Log($"Output shape: {shape}");
                    Debug.Log($"Output length: {shape.length}");
                    
                    // 最初の10サンプルを表示
                    var readableOutput = output.ReadbackAndClone();
                    var samples = new float[Mathf.Min(10, readableOutput.shape.length)];
                    for (int i = 0; i < samples.Length; i++)
                    {
                        samples[i] = readableOutput[i];
                    }
                    Debug.Log($"First 10 samples: [{string.Join(", ", samples.Select(x => x.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)))}]");
                    
                    readableOutput.Dispose();
                }
                else
                {
                    Debug.LogError("Failed to get output");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Inference failed: {ex.Message}");
                Debug.LogError(ex.StackTrace);
            }
            finally
            {
                inputTensor.Dispose();
                lengthTensor.Dispose();
                scalesTensor.Dispose();
                worker.Dispose();
                // Modelは手動でDisposeする必要がない
            }
            
            Debug.Log("=== Test Complete ===");
        }
    }
}