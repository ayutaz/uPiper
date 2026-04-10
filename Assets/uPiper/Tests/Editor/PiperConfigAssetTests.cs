using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using uPiper.Core;

namespace uPiper.Tests.Editor
{
    [TestFixture]
    public sealed class PiperConfigAssetTests
    {
        [Test]
        public void Config_ReturnsNonNull()
        {
            var asset = ScriptableObject.CreateInstance<PiperConfigAsset>();
            try
            {
                Assert.IsNotNull(asset.Config);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void CreateRuntimeCopy_ReturnsNewInstance()
        {
            var asset = ScriptableObject.CreateInstance<PiperConfigAsset>();
            try
            {
                var copy = asset.CreateRuntimeCopy();
                Assert.IsNotNull(copy);
                Assert.AreNotSame(asset.Config, copy);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void CreateRuntimeCopy_PreservesValues()
        {
            var asset = ScriptableObject.CreateInstance<PiperConfigAsset>();
            try
            {
                asset.Config.DefaultLanguage = "en";
                asset.Config.MaxCacheSizeMB = 200;
                asset.Config.EnablePhonemeCache = false;

                var copy = asset.CreateRuntimeCopy();
                Assert.AreEqual("en", copy.DefaultLanguage);
                Assert.AreEqual(200, copy.MaxCacheSizeMB);
                Assert.IsFalse(copy.EnablePhonemeCache);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void CreateRuntimeCopy_DeepCopiesSupportedLanguages()
        {
            var asset = ScriptableObject.CreateInstance<PiperConfigAsset>();
            try
            {
                asset.Config.SupportedLanguages.Add("test-lang");
                var copy = asset.CreateRuntimeCopy();
                Assert.AreNotSame(asset.Config.SupportedLanguages, copy.SupportedLanguages);
                Assert.Contains("test-lang", copy.SupportedLanguages);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void CreateRuntimeCopy_DeepCopiesGPUSettings()
        {
            var asset = ScriptableObject.CreateInstance<PiperConfigAsset>();
            try
            {
                asset.Config.GPUSettings.MaxMemoryMB = 1024;
                var copy = asset.CreateRuntimeCopy();
                Assert.AreNotSame(asset.Config.GPUSettings, copy.GPUSettings);
                Assert.AreEqual(1024, copy.GPUSettings.MaxMemoryMB);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void CreateRuntimeCopy_MutationDoesNotAffectOriginal()
        {
            var asset = ScriptableObject.CreateInstance<PiperConfigAsset>();
            try
            {
                asset.Config.DefaultLanguage = "ja";
                asset.Config.SupportedLanguages = new List<string> { "ja", "en" };

                var copy = asset.CreateRuntimeCopy();
                copy.DefaultLanguage = "zh";
                copy.SupportedLanguages.Add("zh");

                Assert.AreEqual("ja", asset.Config.DefaultLanguage);
                Assert.AreEqual(2, asset.Config.SupportedLanguages.Count);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void CreateRuntimeCopy_PreservesFallbackLanguage()
        {
            var asset = ScriptableObject.CreateInstance<PiperConfigAsset>();
            try
            {
                asset.Config.FallbackLanguage = "en";
                var copy = asset.CreateRuntimeCopy();
                Assert.AreEqual("en", copy.FallbackLanguage);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void CreateRuntimeCopy_PreservesAllFields()
        {
            var asset = ScriptableObject.CreateInstance<PiperConfigAsset>();
            try
            {
                // Set non-default values for all fields
                asset.Config.EnableDebugLogging = true;
                asset.Config.DefaultLanguage = "en";
                asset.Config.AutoDetectLanguage = true;
                asset.Config.FallbackLanguage = "ja";
                asset.Config.SupportedLanguages = new List<string> { "en", "ja", "zh" };
                asset.Config.MixedLanguageMode = MultiLanguageMode.ForceDefault;
                asset.Config.MaxCacheSizeMB = 250;
                asset.Config.EnablePhonemeCache = false;
                asset.Config.WorkerThreads = 4;
                asset.Config.Backend = InferenceBackend.CPU;
                asset.Config.EnablePhonemeSilence = true;
                asset.Config.PhonemeSilenceSpec = "_ 0.3,# 0.2";
                asset.Config.SampleRate = 16000;
                asset.Config.NormalizeAudio = false;
                asset.Config.TargetRMSLevel = -15f;
                asset.Config.EnableWarmup = true;
                asset.Config.WarmupIterations = 3;
                asset.Config.TimeoutMs = 60000;
                asset.Config.EnableMultiThreadedInference = true;
                asset.Config.InferenceBatchSize = 8;
                asset.Config.GPUSettings.MaxMemoryMB = 1024;
                asset.Config.AllowFallbackToCPU = false;

                var copy = asset.CreateRuntimeCopy();

                Assert.AreEqual(true, copy.EnableDebugLogging);
                Assert.AreEqual("en", copy.DefaultLanguage);
                Assert.AreEqual(true, copy.AutoDetectLanguage);
                Assert.AreEqual("ja", copy.FallbackLanguage);
                Assert.AreEqual(3, copy.SupportedLanguages.Count);
                Assert.AreEqual(MultiLanguageMode.ForceDefault, copy.MixedLanguageMode);
                Assert.AreEqual(250, copy.MaxCacheSizeMB);
                Assert.AreEqual(false, copy.EnablePhonemeCache);
                Assert.AreEqual(4, copy.WorkerThreads);
                Assert.AreEqual(InferenceBackend.CPU, copy.Backend);
                Assert.AreEqual(true, copy.EnablePhonemeSilence);
                Assert.AreEqual("_ 0.3,# 0.2", copy.PhonemeSilenceSpec);
                Assert.AreEqual(16000, copy.SampleRate);
                Assert.AreEqual(false, copy.NormalizeAudio);
                Assert.AreEqual(-15f, copy.TargetRMSLevel);
                Assert.AreEqual(true, copy.EnableWarmup);
                Assert.AreEqual(3, copy.WarmupIterations);
                Assert.AreEqual(60000, copy.TimeoutMs);
                Assert.AreEqual(true, copy.EnableMultiThreadedInference);
                Assert.AreEqual(8, copy.InferenceBatchSize);
                Assert.AreEqual(1024, copy.GPUSettings.MaxMemoryMB);
                Assert.AreEqual(false, copy.AllowFallbackToCPU);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void ToValidated_DefaultConfig_Succeeds()
        {
            var asset = ScriptableObject.CreateInstance<PiperConfigAsset>();
            try
            {
                // デフォルト設定でバリデーションが通ることを確認
                var validated = asset.ToValidated();
                Assert.IsNotNull(validated);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }
    }
}