using uPiper.Core.Logging;

namespace uPiper.Core.Backends
{
    /// <summary>
    /// Factory that returns the right <see cref="IPiperTTS"/> implementation for
    /// a given <see cref="PiperConfig"/>. Use this instead of constructing
    /// <c>PiperTTS</c> directly when you want to support both local (ONNX) and
    /// cloud (Camb AI) backends from the same call site.
    ///
    /// <example>
    /// <code>
    /// var config = PiperConfig.CreateDefault();
    /// config.Backend = InferenceBackend.Cloud;
    /// IPiperTTS tts = PiperBackendFactory.Create(config, cambSettings);
    /// await tts.InitializeAsync();
    /// var clip = await tts.GenerateAudioAsync("Hello from Camb AI!");
    /// </code>
    /// </example>
    /// </summary>
    public static class PiperBackendFactory
    {
        /// <summary>
        /// Build a TTS backend. When <see cref="PiperConfig.Backend"/> is
        /// <see cref="InferenceBackend.Cloud"/>, returns a <see cref="CambBackend"/>;
        /// otherwise returns the default local <see cref="PiperTTS"/>.
        /// </summary>
        /// <param name="config">Runtime configuration.</param>
        /// <param name="cambSettings">Required when <c>config.Backend == Cloud</c>.</param>
        public static IPiperTTS Create(PiperConfig config, CambSettings cambSettings = null)
        {
            if (config == null)
            {
                throw new System.ArgumentNullException(nameof(config));
            }

            if (config.Backend == InferenceBackend.Cloud)
            {
                if (cambSettings == null)
                {
                    throw new PiperException(
                        "InferenceBackend.Cloud requires a CambSettings asset. " +
                        "Create one via Assets > Create > uPiper > Camb AI Settings.");
                }
                PiperLogger.LogInfo("[PiperBackendFactory] Creating Camb AI cloud backend.");
                return new CambBackend(config, cambSettings);
            }

            PiperLogger.LogInfo("[PiperBackendFactory] Creating local PiperTTS backend ({0}).", config.Backend);
            return new PiperTTS(config);
        }
    }
}
