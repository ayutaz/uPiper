using NUnit.Framework;
using Unity.InferenceEngine;
using UnityEngine.Rendering;
using uPiper.Core;
using uPiper.Core.AudioGeneration;

namespace uPiper.Tests.Editor.AudioGeneration
{
    /// <summary>
    /// BackendSelector.Determine の境界値テスト。
    /// </summary>
    [TestFixture]
    public class BackendSelectorTests
    {
        /// <summary>
        /// enum 範囲外の InferenceBackend 値が渡された場合、CPU フォールバックすること。
        /// </summary>
        [Test]
        public void Determine_OutOfRangeEnumValue_ReturnsCPU()
        {
            // Arrange: enum に定義されていない値をキャスト
            var invalidBackend = (InferenceBackend)999;
            var platform = new PlatformInfo(
                graphicsDeviceType: GraphicsDeviceType.Direct3D11,
                supportsComputeShaders: true,
                graphicsMemorySize: 1024,
                isWebGPU: false,
                isWebGL: false,
                isMobile: false);

            // Act
            var result = BackendSelector.Determine(invalidBackend, platform);

            // Assert: 未定義 enum 値は [6] フォールバック → CPU
            Assert.AreEqual(BackendType.CPU, result,
                "Out-of-range enum value should fall back to CPU");
        }

        /// <summary>
        /// gpuMemoryThresholdMB と同値（=512）の場合、GPU が選択されること。
        /// Auto 選択ではメモリが閾値以上なら GPUPixel を返す。
        /// </summary>
        [Test]
        public void Determine_Auto_GpuMemoryExactlyAtThreshold_ReturnsGPUPixel()
        {
            // Arrange: メモリが閾値と同値（512MB）
            var platform = new PlatformInfo(
                graphicsDeviceType: GraphicsDeviceType.Direct3D11,
                supportsComputeShaders: true,
                graphicsMemorySize: 512,
                isWebGPU: false,
                isWebGL: false,
                isMobile: false);

            // Act
            var result = BackendSelector.Determine(
                InferenceBackend.Auto, platform, gpuMemoryThresholdMB: 512);

            // Assert: >= 512 なので GPUPixel が選択される
            Assert.AreEqual(BackendType.GPUPixel, result,
                "GPU memory exactly at threshold should select GPUPixel");
        }

        /// <summary>
        /// gpuMemoryThresholdMB より 1MB 少ない場合、CPU にフォールバックすること。
        /// </summary>
        [Test]
        public void Determine_Auto_GpuMemoryBelowThreshold_ReturnsCPU()
        {
            // Arrange: メモリが閾値未満（511MB < 512MB）
            var platform = new PlatformInfo(
                graphicsDeviceType: GraphicsDeviceType.Direct3D11,
                supportsComputeShaders: true,
                graphicsMemorySize: 511,
                isWebGPU: false,
                isWebGL: false,
                isMobile: false);

            // Act
            var result = BackendSelector.Determine(
                InferenceBackend.Auto, platform, gpuMemoryThresholdMB: 512);

            // Assert: < 512 なので CPU にフォールバック
            Assert.AreEqual(BackendType.CPU, result,
                "GPU memory below threshold should fall back to CPU");
        }
    }
}