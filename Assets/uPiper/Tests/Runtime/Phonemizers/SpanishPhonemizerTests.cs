using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core.Phonemizers.Backend;

namespace uPiper.Tests.Phonemizers
{
    /// <summary>
    /// Test suite for Spanish phonemizer implementation
    /// </summary>
    [TestFixture]
    public class SpanishPhonemizerTests
    {
        private SpanishPhonemizer phonemizer;
        // Note: Component classes are now internal to the proxy
        // private SpanishG2P g2pEngine;
        // private SpanishTextNormalizer normalizer;
        
        [SetUp]
        public async Task SetUp()
        {
            phonemizer = new SpanishPhonemizer();
            await phonemizer.InitializeAsync(null);
            
            // Component classes are now internal to the proxy
            // g2pEngine = new SpanishG2P();
            // normalizer = new SpanishTextNormalizer();
        }
        
        [TearDown]
        public void TearDown()
        {
            phonemizer?.Dispose();
        }
        
        #region Basic Functionality Tests
        
        [Test]
        public async Task Spanish_ShouldInitializeSuccessfully()
        {
            Assert.IsTrue(phonemizer.IsInitialized);
            Assert.Contains("es", phonemizer.SupportedLanguages);
            Assert.Contains("es-ES", phonemizer.SupportedLanguages);
            Assert.Contains("es-MX", phonemizer.SupportedLanguages);
        }
        
        [Test]
        public async Task Spanish_ShouldPhonemizeSimpleWords()
        {
            var testWords = new Dictionary<string, string[]>
            {
                ["hola"] = new[] { "o", "l", "a" },
                ["mundo"] = new[] { "m", "u", "n", "d", "o" },
                ["casa"] = new[] { "k", "a", "s", "a" },
                ["perro"] = new[] { "p", "e", "r", "o" },
                ["gato"] = new[] { "g", "a", "t", "o" }
            };
            
            foreach (var (word, expectedPhonemes) in testWords)
            {
                var result = await phonemizer.PhonemizeAsync(word, "es-ES");
                
                Assert.IsNotNull(result);
                Assert.IsNotEmpty(result.Phonemes);
                
                // Remove pauses for comparison
                var actualPhonemes = result.Phonemes.Where(p => p != "_").ToArray();
                
                Debug.Log($"'{word}' -> [{string.Join(", ", actualPhonemes)}]");
                CollectionAssert.AreEqual(expectedPhonemes, actualPhonemes, 
                    $"Mismatch for word '{word}'");
            }
        }
        
        [Test]
        public async Task Spanish_ShouldHandleAccentedCharacters()
        {
            var testWords = new Dictionary<string, string[]>
            {
                ["mamá"] = new[] { "m", "a", "m", "a" },
                ["café"] = new[] { "k", "a", "f", "e" },
                ["niño"] = new[] { "n", "i", "ɲ", "o" },
                ["señor"] = new[] { "s", "e", "ɲ", "o", "ɾ" }
            };
            
            foreach (var (word, expectedPhonemes) in testWords)
            {
                var result = await phonemizer.PhonemizeAsync(word, "es-ES");
                var actualPhonemes = result.Phonemes.Where(p => p != "_").ToArray();
                
                Debug.Log($"'{word}' -> [{string.Join(", ", actualPhonemes)}]");
                CollectionAssert.AreEqual(expectedPhonemes, actualPhonemes);
            }
        }
        
        #endregion
        
        #region G2P Engine Tests
        
        [Test]
        public void G2P_ShouldHandleBasicConsonants()
        {
            var tests = new Dictionary<string, string[]>
            {
                ["ba"] = new[] { "b", "a" },
                ["ca"] = new[] { "k", "a" },
                ["da"] = new[] { "d", "a" },
                ["fa"] = new[] { "f", "a" },
                ["ga"] = new[] { "g", "a" },
                ["ja"] = new[] { "x", "a" },
                ["ka"] = new[] { "k", "a" },
                ["la"] = new[] { "l", "a" },
                ["ma"] = new[] { "m", "a" },
                ["na"] = new[] { "n", "a" },
                ["ña"] = new[] { "ɲ", "a" },
                ["pa"] = new[] { "p", "a" },
                ["ra"] = new[] { "ɾ", "a" },
                ["sa"] = new[] { "s", "a" },
                ["ta"] = new[] { "t", "a" },
                ["va"] = new[] { "b", "a" },
                ["wa"] = new[] { "w", "a" },
                ["xa"] = new[] { "ks", "a" },
                ["ya"] = new[] { "j", "a" },
                ["za"] = new[] { "θ", "a" }
            };
            
            foreach (var (input, expected) in tests)
            {
                var result = g2pEngine.Grapheme2Phoneme(input);
                CollectionAssert.AreEqual(expected, result, 
                    $"Failed for input '{input}'");
            }
        }
        
        [Test]
        public void G2P_ShouldHandleDigraphs()
        {
            var tests = new Dictionary<string, string[]>
            {
                ["llave"] = new[] { "ʎ", "a", "b", "e" },
                ["carro"] = new[] { "k", "a", "r", "o" },
                ["chico"] = new[] { "tʃ", "i", "k", "o" },
                ["queso"] = new[] { "k", "e", "s", "o" },
                ["guerra"] = new[] { "g", "e", "r", "a" }
            };
            
            foreach (var (input, expected) in tests)
            {
                var result = g2pEngine.Grapheme2Phoneme(input);
                CollectionAssert.AreEqual(expected, result, 
                    $"Failed for input '{input}'");
            }
        }
        
        [Test]
        public void G2P_ShouldHandleContextDependentRules()
        {
            var tests = new Dictionary<string, string[]>
            {
                // C before e,i -> θ (Spain Spanish)
                ["cena"] = new[] { "θ", "e", "n", "a" },
                ["cita"] = new[] { "θ", "i", "t", "a" },
                
                // G before e,i -> x
                ["gente"] = new[] { "x", "e", "n", "t", "e" },
                ["gitano"] = new[] { "x", "i", "t", "a", "n", "o" },
                
                // R at beginning -> rolled r
                ["rosa"] = new[] { "r", "o", "s", "a" },
                ["rato"] = new[] { "r", "a", "t", "o" }
            };
            
            foreach (var (input, expected) in tests)
            {
                var result = g2pEngine.Grapheme2Phoneme(input);
                CollectionAssert.AreEqual(expected, result, 
                    $"Failed for input '{input}'");
            }
        }
        
        [Test]
        public void G2P_ShouldHandleIntervocalicFricatives()
        {
            var tests = new Dictionary<string, string[]>
            {
                ["lava"] = new[] { "l", "a", "β", "a" },    // b between vowels
                ["cada"] = new[] { "k", "a", "ð", "a" },    // d between vowels
                ["lago"] = new[] { "l", "a", "ɣ", "o" }     // g between vowels
            };
            
            foreach (var (input, expected) in tests)
            {
                var result = g2pEngine.Grapheme2Phoneme(input);
                CollectionAssert.AreEqual(expected, result, 
                    $"Failed for input '{input}'");
            }
        }
        
        #endregion
        
        #region Text Normalizer Tests
        
        [Test]
        public void Normalizer_ShouldExpandNumbers()
        {
            var tests = new Dictionary<string, string>
            {
                ["1 casa"] = "uno casa",
                ["2 perros"] = "dos perros",
                ["10 años"] = "diez años",
                ["21 días"] = "veintiuno días",
                ["100 euros"] = "cien euros",
                ["365 días"] = "trescientos sesenta y cinco días",
                ["1000 personas"] = "mil personas"
            };
            
            foreach (var (input, expected) in tests)
            {
                var result = normalizer.Normalize(input);
                Assert.AreEqual(expected, result, 
                    $"Failed for input '{input}'");
            }
        }
        
        [Test]
        public void Normalizer_ShouldExpandAbbreviations()
        {
            var tests = new Dictionary<string, string>
            {
                ["Sr. García"] = "señor García",
                ["Dra. López"] = "doctora López",
                ["pág. 10"] = "página diez",
                ["etc."] = "etcétera",
                ["EE.UU."] = "Estados Unidos"
            };
            
            foreach (var (input, expected) in tests)
            {
                var result = normalizer.Normalize(input);
                Assert.AreEqual(expected, result, 
                    $"Failed for input '{input}'");
            }
        }
        
        [Test]
        public void Normalizer_ShouldHandleSpanishPunctuation()
        {
            var tests = new Dictionary<string, string>
            {
                ["¿Cómo estás?"] = "Cómo estás?",
                ["¡Hola!"] = "Hola!",
                ["¿¡Qué sorpresa!?"] = "Qué sorpresa!?"
            };
            
            foreach (var (input, expected) in tests)
            {
                var result = normalizer.Normalize(input);
                Assert.AreEqual(expected, result, 
                    $"Failed for input '{input}'");
            }
        }
        
        [Test]
        public void Normalizer_ShouldHandleSpecialCharacters()
        {
            var tests = new Dictionary<string, string>
            {
                ["$100"] = "dólares cien",
                ["50%"] = "cincuenta por ciento",
                ["café & té"] = "café y té",
                ["email@ejemplo.com"] = "email arroba ejemplo.com"
            };
            
            foreach (var (input, expected) in tests)
            {
                var result = normalizer.Normalize(input);
                Assert.AreEqual(expected, result, 
                    $"Failed for input '{input}'");
            }
        }
        
        #endregion
        
        #region Integration Tests
        
        [Test]
        public async Task Spanish_ShouldPhonemizeSentences()
        {
            var sentences = new[]
            {
                "Hola, ¿cómo estás?",
                "Buenos días, señor García.",
                "Me llamo María y tengo 25 años.",
                "¿Dónde está la biblioteca?",
                "Vivo en España desde hace 10 años."
            };
            
            foreach (var sentence in sentences)
            {
                var result = await phonemizer.PhonemizeAsync(sentence, "es-ES");
                
                Assert.IsNotNull(result);
                Assert.IsNotEmpty(result.Phonemes);
                Assert.Greater(result.Phonemes.Count, 10, 
                    $"Too few phonemes for sentence: {sentence}");
                
                Debug.Log($"Sentence: {sentence}");
                Debug.Log($"Phonemes ({result.Phonemes.Count}): {string.Join(" ", result.Phonemes.Take(20))}...");
            }
        }
        
        [Test]
        public async Task Spanish_ShouldHandleDialectVariations()
        {
            var word = "caza"; // c before a -> different in Latin America
            
            // Spain Spanish
            g2pEngine.SetDialect("es-ES");
            var spainResult = g2pEngine.Grapheme2Phoneme(word);
            Assert.Contains("θ", spainResult, "Spain Spanish should use θ");
            
            // Mexican Spanish
            g2pEngine.SetDialect("es-MX");
            var mexicoResult = g2pEngine.Grapheme2Phoneme(word);
            Assert.Contains("s", mexicoResult, "Mexican Spanish should use s");
        }
        
        [Test]
        public async Task Spanish_ShouldProvideTimingInfo()
        {
            var text = "Hola mundo";
            var result = await phonemizer.PhonemizeAsync(text, "es-ES");
            
            Assert.IsNotNull(result.TimingInfo);
            Assert.AreEqual(result.Phonemes.Count, result.TimingInfo.Count);
            
            // Check timing progression
            double lastTime = 0;
            foreach (var timing in result.TimingInfo)
            {
                Assert.GreaterOrEqual(timing.StartTime, lastTime);
                Assert.Greater(timing.Duration, 0);
                lastTime = timing.StartTime;
            }
        }
        
        #endregion
        
        #region Performance Tests
        
        [Test]
        public async Task Spanish_PerformanceBenchmark()
        {
            var testText = "Este es un texto de prueba para evaluar el rendimiento " +
                          "del sistema de conversión de texto a fonemas en español.";
            
            // Warm up
            await phonemizer.PhonemizeAsync("warmup", "es-ES");
            
            // Measure
            var sw = System.Diagnostics.Stopwatch.StartNew();
            const int iterations = 100;
            
            for (int i = 0; i < iterations; i++)
            {
                await phonemizer.PhonemizeAsync(testText, "es-ES");
            }
            
            sw.Stop();
            double avgMs = sw.ElapsedMilliseconds / (double)iterations;
            
            Debug.Log($"Spanish phonemization average time: {avgMs:F2} ms");
            Assert.Less(avgMs, 10, "Should process text in under 10ms average");
        }
        
        #endregion
    }
}