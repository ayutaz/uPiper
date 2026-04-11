using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace uPiper.Core
{
    /// <summary>
    /// Aggregates initialization validation results as an immutable collection.
    /// </summary>
    public sealed class InitializationValidationResult
    {
        /// <summary>Result entry for a single validation check.</summary>
        public sealed class Entry
        {
            public ValidationCategory Category { get; }
            public ValidationSeverity Severity { get; }
            public string Message { get; }
            public string ActionableAdvice { get; }
            public Exception Exception { get; }

            public Entry(
                ValidationCategory category,
                ValidationSeverity severity,
                string message,
                string actionableAdvice,
                Exception exception = null)
            {
                Category = category;
                Severity = severity;
                Message = message ?? throw new ArgumentNullException(nameof(message));
                ActionableAdvice = actionableAdvice ?? string.Empty;
                Exception = exception;
            }
        }

        private readonly List<Entry> _entries;

        public IReadOnlyList<Entry> Entries => _entries;
        public bool HasErrors => _entries.Any(e => e.Severity == ValidationSeverity.Error);
        public bool HasWarnings => _entries.Any(e => e.Severity == ValidationSeverity.Warning);
        public bool IsValid => !HasErrors;

        public IReadOnlyList<Entry> Errors =>
            _entries.Where(e => e.Severity == ValidationSeverity.Error).ToList();

        public IReadOnlyList<Entry> Warnings =>
            _entries.Where(e => e.Severity == ValidationSeverity.Warning).ToList();

        internal InitializationValidationResult(List<Entry> entries)
        {
            _entries = entries ?? new List<Entry>();
        }

        public string FormatErrorSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("PiperTTS initialization failed with the following errors:");
            sb.AppendLine();
            var errorIndex = 1;
            foreach (var entry in Errors)
            {
                sb.AppendLine($"  [{errorIndex}] [{entry.Category}] {entry.Message}");
                if (!string.IsNullOrEmpty(entry.ActionableAdvice))
                    sb.AppendLine($"      -> {entry.ActionableAdvice}");
                errorIndex++;
            }
            return sb.ToString().TrimEnd();
        }

        public string FormatWarningSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("PiperTTS initialization warnings:");
            foreach (var entry in Warnings)
            {
                sb.AppendLine($"  [Warning][{entry.Category}] {entry.Message}");
                if (!string.IsNullOrEmpty(entry.ActionableAdvice))
                    sb.AppendLine($"      -> {entry.ActionableAdvice}");
            }
            return sb.ToString().TrimEnd();
        }
    }

    public enum ValidationCategory
    {
        RuntimeEnvironment,
        Model,
        VoiceConfig,
        PhonemeIdMap,
        Dictionary,
        Phonemizer,
        InferenceBackend,
        Platform,
        PuaTokenMapping,
        StreamingAssets
    }

    public enum ValidationSeverity
    {
        Error,
        Warning
    }
}