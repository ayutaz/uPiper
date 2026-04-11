using NUnit.Framework;
using Unity.InferenceEngine;
using UnityEngine.Rendering;
using uPiper.Core;
using uPiper.Core.AudioGeneration;

namespace uPiper.Tests.Editor
{
    [TestFixture]
    public class PlatformInitLoggingTests
    {
        #region LogSelectionSummary Tests

        [Test]
        public void LogSelectionSummary_AutoCpu_SelectsCpuForDesktopWithoutComputeShaders()
        {
            var platform = new PlatformInfo(
                graphicsDeviceType: GraphicsDeviceType.Direct3D11,
                supportsComputeShaders: false,
                graphicsMemorySize: 256,
                isWebGPU: false,
                isWebGL: false,
                isMobile: false);

            var actual = BackendSelector.Determine(InferenceBackend.Auto, platform);
            Assert.AreEqual(BackendType.CPU, actual);

            // LogSelectionSummary should not throw
            BackendSelector.LogSelectionSummary(InferenceBackend.Auto, actual, platform);
        }

        [Test]
        public void LogSelectionSummary_AutoGpuPixelDesktop_SelectsGpuPixelForDesktopWithVram()
        {
            var platform = new PlatformInfo(
                graphicsDeviceType: GraphicsDeviceType.Direct3D11,
                supportsComputeShaders: true,
                graphicsMemorySize: 1024,
                isWebGPU: false,
                isWebGL: false,
                isMobile: false);

            var actual = BackendSelector.Determine(InferenceBackend.Auto, platform);
            Assert.AreEqual(BackendType.GPUPixel, actual);

            BackendSelector.LogSelectionSummary(InferenceBackend.Auto, actual, platform);
        }

        [Test]
        public void LogSelectionSummary_AutoMobileGpuPixel_SelectsGpuPixelForMobileWithCompute()
        {
            var platform = new PlatformInfo(
                graphicsDeviceType: GraphicsDeviceType.OpenGLES3,
                supportsComputeShaders: true,
                graphicsMemorySize: 512,
                isWebGPU: false,
                isWebGL: false,
                isMobile: true);

            var actual = BackendSelector.Determine(InferenceBackend.Auto, platform);
            Assert.AreEqual(BackendType.GPUPixel, actual);

            BackendSelector.LogSelectionSummary(InferenceBackend.Auto, actual, platform);
        }

        [Test]
        public void LogSelectionSummary_AutoMobileCpu_SelectsCpuForMobileWithoutCompute()
        {
            var platform = new PlatformInfo(
                graphicsDeviceType: GraphicsDeviceType.OpenGLES3,
                supportsComputeShaders: false,
                graphicsMemorySize: 256,
                isWebGPU: false,
                isWebGL: false,
                isMobile: true);

            var actual = BackendSelector.Determine(InferenceBackend.Auto, platform);
            Assert.AreEqual(BackendType.CPU, actual);

            BackendSelector.LogSelectionSummary(InferenceBackend.Auto, actual, platform);
        }

        [Test]
        public void LogSelectionSummary_AutoWebGPU_SelectsGpuComputeForWebGPU()
        {
            var platform = new PlatformInfo(
                graphicsDeviceType: GraphicsDeviceType.OpenGLES3,
                supportsComputeShaders: true,
                graphicsMemorySize: 0,
                isWebGPU: true,
                isWebGL: true,
                isMobile: false);

            var actual = BackendSelector.Determine(InferenceBackend.Auto, platform);
            Assert.AreEqual(BackendType.GPUCompute, actual);

            BackendSelector.LogSelectionSummary(InferenceBackend.Auto, actual, platform);
        }

        [Test]
        public void LogSelectionSummary_AutoWebGL2_SelectsGpuPixelForWebGL2()
        {
            var platform = new PlatformInfo(
                graphicsDeviceType: GraphicsDeviceType.OpenGLES3,
                supportsComputeShaders: false,
                graphicsMemorySize: 0,
                isWebGPU: false,
                isWebGL: true,
                isMobile: false);

            var actual = BackendSelector.Determine(InferenceBackend.Auto, platform);
            Assert.AreEqual(BackendType.GPUPixel, actual);

            BackendSelector.LogSelectionSummary(InferenceBackend.Auto, actual, platform);
        }

        [Test]
        public void LogSelectionSummary_AutoMetal_SelectsCpuDueToMetalIssues()
        {
            var platform = new PlatformInfo(
                graphicsDeviceType: GraphicsDeviceType.Metal,
                supportsComputeShaders: true,
                graphicsMemorySize: 2048,
                isWebGPU: false,
                isWebGL: false,
                isMobile: false);

            var actual = BackendSelector.Determine(InferenceBackend.Auto, platform);
            Assert.AreEqual(BackendType.CPU, actual);

            BackendSelector.LogSelectionSummary(InferenceBackend.Auto, actual, platform);
        }

        #endregion

        #region Override Detection Tests

        [Test]
        public void Determine_GpuComputeOnDesktop_OverridesToGpuPixelForVitsCompat()
        {
            var platform = new PlatformInfo(
                graphicsDeviceType: GraphicsDeviceType.Direct3D11,
                supportsComputeShaders: true,
                graphicsMemorySize: 1024,
                isWebGPU: false,
                isWebGL: false,
                isMobile: false);

            // GPUCompute requested but overridden to GPUPixel (VITS compatibility)
            var actual = BackendSelector.Determine(
                InferenceBackend.GPUCompute, platform);
            Assert.AreEqual(BackendType.GPUPixel, actual);

            BackendSelector.LogSelectionSummary(
                InferenceBackend.GPUCompute, actual, platform);
        }

        [Test]
        public void Determine_GpuPixelOnMetal_OverridesToCpu()
        {
            var platform = new PlatformInfo(
                graphicsDeviceType: GraphicsDeviceType.Metal,
                supportsComputeShaders: true,
                graphicsMemorySize: 2048,
                isWebGPU: false,
                isWebGL: false,
                isMobile: false);

            // GPUPixel requested on Metal, overridden to CPU
            var actual = BackendSelector.Determine(
                InferenceBackend.GPUPixel, platform);
            Assert.AreEqual(BackendType.CPU, actual);

            BackendSelector.LogSelectionSummary(
                InferenceBackend.GPUPixel, actual, platform);
        }

        [Test]
        public void Determine_ExplicitCpu_ReturnsWithoutOverride()
        {
            var platform = new PlatformInfo(
                graphicsDeviceType: GraphicsDeviceType.Direct3D11,
                supportsComputeShaders: true,
                graphicsMemorySize: 1024,
                isWebGPU: false,
                isWebGL: false,
                isMobile: false);

            // CPU requested and CPU returned — no override
            var actual = BackendSelector.Determine(
                InferenceBackend.CPU, platform);
            Assert.AreEqual(BackendType.CPU, actual);

            BackendSelector.LogSelectionSummary(
                InferenceBackend.CPU, actual, platform);
        }

        #endregion

        #region BackendSelector.Determine Tests

        [Test]
        public void Determine_AllInferenceBackendValues_ReturnsValidBackendType(
            [Values] InferenceBackend backend)
        {
            var platform = new PlatformInfo(
                graphicsDeviceType: GraphicsDeviceType.Direct3D11,
                supportsComputeShaders: true,
                graphicsMemorySize: 1024,
                isWebGPU: false,
                isWebGL: false,
                isMobile: false);

            var actual = BackendSelector.Determine(backend, platform);
            Assert.That(System.Enum.IsDefined(typeof(BackendType), actual),
                $"Determine({backend}) returned undefined BackendType: {actual}");
        }

        #endregion

        #region PlatformInfo Tests

        [Test]
        public void PlatformInfo_FromCurrentEnvironment_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                var platform = PlatformInfo.FromCurrentEnvironment();
                // Verify the struct has reasonable values
                Assert.That(platform.GraphicsMemorySize, Is.GreaterThanOrEqualTo(0),
                    "GPU memory should be non-negative");
            });
        }

        [Test]
        public void PlatformInfo_TestConstructor_SetsAllFields()
        {
            var platform = new PlatformInfo(
                graphicsDeviceType: GraphicsDeviceType.Vulkan,
                supportsComputeShaders: true,
                graphicsMemorySize: 4096,
                isWebGPU: false,
                isWebGL: false,
                isMobile: false);

            Assert.That(platform.GraphicsDeviceType,
                Is.EqualTo(GraphicsDeviceType.Vulkan));
            Assert.That(platform.SupportsComputeShaders, Is.True);
            Assert.That(platform.GraphicsMemorySize, Is.EqualTo(4096));
            Assert.That(platform.IsWebGPU, Is.False);
            Assert.That(platform.IsWebGL, Is.False);
            Assert.That(platform.IsMobile, Is.False);
        }

        #endregion
    }
}