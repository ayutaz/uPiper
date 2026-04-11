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

            Assert.That(result.IsValid, Is.True,
                "Result with no entries should be valid");
            Assert.That(result.HasErrors, Is.False,
                "Result with no entries should have no errors");
            Assert.That(result.HasWarnings, Is.False,
                "Result with no entries should have no warnings");
            Assert.That(result.Entries, Is.Empty,
                "Result with no entries should have empty Entries");
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

            Assert.That(result.IsValid, Is.False,
                "Result with error should not be valid");
            Assert.That(result.HasErrors, Is.True,
                "Result with error entry should report HasErrors");
            Assert.That(result.Errors, Has.Count.EqualTo(1),
                "Result should contain exactly one error");
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

            Assert.That(result.IsValid, Is.True,
                "Result with only warnings should still be valid");
            Assert.That(result.HasWarnings, Is.True,
                "Result with warning entry should report HasWarnings");
            Assert.That(result.HasErrors, Is.False,
                "Result with only warnings should not report HasErrors");
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

            Assert.That(summary, Does.Contain("Model is null"),
                "Summary should contain first error message");
            Assert.That(summary, Does.Contain("PhonemeIdMap missing"),
                "Summary should contain second error message");
            Assert.That(summary, Does.Contain("[1]"),
                "Summary should contain numbered index [1]");
            Assert.That(summary, Does.Contain("[2]"),
                "Summary should contain numbered index [2]");
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
            Assert.That(result.HasErrors, Is.False,
                "Default PiperConfig should not produce validation errors");
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

            Assert.That(result.HasErrors, Is.True,
                "Null model asset should produce validation error");
            Assert.That(result.Errors[0].Category, Is.EqualTo(ValidationCategory.Model),
                "Error category should be Model for null model asset");
        }

        [Test]
        public void ValidateForInference_NullVoiceConfig_HasError()
        {
            var config = new PiperConfig();

            var result = InitializationValidator.ValidateForInference(config, new object(), null);

            Assert.That(result.HasErrors, Is.True,
                "Null voice config should produce validation error");
            Assert.That(result.Errors[0].Category, Is.EqualTo(ValidationCategory.VoiceConfig),
                "Error category should be VoiceConfig for null voice config");
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

            Assert.That(result.HasErrors, Is.True,
                "Null PhonemeIdMap should produce validation error");
            var phonemeError = result.Errors.FirstOrDefault(e =>
                e.Category == ValidationCategory.PhonemeIdMap);
            Assert.That(phonemeError, Is.Not.Null,
                "Should contain an error with PhonemeIdMap category");
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

            Assert.That(result.HasErrors, Is.True,
                "Missing required tokens should produce validation error");
            var tokenError = result.Errors.FirstOrDefault(e =>
                e.Message.Contains("required tokens"));
            Assert.That(tokenError, Is.Not.Null,
                "Should contain an error mentioning 'required tokens'");
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

            Assert.That(result.HasWarnings, Is.True,
                "Small PhonemeIdMap should produce a warning");
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

            Assert.That(result.HasErrors, Is.False,
                "Valid config should not produce validation errors");
        }

        #endregion
    }
}