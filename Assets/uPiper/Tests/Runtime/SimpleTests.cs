using NUnit.Framework;
using uPiper.Core;
using Unity.Sentis;

namespace uPiper.Tests
{
    [TestFixture]
    public class SimpleTests
    {
        [Test]
        public void PiperConfig_CanBeCreated()
        {
            var config = new PiperConfig();
            Assert.IsNotNull(config);
        }

        [Test]
        public void PiperTTS_CanBeCreated()
        {
            var tts = new PiperTTS();
            Assert.IsNotNull(tts);
            tts.Dispose();
        }

        [Test]
        public void PiperConfig_DefaultValues_AreValid()
        {
            var config = new PiperConfig();
            config.ModelPath = "test.onnx";
            
            string errorMessage;
            bool isValid = config.Validate(out errorMessage);
            
            Assert.IsTrue(isValid, errorMessage);
        }
    }
}