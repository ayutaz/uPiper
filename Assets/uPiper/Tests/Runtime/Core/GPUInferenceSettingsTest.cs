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
            
            Assert.AreEqual(1, settings.MaxBatchSize);
            Assert.AreEqual(false, settings.UseFloat16);
            Assert.AreEqual(512, settings.MaxMemoryMB);
            Assert.AreEqual(GPUSyncMode.Automatic, settings.SyncMode);
            Assert.AreEqual(false, settings.EnableProfiling);
            Assert.AreEqual(-1, settings.PreferredDeviceIndex);
        }
        
        [Test]
        public void Validate_ClampsMaxBatchSize()
        {
            var settings = new GPUInferenceSettings();
            
            settings.MaxBatchSize = 100;
            settings.Validate();
            Assert.AreEqual(16, settings.MaxBatchSize);
            
            settings.MaxBatchSize = -5;
            settings.Validate();
            Assert.AreEqual(1, settings.MaxBatchSize);
        }
        
        [Test]
        public void Validate_ClampsMaxMemoryMB()
        {
            var settings = new GPUInferenceSettings();
            
            settings.MaxMemoryMB = 10000;
            settings.Validate();
            Assert.AreEqual(2048, settings.MaxMemoryMB);
            
            settings.MaxMemoryMB = 50;
            settings.Validate();
            Assert.AreEqual(128, settings.MaxMemoryMB);
        }
        
        [Test]
        public void Validate_FixesInvalidDeviceIndex()
        {
            var settings = new GPUInferenceSettings();
            
            settings.PreferredDeviceIndex = -10;
            settings.Validate();
            Assert.AreEqual(-1, settings.PreferredDeviceIndex);
            
            settings.PreferredDeviceIndex = 5;
            settings.Validate();
            Assert.AreEqual(5, settings.PreferredDeviceIndex);
        }
        
        [Test]
        public void AllPropertiesCanBeSetAndRetrieved()
        {
            var settings = new GPUInferenceSettings
            {
                MaxBatchSize = 8,
                UseFloat16 = true,
                MaxMemoryMB = 1024,
                SyncMode = GPUSyncMode.Synchronous,
                EnableProfiling = true,
                PreferredDeviceIndex = 2
            };
            
            Assert.AreEqual(8, settings.MaxBatchSize);
            Assert.AreEqual(true, settings.UseFloat16);
            Assert.AreEqual(1024, settings.MaxMemoryMB);
            Assert.AreEqual(GPUSyncMode.Synchronous, settings.SyncMode);
            Assert.AreEqual(true, settings.EnableProfiling);
            Assert.AreEqual(2, settings.PreferredDeviceIndex);
        }
    }
}