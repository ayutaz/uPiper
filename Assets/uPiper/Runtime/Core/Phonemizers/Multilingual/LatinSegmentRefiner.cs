using System;
using System.Collections.Generic;

namespace uPiper.Core.Phonemizers.Multilingual
{
    /// <summary>
    /// Refines Latin-script segments by re-classifying them using trigram-based detection.
    /// Non-Latin segments (ja, zh, ko) pass through unchanged.
    /// Short Latin segments (below <see cref="TrigramLanguageDetector.MinCharsForDetection"/>)
    /// retain the default language.
    /// </summary>
    internal sealed class LatinSegmentRefiner
    {
        private readonly TrigramLanguageDetector _trigramDetector;
        private readonly Dictionary<string, HashSet<string>> _keywordDictionary;

        /// <summary>
        /// Creates a LatinSegmentRefiner with trigram detection and optional keyword dictionary.
        /// </summary>
        /// <param name="trigramDetector">Trigram detector for scoring Latin segments.</param>
        public LatinSegmentRefiner(TrigramLanguageDetector trigramDetector)
        {
            _trigramDetector = trigramDetector
                ?? throw new ArgumentNullException(nameof(trigramDetector));
            _keywordDictionary = BuildKeywordDictionary();
        }

        /// <summary>
        /// Refines a list of (language, text) segments by re-classifying Latin segments.
        /// </summary>
        /// <param name="segments">Original segments from UnicodeLanguageDetector.</param>
        /// <param name="defaultLatinLanguage">Fallback language for unconfident detection.</param>
        /// <returns>Refined segment list with Latin segments potentially re-classified.</returns>
        public List<(string language, string text)> Refine(
            IReadOnlyList<(string language, string text)> segments,
            string defaultLatinLanguage)
        {
            var result = new List<(string, string)>(segments.Count);

            for (var i = 0; i < segments.Count; i++)
            {
                var (lang, text) = segments[i];

                // Non-Latin segments pass through unchanged
                if (!LanguageConstants.IsLatinLanguage(lang))
                {
                    result.Add((lang, text));
                    continue;
                }

                // Try keyword detection first (works for any length)
                var keywordLang = DetectByKeywords(text);
                if (keywordLang != null)
                {
                    result.Add((keywordLang, text));
                    continue;
                }

                // Use trigram detection for sufficiently long text
                var trigramResult = _trigramDetector.Detect(text);
                if (trigramResult.IsConfident && trigramResult.Language != null)
                {
                    result.Add((trigramResult.Language, text));
                }
                else
                {
                    // Keep default language
                    result.Add((lang, text));
                }
            }

            return result;
        }

        /// <summary>
        /// Detects language by checking for known language-specific keywords.
        /// Returns null if no keyword match is found.
        /// </summary>
        private string DetectByKeywords(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var lower = text.ToLowerInvariant();

            // Check each language's keywords
            string bestLang = null;
            var bestCount = 0;

            foreach (var kvp in _keywordDictionary)
            {
                var count = 0;
                foreach (var keyword in kvp.Value)
                {
                    if (lower.Contains(keyword))
                        count++;
                }

                if (count > bestCount)
                {
                    bestCount = count;
                    bestLang = kvp.Key;
                }
            }

            // Require at least 1 keyword match, and it must be unambiguous
            // (no other language has the same count)
            if (bestCount == 0)
                return null;

            var ambiguous = false;
            foreach (var kvp in _keywordDictionary)
            {
                if (kvp.Key == bestLang)
                    continue;

                var count = 0;
                foreach (var keyword in kvp.Value)
                {
                    if (lower.Contains(keyword))
                        count++;
                }

                if (count == bestCount)
                {
                    ambiguous = true;
                    break;
                }
            }

            return ambiguous ? null : bestLang;
        }

        /// <summary>
        /// Builds the keyword dictionary for Latin-script language detection.
        /// Contains 30-50 highly distinctive words per language.
        /// </summary>
        private static Dictionary<string, HashSet<string>> BuildKeywordDictionary()
        {
            return new Dictionary<string, HashSet<string>>
            {
                ["fr"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "bonjour", "merci", "beaucoup", "monsieur", "madame",
                    "aujourd", "toujours", "quelque", "pourquoi", "comment",
                    "oui", "aussi", "avec", "mais", "dans",
                    "cette", "sont", "nous", "vous", "leur",
                    "peut", "fait", "etre", "avoir", "faire",
                    "comme", "tout", "bien", "plus", "tres",
                    "encore", "depuis", "avant", "apres", "pendant",
                    "chez", "entre", "sous", "aussi", "jamais",
                    "parce", "parler", "mange", "boire", "cherche",
                    "travail", "ecole", "maison", "jour", "monde"
                },
                ["es"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "hola", "gracias", "buenos", "buenas", "senor",
                    "senora", "donde", "porque", "cuando", "siempre",
                    "tambien", "ahora", "aqui", "pero", "como",
                    "esta", "este", "esto", "estos", "estas",
                    "tiene", "puede", "hacer", "decir", "saber",
                    "querer", "mucho", "poco", "muy", "bien",
                    "mejor", "peor", "nuevo", "mismo", "otro",
                    "todo", "nada", "algo", "cada", "entre",
                    "sobre", "bajo", "desde", "hasta", "hacia",
                    "mundo", "casa", "tiempo", "trabajo", "vida"
                },
                ["pt"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "obrigado", "obrigada", "muito", "voce", "todos",
                    "tambem", "agora", "aqui", "onde", "quando",
                    "porque", "ainda", "antes", "depois", "sempre",
                    "nunca", "ja", "hoje", "ontem", "amanha",
                    "bom", "boa", "isso", "isto", "esse",
                    "esta", "este", "esse", "aquele", "nosso",
                    "seu", "meu", "dele", "dela", "gente",
                    "fazer", "dizer", "saber", "poder", "olhar",
                    "mundo", "casa", "tempo", "trabalho", "vida",
                    "cidade", "filho", "filha", "homem", "mulher"
                },
                ["en"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "the", "and", "that", "have", "with",
                    "this", "will", "your", "from", "they",
                    "been", "would", "their", "which", "about",
                    "could", "other", "into", "than", "some",
                    "these", "them", "should", "what", "there",
                    "when", "where", "because", "through", "between",
                    "after", "before", "while", "although", "however",
                    "whether", "already", "without", "against", "during",
                    "something", "everything", "nothing", "anything", "everyone",
                    "morning", "evening", "night", "today", "tomorrow"
                }
            };
        }
    }
}
