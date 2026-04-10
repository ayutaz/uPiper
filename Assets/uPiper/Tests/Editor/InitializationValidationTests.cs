using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using uPiper.Core;
using uPiper.Tests.Editor.TestHelpers;

namespace uPiper.Tests.Editor
{
    [TestFixture]
    public class InitializationValidationTests
    {
        #region InitializationValidationResult Tests

        [Test]
        public void Result_NoEntries_IsValid()
        {
            var result = new InitializationValidationResult(new List<InitializationValidationResult.Entry>());

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.HasErrors, Is.False);
            Assert.That(result.HasWarnings, Is.False);
            Assert.That(result.Entries, Is.Empty);
        }

        [Test]
        public void Result_WithError_HasErrorsTrue()
        {
            var entries = new List<InitializationValidationResult.Entry>
            {
                new(ValidationCategory.Model, ValidationSeverity.Error,
                    "Model is null", "Load a model")
            };
            var result = new InitializationValidationResult(entries);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Errors, Has.Count.EqualTo(1));
        }

        [Test]
        public void Result_WithWarningOnly_IsStillValid()
        {
            var entries = new List<InitializationValidationResult.Entry>
            {
                new(ValidationCategory.RuntimeEnvironment, ValidationSeverity.Warning,
                    "Old Unity", "Upgrade")
            };
            var result = new InitializationValidationResult(entries);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.HasWarnings, Is.True);
            Assert.That(result.HasErrors, Is.False);
        }

        [Test]
        public void FormatErrorSummary_ContainsAllErrors()
        {
            var entries = new List<InitializationValidationResult.Entry>
            {
                new(ValidationCategory.Model, ValidationSeverity.Error,
                    "Model is null", "Load a model"),
                new(ValidationCategory.PhonemeIdMap, ValidationSeverity.Error,
                    "PhonemeIdMap missing", "Load config JSON")
            };
            var result = new InitializationValidationResult(entries);
            var summary = result.FormatErrorSummary();

            StringAssert.Contains("Model is null", summary);
            StringAssert.Contains("PhonemeIdMap missing", summary);
            StringAssert.Contains("[1]", summary);
            StringAssert.Contains("[2]", summary);
        }

        [Test]
        public void Entry_NullMessage_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                new InitializationValidationResult.Entry(
                    ValidationCategory.Model, ValidationSeverity.Error,
                    null, "advice"));
        }

        #endregion

        #region InitializationValidator Tests

        [Test]
        public void ValidateForInitialize_DefaultConfig_IsValid()
        {
            var config = new PiperConfig();
            var result = InitializationValidator.ValidateForInitialize(config);

            // Default config should not produce errors (warnings are ok)
            Assert.That(result.HasErrors, Is.False);
        }

        [Test]
        public void ValidateForInference_NullModelAsset_HasError()
        {
            var config = new PiperConfig();
            var voiceConfig = new PiperVoiceConfig
            {
                VoiceId = "test",
                Language = "ja",
                PhonemeIdMap = TestPhonemeIdMapFactory.CreateMinimal()
            };

            var result = InitializationValidator.ValidateForInference(config, null, voiceConfig);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Errors[0].Category, Is.EqualTo(ValidationCategory.Model));
        }

        [Test]
        public void ValidateForInference_NullVoiceConfig_HasError()
        {
            var config = new PiperConfig();

            var result = InitializationValidator.ValidateForInference(config, new object(), null);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Errors[0].Category, Is.EqualTo(ValidationCategory.VoiceConfig));
        }

        [Test]
        public void ValidateForInference_NullPhonemeIdMap_HasError()
        {
            var config = new PiperConfig();
            var voiceConfig = new PiperVoiceConfig
            {
                VoiceId = "test",
                Language = "ja",
                PhonemeIdMap = null
            };

            var result = InitializationValidator.ValidateForInference(
                config, new object(), voiceConfig);

            Assert.That(result.HasErrors, Is.True);
            var phonemeError = result.Errors.FirstOrDefault(e =>
                e.Category == ValidationCategory.PhonemeIdMap);
            Assert.That(phonemeError, Is.Not.Null);
        }

        [Test]
        public void ValidateForInference_MissingRequiredTokens_HasError()
        {
            var config = new PiperConfig();
            var voiceConfig = new PiperVoiceConfig
            {
                VoiceId = "test",
                Language = "ja",
                PhonemeIdMap = new Dictionary<string, int[]>
                {
                    { "a", new[] { 3 } }
                }
            };

            var result = InitializationValidator.ValidateForInference(
                config, new object(), voiceConfig);

            Assert.That(result.HasErrors, Is.True);
            var tokenError = result.Errors.FirstOrDefault(e =>
                e.Message.Contains("required tokens"));
            Assert.That(tokenError, Is.Not.Null);
        }

        [Test]
        public void ValidateForInference_SmallPhonemeIdMap_HasWarning()
        {
            var config = new PiperConfig();
            var voiceConfig = new PiperVoiceConfig
            {
                VoiceId = "test",
                Language = "ja",
                PhonemeIdMap = TestPhonemeIdMapFactory.CreateMinimal()
            };

            var result = InitializationValidator.ValidateForInference(
                config, new object(), voiceConfig);

            Assert.That(result.HasWarnings, Is.True);
        }

        [Test]
        public void ValidateForInference_ValidConfig_NoErrors()
        {
            var config = new PiperConfig();
            var voiceConfig = new PiperVoiceConfig
            {
                VoiceId = "test-voice",
                Language = "ja",
                PhonemeIdMap = TestPhonemeIdMapFactory.CreateFull()
            };

            var result = InitializationValidator.ValidateForInference(
                config, new object(), voiceConfig);

            Assert.That(result.HasErrors, Is.False);
        }

        #endregion
    }
}