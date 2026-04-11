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
        public void LogSelectionSummary_AutoCpu_DoesNotThrow()
        {
            var platform = new PlatformInfo(
                graphicsDeviceType: GraphicsDeviceType.Direct3D11,
                supportsComputeShaders: false,
                graphicsMemorySize: 256,
                isWebGPU: false,
                isWebGL: false,
                isMobile: false);

            Assert.DoesNotThrow(() =>
                BackendSelector.LogSelectionSummary(
                    InferenceBackend.Auto, BackendType.CPU, platform));
        }

        [Test]
        public void LogSelectionSummary_AutoGpuPixelDesktop_DoesNotThrow()
        {
            var platform = new PlatformInfo(
                graphicsDeviceType: GraphicsDeviceType.Direct3D11,
                supportsComputeShaders: true,
                graphicsMemorySize: 1024,
                isWebGPU: false,
                isWebGL: false,
                isMobile: false);

            Assert.DoesNotThrow(() =>
                BackendSelector.LogSelectionSummary(
                    InferenceBackend.Auto, BackendType.GPUPixel, platform));
        }

        [Test]
        public void LogSelectionSummary_AutoMobileGpuPixel_DoesNotThrow()
        {
            var platform = new PlatformInfo(
                graphicsDeviceType: GraphicsDeviceType.OpenGLES3,
                supportsComputeShaders: true,
                graphicsMemorySize: 512,
                isWebGPU: false,
                isWebGL: false,
                isMobile: true);

            Assert.DoesNotThrow(() =>
                BackendSelector.LogSelectionSummary(
                    InferenceBackend.Auto, BackendType.GPUPixel, platform));
        }

        [Test]
        public void LogSelectionSummary_AutoMobileCpu_DoesNotThrow()
        {
            var platform = new PlatformInfo(
                graphicsDeviceType: GraphicsDeviceType.OpenGLES3,
                supportsComputeShaders: false,
                graphicsMemorySize: 256,
                isWebGPU: false,
                isWebGL: false,
                isMobile: true);

            Assert.DoesNotThrow(() =>
                BackendSelector.LogSelectionSummary(
                    InferenceBackend.Auto, BackendType.CPU, platform));
        }

        [Test]
        public void LogSelectionSummary_AutoWebGPU_DoesNotThrow()
        {
            var platform = new PlatformInfo(
                graphicsDeviceType: GraphicsDeviceType.OpenGLES3,
                supportsComputeShaders: true,
                graphicsMemorySize: 0,
                isWebGPU: true,
                isWebGL: true,
                isMobile: false);

            Assert.DoesNotThrow(() =>
                BackendSelector.LogSelectionSummary(
                    InferenceBackend.Auto, BackendType.GPUCompute, platform));
        }

        [Test]
        public void LogSelectionSummary_AutoWebGL2_DoesNotThrow()
        {
            var platform = new PlatformInfo(
                graphicsDeviceType: GraphicsDeviceType.OpenGLES3,
                supportsComputeShaders: false,
                graphicsMemorySize: 0,
                isWebGPU: false,
                isWebGL: true,
                isMobile: false);

            Assert.DoesNotThrow(() =>
                BackendSelector.LogSelectionSummary(
                    InferenceBackend.Auto, BackendType.GPUPixel, platform));
        }

        [Test]
        public void LogSelectionSummary_AutoMetal_DoesNotThrow()
        {
            var platform = new PlatformInfo(
                graphicsDeviceType: GraphicsDeviceType.Metal,
                supportsComputeShaders: true,
                graphicsMemorySize: 2048,
                isWebGPU: false,
                isWebGL: false,
                isMobile: false);

            Assert.DoesNotThrow(() =>
                BackendSelector.LogSelectionSummary(
                    InferenceBackend.Auto, BackendType.CPU, platform));
        }

        #endregion

        #region Override Detection Tests

        [Test]
        public void LogSelectionSummary_GpuComputeOverriddenToGpuPixel_DoesNotThrow()
        {
            var platform = new PlatformInfo(
                graphicsDeviceType: GraphicsDeviceType.Direct3D11,
                supportsComputeShaders: true,
                graphicsMemorySize: 1024,
                isWebGPU: false,
                isWebGL: false,
                isMobile: false);

            // GPUCompute requested but got GPUPixel (VITS compatibility override)
            Assert.DoesNotThrow(() =>
                BackendSelector.LogSelectionSummary(
                    InferenceBackend.GPUCompute, BackendType.GPUPixel, platform));
        }

        [Test]
        public void LogSelectionSummary_GpuPixelOnMetal_OverriddenToCpu_DoesNotThrow()
        {
            var platform = new PlatformInfo(
                graphicsDeviceType: GraphicsDeviceType.Metal,
                supportsComputeShaders: true,
                graphicsMemorySize: 2048,
                isWebGPU: false,
                isWebGL: false,
                isMobile: false);

            // GPUPixel requested on Metal, overridden to CPU
            Assert.DoesNotThrow(() =>
                BackendSelector.LogSelectionSummary(
                    InferenceBackend.GPUPixel, BackendType.CPU, platform));
        }

        [Test]
        public void LogSelectionSummary_ExplicitCpuNotOverridden_DoesNotThrow()
        {
            var platform = new PlatformInfo(
                graphicsDeviceType: GraphicsDeviceType.Direct3D11,
                supportsComputeShaders: true,
                graphicsMemorySize: 1024,
                isWebGPU: false,
                isWebGL: false,
                isMobile: false);

            // CPU requested and CPU returned — no override
            Assert.DoesNotThrow(() =>
                BackendSelector.LogSelectionSummary(
                    InferenceBackend.CPU, BackendType.CPU, platform));
        }

        #endregion

        #region BackendSelector.Determine Tests

        [Test]
        public void Determine_AllInferenceBackendValues_DoesNotThrow(
            [Values] InferenceBackend backend)
        {
            var platform = new PlatformInfo(
                graphicsDeviceType: GraphicsDeviceType.Direct3D11,
                supportsComputeShaders: true,
                graphicsMemorySize: 1024,
                isWebGPU: false,
                isWebGL: false,
                isMobile: false);

            Assert.DoesNotThrow(() =>
                BackendSelector.Determine(backend, platform));
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