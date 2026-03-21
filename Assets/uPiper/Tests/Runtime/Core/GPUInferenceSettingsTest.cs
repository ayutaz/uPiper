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
        public void Validate_ClampsMaxMemoryMB()
        {
            var settings = new GPUInferenceSettings
            {
                MaxMemoryMB = 10000
            };
            settings.Validate();
            Assert.AreEqual(2048, settings.MaxMemoryMB);

            settings.MaxMemoryMB = 50;
            settings.Validate();
            Assert.AreEqual(128, settings.MaxMemoryMB);
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