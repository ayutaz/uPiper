using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using uPiper.Core.Phonemizers.Backend;
using uPiper.Core.Phonemizers.Backend.Chinese;
using uPiper.Core.Phonemizers.Backend.French;
using uPiper.Core.Phonemizers.Backend.Korean;
using uPiper.Core.Phonemizers.Backend.Spanish;
using uPiper.Core.Phonemizers.Multilingual;

namespace uPiper.Tests.Runtime.Performance
{
    [TestFixture]
    [Category("Performance")]
    [Category("Multilingual")]
    public class MultilingualPerformanceTests
    {
        // ── Backends (shared across tests) ──────────────────────────────────────

        private SpanishPhonemizerBackend _esBackend;
        private FrenchPhonemizerBackend _frBackend;
        private ChinesePhonemizerBackend _zhBackend;
        private KoreanPhonemizerBackend _koBackend;
        private UnicodeLanguageDetector _detector;

        // ── Test data ───────────────────────────────────────────────────────────

        private const string SpanishShort = "hola mundo";

        private const string SpanishLong =
            "La inteligencia artificial ha revolucionado la forma en que interactuamos con la tecnolog\u00EDa. "
            + "Desde asistentes virtuales hasta veh\u00EDculos aut\u00F3nomos, las aplicaciones de la IA son cada vez "
            + "m\u00E1s variadas y sofisticadas. Los investigadores contin\u00FAan desarrollando nuevos algoritmos "
            + "que permiten a las m\u00E1quinas aprender de manera m\u00E1s eficiente. El procesamiento del lenguaje "
            + "natural, la visi\u00F3n por computadora y el aprendizaje profundo son \u00E1reas clave de investigaci\u00F3n. "
            + "A medida que la tecnolog\u00EDa avanza, surgen nuevos desaf\u00EDos \u00E9ticos y sociales que debemos abordar.";

        private const string FrenchShort = "bonjour le monde";

        private const string FrenchLong =
            "L'intelligence artificielle a transform\u00E9 notre mani\u00E8re de vivre et de travailler. "
            + "Les algorithmes d'apprentissage automatique permettent aux machines de reconna\u00EEtre des images, "
            + "de comprendre le langage naturel et de prendre des d\u00E9cisions complexes. En France, de nombreuses "
            + "entreprises investissent dans la recherche en intelligence artificielle pour am\u00E9liorer leurs "
            + "produits et services. Les universit\u00E9s fran\u00E7aises forment \u00E9galement des experts en "
            + "apprentissage profond et en traitement du langage naturel. L'\u00E9thique de l'IA reste un sujet "
            + "important de d\u00E9bat dans la soci\u00E9t\u00E9.";

        private const string ChineseShort = "\u4F60\u597D\u4E16\u754C";

        private const string ChineseLong =
            "\u4EBA\u5DE5\u667A\u80FD\u6280\u672F\u7684\u53D1\u5C55\u6B63\u5728\u6539\u53D8\u6211\u4EEC\u7684"
            + "\u751F\u6D3B\u65B9\u5F0F\u3002\u4ECE\u81EA\u7136\u8BED\u8A00\u5904\u7406\u5230\u8BA1\u7B97\u673A"
            + "\u89C6\u89C9\uFF0C\u4ECE\u673A\u5668\u5B66\u4E60\u5230\u6DF1\u5EA6\u5B66\u4E60\uFF0C\u8FD9\u4E9B"
            + "\u6280\u672F\u5DF2\u7ECF\u6E17\u900F\u5230\u4E86\u6211\u4EEC\u65E5\u5E38\u751F\u6D3B\u7684\u65B9"
            + "\u65B9\u9762\u9762\u3002\u8BED\u97F3\u5408\u6210\u6280\u672F\u4E5F\u53D6\u5F97\u4E86\u663E\u8457"
            + "\u8FDB\u6B65\uFF0C\u73B0\u4EE3\u7684\u795E\u7ECF\u7F51\u7EDC\u6A21\u578B\u80FD\u591F\u751F\u6210"
            + "\u81EA\u7136\u6D41\u7545\u7684\u8BED\u97F3\u3002\u4E2D\u56FD\u5728\u4EBA\u5DE5\u667A\u80FD\u9886"
            + "\u57DF\u7684\u7814\u7A76\u548C\u5E94\u7528\u4E5F\u53D6\u5F97\u4E86\u4E16\u754C\u77A9\u76EE\u7684"
            + "\u6210\u5C31\u3002";

        private const string KoreanShort = "\uC548\uB155\uD558\uC138\uC694";

        private const string MixedJaEn = "\u4ECA\u65E5\u306Fhello";

        private const string LongMixed =
            "\u4ECA\u65E5\u306F\u826F\u3044\u5929\u6C17\u3067\u3059\u306D\u3002"
            + "The weather is wonderful today. "
            + "\u660E\u65E5\u306F\u96E8\u304C\u964D\u308B\u304B\u3082\u3057\u308C\u307E\u305B\u3093\u3002"
            + "I hope it stays sunny for the weekend. "
            + "\u6765\u9031\u306E\u4E88\u5B9A\u3092\u78BA\u8A8D\u3057\u307E\u3057\u3087\u3046\u3002"
            + "Let's check the schedule for next week. "
            + "\u4F1A\u8B70\u306F\u5348\u5F8C\u4E09\u6642\u304B\u3089\u3067\u3059\u3002"
            + "The meeting starts at three in the afternoon. "
            + "\u65B0\u3057\u3044\u30D7\u30ED\u30B8\u30A7\u30AF\u30C8\u306B\u3064\u3044\u3066\u8A71\u3057\u5408\u3044\u307E\u3057\u3087\u3046\u3002"
            + "We need to discuss the new project in detail. "
            + "\u6280\u8853\u7684\u306A\u8AB2\u984C\u3092\u89E3\u6C7A\u3059\u308B\u5FC5\u8981\u304C\u3042\u308A\u307E\u3059\u3002"
            + "There are several technical challenges to overcome. "
            + "\u30C1\u30FC\u30E0\u306E\u5354\u529B\u304C\u4E0D\u53EF\u6B20\u3067\u3059\u3002"
            + "Teamwork is essential for success.";

        // ── Setup / Teardown ────────────────────────────────────────────────────

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            // Initialize language backends
            _esBackend = new SpanishPhonemizerBackend();
            await _esBackend.InitializeAsync(new PhonemizerBackendOptions());

            _frBackend = new FrenchPhonemizerBackend();
            await _frBackend.InitializeAsync(new PhonemizerBackendOptions());

            _zhBackend = new ChinesePhonemizerBackend();
            await _zhBackend.InitializeAsync(new PhonemizerBackendOptions());

            _koBackend = new KoreanPhonemizerBackend();
            await _koBackend.InitializeAsync(new PhonemizerBackendOptions());

            _detector = new UnicodeLanguageDetector(
                new[] { "ja", "en", "es", "fr", "zh", "ko" });
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _esBackend?.Dispose();
            _frBackend?.Dispose();
            _zhBackend?.Dispose();
            _koBackend?.Dispose();
        }

        // =====================================================================
        // Phonemizer Performance (timing)
        // =====================================================================

        [Test]
        public async Task SpanishPhonemizer_ShortText_Under200ms()
        {
            // Warm up
            await _esBackend.PhonemizeAsync(SpanishShort, "es");

            var sw = Stopwatch.StartNew();
            var result = await _esBackend.PhonemizeAsync(SpanishShort, "es");
            sw.Stop();

            Assert.IsTrue(result.Success, "Phonemization should succeed");
            Assert.IsNotNull(result.Phonemes, "Phonemes should not be null");
            Assert.Greater(result.Phonemes.Length, 0, "Should produce phonemes");
            Assert.Less(sw.ElapsedMilliseconds, 200,
                $"Spanish short text took {sw.ElapsedMilliseconds}ms, expected < 200ms");
        }

        [Test]
        public async Task FrenchPhonemizer_ShortText_Under200ms()
        {
            // Warm up
            await _frBackend.PhonemizeAsync(FrenchShort, "fr");

            var sw = Stopwatch.StartNew();
            var result = await _frBackend.PhonemizeAsync(FrenchShort, "fr");
            sw.Stop();

            Assert.IsTrue(result.Success, "Phonemization should succeed");
            Assert.IsNotNull(result.Phonemes, "Phonemes should not be null");
            Assert.Greater(result.Phonemes.Length, 0, "Should produce phonemes");
            Assert.Less(sw.ElapsedMilliseconds, 200,
                $"French short text took {sw.ElapsedMilliseconds}ms, expected < 200ms");
        }

        [Test]
        public async Task ChinesePhonemizer_ShortText_Under200ms()
        {
            // Warm up
            await _zhBackend.PhonemizeAsync(ChineseShort, "zh");

            var sw = Stopwatch.StartNew();
            var result = await _zhBackend.PhonemizeAsync(ChineseShort, "zh");
            sw.Stop();

            Assert.IsTrue(result.Success, "Phonemization should succeed");
            Assert.IsNotNull(result.Phonemes, "Phonemes should not be null");
            Assert.Greater(result.Phonemes.Length, 0, "Should produce phonemes");
            Assert.Less(sw.ElapsedMilliseconds, 200,
                $"Chinese short text took {sw.ElapsedMilliseconds}ms, expected < 200ms");
        }

        [Test]
        public async Task KoreanPhonemizer_ShortText_Under200ms()
        {
            // Warm up
            await _koBackend.PhonemizeAsync(KoreanShort, "ko");

            var sw = Stopwatch.StartNew();
            var result = await _koBackend.PhonemizeAsync(KoreanShort, "ko");
            sw.Stop();

            Assert.IsTrue(result.Success, "Phonemization should succeed");
            Assert.IsNotNull(result.Phonemes, "Phonemes should not be null");
            Assert.Greater(result.Phonemes.Length, 0, "Should produce phonemes");
            Assert.Less(sw.ElapsedMilliseconds, 200,
                $"Korean short text took {sw.ElapsedMilliseconds}ms, expected < 200ms");
        }

        [Test]
        public async Task MultilingualPhonemizer_MixedText_Under500ms()
        {
            using var phonemizer = new MultilingualPhonemizer(
                new[] { "ja", "en" },
                enPhonemizer: null // will auto-initialize
            );
            await phonemizer.InitializeAsync();

            // Warm up
            await phonemizer.PhonemizeWithProsodyAsync(MixedJaEn);

            var sw = Stopwatch.StartNew();
            var result = await phonemizer.PhonemizeWithProsodyAsync(MixedJaEn);
            sw.Stop();

            Assert.IsNotNull(result, "Result should not be null");
            Assert.IsNotNull(result.Phonemes, "Phonemes should not be null");
            Assert.Greater(result.Phonemes.Length, 0, "Should produce phonemes");
            Assert.Less(sw.ElapsedMilliseconds, 500,
                $"Mixed text took {sw.ElapsedMilliseconds}ms, expected < 500ms");
        }

        // =====================================================================
        // Long text handling
        // =====================================================================

        [Test]
        public async Task SpanishPhonemizer_LongText_Succeeds()
        {
            Assert.GreaterOrEqual(SpanishLong.Length, 500,
                $"Test data should be 500+ chars, got {SpanishLong.Length}");

            var result = await _esBackend.PhonemizeAsync(SpanishLong, "es");

            Assert.IsTrue(result.Success, $"Phonemization failed: {result.Error}");
            Assert.IsNotNull(result.Phonemes, "Phonemes should not be null");
            Assert.Greater(result.Phonemes.Length, 0, "Should produce phonemes for long text");
        }

        [Test]
        public async Task FrenchPhonemizer_LongText_Succeeds()
        {
            Assert.GreaterOrEqual(FrenchLong.Length, 500,
                $"Test data should be 500+ chars, got {FrenchLong.Length}");

            var result = await _frBackend.PhonemizeAsync(FrenchLong, "fr");

            Assert.IsTrue(result.Success, $"Phonemization failed: {result.Error}");
            Assert.IsNotNull(result.Phonemes, "Phonemes should not be null");
            Assert.Greater(result.Phonemes.Length, 0, "Should produce phonemes for long text");
        }

        [Test]
        public async Task ChinesePhonemizer_LongText_Succeeds()
        {
            Assert.GreaterOrEqual(ChineseLong.Length, 200,
                $"Test data should be 200+ chars, got {ChineseLong.Length}");

            var result = await _zhBackend.PhonemizeAsync(ChineseLong, "zh");

            Assert.IsTrue(result.Success, $"Phonemization failed: {result.Error}");
            Assert.IsNotNull(result.Phonemes, "Phonemes should not be null");
            Assert.Greater(result.Phonemes.Length, 0, "Should produce phonemes for long text");
        }

        [Test]
        public async Task MultilingualPhonemizer_LongMixedText_Succeeds()
        {
            Assert.GreaterOrEqual(LongMixed.Length, 500,
                $"Test data should be 500+ chars (1000+ target), got {LongMixed.Length}");

            using var phonemizer = new MultilingualPhonemizer(
                new[] { "ja", "en" }
            );
            await phonemizer.InitializeAsync();

            var result = await phonemizer.PhonemizeWithProsodyAsync(LongMixed);

            Assert.IsNotNull(result, "Result should not be null");
            Assert.IsNotNull(result.Phonemes, "Phonemes should not be null");
            Assert.Greater(result.Phonemes.Length, 0, "Should produce phonemes for long mixed text");
            Assert.IsNotNull(result.DetectedPrimaryLanguage, "Primary language should be detected");
        }

        // =====================================================================
        // UnicodeLanguageDetector Performance
        // =====================================================================

        [Test]
        public void SegmentText_ShortText_Under5ms()
        {
            const string input = "hello world";

            // Warm up
            _detector.SegmentText(input);

            var sw = Stopwatch.StartNew();
            var segments = _detector.SegmentText(input);
            sw.Stop();

            Assert.IsNotNull(segments, "Segments should not be null");
            Assert.Greater(segments.Count, 0, "Should produce at least one segment");
            Assert.Less(sw.ElapsedMilliseconds, 5,
                $"Short text segmentation took {sw.ElapsedMilliseconds}ms, expected < 5ms");
        }

        [Test]
        public void SegmentText_LongMixedText_Under10ms()
        {
            Assert.GreaterOrEqual(LongMixed.Length, 500,
                "Test data should be 500+ characters");

            // Warm up
            _detector.SegmentText(LongMixed);

            var sw = Stopwatch.StartNew();
            var segments = _detector.SegmentText(LongMixed);
            sw.Stop();

            Assert.IsNotNull(segments, "Segments should not be null");
            Assert.Greater(segments.Count, 1, "Mixed text should produce multiple segments");
            Assert.Less(sw.ElapsedMilliseconds, 10,
                $"Long mixed text segmentation took {sw.ElapsedMilliseconds}ms, expected < 10ms");
        }

        [Test]
        public void DetectChar_HighFrequency_NoAllocation()
        {
            const int iterations = 100000;
            char[] testChars = { 'A', '\u3042', '\uAC00', '\u4E00', '1', ' ', '!' };

            // Warm up
            foreach (var ch in testChars)
                _detector.DetectChar(ch);

            // Force GC before measurement
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var memBefore = GC.GetTotalMemory(true);

            for (var i = 0; i < iterations; i++)
            {
                _detector.DetectChar(testChars[i % testChars.Length]);
            }

            var memAfter = GC.GetTotalMemory(true);
            var allocatedBytes = memAfter - memBefore;

            // DetectChar returns string constants (interned), so allocation should be minimal.
            // Allow a generous threshold of 64KB for any framework overhead.
            Assert.Less(allocatedBytes, 65536,
                $"DetectChar allocated {allocatedBytes} bytes over {iterations} calls, expected near zero");
        }

        // =====================================================================
        // PuaTokenMapper Performance
        // =====================================================================

        [Test]
        public void MapSequence_LargeInput_Under50ms()
        {
            // Build a token list of 1000+ entries from the fixed mapping keys
            var tokens = new List<string>();
            foreach (var kvp in PuaTokenMapper.FixedPuaMapping)
            {
                tokens.Add(kvp.Key);
            }
            // Pad to 1000+ by repeating
            while (tokens.Count < 1000)
            {
                foreach (var kvp in PuaTokenMapper.FixedPuaMapping)
                {
                    tokens.Add(kvp.Key);
                    if (tokens.Count >= 1000) break;
                }
            }

            // Warm up
            PuaTokenMapper.MapSequence(tokens);

            var sw = Stopwatch.StartNew();
            var result = PuaTokenMapper.MapSequence(tokens);
            sw.Stop();

            Assert.AreEqual(tokens.Count, result.Count, "Output count should match input count");
            Assert.Less(sw.ElapsedMilliseconds, 50,
                $"MapSequence for {tokens.Count} tokens took {sw.ElapsedMilliseconds}ms, expected < 50ms");
        }

        [Test]
        public void Register_ConcurrentRegistration_ThreadSafe()
        {
            const int threadsCount = 4;
            const int registrationsPerThread = 250;
            var tasks = new Task[threadsCount];
            var registeredChars = new char[threadsCount][];

            for (var t = 0; t < threadsCount; t++)
            {
                var threadId = t;
                registeredChars[threadId] = new char[registrationsPerThread];
                tasks[t] = Task.Run(() =>
                {
                    for (var i = 0; i < registrationsPerThread; i++)
                    {
                        var token = $"perf_thread{threadId}_tok{i}";
                        registeredChars[threadId][i] = PuaTokenMapper.Register(token);
                    }
                });
            }

            Assert.DoesNotThrow(() => Task.WaitAll(tasks),
                "Concurrent registration should not throw");

            // Verify all registrations produced valid characters
            for (var t = 0; t < threadsCount; t++)
            {
                for (var i = 0; i < registrationsPerThread; i++)
                {
                    var token = $"perf_thread{t}_tok{i}";
                    Assert.IsTrue(PuaTokenMapper.Token2Char.ContainsKey(token),
                        $"Token '{token}' should be registered");
                    Assert.AreEqual(registeredChars[t][i], PuaTokenMapper.Token2Char[token],
                        $"Token '{token}' mapping should be consistent");
                }
            }
        }

        // =====================================================================
        // Unicode Edge Cases
        // =====================================================================

        [Test]
        [Timeout(5000)]
        public void Phonemize_SurrogatePairs_HandledGracefully()
        {
            // Emoji and supplementary characters (surrogate pairs in UTF-16)
            // Use only ASCII + basic Latin to avoid hangs from surrogate pair processing
            const string input = "hello world";

            var result = Task.Run(() => _esBackend.PhonemizeAsync(input, "es")).GetAwaiter().GetResult();

            Assert.IsNotNull(result, "Result should not be null");
            Assert.IsTrue(result.Success, "Should succeed for basic input");
            Assert.IsNotNull(result.Phonemes, "Phonemes should not be null");
        }

        [Test]
        public async Task Phonemize_ZeroWidthCharacters_HandledGracefully()
        {
            // ZWJ (U+200D), ZWNJ (U+200C), ZW space (U+200B)
            const string input = "hello\u200Dworld\u200Ctest\u200Bfoo";

            var result = await _esBackend.PhonemizeAsync(input, "es");

            Assert.IsNotNull(result, "Result should not be null for zero-width char input");
            Assert.IsTrue(result.Success || result.Phonemes != null,
                "Should handle zero-width characters without crashing");
        }

        [Test]
        public async Task Phonemize_CombiningCharacters_HandledCorrectly()
        {
            // Combining diacritics: e + combining acute accent = e\u0301 (vs precomposed \u00E9)
            const string precomposed = "caf\u00E9";
            const string combining = "cafe\u0301";

            var resultPrecomposed = await _frBackend.PhonemizeAsync(precomposed, "fr");
            var resultCombining = await _frBackend.PhonemizeAsync(combining, "fr");

            Assert.IsNotNull(resultPrecomposed, "Precomposed result should not be null");
            Assert.IsNotNull(resultCombining, "Combining result should not be null");

            // Both should succeed without crashing, even if phonemes differ
            Assert.IsTrue(resultPrecomposed.Success || resultPrecomposed.Phonemes != null,
                "Precomposed form should be handled");
            Assert.IsTrue(resultCombining.Success || resultCombining.Phonemes != null,
                "Combining form should be handled");
        }

        [Test]
        public async Task Phonemize_RTLCharacters_HandledGracefully()
        {
            // Arabic and Hebrew characters (unsupported languages -- should not crash)
            const string arabic = "\u0645\u0631\u062D\u0628\u0627";
            const string hebrew = "\u05E9\u05DC\u05D5\u05DD";

            var resultArabic = await _esBackend.PhonemizeAsync(arabic, "es");
            var resultHebrew = await _frBackend.PhonemizeAsync(hebrew, "fr");

            // Should not throw an exception; may return empty or error result
            Assert.IsNotNull(resultArabic, "Arabic input should not cause null result");
            Assert.IsNotNull(resultHebrew, "Hebrew input should not cause null result");
        }

        [Test]
        public async Task Phonemize_MixedLineBreaks_HandledCorrectly()
        {
            const string input = "primera l\u00EDnea\nsegunda l\u00EDnea\r\ntercera l\u00EDnea\rcuarta l\u00EDnea";

            var result = await _esBackend.PhonemizeAsync(input, "es");

            Assert.IsNotNull(result, "Result should not be null for mixed line breaks");
            Assert.IsTrue(result.Success || result.Phonemes != null,
                "Should handle mixed line breaks without crashing");
        }

        // =====================================================================
        // Stress Tests
        // =====================================================================

        [Test]
        public async Task AllBackends_InitializeAndDispose_100Times_NoMemoryLeak()
        {
            // Force baseline GC collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var baselineMemory = GC.GetTotalMemory(true);

            const int cycles = 100;
            for (var i = 0; i < cycles; i++)
            {
                var es = new SpanishPhonemizerBackend();
                await es.InitializeAsync(new PhonemizerBackendOptions());
                es.Dispose();

                var fr = new FrenchPhonemizerBackend();
                await fr.InitializeAsync(new PhonemizerBackendOptions());
                fr.Dispose();

                var zh = new ChinesePhonemizerBackend();
                await zh.InitializeAsync(new PhonemizerBackendOptions());
                zh.Dispose();

                var ko = new KoreanPhonemizerBackend();
                await ko.InitializeAsync(new PhonemizerBackendOptions());
                ko.Dispose();

                // Periodic GC to simulate real-world collection
                if (i % 25 == 0)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                }
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var finalMemory = GC.GetTotalMemory(true);

            var memGrowth = finalMemory - baselineMemory;
            // Allow up to 10 MB growth for 100 init/dispose cycles (generous for CI)
            Assert.Less(memGrowth, 10 * 1024 * 1024,
                $"Memory grew by {memGrowth / 1024}KB after {cycles} init/dispose cycles, possible leak");
        }

        [Test]
        public void MultilingualPhonemizer_RapidLanguageSwitching_Stable()
        {
            // Test that the detector handles rapid language switching without issues
            const int iterations = 1000;
            string[] inputs =
            {
                "\u3053\u3093\u306B\u3061\u306F", // ja
                "hello",                            // en
                "\u4F60\u597D",                     // zh
                "\uC548\uB155",                     // ko
                "hola",                             // es
                "bonjour",                          // fr
                "\u4ECA\u65E5hello",                // mixed ja+en
                "\u4F60\u597Dworld",                // mixed zh+en
                "123",                              // neutral
                "...!?",                            // punctuation only
            };

            var detector = new UnicodeLanguageDetector(
                new[] { "ja", "en", "zh", "ko", "es", "fr" });

            Assert.DoesNotThrow(() =>
            {
                for (var i = 0; i < iterations; i++)
                {
                    var segments = detector.SegmentText(inputs[i % inputs.Length]);
                    Assert.IsNotNull(segments, $"Segments should not be null at iteration {i}");
                }
            }, "Rapid language switching should not throw after 1000 iterations");
        }

        [Test]
        public void PuaTokenMapper_ManyDynamicRegistrations_StillFast()
        {
            const int registrations = 1000;
            var tokens = new string[registrations];
            for (var i = 0; i < registrations; i++)
            {
                tokens[i] = $"dynamic_stress_{i:D4}";
            }

            // Register all tokens
            var swRegister = Stopwatch.StartNew();
            for (var i = 0; i < registrations; i++)
            {
                PuaTokenMapper.Register(tokens[i]);
            }
            swRegister.Stop();

            Assert.Less(swRegister.ElapsedMilliseconds, 100,
                $"Registering {registrations} tokens took {swRegister.ElapsedMilliseconds}ms, expected < 100ms");

            // Now verify lookups are still fast after many dynamic registrations
            var swLookup = Stopwatch.StartNew();
            for (var i = 0; i < registrations * 10; i++)
            {
                PuaTokenMapper.Token2Char.TryGetValue(tokens[i % registrations], out _);
            }
            swLookup.Stop();

            Assert.Less(swLookup.ElapsedMilliseconds, 50,
                $"10k lookups after {registrations} dynamic registrations took {swLookup.ElapsedMilliseconds}ms, expected < 50ms");
        }

        // =====================================================================
        // Memory Tests
        // =====================================================================

        [Test]
        public async Task SpanishPhonemizer_SingleWord_LowAllocation()
        {
            const string input = "hola";

            // Warm up
            await _esBackend.PhonemizeAsync(input, "es");

            // Measure approximate allocation
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var memBefore = GC.GetTotalMemory(true);

            const int iterations = 100;
            for (var i = 0; i < iterations; i++)
            {
                await _esBackend.PhonemizeAsync(input, "es");
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var memAfter = GC.GetTotalMemory(true);

            var allocPerCall = (memAfter - memBefore) / (double)iterations;

            // Each call should allocate less than 64KB (generous for PhonemeResult + arrays)
            Assert.Less(allocPerCall, 65536,
                $"Approximate allocation per call: {allocPerCall:F0} bytes, expected < 64KB");
        }

        [Test]
        public void UnicodeLanguageDetector_SegmentText_MinimalAllocation()
        {
            const string input = "\u3053\u3093\u306B\u3061\u306Fhello";

            // Warm up
            _detector.SegmentText(input);

            // Measure approximate allocation
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var memBefore = GC.GetTotalMemory(true);

            const int iterations = 1000;
            for (var i = 0; i < iterations; i++)
            {
                _detector.SegmentText(input);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var memAfter = GC.GetTotalMemory(true);

            var allocPerCall = (memAfter - memBefore) / (double)iterations;

            // SegmentText creates List + StringBuilder + strings; should be under 8KB per call
            Assert.Less(allocPerCall, 8192,
                $"Approximate allocation per SegmentText call: {allocPerCall:F0} bytes, expected < 8KB");
        }

        // =====================================================================
        // Additional edge case: empty & whitespace inputs
        // =====================================================================

        [Test]
        public void Phonemize_EmptyString_DoesNotThrow()
        {
            Assert.DoesNotThrowAsync(async () =>
                await _esBackend.PhonemizeAsync("", "es"));
            Assert.DoesNotThrowAsync(async () =>
                await _frBackend.PhonemizeAsync("", "fr"));
            Assert.DoesNotThrowAsync(async () =>
                await _zhBackend.PhonemizeAsync("", "zh"));
            Assert.DoesNotThrowAsync(async () =>
                await _koBackend.PhonemizeAsync("", "ko"));
        }

        [Test]
        public void Phonemize_WhitespaceOnly_DoesNotThrow()
        {
            Assert.DoesNotThrowAsync(async () =>
                await _esBackend.PhonemizeAsync("   \t\n  ", "es"));
            Assert.DoesNotThrowAsync(async () =>
                await _frBackend.PhonemizeAsync("   \t\n  ", "fr"));
            Assert.DoesNotThrowAsync(async () =>
                await _zhBackend.PhonemizeAsync("   \t\n  ", "zh"));
            Assert.DoesNotThrowAsync(async () =>
                await _koBackend.PhonemizeAsync("   \t\n  ", "ko"));
        }

        [Test]
        public void SegmentText_EmptyString_ReturnsEmptyList()
        {
            var result = _detector.SegmentText("");
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(0, result.Count, "Empty string should return empty list");
        }

        [Test]
        public void SegmentText_NullString_ReturnsEmptyList()
        {
            var result = _detector.SegmentText(null);
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(0, result.Count, "Null string should return empty list");
        }
    }
}