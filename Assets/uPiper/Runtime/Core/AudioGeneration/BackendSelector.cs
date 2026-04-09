using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.Rendering;
using uPiper.Core.Logging;

namespace uPiper.Core.AudioGeneration
{
    /// <summary>
    /// プラットフォーム依存情報をカプセル化するイミュータブル構造体。
    /// テスト時に任意の値を注入可能にする。
    /// </summary>
    public readonly struct PlatformInfo
    {
        /// <summary>現在のグラフィクスデバイスタイプ</summary>
        public GraphicsDeviceType GraphicsDeviceType { get; }

        /// <summary>Compute Shader サポート有無</summary>
        public bool SupportsComputeShaders { get; }

        /// <summary>GPU メモリサイズ (MB)</summary>
        public int GraphicsMemorySize { get; }

        /// <summary>WebGPU 上で動作しているか（WebGL プラットフォームのみ有効）</summary>
        public bool IsWebGPU { get; }

        /// <summary>WebGL プラットフォームか</summary>
        public bool IsWebGL { get; }

        /// <summary>モバイルプラットフォームか (iOS/Android)</summary>
        public bool IsMobile { get; }

        /// <summary>
        /// テスト用コンストラクタ。全フィールドを明示的に指定する。
        /// </summary>
        public PlatformInfo(
            GraphicsDeviceType graphicsDeviceType,
            bool supportsComputeShaders,
            int graphicsMemorySize,
            bool isWebGPU,
            bool isWebGL,
            bool isMobile)
        {
            GraphicsDeviceType = graphicsDeviceType;
            SupportsComputeShaders = supportsComputeShaders;
            GraphicsMemorySize = graphicsMemorySize;
            IsWebGPU = isWebGPU;
            IsWebGL = isWebGL;
            IsMobile = isMobile;
        }

        /// <summary>
        /// 現在の実行環境からPlatformInfoを構築するファクトリメソッド。
        /// プリプロセッサ条件はこのメソッド内のみに閉じ込められる。
        /// </summary>
        public static PlatformInfo FromCurrentEnvironment()
        {
            bool isWebGL;
            bool isWebGPU;
            bool isMobile;

#if UNITY_WEBGL
            isWebGL = true;
            isWebGPU = Platform.PlatformHelper.IsWebGPU;
#else
            isWebGL = false;
            isWebGPU = false;
#endif

#if UNITY_IOS || UNITY_ANDROID
            isMobile = true;
#else
            isMobile = false;
#endif

            return new PlatformInfo(
                graphicsDeviceType: SystemInfo.graphicsDeviceType,
                supportsComputeShaders: SystemInfo.supportsComputeShaders,
                graphicsMemorySize: SystemInfo.graphicsMemorySize,
                isWebGPU: isWebGPU,
                isWebGL: isWebGL,
                isMobile: isMobile);
        }
    }

    /// <summary>
    /// 推論バックエンドの選択ロジックを担当するstaticクラス。
    /// Determine メソッドはプリプロセッサフリーであり、PlatformInfo のフィールドのみで分岐する。
    /// </summary>
    public static class BackendSelector
    {
        /// <summary>
        /// 要求されたバックエンドとプラットフォーム情報に基づいて、実際に使用するバックエンドタイプを決定する。
        /// </summary>
        /// <param name="requested">ユーザーが要求したバックエンド</param>
        /// <param name="platform">プラットフォーム情報</param>
        /// <param name="gpuMemoryThresholdMB">Auto選択時のGPUメモリ閾値(MB)</param>
        /// <returns>決定されたバックエンドタイプ</returns>
        public static BackendType Determine(
            InferenceBackend requested,
            PlatformInfo platform,
            int gpuMemoryThresholdMB = 512)
        {
            // [1] Metal チェック - Metal has known issues with GPU backends
            if (platform.GraphicsDeviceType == GraphicsDeviceType.Metal)
            {
                if (requested == InferenceBackend.GPUCompute
                    || requested == InferenceBackend.GPUPixel)
                {
                    PiperLogger.LogWarning(
                        $"[BackendSelector] {requested} requested on Metal, but Metal has known issues " +
                        "with GPU inference. Using CPU backend instead.");
                    PiperLogger.LogWarning(
                        "[BackendSelector] This is a known issue with Unity.InferenceEngine on macOS. " +
                        "GPU inference may produce corrupted audio.");
                    return BackendType.CPU;
                }

                if (requested == InferenceBackend.Auto)
                {
                    PiperLogger.LogWarning(
                        "[BackendSelector] Metal detected - using CPU backend due to known " +
                        "shader compilation issues");
                    return BackendType.CPU;
                }
            }

            // [2] GPUCompute 要求時
            if (requested == InferenceBackend.GPUCompute)
            {
                if (platform.IsWebGL && platform.IsWebGPU)
                {
                    PiperLogger.LogInfo(
                        "[BackendSelector] GPUCompute backend on WebGPU - allowing " +
                        "(WebGPU compute shaders are supported).");
                    return BackendType.GPUCompute;
                }

                PiperLogger.LogWarning(
                    "[BackendSelector] GPU Compute backend has known issues with VITS audio models.");
                PiperLogger.LogWarning(
                    "[BackendSelector] Switching to GPU Pixel backend for better compatibility.");
                PiperLogger.LogWarning(
                    "[BackendSelector] If issues persist, please use CPU backend explicitly.");
                return BackendType.GPUPixel;
            }

            // [3] CPU 明示指定
            if (requested == InferenceBackend.CPU)
            {
                return BackendType.CPU;
            }

            // [4] GPUPixel 明示指定
            if (requested == InferenceBackend.GPUPixel)
            {
                return BackendType.GPUPixel;
            }

            // [5] Auto 選択
            if (requested == InferenceBackend.Auto)
            {
                return DetermineAutoBackend(platform, gpuMemoryThresholdMB);
            }

            // [6] フォールバック
            return BackendType.CPU;
        }

        /// <summary>
        /// Auto選択時のバックエンド決定ロジック。
        /// </summary>
        private static BackendType DetermineAutoBackend(
            PlatformInfo platform, int gpuMemoryThresholdMB)
        {
            // WebGL: WebGPU → GPUCompute, WebGL2 → GPUPixel
            if (platform.IsWebGL)
            {
                if (platform.IsWebGPU)
                {
                    PiperLogger.LogInfo(
                        "[BackendSelector] Auto-selecting GPUCompute backend for WebGPU");
                    return BackendType.GPUCompute;
                }

                PiperLogger.LogInfo(
                    "[BackendSelector] Auto-selecting GPUPixel backend for WebGL2");
                return BackendType.GPUPixel;
            }

            // Mobile: GPUPixel (better VITS compatibility) or CPU fallback
            if (platform.IsMobile)
            {
                if (platform.SupportsComputeShaders)
                {
                    PiperLogger.LogInfo(
                        "[BackendSelector] Auto-selecting GPUPixel backend for mobile");
                    return BackendType.GPUPixel;
                }

                PiperLogger.LogInfo(
                    "[BackendSelector] Auto-selecting CPU backend for mobile " +
                    "(no compute shader support)");
                return BackendType.CPU;
            }

            // Desktop: Metal → CPU (already handled above for explicit requests),
            // VRAM十分+CS対応 → GPUPixel, その他 → CPU
            if (platform.SupportsComputeShaders
                && platform.GraphicsMemorySize >= gpuMemoryThresholdMB)
            {
                PiperLogger.LogInfo(
                    "[BackendSelector] Auto-selecting GPUPixel backend for desktop " +
                    "(better VITS compatibility)");
                return BackendType.GPUPixel;
            }

            PiperLogger.LogInfo(
                "[BackendSelector] Auto-selecting CPU backend for desktop");
            return BackendType.CPU;
        }
    }
}