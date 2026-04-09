using NUnit.Framework;
using Unity.InferenceEngine;
using UnityEngine.Rendering;
using uPiper.Core;
using uPiper.Core.AudioGeneration;

namespace uPiper.Tests.Runtime.AudioGeneration
{
    [TestFixture]
    public class BackendSelectorTests
    {
        [Test]
        public void Determine_MetalWithGPUCompute_ReturnsCPU()
        {
            var platform = new PlatformInfo(
                graphicsDeviceType: GraphicsDeviceType.Metal,
                supportsComputeShaders: true,
                graphicsMemorySize: 4096,
                isWebGPU: false,
                isWebGL: false,
                isMobile: false);

            var result = BackendSelector.Determine(InferenceBackend.GPUCompute, platform);

            Assert.AreEqual(BackendType.CPU, result);
        }

        [Test]
        public void Determine_MetalWithGPUPixel_ReturnsCPU()
        {
            var platform = new PlatformInfo(
                graphicsDeviceType: GraphicsDeviceType.Metal,
                supportsComputeShaders: true,
                graphicsMemorySize: 4096,
                isWebGPU: false,
                isWebGL: false,
                isMobile: false);

            var result = BackendSelector.Determine(InferenceBackend.GPUPixel, platform);

            Assert.AreEqual(BackendType.CPU, result);
        }

        [Test]
        public void Determine_MetalWithCPU_ReturnsCPU()
        {
            var platform = new PlatformInfo(
                graphicsDeviceType: GraphicsDeviceType.Metal,
                supportsComputeShaders: true,
                graphicsMemorySize: 4096,
                isWebGPU: false,
                isWebGL: false,
                isMobile: false);

            var result = BackendSelector.Determine(InferenceBackend.CPU, platform);

            Assert.AreEqual(BackendType.CPU, result);
        }

        [Test]
        public void Determine_MetalWithAuto_ReturnsCPU()
        {
            var platform = new PlatformInfo(
                graphicsDeviceType: GraphicsDeviceType.Metal,
                supportsComputeShaders: true,
                graphicsMemorySize: 4096,
                isWebGPU: false,
                isWebGL: false,
                isMobile: false);

            var result = BackendSelector.Determine(InferenceBackend.Auto, platform);

            Assert.AreEqual(BackendType.CPU, result);
        }

        [Test]
        public void Determine_GPUCompute_WebGPU_ReturnsGPUCompute()
        {
            var platform = new PlatformInfo(
                graphicsDeviceType: GraphicsDeviceType.WebGPU,
                supportsComputeShaders: true,
                graphicsMemorySize: 4096,
                isWebGPU: true,
                isWebGL: true,
                isMobile: false);

            var result = BackendSelector.Determine(InferenceBackend.GPUCompute, platform);

            Assert.AreEqual(BackendType.GPUCompute, result);
        }

        [Test]
        public void Determine_GPUCompute_NonWebGPU_ReturnsGPUPixel()
        {
            var platform = new PlatformInfo(
                graphicsDeviceType: GraphicsDeviceType.Direct3D11,
                supportsComputeShaders: true,
                graphicsMemorySize: 4096,
                isWebGPU: false,
                isWebGL: false,
                isMobile: false);

            var result = BackendSelector.Determine(InferenceBackend.GPUCompute, platform);

            Assert.AreEqual(BackendType.GPUPixel, result);
        }

        [Test]
        public void Determine_CPU_ReturnsCPU()
        {
            var platform = new PlatformInfo(
                graphicsDeviceType: GraphicsDeviceType.Direct3D11,
                supportsComputeShaders: true,
                graphicsMemorySize: 4096,
                isWebGPU: false,
                isWebGL: false,
                isMobile: false);

            var result = BackendSelector.Determine(InferenceBackend.CPU, platform);

            Assert.AreEqual(BackendType.CPU, result);
        }

        [Test]
        public void Determine_GPUPixel_ReturnsGPUPixel()
        {
            var platform = new PlatformInfo(
                graphicsDeviceType: GraphicsDeviceType.Direct3D11,
                supportsComputeShaders: true,
                graphicsMemorySize: 4096,
                isWebGPU: false,
                isWebGL: false,
                isMobile: false);

            var result = BackendSelector.Determine(InferenceBackend.GPUPixel, platform);

            Assert.AreEqual(BackendType.GPUPixel, result);
        }

        [Test]
        public void Determine_Auto_WebGPU_ReturnsGPUCompute()
        {
            var platform = new PlatformInfo(
                graphicsDeviceType: GraphicsDeviceType.WebGPU,
                supportsComputeShaders: true,
                graphicsMemorySize: 4096,
                isWebGPU: true,
                isWebGL: true,
                isMobile: false);

            var result = BackendSelector.Determine(InferenceBackend.Auto, platform);

            Assert.AreEqual(BackendType.GPUCompute, result);
        }

        [Test]
        public void Determine_Auto_WebGL2_ReturnsGPUPixel()
        {
            var platform = new PlatformInfo(
                graphicsDeviceType: GraphicsDeviceType.OpenGLES3,
                supportsComputeShaders: false,
                graphicsMemorySize: 2048,
                isWebGPU: false,
                isWebGL: true,
                isMobile: false);

            var result = BackendSelector.Determine(InferenceBackend.Auto, platform);

            Assert.AreEqual(BackendType.GPUPixel, result);
        }

        [Test]
        public void Determine_Auto_Mobile_ComputeShader_ReturnsGPUPixel()
        {
            var platform = new PlatformInfo(
                graphicsDeviceType: GraphicsDeviceType.Vulkan,
                supportsComputeShaders: true,
                graphicsMemorySize: 2048,
                isWebGPU: false,
                isWebGL: false,
                isMobile: true);

            var result = BackendSelector.Determine(InferenceBackend.Auto, platform);

            Assert.AreEqual(BackendType.GPUPixel, result);
        }

        [Test]
        public void Determine_Auto_Mobile_NoComputeShader_ReturnsCPU()
        {
            var platform = new PlatformInfo(
                graphicsDeviceType: GraphicsDeviceType.OpenGLES3,
                supportsComputeShaders: false,
                graphicsMemorySize: 1024,
                isWebGPU: false,
                isWebGL: false,
                isMobile: true);

            var result = BackendSelector.Determine(InferenceBackend.Auto, platform);

            Assert.AreEqual(BackendType.CPU, result);
        }

        [Test]
        public void Determine_Auto_Desktop_SufficientVRAM_ReturnsGPUPixel()
        {
            var platform = new PlatformInfo(
                graphicsDeviceType: GraphicsDeviceType.Direct3D11,
                supportsComputeShaders: true,
                graphicsMemorySize: 4096,
                isWebGPU: false,
                isWebGL: false,
                isMobile: false);

            var result = BackendSelector.Determine(
                InferenceBackend.Auto, platform, gpuMemoryThresholdMB: 512);

            Assert.AreEqual(BackendType.GPUPixel, result);
        }

        [Test]
        public void Determine_Auto_Desktop_InsufficientVRAM_ReturnsCPU()
        {
            var platform = new PlatformInfo(
                graphicsDeviceType: GraphicsDeviceType.Direct3D11,
                supportsComputeShaders: true,
                graphicsMemorySize: 256,
                isWebGPU: false,
                isWebGL: false,
                isMobile: false);

            var result = BackendSelector.Determine(
                InferenceBackend.Auto, platform, gpuMemoryThresholdMB: 512);

            Assert.AreEqual(BackendType.CPU, result);
        }
    }
}
