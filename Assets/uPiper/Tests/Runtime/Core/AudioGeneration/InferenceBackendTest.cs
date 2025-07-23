using System.Threading.Tasks;
using NUnit.Framework;
using uPiper.Core;
using uPiper.Core.AudioGeneration;
using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.TestTools;

namespace uPiper.Tests.Runtime.Core.AudioGeneration
{
    /// <summary>
    /// 推論バックエンド選択のテスト
    /// </summary>
    public class InferenceBackendTest
    {
        [Test]
        public void PiperConfig_HasGPUSettings()
        {
            var config = PiperConfig.CreateDefault();

            Assert.IsNotNull(config.GPUSettings);
            Assert.AreEqual(InferenceBackend.Auto, config.Backend);
            Assert.AreEqual(true, config.AllowFallbackToCPU);
        }

        [Test]
        public void PiperConfig_CanSetBackend()
        {
            var config = new PiperConfig
            {
                Backend = InferenceBackend.GPUCompute
            };

            Assert.AreEqual(InferenceBackend.GPUCompute, config.Backend);
        }

        [Test]
        public void GPUSettings_AreValidatedWithConfig()
        {
            var config = new PiperConfig();
            config.GPUSettings.MaxBatchSize = 100;
            config.GPUSettings.MaxMemoryMB = 10;

            // PiperConfig.Validate should also validate GPU settings
            config.Validate();

            // GPUSettings should be validated
            Assert.LessOrEqual(config.GPUSettings.MaxBatchSize, 16);
            Assert.GreaterOrEqual(config.GPUSettings.MaxMemoryMB, 128);
        }

        [Test]
        public void InferenceBackend_EnumHasAllValues()
        {
            var values = System.Enum.GetValues(typeof(InferenceBackend));

            Assert.AreEqual(4, values.Length);
            Assert.Contains(InferenceBackend.Auto, values);
            Assert.Contains(InferenceBackend.CPU, values);
            Assert.Contains(InferenceBackend.GPUCompute, values);
            Assert.Contains(InferenceBackend.GPUPixel, values);
        }

        [Test]
        public void GPUSyncMode_EnumHasAllValues()
        {
            var values = System.Enum.GetValues(typeof(GPUSyncMode));

            Assert.AreEqual(3, values.Length);
            Assert.Contains(GPUSyncMode.Automatic, values);
            Assert.Contains(GPUSyncMode.Synchronous, values);
            Assert.Contains(GPUSyncMode.Asynchronous, values);
        }

        [UnityTest]
        public async Task InferenceAudioGenerator_CanBeCreatedAndDisposed()
        {
            var generator = new InferenceAudioGenerator();

            // Verify generator can be created
            Assert.IsNotNull(generator);

            // Note: Cannot test actual initialization without valid ModelAsset
            // This just verifies the basic lifecycle

            generator.Dispose();
            await Task.Yield();
        }
    }
}