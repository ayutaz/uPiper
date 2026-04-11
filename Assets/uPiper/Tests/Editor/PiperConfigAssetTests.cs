using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using uPiper.Core;

namespace uPiper.Tests.Editor
{
    [TestFixture]
    public sealed class PiperConfigAssetTests
    {
        private PiperConfigAsset _asset;

        [SetUp]
        public void SetUp()
        {
            _asset = ScriptableObject.CreateInstance<PiperConfigAsset>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_asset != null)
                Object.DestroyImmediate(_asset);
        }

        [Test]
        public void Config_ReturnsNonNull()
        {
            Assert.That(_asset.Config, Is.Not.Null,
                "Config should not be null after asset creation");
        }

        [Test]
        public void CreateRuntimeCopy_ReturnsNewInstance()
        {
            var copy = _asset.CreateRuntimeCopy();
            Assert.That(copy, Is.Not.Null,
                "Runtime copy should not be null");
            Assert.That(copy, Is.Not.SameAs(_asset.Config),
                "Runtime copy should be a different instance from original");
        }

        [Test]
        public void CreateRuntimeCopy_PreservesValues()
        {
            _asset.Config.DefaultLanguage = "en";
            _asset.Config.MaxCacheSizeMB = 200;
            _asset.Config.EnablePhonemeCache = false;

            var copy = _asset.CreateRuntimeCopy();
            Assert.That(copy.DefaultLanguage, Is.EqualTo("en"),
                "DefaultLanguage should be preserved in copy");
            Assert.That(copy.MaxCacheSizeMB, Is.EqualTo(200),
                "MaxCacheSizeMB should be preserved in copy");
            Assert.That(copy.EnablePhonemeCache, Is.False,
                "EnablePhonemeCache should be preserved in copy");
        }

        [Test]
        public void CreateRuntimeCopy_DeepCopiesSupportedLanguages()
        {
            _asset.Config.SupportedLanguages.Add("test-lang");
            var copy = _asset.CreateRuntimeCopy();
            Assert.That(copy.SupportedLanguages, Is.Not.SameAs(_asset.Config.SupportedLanguages),
                "SupportedLanguages should be a different list instance");
            Assert.That(copy.SupportedLanguages, Does.Contain("test-lang"),
                "SupportedLanguages copy should contain 'test-lang'");
        }

        [Test]
        public void CreateRuntimeCopy_DeepCopiesGPUSettings()
        {
            _asset.Config.GPUSettings.MaxMemoryMB = 1024;
            var copy = _asset.CreateRuntimeCopy();
            Assert.That(copy.GPUSettings, Is.Not.SameAs(_asset.Config.GPUSettings),
                "GPUSettings should be a different instance");
            Assert.That(copy.GPUSettings.MaxMemoryMB, Is.EqualTo(1024),
                "GPUSettings.MaxMemoryMB should be preserved in copy");
        }

        [Test]
        public void CreateRuntimeCopy_MutationDoesNotAffectOriginal()
        {
            _asset.Config.DefaultLanguage = "ja";
            _asset.Config.SupportedLanguages = new List<string> { "ja", "en" };

            var copy = _asset.CreateRuntimeCopy();
            copy.DefaultLanguage = "zh";
            copy.SupportedLanguages.Add("zh");

            Assert.That(_asset.Config.DefaultLanguage, Is.EqualTo("ja"),
                "Original DefaultLanguage should not be affected by copy mutation");
            Assert.That(_asset.Config.SupportedLanguages.Count, Is.EqualTo(2),
                "Original SupportedLanguages count should not be affected by copy mutation");
        }

        [Test]
        public void CreateRuntimeCopy_PreservesFallbackLanguage()
        {
            _asset.Config.FallbackLanguage = "en";
            var copy = _asset.CreateRuntimeCopy();
            Assert.That(copy.FallbackLanguage, Is.EqualTo("en"),
                "FallbackLanguage should be preserved in copy");
        }

        [Test]
        public void CreateRuntimeCopy_PreservesAllFields()
        {
            // Set non-default values for all fields
            _asset.Config.EnableDebugLogging = true;
            _asset.Config.DefaultLanguage = "en";
            _asset.Config.AutoDetectLanguage = true;
            _asset.Config.FallbackLanguage = "ja";
            _asset.Config.SupportedLanguages = new List<string> { "en", "ja", "zh" };
            _asset.Config.MixedLanguageMode = MultiLanguageMode.ForceDefault;
            _asset.Config.MaxCacheSizeMB = 250;
            _asset.Config.EnablePhonemeCache = false;
            _asset.Config.WorkerThreads = 4;
            _asset.Config.Backend = InferenceBackend.CPU;
            _asset.Config.EnablePhonemeSilence = true;
            _asset.Config.PhonemeSilenceSpec = "_ 0.3,# 0.2";
            _asset.Config.SampleRate = 16000;
            _asset.Config.NormalizeAudio = false;
            _asset.Config.TargetRMSLevel = -15f;
            _asset.Config.EnableWarmup = true;
            _asset.Config.WarmupIterations = 3;
            _asset.Config.TimeoutMs = 60000;
            _asset.Config.EnableMultiThreadedInference = true;
            _asset.Config.InferenceBatchSize = 8;
            _asset.Config.GPUSettings.MaxMemoryMB = 1024;
            _asset.Config.AllowFallbackToCPU = false;

            var copy = _asset.CreateRuntimeCopy();

            Assert.That(copy.EnableDebugLogging, Is.True,
                "EnableDebugLogging should be preserved");
            Assert.That(copy.DefaultLanguage, Is.EqualTo("en"),
                "DefaultLanguage should be preserved");
            Assert.That(copy.AutoDetectLanguage, Is.True,
                "AutoDetectLanguage should be preserved");
            Assert.That(copy.FallbackLanguage, Is.EqualTo("ja"),
                "FallbackLanguage should be preserved");
            Assert.That(copy.SupportedLanguages.Count, Is.EqualTo(3),
                "SupportedLanguages count should be preserved");
            Assert.That(copy.MixedLanguageMode, Is.EqualTo(MultiLanguageMode.ForceDefault),
                "MixedLanguageMode should be preserved");
            Assert.That(copy.MaxCacheSizeMB, Is.EqualTo(250),
                "MaxCacheSizeMB should be preserved");
            Assert.That(copy.EnablePhonemeCache, Is.False,
                "EnablePhonemeCache should be preserved");
            Assert.That(copy.WorkerThreads, Is.EqualTo(4),
                "WorkerThreads should be preserved");
            Assert.That(copy.Backend, Is.EqualTo(InferenceBackend.CPU),
                "Backend should be preserved");
            Assert.That(copy.EnablePhonemeSilence, Is.True,
                "EnablePhonemeSilence should be preserved");
            Assert.That(copy.PhonemeSilenceSpec, Is.EqualTo("_ 0.3,# 0.2"),
                "PhonemeSilenceSpec should be preserved");
            Assert.That(copy.SampleRate, Is.EqualTo(16000),
                "SampleRate should be preserved");
            Assert.That(copy.NormalizeAudio, Is.False,
                "NormalizeAudio should be preserved");
            Assert.That(copy.TargetRMSLevel, Is.EqualTo(-15f),
                "TargetRMSLevel should be preserved");
            Assert.That(copy.EnableWarmup, Is.True,
                "EnableWarmup should be preserved");
            Assert.That(copy.WarmupIterations, Is.EqualTo(3),
                "WarmupIterations should be preserved");
            Assert.That(copy.TimeoutMs, Is.EqualTo(60000),
                "TimeoutMs should be preserved");
            Assert.That(copy.EnableMultiThreadedInference, Is.True,
                "EnableMultiThreadedInference should be preserved");
            Assert.That(copy.InferenceBatchSize, Is.EqualTo(8),
                "InferenceBatchSize should be preserved");
            Assert.That(copy.GPUSettings.MaxMemoryMB, Is.EqualTo(1024),
                "GPUSettings.MaxMemoryMB should be preserved");
            Assert.That(copy.AllowFallbackToCPU, Is.False,
                "AllowFallbackToCPU should be preserved");
        }

        [Test]
        public void ToValidated_DefaultConfig_Succeeds()
        {
            // デフォルト設定でバリデーションが通ることを確認
            var validated = _asset.ToValidated();
            Assert.That(validated, Is.Not.Null,
                "ToValidated should return non-null for default config");
        }
    }
}