using NUnit.Framework;
using uPiper.Core;

namespace uPiper.Tests.Runtime.Core
{
    /// <summary>
    /// GPU推論設定のテスト
    /// </summary>
    public class GPUInferenceSettingsTest
    {
        [Test]
        public void Constructor_SetsDefaultValues()
        {
            var settings = new GPUInferenceSettings();

            Assert.AreEqual(512, settings.MaxMemoryMB);
        }

        [Test]
        public void ToValidated_ClampsMaxMemoryMB()
        {
            // High value
            var settings = new GPUInferenceSettings { MaxMemoryMB = 10000 };
            var config = new PiperConfig { GPUSettings = settings };
            var validated = config.ToValidated();

            Assert.AreEqual(10000, settings.MaxMemoryMB);
            Assert.AreEqual(2048, validated.Inference.GPUSettings.MaxMemoryMB);

            // Low value
            var settingsLow = new GPUInferenceSettings { MaxMemoryMB = 50 };
            var configLow = new PiperConfig { GPUSettings = settingsLow };
            var validatedLow = configLow.ToValidated();

            Assert.AreEqual(50, settingsLow.MaxMemoryMB);
            Assert.AreEqual(128, validatedLow.Inference.GPUSettings.MaxMemoryMB);
        }

        [Test]
        public void AllPropertiesCanBeSetAndRetrieved()
        {
            var settings = new GPUInferenceSettings
            {
                MaxMemoryMB = 1024,
            };

            Assert.AreEqual(1024, settings.MaxMemoryMB);
        }
    }
}